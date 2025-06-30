//using System;
//using System.Collections;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Data;
//using System.Data.SqlTypes;
//using System.Diagnostics;
//using System.Globalization;
//using System.Linq;
//using System.Reflection;
//using RuntimeStuff.Extensions;

//namespace RuntimeStuff
//{
//    public static class TypeHelper
//    {
//        public static Dictionary<string, Assembly> CachedAssemblies = new Dictionary<string, Assembly>();

//        /// <summary>
//        ///     Целочисленные типы
//        /// </summary>
//        public static Type[] IntNumberTypes { get; } = new[] {
//            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte), typeof(sbyte),
//            typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?), typeof(byte?), typeof(sbyte?)
//        };

//        /// <summary>
//        ///     Вещественные типы
//        /// </summary>
//        public static Type[] FloatNumberTypes { get; } = new Type[]
//        {
//            typeof(float), typeof(double), typeof(decimal),
//            typeof(float?), typeof(double?), typeof(decimal?)
//        };

//        /// <summary>
//        ///     Даты
//        /// </summary>
//        public static Type[] DateTypes { get; } = new Type[]
//        {
//            typeof(DateTime), typeof(DateTime?),
//        };

//        /// <summary>
//        /// Булевы типы
//        /// </summary>
//        public static Type[] BoolTypes { get; } = new Type[]
//        {
//            typeof(bool),
//            typeof(bool?),
//            typeof(SqlBoolean),
//            typeof(SqlBoolean?),
//        };

//        /// <summary>
//        ///     Значения, которые считать NULL
//        /// </summary>
//        public static object[] NullValues { get; } = new object[] { null, DBNull.Value, double.NaN, float.NaN };

//        /// <summary>
//        ///     Числовые типы
//        /// </summary>
//        public static Type[] NumberTypes { get; } = IntNumberTypes.Concat(FloatNumberTypes).ToArray();

//        /// <summary>
//        /// Просты типы
//        /// </summary>
//        public static Type[] BasicTypes { get; } =
//            NumberTypes
//                .Concat(BoolTypes)
//                .Concat(new Type[] { typeof(string), typeof(DateTime), typeof(DateTime?), typeof(TimeSpan), typeof(Guid), typeof(Guid?), typeof(char), typeof(char?), typeof(Enum) })
//                .ToArray();

//        /// <summary>
//        /// Маппер с интерфейсов на конкретный тип. Используется в <see cref="CreateInstance(Type, object[])"/>
//        /// </summary>
//        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>()
//        {
//            {typeof(IEnumerable), typeof(List<object>) },
//            {typeof(IEnumerable<>), typeof(List<>) },
//            {typeof(ICollection), typeof(ObservableCollection<object>) },
//            {typeof(ICollection<>), typeof(ObservableCollection<>) },
//            {typeof(IDictionary<,>), typeof(Dictionary<,>) },
//        };

//        public static Dictionary<Type, Dictionary<Type, Func<object, object>>> CustomTypeConverters = new Dictionary<Type, Dictionary<Type, Func<object, object>>>();

//        public static void AddCustomTypeConverter<TFrom, TTo>(Func<TFrom, TTo> converter)
//        {
//            if (!CustomTypeConverters.TryGetValue(typeof(TFrom), out var typeConverters) || typeConverters == null)
//            {
//                typeConverters = new Dictionary<Type, Func<object, object>>();
//                CustomTypeConverters[typeof(TFrom)] = typeConverters;
//            }
//            typeConverters[typeof(TTo)] = converter.ConvertFunc();
//        }

//        public static Func<TFrom, TTo> GetCustomTypeConverter<TFrom, TTo>()
//        {
//            return GetCustomTypeConverter(typeof(TFrom), typeof(TTo)).ConvertFunc<TFrom, TTo>();
//        }

//        public static Func<object, object> GetCustomTypeConverter(Type typeFrom, Type typeTo)
//        {
//            if (!CustomTypeConverters.TryGetValue(typeFrom, out var typeConverters) || typeConverters == null)
//                return null;

//            if (!typeConverters.TryGetValue(typeTo, out var converter) || converter == null)
//                return null;

//            return converter;
//        }

