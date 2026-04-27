namespace System.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;

    public static class ReflectionHelper
    {
        private static readonly BindingFlags BindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy; // Позволяет видеть статические члены из базовых классов


        public static Action<object, object> CreateDirectFieldSetter(FieldInfo fi) => (instance, value) =>
        {
            var tr = __makeref(instance);
            fi.SetValueDirect(tr, value);
        };

        public static Action<T, TField> CreateDirectFieldSetter<T, TField>(FieldInfo fi) => (instance, value) =>
        {
            var tr = __makeref(instance);
            fi.SetValueDirect(tr, value);
        };

        public static Action<object, object> CreateFieldSetter(FieldInfo field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            var dm = new DynamicMethod(
                $"Set_{field.Name}",
                typeof(void),
                [typeof(object), typeof(object)],
                restrictedSkipVisibility: true);

            var il = dm.GetILGenerator();

            // local 0: TypedReference
            il.DeclareLocal(typeof(TypedReference));

            // __makeref((T)target)
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            Debug.Assert(field.DeclaringType != null, "field.DeclaringType != null");
            il.Emit(System.Reflection.Emit.OpCodes.Unbox, field.DeclaringType ?? throw new InvalidOperationException());
            il.Emit(System.Reflection.Emit.OpCodes.Mkrefany, field.DeclaringType);
            il.Emit(System.Reflection.Emit.OpCodes.Stloc_0);

            // ref field
            il.Emit(System.Reflection.Emit.OpCodes.Ldloc_0);
            il.Emit(System.Reflection.Emit.OpCodes.Refanyval, field.DeclaringType);
            il.Emit(System.Reflection.Emit.OpCodes.Ldflda, field);

            // value
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);

            if (field.FieldType.IsValueType)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, field.FieldType);
            }
            else
            {
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, field.FieldType);
            }

            il.Emit(System.Reflection.Emit.OpCodes.Stobj, field.FieldType);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
        }

        public static Func<object, object> CreateFieldGetter(FieldInfo fi)
        {
            try
            {
                if (fi == null)
                {
                    throw new ArgumentNullException(nameof(fi));
                }

                var declaringType = fi.DeclaringType ??
                                    throw new ArgumentException(@"Field has no declaring type", nameof(fi));
                var fieldType = fi.FieldType;

                // Проверяем, является ли поле константой
                if (fi.IsLiteral && !fi.IsInitOnly)
                {
                    // Для const полей возвращаем делегат, который всегда возвращает значение константы
                    var constValue = fi.GetRawConstantValue();
                    return _ => constValue;
                }

                var dm = new DynamicMethod(
                    $"get_{declaringType.Name}_{fi.Name}",
                    typeof(object),
                    [typeof(object)],
                    declaringType.Module,
                    true);

                var il = dm.GetILGenerator();

                // Для статических полей (не констант)
                if (fi.IsStatic)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, fi); // Загружаем статическое поле
                    if (fieldType.IsValueType)
                    {
                        il.Emit(System.Reflection.Emit.OpCodes.Box, fieldType); // Боксим value type
                    }

                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
                }

                // Для нестатических полей
                if (!declaringType.IsValueType)
                {
                    // Для ссылочных типов
                    var lblOk = il.DefineLabel();

                    // Проверяем целевой объект
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaringType);
                    il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                    // Если тип не подходит, выбрасываем исключение
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                    il.Emit(System.Reflection.Emit.OpCodes.Throw);

                    il.MarkLabel(lblOk);

                    // Загружаем целевой объект и приводим к правильному типу
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaringType);

                    // Загружаем поле
                    il.Emit(System.Reflection.Emit.OpCodes.Ldfld, fi);
                }
                else
                {
                    // Для value types (структур)

                    // Проверяем на null
                    var lblNotNull = il.DefineLabel();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Dup);
                    il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblNotNull);

                    // Если null, выбрасываем исключение
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(NullReferenceException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                    il.Emit(System.Reflection.Emit.OpCodes.Throw);

                    il.MarkLabel(lblNotNull);

                    // Распаковываем структуру
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, declaringType);

                    // Создаем локальную переменную
                    var local = il.DeclareLocal(declaringType);
                    il.Emit(System.Reflection.Emit.OpCodes.Stloc, local);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local); // Загружаем адрес

                    // Загружаем поле
                    il.Emit(System.Reflection.Emit.OpCodes.Ldflda, fi); // Загружаем адрес поля
                    il.Emit(System.Reflection.Emit.OpCodes.Ldobj, fieldType); // Загружаем значение по адресу
                }

                // Боксим результат, если это value type
                if (fieldType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Box, fieldType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Ret);

                return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create field getter for field '{fi?.DeclaringType?.Name}.{fi?.Name}': {ex.Message}",
                    ex);
            }
        }

        public static Func<object[], object> CreateFactory(ConstructorInfo ctor)
        {
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var ctorArgs = ctor.GetParameters()
                .Select((p, i) =>
                    Expression.Convert(
                        Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                        p.ParameterType))
                .ToArray<Expression>();

            var newExpr = Expression.New(ctor, ctorArgs);

            var body = Expression.Convert(newExpr, typeof(object));

            return Expression
                .Lambda<Func<object[], object>>(body, argsParam)
                .Compile();
        }

        public static Action<object, object> CreatePropertySetter(PropertyInfo pi)
        {
            var setter = pi.GetSetMethod(true);
            if (setter == null)
            {
                var backingField = GetFieldInfoFromGetAccessor(pi.GetMethod);
                if (backingField != null)
                {
                    return CreateDirectFieldSetter(backingField);
                }

                return null;
            }

            var declaring = pi.DeclaringType;
            var propertyType = pi.PropertyType;

            if (declaring == null)
            {
                throw new ArgumentException("Property must have a declaring type", nameof(pi));
            }

            var dm = new DynamicMethod(
                "set_" + pi.Name,
                null,
                [typeof(object), typeof(object)],
                declaring.Module,
                true);

            var il = dm.GetILGenerator();

            // Для статических методов
            if (setter.IsStatic)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1); // Загружаем значение
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Call, setter);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
            }

            // Для нестатических методов
            if (!declaring.IsValueType)
            {
                // Для ссылочных типов
                var lblOk = il.DefineLabel();

                // Проверяем целевой объект (obj)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblOk);

                // Загружаем целевой объект и приводим к правильному типу
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaring);

                // Загружаем значение
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Callvirt, setter);
            }
            else
            {
                // Для value types (структур)
                // Создаем локальную переменную для хранения распакованной структуры
                var local = il.DeclareLocal(declaring);

                // Проверяем целевой объект на null
                var lblNotNull = il.DefineLabel();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Dup);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblNotNull);

                // Если null, выбрасываем исключение
                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(NullReferenceException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblNotNull);

                // Распаковываем структуру
                il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, declaring);

                // Сохраняем в локальную переменную
                il.Emit(System.Reflection.Emit.OpCodes.Stloc, local);

                // Загружаем адрес локальной переменной
                il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local);

                // Загружаем значение
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                // Вызываем setter
                il.Emit(System.Reflection.Emit.OpCodes.Call, setter);

                // Боксим структуру обратно в object (обновляем исходный объект)
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, local);
                il.Emit(System.Reflection.Emit.OpCodes.Box, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Starg_S, 0); // Сохраняем обратно в первый аргумент
            }

            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
        }

        public static FieldInfo GetFieldInfoFromGetAccessor(MethodInfo accessor)
        {
            if (accessor == null)
            {
                throw new ArgumentNullException(nameof(accessor));
            }

            var declaringType = accessor.DeclaringType ??
                                throw new ArgumentException(@"Method has no declaring type", nameof(accessor));
            var propertyName = accessor.Name.Substring(4);

            // Вариант 1: Поиск автоматически сгенерированного поля для автосвойств
            var autoBackingFieldName = $"<{propertyName}>k__BackingField";
            var field = declaringType.GetField(autoBackingFieldName, BindingFlags);

            if (field != null)
            {
                return field;
            }

            // Вариант 2: Поиск в базовых типах
            var baseType = declaringType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                field = baseType.GetField(autoBackingFieldName, BindingFlags);

                if (field != null)
                {
                    return field;
                }

                baseType = baseType.BaseType;
            }

            // Вариант 3: Анализ IL-кода
            field = GetBackingFieldFromIl(accessor);
            if (field != null)
            {
                return field;
            }

            // Вариант 4: Поиск по стандартным шаблонам именования
            return FindFieldByNamingPatterns(declaringType, propertyName);
        }

        private static FieldInfo FindFieldByNamingPatterns(Type declaringType, string propertyName)
        {
            var property = declaringType.GetProperties(BindingFlags).FirstOrDefault(x => x.Name == propertyName);

            if (property == null)
            {
                return null;
            }

            // Стандартные шаблоны именования полей
            var possibleFieldNames = new[]
            {
                $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}", // _propertyName
                $"m_{propertyName}", // m_PropertyName
                $"_{propertyName}", // _PropertyName
                propertyName, // PropertyName (для публичных полей)
                $"m{char.ToUpper(propertyName[0])}{propertyName.Substring(1)}", // mPropertyName
                $"{propertyName.ToLower()}",
            };

            // Поиск в текущем типе
            foreach (var fieldName in possibleFieldNames)
            {
                var field = declaringType.GetField(fieldName, BindingFlags);

                if (field != null && field.FieldType == property.PropertyType)
                {
                    return field;
                }
            }

            // Поиск в базовых классах
            var baseType = declaringType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                foreach (var fieldName in possibleFieldNames)
                {
                    var field = baseType.GetField(fieldName, BindingFlags);

                    if (field != null && field.FieldType == property.PropertyType)
                    {
                        return field;
                    }
                }

                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// The op codes.
        /// </summary>
        private static readonly Dictionary<short, OpCode> OpCodes = InitializeOpCodes();

        private static FieldInfo GetBackingFieldFromIl(MethodInfo getter)
        {
            try
            {
                var methodBody = getter.GetMethodBody();
                if (methodBody == null)
                {
                    return null;
                }

                var ilBytes = methodBody.GetILAsByteArray();
                if (ilBytes.Length == 0)
                {
                    return null;
                }

                // Анализируем IL-байты
                var i = 0;
                while (i < ilBytes.Length)
                {
                    short opCodeValue = ilBytes[i];

                    // Проверяем двухбайтовые опкоды
                    if (opCodeValue == 0xFE && i + 1 < ilBytes.Length)
                    {
                        opCodeValue = (short)((opCodeValue << 8) | ilBytes[i + 1]);
                        i++; // Пропускаем второй байт
                    }

                    if (OpCodes.TryGetValue(opCodeValue, out var opCode))
                    {
                        // Проверяем инструкции загрузки поля
                        if ((opCode == System.Reflection.Emit.OpCodes.Ldfld ||
                             opCode == System.Reflection.Emit.OpCodes.Ldsfld ||
                             opCode == System.Reflection.Emit.OpCodes.Ldflda ||
                             opCode == System.Reflection.Emit.OpCodes.Ldsflda) && i + 4 < ilBytes.Length)
                        {
                            var token = BitConverter.ToInt32(ilBytes, i + 1);

                            try
                            {
                                var field = getter.Module.ResolveField(token);
                                if (field != null && IsValidBackingField(field, getter.DeclaringType))
                                {
                                    return field;
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки разрешения токена
                            }
                        }

                        // Пропускаем байты операнда в зависимости от типа операнда
                        i += GetOperandSize(opCode.OperandType, ilBytes, i + 1);
                    }

                    i++;
                }
            }
            catch
            {
                // Игнорируем ошибки анализа IL
            }

            return null;
        }

        /// <summary>
        /// Determines whether [is valid backing field] [the specified field].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="declaringType">Type of the declaring.</param>
        /// <returns><c>true</c> if [is valid backing field] [the specified field]; otherwise, <c>false</c>.</returns>
        private static bool IsValidBackingField(FieldInfo field, Type declaringType)
        {
            if (field == null)
            {
                return false;
            }

            // Поле должно быть приватным (или защищенным для базовых классов)
            if (!field.IsPrivate && !field.IsFamily && !field.IsAssembly && !field.IsFamilyOrAssembly)
            {
                return false;
            }

            // Поле должно принадлежать этому типу или его базовому типу
            Debug.Assert(field.DeclaringType != null, "field.DeclaringType != null");
            if (!declaringType.IsAssignableFrom(field.DeclaringType) &&
                field.DeclaringType != null &&
                !field.DeclaringType.IsAssignableFrom(declaringType))
            {
                return false;
            }

            return true;
        }

        private static int GetOperandSize(OperandType operandType, byte[] ilBytes, int position)
        {
            switch (operandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    return 4;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;

                case OperandType.InlineSwitch:
                    if (position + 4 <= ilBytes.Length)
                    {
                        var count = BitConverter.ToInt32(ilBytes, position);
                        return 4 + (count * 4);
                    }

                    return 0;

                case OperandType.InlineVar:
                    return 2;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineR:
                case OperandType.ShortInlineVar:
                    return 1;

                default:
                    return 0;
            }
        }

        private static Dictionary<short, OpCode> InitializeOpCodes()
        {
            var dict = new Dictionary<short, OpCode>();
            var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(OpCode))
                {
                    var opCode = (OpCode)field.GetValue(null);
                    dict[opCode.Value] = opCode;
                }
            }

            return dict;
        }
    }
}
