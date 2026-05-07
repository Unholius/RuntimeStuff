// <copyright file="MemberAccessorHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;

    /// <summary>
    /// Вспомогательный класс для создания делегатов доступа к членам классов (полям, свойствам, конструкторам) с помощью динамической генерации IL-кода.
    /// </summary>
    public static class MemberAccessorHelper
    {
        private static readonly BindingFlags BindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy; // Позволяет видеть статические члены из базовых классов

        private static readonly ConcurrentDictionary<(Type From, Type To), Func<object, object>> Cache = new();
        private static readonly ConcurrentDictionary<ConstructorInfo, Func<object[], object>> ConstructorInvokersCache = new();
        private static readonly ConcurrentDictionary<Type, Func<object>> DefaultConstructorCache = new();
        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> FieldGettersCache = new();
        private static readonly ConcurrentDictionary<FieldInfo, Action<object, object>> FieldSettersCache = new();
        private static readonly Dictionary<short, OpCode> OpCodes = InitializeOpCodes();
        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> PropertyGettersCache = new();
        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> PropertySettersCache = new();

        /// <summary>
        /// Возвращает делегат, создающий экземпляр указанного типа с использованием конструктора по умолчанию
        /// или первого доступного конструктора с аргументами по умолчанию.
        /// </summary>
        /// <param name="type">Тип, для которого требуется получить фабрику создания экземпляра.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object}"/>, создающий новый экземпляр типа,
        /// либо <see langword="null"/>, если конструкторы отсутствуют.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Результат кэшируется для повторного использования.
        /// </para>
        /// <para>
        /// Поведение:
        /// <list type="bullet">
        /// <item>
        /// Для значимых типов (<see cref="Type.IsValueType"/>) создаётся выражение,
        /// возвращающее значение по умолчанию.
        /// </item>
        /// <item>
        /// Для ссылочных типов ищется конструктор без параметров.
        /// Если он отсутствует — используется первый доступный конструктор.
        /// </item>
        /// <item>
        /// Для параметризованных конструкторов аргументы инициализируются значениями по умолчанию:
        /// для значимых типов — через <see cref="Activator.CreateInstance(Type)"/>,
        /// для ссылочных — <see langword="null"/>.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public static Func<object> GetDefaultConstructor(Type type)
        {
            return DefaultConstructorCache.GetOrAdd(type, x =>
            {
                if (type.IsValueType)
                {
                    var b = Expression.Convert(
                        Expression.Default(type),
                        typeof(object));

                    return Expression.Lambda<Func<object>>(b).Compile();
                }

                const BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var ctors = x.GetConstructors(flags);

                if (ctors.Length == 0)
                {
                    return null;
                }

                var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0)
                           ?? ctors.First();

                var parameters = ctor.GetParameters();

                // аргументы по умолчанию
                var defaultArgs = parameters
                    .Select(p => p.ParameterType.IsValueType
                        ? Activator.CreateInstance(p.ParameterType)
                        : null)
                    .ToArray();

                // () => new T(defaultArgs...)
                var argsExpr = parameters
                    .Select((p, i) =>
                        Expression.Constant(defaultArgs[i], p.ParameterType))
                    .ToArray();

                var newExpr = Expression.New(ctor, argsExpr);
                var body = Expression.Convert(newExpr, typeof(object));

                return Expression.Lambda<Func<object>>(body).Compile();
            });
        }

        /// <summary>
        /// Получает делегат для вызова конструктора, представленного <see cref="ConstructorInfo"/>.
        /// </summary>
        /// <param name="ctor">Конструктор, для которого нужно создать делегат.</param>
        /// <returns>Делегат для вызова конструктора.</returns>
        public static Func<object[], object> GetConstructorInvoker(ConstructorInfo ctor)
        {
            return ConstructorInvokersCache.GetOrAdd(ctor, (x) =>
            {
                var argsParam = Expression.Parameter(typeof(object[]), "args");

                var ctorArgs = x.GetParameters()
                    .Select((p, i) =>
                        Expression.Convert(
                            Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                            p.ParameterType))
                    .ToArray<Expression>();

                var newExpr = Expression.New(x, ctorArgs);

                var body = Expression.Convert(newExpr, typeof(object));

                return Expression
                    .Lambda<Func<object[], object>>(body, argsParam)
                    .Compile();
            });
        }

        /// <summary>
        /// Возвращает делегат для конвертации объекта из одного типа в другой, используя встроенные механизмы преобразования .NET (например, IConvertible).
        /// </summary>
        /// <param name="fromType">Тип объекта, из которого происходит конвертация.</param>
        /// <param name="toType">Тип объекта, в который происходит конвертация.</param>
        /// <returns>Делегат для конвертации объектов.</returns>
        public static Func<object, object> GetConverter(Type fromType, Type toType)
        {
            if (fromType == null)
            {
                throw new ArgumentNullException(nameof(fromType));
            }

            if (toType == null)
            {
                throw new ArgumentNullException(nameof(toType));
            }

            return Cache.GetOrAdd((fromType, toType), static key =>
            {
                var from = key.From;
                var to = key.To;

                // identity
                if (to.IsAssignableFrom(from))
                {
                    return static x => x;
                }

                var input = Expression.Parameter(typeof(object), "x");

                var body = BuildExpression(input, from, to);

                return Expression
                    .Lambda<Func<object, object>>(body, input)
                    .Compile();
            });
        }

        /// <summary>
        /// Получает делегат для прямой установки значения поля, представленного <see cref="FieldInfo"/>.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения поля.</returns>
        public static Action<object, object> GetDirectFieldSetter(FieldInfo fi) => (instance, value) =>
                {
                    var tr = __makeref(instance);
                    fi.SetValueDirect(tr, value);
                };

        /// <summary>
        /// Получает делегат для получения значения поля, представленного <see cref="FieldInfo"/>. Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для получения значения поля.</returns>
        public static Func<object, object> GetFieldGetter(FieldInfo fi)
        {
            return FieldGettersCache.GetOrAdd(fi, (x) =>
            {
                try
                {
                    if (x == null)
                    {
                        throw new ArgumentNullException(nameof(x));
                    }

                    var declaringType = x.DeclaringType ??
                                        throw new ArgumentException(@"Field has no declaring type", nameof(x));
                    var fieldType = x.FieldType;

                    // Проверяем, является ли поле константой
                    if (x.IsLiteral && !x.IsInitOnly)
                    {
                        // Для const полей возвращаем делегат, который всегда возвращает значение константы
                        var constValue = x.GetRawConstantValue();
                        return _ => constValue;
                    }

                    var dm = new DynamicMethod(
                        $"get_{declaringType.Name}_{x.Name}",
                        typeof(object),
                        [typeof(object)],
                        declaringType.Module,
                        true);

                    var il = dm.GetILGenerator();

                    // Для статических полей (не констант)
                    if (x.IsStatic)
                    {
                        il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, x); // Загружаем статическое поле
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
                        il.Emit(System.Reflection.Emit.OpCodes.Ldfld, x);
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
                        il.Emit(System.Reflection.Emit.OpCodes.Ldflda, x); // Загружаем адрес поля
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
                        $"Failed to create field getter for field '{x?.DeclaringType?.Name}.{x?.Name}': {ex.Message}",
                        ex);
                }
            });
        }

        /// <summary>
        /// Получает <see cref="FieldInfo"/> для поля, связанного с методом доступа (getter) свойства. Метод пытается найти поле, которое может быть связано с данным геттером.
        /// </summary>
        /// <param name="accessor">Метод доступа (getter) свойства.</param>
        /// <returns>Информация о поле, связанном с методом доступа.</returns>
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

        /// <summary>
        /// Возвращает делегат для установки значения поля или свойства.
        /// </summary>
        /// <param name="member">
        /// Член типа, для которого требуется получить сеттер.
        /// Поддерживаются <see cref="FieldInfo"/> и <see cref="PropertyInfo"/>.
        /// </param>
        /// <returns>
        /// Делегат, принимающий целевой объект и новое значение члена.
        /// </returns>
        /// <remarks>
        /// Для поля используется прямой сеттер,
        /// для свойства — метод установки свойства.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если передан неподдерживаемый тип члена.
        /// </exception>
        public static Action<object, object> GetSetter(MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }

            if (member is PropertyInfo pi)
            {
                return GetPropertySetter(pi);
            }

            if (member is FieldInfo fi)
            {
                return GetFieldSetter(fi);
            }

            throw new ArgumentException("Unsupported member type", nameof(member));
        }

        /// <summary>
        /// Возвращает делегат для получения значения поля или свойства.
        /// </summary>
        /// <param name="type">
        /// Тип владельца члена. Используется вызывающим кодом
        /// для согласованности API.
        /// </param>
        /// <param name="member">
        /// Член типа, для которого требуется получить геттер.
        /// Поддерживаются <see cref="FieldInfo"/> и <see cref="PropertyInfo"/>.
        /// </param>
        /// <returns>
        /// Делегат, принимающий целевой объект и возвращающий текущее значение члена.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если передан неподдерживаемый тип члена.
        /// </exception>
        public static Func<object, object> GetGetter(Type type, MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }

            if (member is PropertyInfo pi)
            {
                return GetPropertyGetter(pi);
            }

            if (member is FieldInfo fi)
            {
                return GetFieldGetter(fi);
            }

            throw new ArgumentException("Unsupported member type", nameof(member));
        }

        /// <summary>
        /// Получает делегат для установки значения поля, представленного <see cref="FieldInfo"/>. Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения поля.</returns>
        public static Action<object, object> GetFieldSetter(FieldInfo fi)
        {
            return FieldSettersCache.GetOrAdd(fi, (x) =>
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                var dm = new DynamicMethod(
                    $"Set_{x.Name}",
                    typeof(void),
                    [typeof(object), typeof(object)],
                    restrictedSkipVisibility: true);

                var il = dm.GetILGenerator();

                // local 0: TypedReference
                il.DeclareLocal(typeof(TypedReference));

                // __makeref((T)target)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                Debug.Assert(x.DeclaringType != null, "field.DeclaringType != null");
                il.Emit(System.Reflection.Emit.OpCodes.Unbox, x.DeclaringType ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Mkrefany, x.DeclaringType);
                il.Emit(System.Reflection.Emit.OpCodes.Stloc_0);

                // ref field
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc_0);
                il.Emit(System.Reflection.Emit.OpCodes.Refanyval, x.DeclaringType);
                il.Emit(System.Reflection.Emit.OpCodes.Ldflda, x);

                // value
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);

                if (x.FieldType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, x.FieldType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, x.FieldType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Stobj, x.FieldType);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);

                return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
            });
        }

        /// <summary>
        /// Создаёт делегат для получения значения свойства.
        /// Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="pi">Метаданные свойства (<see cref="PropertyInfo"/>), для которого создается геттер.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object, Object}"/>, который возвращает значение указанного свойства.
        /// Если свойство не имеет метода get, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Метод поддерживает как статические, так и нестатические свойства, а также свойства value-типа и ссылочного типа.
        /// </para>
        /// <para>
        /// Для value-типа аргумент должен быть упакованным объектом (boxed value type).
        /// В случае попытки передачи <c>null</c> для value-типа будет выброшено <see cref="NullReferenceException"/>.
        /// </para>
        /// <para>
        /// Для ссылочных типов, если объект не совместим с ожидаемым типом, будет выброшено <see cref="InvalidCastException"/>.
        /// </para>
        /// </remarks>
        public static Func<object, object> GetPropertyGetter(PropertyInfo pi)
        {
            return PropertyGettersCache.GetOrAdd(pi, (x) => GetPropertyGetter<object, object>(pi));
        }

        /// <summary>
        /// Получает делегат для установки значения свойства.
        /// </summary>
        /// <param name="pi">Свойство, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения свойства.</returns>
        public static Action<object, object> GetPropertySetter(PropertyInfo pi)
        {
            return PropertySettersCache.GetOrAdd(pi, (x) =>
            {
                var setter = x.GetSetMethod(true);
                if (setter == null)
                {
                    var backingField = GetFieldInfoFromGetAccessor(x.GetMethod);
                    if (backingField != null)
                    {
                        return GetDirectFieldSetter(backingField);
                    }

                    return null;
                }

                var declaring = x.DeclaringType;
                var propertyType = x.PropertyType;

                if (declaring == null)
                {
                    throw new ArgumentException("Property must have a declaring type", nameof(x));
                }

                var dm = new DynamicMethod(
                    "set_" + x.Name,
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
            });
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

        private static Action<T, TField> GetDirectFieldSetter<T, TField>(FieldInfo fi) => (instance, value) =>
                                                                {
                                                                    var tr = __makeref(instance);
                                                                    fi.SetValueDirect(tr, value);
                                                                };

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

        /// <summary>
        /// Создаёт делегат для получения значения свойства <typeparamref name="TProperty"/> объекта <typeparamref name="TObject"/>.
        /// Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <typeparam name="TObject">Тип объекта, содержащего свойство.</typeparam>
        /// <typeparam name="TProperty">Тип значения свойства.</typeparam>
        /// <param name="pi">Метаданные свойства (<see cref="PropertyInfo"/>), для которого создается геттер.</param>
        /// <returns>
        /// Делегат <see cref="Func{TObject, TProperty}"/>, который возвращает значение указанного свойства.
        /// Если свойство не имеет метода get, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Метод поддерживает как статические, так и нестатические свойства, а также свойства value-типа и ссылочного типа.
        /// </para>
        /// <para>
        /// Для value-типа аргумент <typeparamref name="TObject"/> должен быть упакованным объектом (boxed value type).
        /// В случае попытки передачи <c>null</c> для value-типа будет выброшено <see cref="NullReferenceException"/>.
        /// </para>
        /// <para>
        /// Для ссылочных типов, если объект не совместим с ожидаемым типом, будет выброшено <see cref="InvalidCastException"/>.
        /// </para>
        /// </remarks>
        private static Func<TObject, TProperty> GetPropertyGetter<TObject, TProperty>(PropertyInfo pi)
        {
            var getter = pi.GetGetMethod(true);
            if (getter == null)
            {
                return null;
            }

            var declaring = pi.DeclaringType;
            var propertyType = pi.PropertyType;

            if (declaring == null)
            {
                throw new ArgumentException("Property must have a declaring type", nameof(pi));
            }

            var dm = new DynamicMethod(
                "get_" + pi.Name,
                typeof(TObject),
                [typeof(TProperty)],
                declaring.Module,
                true);

            var il = dm.GetILGenerator();

            // Для статических методов
            if (getter.IsStatic)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Call, getter);
                if (propertyType.IsValueType && !propertyType.IsPrimitive)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Box, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Func<TObject, TProperty>)dm.CreateDelegate(typeof(Func<TObject, TProperty>));
            }

            // Для нестатических методов
            if (!declaring.IsValueType)
            {
                // Для ссылочных типов
                var lblOk = il.DefineLabel();

                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblOk);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Callvirt, getter);
            }
            else
            {
                // Для value types
                // Создаем локальную переменную для хранения распакованной структуры
                var local = il.DeclareLocal(declaring);

                // Загружаем аргумент (упакованную структуру)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);

                // Проверяем, что это не null (для упакованных структур)
                var lblNotNull = il.DefineLabel();
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

                // Загружаем адрес локальной переменной (для вызова метода структуры)
                il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local);

                // Вызываем getter
                il.Emit(System.Reflection.Emit.OpCodes.Call, getter);
            }

            // Бокс возвращаемого значения, если это value type
            if (propertyType.IsValueType)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Box, propertyType);
            }

            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Func<TObject, TProperty>)dm.CreateDelegate(typeof(Func<TObject, TProperty>));
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

        private static Expression BuildExpression(ParameterExpression input, Type fromType, Type toType)
        {
            Expression source = Expression.Convert(input, fromType);

            var targetNullable = Nullable.GetUnderlyingType(toType);
            var sourceNullable = Nullable.GetUnderlyingType(fromType);

            var realFrom = sourceNullable ?? fromType;
            var realTo = targetNullable ?? toType;

            // Nullable<T> -> T
            if (sourceNullable != null)
            {
                source = Expression.Property(source, "Value");
            }

            // прямой cast
            if (realTo.IsAssignableFrom(realFrom))
            {
                Expression result = Expression.Convert(source, realTo);

                if (targetNullable != null)
                {
                    result = Expression.New(toType.GetConstructor(new[] { realTo })!, result);
                }

                return Expression.Convert(result, typeof(object));
            }

            // enum
            if (realTo.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(realTo);

                Expression value = realFrom == typeof(string)
                    ? Expression.Call(
                        typeof(Enum).GetMethod(nameof(Enum.Parse), new[] { typeof(Type), typeof(string), typeof(bool) })!,
                        Expression.Constant(realTo),
                        source,
                        Expression.Constant(true))
                    : Expression.Convert(source, underlying);

                value = Expression.Convert(value, realTo);

                if (targetNullable != null)
                {
                    value = Expression.New(toType.GetConstructor(new[] { realTo })!, value);
                }

                return Expression.Convert(value, typeof(object));
            }

            // primitive numeric / bool / DateTime через Convert.ChangeType
            if (typeof(IConvertible).IsAssignableFrom(realFrom) &&
                typeof(IConvertible).IsAssignableFrom(realTo))
            {
                var call = Expression.Call(
                    typeof(Convert),
                    nameof(Convert.ChangeType),
                    Type.EmptyTypes,
                    Expression.Convert(source, typeof(object)),
                    Expression.Constant(realTo),
                    Expression.Constant(CultureInfo.InvariantCulture));

                Expression result = Expression.Convert(call, realTo);

                if (targetNullable != null)
                {
                    result = Expression.New(toType.GetConstructor(new[] { realTo })!, result);
                }

                return Expression.Convert(result, typeof(object));
            }

            // TypeConverter fallback
            var converter = TypeDescriptor.GetConverter(realTo);
            if (converter.CanConvertFrom(realFrom))
            {
                return Expression.Call(
                    Expression.Constant(new Func<object, object>(x => converter.ConvertFrom(null, CultureInfo.InvariantCulture, x)!)),
                    typeof(Func<object, object>).GetMethod("Invoke")!,
                    input);
            }

            throw new InvalidOperationException(
                $"Cannot convert from {fromType.FullName} to {toType.FullName}");
        }
    }
}