//        /// <summary>
//        ///     Получить уникальные типы всех вложенных свойств объекта
//        /// </summary>
//        /// <param name="t">Тип из которого нужно составить список всех вложенных типов свойств/полей</param>
//        /// <param name="typesList">Найденные типы добавляются в этот список, который возвращается</param>
//        /// <param name="includeFields">Включать в список типы полей</param>
//        /// <param name="includePrivates">Включать в список приватные свойства/поля</param>
//        /// <param name="includeReadonly">Включать в список свойства/поля только для чтения</param>
//        public static IEnumerable<Type> TypeWalkerLite(Type t, IList<Type> typesList = null, bool includeFields = true, bool includePrivates = true, bool includeReadonly = true)
//        {
//            try
//            {
//                if (typesList == null)
//                    typesList = new List<Type>();
//                if (t == null)
//                    return typesList;

//                if (t.IsCollection())
//                {
//                    var elemType = t.GetCollectionItemType();
//                    if (elemType != null && !typesList.Contains(elemType))
//                    {
//                        typesList.Add(elemType);
//                        TypeWalkerLite(elemType, typesList, includeFields, includePrivates, includeReadonly);
//                    }
//                }
//                else
//                {
//                    var members = t.GetProperties(DefaultBindingFlags);
//                    if (members.Length == 0)
//                        return typesList;
//                    foreach (var mi in members)
//                    {
//                        var miType = mi.PropertyType;
//                        var pt = Nullable.GetUnderlyingType(miType) ?? miType;
//                        if (!typesList.Contains(pt))
//                        {
//                            typesList.Add(pt);
//                            TypeWalkerLite(pt, typesList, includeFields, includePrivates, includeReadonly);
//                        }
//                    }
//                }

//                return typesList;
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine(ex);
//                return typesList ?? new Type[0];
//            }
//        }

//        /// <summary>
//        ///     Получить верхний класс в котором содержится данное свойство или поле
//        /// </summary>
//        /// <param name="member"></param>
//        /// <returns></returns>
//        public static Type GetRootType(MemberInfo member)
//        {
//            if (member.DeclaringType == null)
//                return null;
//            var rootType = member.DeclaringType;
//            while (rootType.DeclaringType != null)
//                rootType = rootType.DeclaringType;
//            return rootType;
//        }


//        public static Type[] GetTypesOf(Type implementedType, Assembly fromAssembly = null)
//        {
//            var a = fromAssembly ?? Assembly.GetCallingAssembly();
//            return a.GetTypes().Where(x => x.IsImplements(implementedType)).ToArray();
//        }

//        /// <summary>
//        /// Если type - коллекция, то возвращается тип элемента коллекции, иначе null
//        /// </summary>
//        /// <param name="type"></param>
//        /// <returns></returns>
//        public static Type GetCollectionItemType(Type type)
//        {
//            if (type == null)
//                return null;
//            var isDic = typeof(IDictionary).IsAssignableFrom(type);
//            var ga = type.GetGenericArguments();
//            return type.IsArray
//                ? type.GetElementType()
//                : isDic && ga.Length > 1 ? ga[1] : type.GetGenericArguments().FirstOrDefault();
//        }

//        /// <summary>
//        /// Get all base types of specific type except object
//        /// </summary>
//        /// <param name="type">Тип у которого необходимо получить все базовые типы</param>
//        /// <param name="includeThis">Включать в результат текущий тип</param>
//        /// <param name="getInterfaces">Включать в список все интерфейсы от которых идет наследование</param>
//        /// <returns></returns>
//        public static Type[] GetBaseTypes(Type type, bool includeThis = false, bool getInterfaces = false)
//        {
//            var baseTypes = new List<Type>();
//            var baseType = type;
//            while (baseType.BaseType != null && baseType.BaseType != typeof(object))
//            {
//                baseType = baseType.BaseType;
//                baseTypes.Add(baseType);
//            }
//            if (getInterfaces)
//                baseTypes.AddRange(baseType.GetInterfaces());
//            if (includeThis)
//                baseTypes.Add(type);
//            return baseTypes.ToArray();
//        }

//        /// <summary>
//        ///     Создать экземпляр указанного типа
//        /// </summary>
//        /// <param name="type">ReaderFieldDataType</param>
//        /// <param name="ctorArgs">Аргументы для конструктора типа</param>
//        /// <returns></returns>
//        public static T CreateInstance<T>(params object[] ctorArgs)
//        {
//            var type = typeof(T);
//            return (T)CreateInstance(type, ctorArgs);
//        }

//        /// <summary>
//        ///     Создать экземпляр указанного типа
//        /// </summary>
//        /// <param name="type">ReaderFieldDataType</param>
//        /// <param name="ctorArgs">Аргументы для конструктора типа</param>
//        /// <returns></returns>
//        public static T CreateInstanceAs<T>(Type instanceType, params object[] ctorArgs) where T : class
//        {
//            return CreateInstance(instanceType, ctorArgs).As<T>();
//        }

//        /// <summary>
//        ///     Создать экземпляр указанного типа. Если возникает исключение, то возвращается null
//        /// </summary>
//        /// <param name="type">ReaderFieldDataType</param>
//        /// <param name="ctorArgs">Аргументы для конструктора типа</param>
//        /// <returns></returns>
//        public static object TryCreateInstance(Type type, params object[] ctorArgs)
//        {
//            try
//            {
//                return CreateInstance(type, ctorArgs);
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        /// <summary>
//        ///     Создать экземпляр указанного типа
//        /// </summary>
//        /// <param name="type">ReaderFieldDataType</param>
//        /// <param name="ctorArgs">Аргументы для конструктора типа</param>
//        /// <returns></returns>
//        public static object CreateInstance(Type type, params object[] ctorArgs)
//        {
//            if (ctorArgs == null)
//                ctorArgs = Array.Empty<object>();
//            var typeInfo = type.GetInfo();
//            if (typeInfo.DefaultConstructor != null && ctorArgs.Length == 0)
//                return typeInfo.DefaultConstructor();

//            if (typeInfo.IsDelegate)
//                return null;

//            if (type.IsInterface)
//            {
//                if (typeInfo.IsCollection)
//                {
//                    var lstType = InterfaceToInstanceMap.GetValueOrDefault(type) ?? InterfaceToInstanceMap.GetValueOrDefault(type.GetGenericTypeDefinition());
//                    var genericArgs = type.GetGenericArguments();
//                    if (genericArgs.Length == 0)
//                        genericArgs = new[] { typeof(object) };
//                    if (lstType.IsGenericTypeDefinition)
//                        lstType = lstType.MakeGenericType(genericArgs);
//                    return Activator.CreateInstance(lstType);
//                }
//                else
//                {
//                    throw new NotImplementedException();
//                }
//            }

//            if (type.IsArray)
//            {
//                if (ctorArgs.Length == 0)
//                    return Activator.CreateInstance(type, 0);
//                if (ctorArgs.Length == 1 && ctorArgs[0] is int)
//                    return Activator.CreateInstance(type, ctorArgs[0]);
//                return Activator.CreateInstance(type, ctorArgs.Length);
//            }

//            if (type.IsEnum)
//            {
//                return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? GetDefaultInstance(type);
//            }

//            if (type == typeof(string) && ctorArgs.Length == 0)
//                return string.Empty;

//            var defaultCtor = typeInfo.DefaultConstructor;
//            if (defaultCtor != null && ctorArgs.Length == 0)
//            {
//                try
//                {
//                    var objInstance = defaultCtor();
//                    return objInstance;
//                }
//                catch
//                {
//                    return GetDefaultInstance(type);
//                }
//            }

//            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);

//            if (ctor == null && type.IsValueType)
//                return GetDefaultInstance(type);

//            if (ctor == null)
//            {
//                throw new InvalidOperationException($"No constructor for type '{type}' with args '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'");
//            }

//            var obj = ctor.Invoke(ctorArgs);
//            return obj;
//        }

//        /// <summary>
//        ///     Преобразование объекта в другой тип. Ссылочные свойства копируются согласно параметру <see cref="deepProcessing"/>
//        /// </summary>
//        /// <param name="objInstance"></param>
//        /// <param name="nameComparison">Сравнение имён</param>
//        /// <param name="ctorArgs"></param>
//        /// <param name="deepProcessing">Копировать свойства ссылочного типа как новые экземпляры</param>
//        /// <returns></returns>
//        public static T Cast<T>(object objInstance, bool deepProcessing, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, object[] ctorArgs = null)
//        {
//            if (objInstance is T t)
//                return t;
//            if (objInstance == null)
//                return default;
//            return (T)Cast(objInstance, typeof(T), deepProcessing, nameComparison, ctorArgs);
//        }

//        /// <summary>
//        ///     Преобразование объекта в другой тип. Ссылочные свойства копируются согласно параметру <see cref="deepProcessing"/>
//        /// </summary>
//        /// <param name="objInstance"></param>
//        /// <param name="toType"></param>
//        /// <param name="nameComparison">Сравнение имён</param>
//        /// <param name="ctorArgs"></param>
//        /// <param name="deepProcessing">Копировать свойства ссылочного типа как новые экземпляры</param>
//        /// <returns></returns>
//        public static object Cast(object objInstance, Type toType, bool deepProcessing, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, object[] ctorArgs = null)
//        {
//            if (objInstance == null)
//                return null;

//            if (toType == typeof(object))
//                return objInstance;

//            var objType = objInstance.GetType();

//            if (objType == toType && !deepProcessing)
//                return objInstance;

//            if (objType.FullName == "System.RuntimeType" && toType.FullName == "System.ReaderFieldDataType")
//                return objInstance;

//            if (toType.IsAssignableFrom(objType) && deepProcessing && objInstance is ICloneable c)
//                return c.Clone();

//            if (toType.IsAssignableFrom(objType) && !deepProcessing)
//                return objInstance;

//            var objTypeInfo = objType.GetTypeInfo();
//            var toTypeInfo = toType.GetInfo();

//            if (objType.IsBasic() && toType.IsBasic())
//                return ChangeType(objInstance, toType);
//            if (objTypeInfo.IsValueType)
//            {
//                if (objType == typeof(string) && toType.IsCollection())
//                {
//                    var sl = $"{objInstance}".Split(new[] { ',', ';', ';', '|', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
//                    return ChangeType(sl, toType);
//                }

//                return ChangeType(objInstance, toType);
//            }

//            if (toType.IsAssignableFrom(objType) && toType.IsInterface)
//                toType = objType;

//            if (ctorArgs?.Length == 0)
//                ctorArgs = toType.GetConstructor(Type.EmptyTypes) == null ? GetValues(objInstance).Values.ToArray() : Array.Empty<object>();

//            object newInstance;
//            if (toType.IsInterface && toType == typeof(IEnumerable) && objType.IsCollection())
//                return objInstance;

//            if (toType.IsArray && objType.IsCollection())
//            {
//                newInstance = CreateInstance(toType, ((IEnumerable)objInstance).CountItems());
//            }
//            else
//            {
//                newInstance = CreateInstance(toType, ctorArgs);
//            }

//            Copy(objInstance, newInstance, deepProcessing, true, false, nameComparison);

//            return newInstance;
//        }

//        /// <summary>
//        /// Копировать значения свойств одного объекта в другой
//        /// </summary>
//        /// <param name="source"></param>
//        /// <param name="target"></param>
//        /// <param name="deepProcessing">True: Копировать ссылочные свойства как новые экземпляры, иначе простым присваиванием</param>
//        /// <param name="overwritePropertiesWithNullValues">Перезаписывать свойства в <see cref="target"/> null значениями из <see cref="source"/></param>
//        /// <param name="overwriteOnlyNullProperties">Перезаписывать свойства в <see cref="target"/> значениями из <see cref="source"/> только, если значения свойства в <see cref="target"/> == <see cref="NullValues"/></param>
//        /// <param name="nameComparison">Сравнение имен свойств</param>
//        public static void Copy(object source, object target, bool deepProcessing, bool overwritePropertiesWithNullValues, bool overwriteOnlyNullProperties, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
//        {
//            if (source == null || target == null)
//                return;

//            if (source is DataRowView drv)
//                source = drv.Row;

//            var srcType = source.GetType();
//            var sourceTypeInfo = srcType.GetInfo();
//            var targetType = target.GetType();
//            var targetTypeInfo = targetType.GetInfo();

//            if (source is DataRow dr)
//            {
//                var sharedPropNames = Enumerable.Intersect(targetTypeInfo.Properties.Select(x => x.Name), dr.Table.Columns.OfType<DataColumn>().Select(x => x.ColumnName), nameComparison.ToStringComparer()).ToArray();
//                foreach (var s in sharedPropNames)
//                    SetValue(target, dr[s], s);
//                return;
//            }


//            if (sourceTypeInfo.IsValueType && targetTypeInfo.IsValueType)
//                return;

//            if (typeof(IList).IsAssignableFrom(srcType) && typeof(IList).IsAssignableFrom(targetType))
//            {
//                var srcItemType = srcType.GetElementType() ?? srcType.GenericTypeArguments.FirstOrDefault() ?? typeof(object);
//                var srcItemTypeInfo = srcItemType.GetTypeInfo();
//                var targetItemType = targetType.GenericTypeArguments.FirstOrDefault() ??
//                                     targetType.GetElementType() ?? typeof(object);
//                var targetItemTypeInfo = targetItemType.GetTypeInfo();
//                var idx = 0;
//                foreach (var i in (IList)source)
//                {
//                    if (targetItemType == typeof(object) || targetItemType.IsImplements(srcItemType) || srcItemTypeInfo.IsValueType && targetItemTypeInfo.IsValueType)
//                    {
//                        var targetListItem = Cast(i, targetItemType, deepProcessing, nameComparison);
//                        if (targetType.IsArray)
//                            ((Array)target).SetValue(targetListItem, idx);
//                        else
//                            Invoke(target, nameof(IList.Add), targetListItem);
//                    }
//                    else
//                    {
//                        var targetListItem = CreateInstance(targetItemType);
//                        Copy(i, targetListItem, deepProcessing, overwritePropertiesWithNullValues, overwriteOnlyNullProperties, nameComparison);
//                        if (targetType.IsArray)
//                            ((Array)target).SetValue(targetListItem, idx);
//                        else
//                            Invoke(target, nameof(IList.Add), targetListItem);
//                    }

//                    idx++;
//                }

//                return;
//            }

//            if (sourceTypeInfo.IsDictionary && targetTypeInfo.IsDictionary)
//            {
//                var targetKeyType = targetTypeInfo.Type.GetGenericArguments()[0];
//                var targetValueType = targetTypeInfo.Type.GetGenericArguments()[1];
//                foreach (var i in (IDictionary)source)
//                {
//                    ((IDictionary)target)[ChangeType(GetValue(i, nameComparison, "Key"), targetKeyType) ?? throw new NullReferenceException("TypeHelper.Copy dictionary key is null!")] = ChangeType(GetValue(i, nameComparison, "Value"), targetValueType);
//                }
//                return;
//            }

//            var srcMembers = sourceTypeInfo.Properties;
//            var targetMembers = targetTypeInfo.Properties;
//            var sharedNames = Enumerable.Intersect(srcMembers.Select(x => x.Name), targetMembers.Select(x => x.Name), nameComparison.ToStringComparer()).ToArray();
//            foreach (var sn in sharedNames)
//            {
//                var srcPropValue = GetValue(source, nameComparison, sn);
//                if (NullValues.Contains(srcPropValue) && overwritePropertiesWithNullValues)
//                    continue;
//                var tm = targetMembers.FirstOrDefault(x => x.Name.Equals(sn, nameComparison));
//                if (tm == null || !tm.CanWrite)
//                    continue;
//                if (overwriteOnlyNullProperties)
//                {
//                    var targetPropValue = GetValue(target, nameComparison, tm.Name);
//                    if (!NullValues.Contains(targetPropValue))
//                        continue;
//                }
//                var castedValue = Cast(srcPropValue, tm.PropertyType, deepProcessing, nameComparison);
//                SetValue(target, castedValue, nameComparison, tm.Name);
//            }
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="toType">Новый тип</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null)
//        {
//            var toType = typeof(T);
//            return (T)ChangeType(value, toType, formatProvider);
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static bool TryChangeType<T>(object value, out T convert, IFormatProvider formatProvider = null)
//        {
//            convert = default;
//            if (TryChangeType(value, new[] { typeof(T) }, out var convert2, formatProvider))
//            {
//                convert = (T)convert2;
//                return true;
//            }
//            else
//                return false;
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static T ChangeTypeOrDefault<T>(object value, T defaultValue, IFormatProvider formatProvider = null)
//        {
//            if (TryChangeType<T>(value, out var convertedValue, formatProvider))
//            {
//                return convertedValue;
//            }
//            else
//                return defaultValue;
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="toTypes">Типы в которые пробовать конвертировать значения до первого успешного</param>
//        /// <param name="convert">Конвертированное значение</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static bool TryChangeType(object value, Type[] toTypes, out object convert, IFormatProvider formatProvider = null)
//        {
//            foreach (var t in toTypes)
//            {
//                if (TryChangeType(value, t, out convert, formatProvider))
//                    return true;
//            }
//            convert = null;
//            return false;
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="toTypes">Типы в которые пробовать конвертировать значения до первого успешного</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static object ChangeType(object value, Type[] toTypes, IFormatProvider formatProvider = null)
//        {
//            foreach (var t in toTypes)
//            {
//                if (TryChangeType(value, t, out var convert, formatProvider))
//                    return convert;
//            }
//            throw new Exception($"Нельзя конвертировать {value} ни в один из типов: {toTypes.ToCsv(x => x.Name)}");
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="toType">Новый тип</param>
//        /// <param name="convert">Конвертированное значение</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static bool TryChangeType(object value, Type toType, out object convert, IFormatProvider formatProvider = null)
//        {
//            try
//            {
//                convert = ChangeType(value, toType, formatProvider);
//                return true;
//            }
//            catch
//            {
//                convert = null;
//                return false;
//            }
//        }

//        /// <summary>
//        ///     Сменить тип значения
//        /// </summary>
//        /// <param name="value">Значение</param>
//        /// <param name="toType">Новый тип</param>
//        /// <param name="formatProvider">По умолчанию <see cref="CultureInfo.InvariantCulture"/></param>
//        /// <returns></returns>
//        public static object ChangeType(object value, Type toType, IFormatProvider formatProvider = null)
//        {
//            if (value == null || value.Equals(DBNull.Value))
//                return GetDefaultInstance(toType);
//            toType = Nullable.GetUnderlyingType(toType) ?? toType;
//            var fromType = value.GetType();
//            if (fromType == toType || toType.IsAssignableFrom(fromType))
//                return value;

//            var customConverter = GetCustomTypeConverter(fromType, toType);
//            if (customConverter != null)
//                return customConverter(value);

//            if (formatProvider == null)
//                formatProvider = CultureInfo.InvariantCulture;

//            if (toType == typeof(string))
//                return string.Format(formatProvider, "{0}", value);

//            var isValueNumeric = fromType.IsNumeric();

//            if (toType.IsEnum)
//            {
//                if (isValueNumeric)
//                    return Enum.ToObject(toType, ChangeType(value, typeof(int), formatProvider) ?? throw new NullReferenceException("ChangeType: Enum.ToObject"));
//                if (fromType == typeof(bool))
//                    return Enum.ToObject(toType, ChangeType(Convert.ChangeType(value, typeof(int)), typeof(int), formatProvider) ?? throw new NullReferenceException("ChangeType: Enum.ToObject"));
//                if (fromType == typeof(string))
//                    return Enum.Parse(toType, $"{value}");
//            }

//            if (typeof(Tuple).IsAssignableFrom(toType))
//                return CreateInstance(toType, GetValues(value).ToArray());

//            if (value is string s)
//            {
//                if (string.IsNullOrWhiteSpace(s))
//                    return toType.Default();
//                if (toType.IsEnum)
//                    return Enum.Parse(toType, s, true);
//                if (toType == typeof(DateTime))
//                    return StringToDateTimeConverter(s);
//            }

//            //try
//            //{
//            return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
//            //}
//            //catch (Exception ex)
//            //{
//            //    return default;
//            //}
//        }

//        /// <summary>
//        ///     Возвращает значение типа по умолчанию. Для ссылочных типов всегда возвращает null.
//        /// </summary>
//        /// <param name="type"></param>
//        /// <returns></returns>
//        public static object GetDefaultInstance(Type type)
//        {
//            return type.IsValueType ? Activator.CreateInstance(type) : null;
//        }

//        /// <summary>
//        /// Возвращает значение свойства объекта по имени или пути. Сравнение имен <see cref="StringComparison.OrdinalIgnoreCase"/>
//        /// </summary>
//        /// <param name="obj">Объект.</param>
//        /// <param name="propertyNames">Путь до вложенного свойства.</param>
//        /// <returns></returns>
//        public static object GetValue(object obj, params string[] propertyNames)
//        {
//            return GetValue(obj, StringComparison.OrdinalIgnoreCase, propertyNames);
//        }


//        private static ConcurrentDictionary<string, MemberInfo> _typePropsCache =
//            new ConcurrentDictionary<string, MemberInfo>();

//        /// <summary>
//        /// Возвращает значение свойства или поля объекта по имени или пути.
//        /// </summary>
//        /// <param name="obj">Объект.</param>
//        /// <param name="nameComparison">Сравнение имен свойств.</param>
//        /// <param name="propertyNames">Путь до вложенного свойства.</param>
//        /// <returns></returns>
//        public static object GetValue(object obj, StringComparison nameComparison, params string[] propertyNames)
//        {
//            if (obj == null || !propertyNames.Any())
//                return null;

//            if (obj is DataRow dr)
//                return dr[propertyNames.First()];

//            var memberName = propertyNames[0];
//            if (string.IsNullOrWhiteSpace(memberName))
//                return null;

//            var objTypeInfo = obj.GetType().GetInfo();
//            var propertyName = propertyNames[0];
//            var objMember = objTypeInfo.GetMember(propertyName);

//            if (objMember == null)
//                return null;
//            try
//            {
//                var value = objMember.IsProperty ? objMember.AsPropertyInfo().GetValue(obj) : objMember.IsField ? objMember.AsFieldInfo().GetValue(obj) : objMember.IsMethod ? objMember.AsMethodInfo().Invoke(obj, Array.Empty<object>()) : null;
//                return propertyNames.Length == 1 ? value : GetValue(value, nameComparison, propertyNames.Skip(1).ToArray());
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        /// <summary>
//        /// Устанавливает значение свойства объекта по имени или пути. Сравнение имен <see cref="StringComparison.OrdinalIgnoreCase"/>
//        /// </summary>
//        /// <param name="obj">Объект.</param>
//        /// <param name="value">Значение</param>
//        /// <param name="propertyNames">Путь до вложенного свойства.</param>
//        /// <returns>В случае успеха: true, иначе: false</returns>
//        public static bool SetValue(object obj, object value, params string[] propertyNames)
//        {
//            return SetValue(obj, value, StringComparison.OrdinalIgnoreCase, propertyNames);
//        }

//        /// <summary>
//        /// Устанавливает значение свойства объекта по имени или пути.
//        /// </summary>
//        /// <param name="obj">Объект.</param>
//        /// <param name="value">Значение</param>
//        /// <param name="nameComparison">Сравнение имен свойств.</param>
//        /// <param name="propertyNames">Путь до вложенного свойства.</param>
//        /// <returns>В случае успеха: true, иначе: false</returns>
//        public static bool SetValue(object obj, object value, StringComparison nameComparison, params string[] propertyNames)
//        {
//            if (obj == null || !propertyNames.Any())
//                return false;

//            if (obj is DataRow dr)
//            {
//                dr[propertyNames.First()] = ChangeType(value, dr.Table.Columns[propertyNames.First()].DataType ?? typeof(object));
//                return true;
//            }

//            var objTypeInfo = obj.GetType().GetInfo();
//            var propertyName = propertyNames[0];
//            var propertyOrField = objTypeInfo.GetMember(propertyName, x => x.IsProperty || x.IsField);
//            if (propertyOrField == null)
//                return false;
//            try
//            {
//                if (propertyNames.Length == 1)
//                {
//                    var valueType = value?.GetType();
//                    if (propertyOrField.IsProperty)
//                    {
//                        var pi = propertyOrField.AsPropertyInfo();
//                        if (pi.CanWrite)
//                        {
//                            pi.SetValue(obj, valueType == null || valueType == propertyOrField.Type || valueType.IsCollection() ? value : ChangeType(value, propertyOrField.Type));
//                            return true;
//                        }
//                        else
//                        {
//                            if (propertyOrField.PropertyBackingField != null)
//                            {
//                                propertyOrField.PropertyBackingField.SetValue(obj, valueType == null || valueType == propertyOrField.Type ? value : ChangeType(value, propertyOrField.Type));
//                                return true;
//                            }
//                        }
//                    }
//                    else
//                    {
//                        propertyOrField.AsFieldInfo().SetValue(obj, valueType == null || valueType == propertyOrField.Type ? value : ChangeType(value, propertyOrField.Type));
//                        return true;
//                    }
//                }
//                else
//                {
//                    var propValue = GetValue(obj, nameComparison, propertyName);
//                    if (propValue == null)
//                    {
//                        propValue = CreateInstance(propertyOrField.Type);
//                        var result = SetValue(obj, propValue, nameComparison, propertyName);
//                        if (!result)
//                            return false;
//                    }
//                    return SetValue(propValue, value, nameComparison, propertyNames.Skip(1).ToArray());
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine(ex.ToString());
//                return false;
//            }

//            return false;
//        }

//        /// <summary>
//        /// Вернуть массив значений свойств объекта. Сравнение имен <see cref="StringComparer.OrdinalIgnoreCase"/>
//        /// </summary>
//        /// <param name="obj"></param>
//        /// <param name="nameComparer"></param>
//        /// <param name="propertyNames">Указать конкретные имена свойств</param>
//        /// <returns></returns>
//        public static IDictionary<string, object> GetValues(object obj, StringComparer nameComparer, params string[] propertyNames)
//        {
//            return GetValues(obj, nameComparer, x => x.IsProperty && (propertyNames.Length == 0 || propertyNames.Contains(x.Name, nameComparer)));
//        }

//        /// <summary>
//        /// Вернуть массив значений свойств объекта. Сравнение имен <see cref="StringComparer.OrdinalIgnoreCase"/>
//        /// </summary>
//        /// <param name="obj"></param>
//        /// <param name="propertyNames">Указать конкретные имена свойств</param>
//        /// <returns></returns>
//        public static IDictionary<string, object> GetValues(object obj, params string[] propertyNames)
//        {
//            return GetValues(obj, StringComparer.OrdinalIgnoreCase, propertyNames);
//        }

//        /// <summary>
//        /// Вернуть массив значений свойств объекта
//        /// </summary>
//        /// <param name="obj">Объект</param>
//        /// <param name="nameComparer">Сравнение имен</param>
//        /// <param name="propertyFilter">Фильтр свойств</param>
//        /// <returns></returns>
//        public static Dictionary<string, object> GetValues(object obj, StringComparer nameComparer, Func<MemberInfoEx, bool> propertyFilter = null)
//        {
//            var propValues = new Dictionary<string, object>(nameComparer);
//            if (obj == null)
//                return propValues;

//            var typeInfo = obj.GetType().GetInfo();
//            var props = propertyFilter == null ? typeInfo.Members.Where(x => x.IsProperty).ToArray() : typeInfo.Members.Where(propertyFilter).ToArray();
//            foreach (var property in props)
//            {
//                if (property == null) continue;
//                try
//                {
//                    propValues[property.Name] = property.GetValue(obj);
//                }
//                catch (Exception ex)
//                {
//                    propValues[property.Name] = ex;
//                }
//            }

//            return propValues;
//        }

//        /// <summary>
//        ///     Вызов метода объекта по имени с любым модификатором доступа
//        /// </summary>
//        /// <param name="obj">Объект</param>
//        /// <param name="methodName">Имя метода</param>
//        /// <param name="args">Аргументы для метода</param>
//        /// <returns></returns>
//        public static object Invoke(object obj, string methodName, params object[] args)
//        {
//            if (obj == null)
//                return null;
//            var objTypes = obj is Type type ? new[] { type } : GetBaseTypes(obj.GetType(), true).ToArray();
//            var methodInfo = objTypes.Select(t =>
//                t.GetMethod(methodName, DefaultBindingFlags, Type.DefaultBinder,
//                    args.Select(arg => arg?.GetType()).ToArray(), null))
//                .FirstOrDefault(x => x != null) ?? throw new Exception($"Не найден метод {methodName} ( {string.Join(",", args.Select(arg => arg?.GetType().Name))} )");
//            if (methodInfo.ContainsGenericParameters)
//            {
//                Type[] genericArgs = methodInfo.GetGenericArguments();
//                methodInfo = methodInfo.MakeGenericMethod(genericArgs);
//                args = args.Skip(genericArgs.Length).ToArray();
//            }

//            return methodInfo.Invoke(obj, args);
//        }

//        /// <summary>
//        /// BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
//        /// </summary>
//        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

//        static Converter<string, DateTime?> StringToDateTimeConverter = (s) =>
//        {
//            DateTime d;
//            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out d))
//                return d;
//            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
//                return d;

//            var dateTimeParts = s.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
//            var dateParts = dateTimeParts[0].Split(new[] { '.', '\\', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
//            var year = dateParts.IndexOf(x => x.Length == 4);
//            var dayForSure = dateParts.IndexOf(x => x.Length <= 2 && x.ToInt() > 12 && x.ToInt() <= 31);
//            var dayPossible = dateParts.IndexOf((x, i) => x.Length <= 2 && x.ToInt() > 0 && x.ToInt() <= 31 && i != dayForSure);
//            var day = dayForSure >= 0 ? dayForSure : dayPossible;
//            var month = dateParts.IndexOf((x, i) => x.Length <= 2 && x.ToInt() > 0 && x.ToInt() <= 12 && i != day);


//            if (year >= 0 && month >= 0 && day >= 0 && year != month && month != day && day != year)
//                return new DateTime((int)dateParts[year].ToInt(), (int)dateParts[month].ToInt(), (int)dateParts[day].ToInt());

//            if (dateTimeParts[0].Length == 8)
//                return new DateTime(ChangeType<int>(s.SubStr(0, 3)), ChangeType<int>(s.SubStr(4, 5)), ChangeType<int>(s.SubStr(6, 7)));

//            return null;
//        };
//    }
//}
