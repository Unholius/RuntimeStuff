using RuntimeStuff.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RuntimeStuff
{
    /// <summary>
    /// Статический класс для работы с объектами, включая преобразование типов, доступ к членам объектов, копирование значений и другие операции
    /// </summary>
    public static class Obj
    {
        /// <summary>
        /// Словарь пользовательских преобразователей типов
        /// </summary>
        public static ConcurrentDictionary<Type, ConcurrentDictionary<Type, Func<object, object>>> CustomTypeConverters =
            new ConcurrentDictionary<Type, ConcurrentDictionary<Type, Func<object, object>>>();

        /// <summary>
        /// Получить расширенную информацию о свойстве/поле/методе/конструкторе объекта по имени или пути
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="obj">Объект</param>
        /// <param name="memberName">Имя члена или путь через точку</param>
        /// <returns>Информация о члене или null если не найден</returns>
        public static MemberInfoEx GetMember<T>(T obj, string memberName)
        {
            var objType = obj?.GetType() ?? typeof(T);
            var memberNames = SplitCache.Get(memberName);
            var ti = objType.GetMemberInfoEx();

            for (var i = 0; i < memberNames.Length; i++)
            {
                var name = memberNames[i];
                var mi = ti.GetMember(name);
                if (mi == null)
                    return null;

                mi = mi.IsCollection && i < memberNames.Length - 1 && !memberNames[i + 1].Equals("Item", StringComparison.OrdinalIgnoreCase)
                    ? mi.ElementType.GetMemberInfoEx()
                    : mi.GetMemberInfoEx();
                ti = mi;
            }
            return ti;
        }

        /// <summary>
        /// Увеличить значение переменной на указанный шаг
        /// </summary>
        /// <param name="value">Значение для увеличения (передается по ссылке)</param>
        /// <param name="step">Шаг увеличения (по умолчанию 1)</param>
        /// <exception cref="ArgumentNullException">Если значение null</exception>
        /// <exception cref="NotImplementedException">Если тип значения не поддерживается</exception>
        public static void Increase(ref object value, int step = 1)
        {
            if (value == null)
                throw new ArgumentNullException("Obj.Increase: value is null!");

            var valueType = value.GetType();

            if (valueType.IsEnum || valueType.IsNumeric(false))
            {
                value = ChangeType(ChangeType<long>(value) + step, valueType);
                return;
            }

            if (valueType.IsNumeric(true))
            {
                value = ChangeType(ChangeType<decimal>(value) + step, valueType);
                return;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Преобразовать значение в указанный тип
        /// </summary>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="toType">Тип, в который нужно преобразовать</param>
        /// <param name="formatProvider">Формат преобразования (по умолчанию CultureInfo.InvariantCulture)</param>
        /// <returns>Преобразованное значение</returns>
        public static object ChangeType(object value, Type toType, IFormatProvider formatProvider = null)
        {
            if (value == null || value.Equals(DBNull.Value))
                return TypeExtensions.Default(toType);

            toType = Nullable.GetUnderlyingType(toType) ?? toType;
            var fromType = value.GetType();

            if (fromType == toType || toType.IsAssignableFrom(fromType))
                return value;

            // Проверка пользовательских преобразователей
            var customConverter = GetCustomTypeConverter(fromType, toType);
            if (customConverter != null)
                return customConverter(value);

            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture;

            if (toType == typeof(string))
                return string.Format(formatProvider, "{0}", value);

            var isValueNumeric = fromType.IsNumeric();

            // Обработка преобразования в перечисление
            if (toType.IsEnum)
            {
                if (isValueNumeric)
                    return Enum.ToObject(toType, ChangeType(value, typeof(int), formatProvider) ?? throw new NullReferenceException("ChangeType: Enum.ToObject"));
                if (fromType == typeof(bool))
                    return Enum.ToObject(toType, ChangeType(Convert.ChangeType(value, typeof(int)), typeof(int), formatProvider) ?? throw new NullReferenceException("ChangeType: Enum.ToObject"));
                if (fromType == typeof(string))
                    return Enum.Parse(toType, $"{value}");
            }

            // Обработка кортежей
            if (typeof(Tuple).IsAssignableFrom(toType))
                return TypeExtensions.Create(toType, GetValues(value).ToArray());

            // Обработка строковых значений
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return toType.Default();
                if (toType.IsEnum)
                    return Enum.Parse(toType, s, true);
                if (toType == typeof(DateTime))
                    return Converters.StringToDateTimeConverter(s);
            }

            // Стандартное преобразование
            return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Получить пользовательский преобразователь типов
        /// </summary>
        /// <param name="typeFrom">Исходный тип</param>
        /// <param name="typeTo">Целевой тип</param>
        /// <returns>Функция преобразования или null</returns>
        public static Func<object, object> GetCustomTypeConverter(Type typeFrom, Type typeTo)
        {
            if (!CustomTypeConverters.TryGetValue(typeFrom, out var typeConverters) || typeConverters == null)
                return null;

            if (!typeConverters.TryGetValue(typeTo, out var converter) || converter == null)
                return null;

            return converter;
        }

        /// <summary>
        /// Добавить пользовательский преобразователь типов
        /// </summary>
        /// <typeparam name="TFrom">Исходный тип</typeparam>
        /// <typeparam name="TTo">Целевой тип</typeparam>
        /// <param name="converter">Функция преобразования</param>
        public static void AddCustomTypeConverter<TFrom, TTo>(Func<TFrom, TTo> converter)
        {
            if (!CustomTypeConverters.TryGetValue(typeof(TFrom), out var typeConverters) || typeConverters == null)
            {
                typeConverters = new ConcurrentDictionary<Type, Func<object, object>>();
                CustomTypeConverters[typeof(TFrom)] = typeConverters;
            }
            typeConverters[typeof(TTo)] = converter.ConvertFunc();
        }

        /// <summary>
        /// Преобразовать значение в указанный тип
        /// </summary>
        /// <typeparam name="T">Целевой тип</typeparam>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="formatProvider">Формат преобразования</param>
        /// <returns>Преобразованное значение</returns>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null)
        {
            var toType = typeof(T);
            return (T)ChangeType(value, toType, formatProvider);
        }

        /// <summary>
        /// Попытаться преобразовать значение в указанный тип
        /// </summary>
        /// <typeparam name="T">Целевой тип</typeparam>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="convert">Результат преобразования</param>
        /// <param name="formatProvider">Формат преобразования</param>
        /// <returns>Успешно ли выполнено преобразование</returns>
        public static bool TryChangeType<T>(object value, out T convert, IFormatProvider formatProvider = null)
        {
            convert = default;
            if (TryChangeType(value, new[] { typeof(T) }, out var convert2, formatProvider))
            {
                convert = (T)convert2;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Попытаться преобразовать значение в один из указанных типов
        /// </summary>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="toTypes">Возможные целевые типы</param>
        /// <param name="convert">Результат преобразования</param>
        /// <param name="formatProvider">Формат преобразования</param>
        /// <returns>Успешно ли выполнено преобразование</returns>
        public static bool TryChangeType(object value, Type[] toTypes, out object convert, IFormatProvider formatProvider = null)
        {
            foreach (var t in toTypes)
            {
                if (TryChangeType(value, t, out convert, formatProvider))
                    return true;
            }
            convert = null;
            return false;
        }

        /// <summary>
        /// Попытаться преобразовать значение в указанный тип
        /// </summary>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="toType">Целевой тип</param>
        /// <param name="convert">Результат преобразования</param>
        /// <param name="formatProvider">Формат преобразования</param>
        /// <returns>Успешно ли выполнено преобразование</returns>
        public static bool TryChangeType(object value, Type toType, out object convert, IFormatProvider formatProvider = null)
        {
            try
            {
                convert = ChangeType(value, toType, formatProvider);
                return true;
            }
            catch
            {
                convert = null;
                return false;
            }
        }

        /// <summary>
        /// Сжать массив байт с использованием GZip
        /// </summary>
        /// <param name="bytes">Исходные данные</param>
        /// <returns>Сжатые данные</returns>
        public static byte[] Zip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        /// <summary>
        /// Распаковать массив байт, сжатый GZip
        /// </summary>
        /// <param name="bytes">Сжатые данные</param>
        /// <returns>Распакованные данные</returns>
        public static byte[] UnZip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    CopyTo(gs, mso);
                }

                return mso.ToArray();
            }
        }

        /// <summary>
        /// Копировать данные из одного потока в другой
        /// </summary>
        /// <param name="src">Исходный поток</param>
        /// <param name="dest">Целевой поток</param>
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];
            int cnt;
            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        /// <summary>
        /// Кеш для результатов string.Split
        /// </summary>
        public static readonly Cache<string, string[]> SplitCache =
            new Cache<string, string[]>(x => x.Split(new[] { '.', '\\', '/', '[', ']', '(', ')' }, StringSplitOptions.RemoveEmptyEntries));

        // Остальные методы класса с аналогичными комментариями...
        // (Продолжение комментариев для всех оставшихся методов)

        /// <summary>
        /// Получить значение свойства объекта по пути с преобразованием в указанный тип
        /// </summary>
        /// <typeparam name="T">Тип результата</typeparam>
        /// <param name="obj">Объект</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <param name="propNames">Путь к свойству (массив имен)</param>
        /// <returns>Значение свойства или default(T)</returns>
        public static T Get<T>(object obj, StringComparison nameComparison, string[] propNames)
        {
            return ChangeType<T>(Get(obj, nameComparison, propNames));
        }

        /// <summary>
        /// Получить значение свойства объекта по имени с преобразованием в указанный тип
        /// </summary>
        /// <typeparam name="T">Тип результата</typeparam>
        /// <param name="obj">Объект</param>
        /// <param name="propName">Имя свойства или путь через точки</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <returns>Значение свойства или default(T)</returns>
        public static T Get<T>(object obj, string propName, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            return (T)Get(obj, propName, nameComparison, typeof(T));
        }

        /// <summary>
        /// Получить значение свойства объекта по имени
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="propName">Имя свойства или путь через точки</param>
        /// <param name="convertToType">Тип для преобразования результата</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <returns>Значение свойства или null</returns>
        public static object Get(object obj, string propName, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, Type convertToType = null)
        {
            if (obj == null || string.IsNullOrWhiteSpace((propName)))
                return null;

            object getValue;
            switch (obj)
            {
                case DataRow dr:
                    getValue = dr.RowState == DataRowState.Detached ? null : dr[propName];
                    break;
                case DataRowView drv:
                    return Get(drv.Row, propName, nameComparison, convertToType);
                default:
                    getValue = Get(obj, nameComparison, SplitCache.Get(propName));
                    break;
            }
            return convertToType == null ? getValue : ChangeType(getValue, convertToType);
        }

        /// <summary>
        /// Получить значение свойства объекта по пути
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <param name="propertyNames">Путь к свойству (массив имен)</param>
        /// <returns>Значение свойства или null</returns>
        public static object Get(object obj, StringComparison nameComparison, params string[] propertyNames)
        {
            if (obj == null || !propertyNames.Any())
                return null;

            if (obj is DataRow dr)
                return dr[propertyNames.First()];

            var memberName = propertyNames[0];
            if (string.IsNullOrWhiteSpace(memberName))
                return null;

            var objTypeInfo = obj.GetType().GetMemberInfoEx();
            var propertyName = propertyNames[0];
            var objMember = objTypeInfo.GetMember(propertyName);

            if (objMember == null)
                return null;

            try
            {
                var value = objMember.IsProperty
                    ? objMember.AsPropertyInfo().GetValue(obj)
                    : objMember.IsField
                        ? objMember.AsFieldInfo().GetValue(obj)
                        : objMember.IsMethod
                            ? objMember.AsMethodInfo().Invoke(obj, Array.Empty<object>())
                            : null;

                return propertyNames.Length == 1
                    ? value
                    : Get(value, nameComparison, propertyNames.Skip(1).ToArray());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Установить значение свойства объекта по имени
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="propName">Имя свойства или путь через точки</param>
        /// <param name="value">Новое значение</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <returns>Успешно ли выполнена операция</returns>
        public static bool Set(object obj, string propName, object value, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            return Set(obj, value, nameComparison, SplitCache.Get(propName));
        }

        /// <summary>
        /// Установить несколько значений свойств объекта
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="values">Пары имя-значение</param>
        public static void Set(object obj, IEnumerable<KeyValuePair<string, object>> values)
        {
            foreach (var kv in values)
                Set(obj, kv.Key, kv.Value);
        }

        /// <summary>
        /// Установить значение свойства объекта по пути
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="value">Новое значение</param>
        /// <param name="propertyNames">Путь к свойству (массив имен)</param>
        /// <returns>Успешно ли выполнена операция</returns>
        public static bool Set(object obj, object value, params string[] propertyNames)
        {
            return Set(obj, value, StringComparison.OrdinalIgnoreCase, propertyNames);
        }

        /// <summary>
        /// Установить значение свойства объекта по пути
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="value">Новое значение</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <param name="propertyNames">Путь к свойству (массив имен)</param>
        /// <returns>Успешно ли выполнена операция</returns>
        public static bool Set(object obj, object value, StringComparison nameComparison, params string[] propertyNames)
        {
            if (obj == null || !propertyNames.Any())
                return false;

            if (obj is DataRow dr)
            {
                dr[propertyNames.First()] = ChangeType(value, dr.Table.Columns[propertyNames.First()].DataType ?? typeof(object));
                return true;
            }

            var objTypeInfo = obj.GetType().GetMemberInfoEx();
            var propertyName = propertyNames[0];
            var propertyOrField = objTypeInfo.GetMember(propertyName, x => x.IsProperty || x.IsField);

            if (propertyOrField == null)
                return false;

            try
            {
                if (propertyNames.Length == 1)
                {
                    var valueType = value?.GetType();
                    if (propertyOrField.IsProperty)
                    {
                        var pi = propertyOrField.AsPropertyInfo();
                        if (pi.CanWrite)
                        {
                            pi.SetValue(obj, valueType == null || valueType == propertyOrField.Type || valueType.IsCollection()
                                ? value
                                : ChangeType(value, propertyOrField.Type));
                            return true;
                        }
                        else
                        {
                            if (propertyOrField.PropertyBackingField != null)
                            {
                                propertyOrField.PropertyBackingField.SetValue(obj,
                                    valueType == null || valueType == propertyOrField.Type
                                        ? value
                                        : ChangeType(value, propertyOrField.Type));
                                return true;
                            }
                        }
                    }
                    else
                    {
                        propertyOrField.AsFieldInfo().SetValue(obj,
                            valueType == null || valueType == propertyOrField.Type
                                ? value
                                : ChangeType(value, propertyOrField.Type));
                        return true;
                    }
                }
                else
                {
                    var propValue = Get(obj, nameComparison, propertyName);
                    if (propValue == null)
                    {
                        propValue = TypeExtensions.Create(propertyOrField.Type);
                        var result = Set(obj, propValue, nameComparison, propertyName);
                        if (!result)
                            return false;
                    }
                    return Set(propValue, value, nameComparison, propertyNames.Skip(1).ToArray());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return false;
            }

            return false;
        }

        /// <summary>
        /// Копировать значения свойств из одного объекта в другой
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="target">Целевой объект</param>
        /// <param name="deepProcessing">Глубокое копирование (создание новых экземпляров для ссылочных типов)</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        public static void Copy(object source, object target, bool deepProcessing = false, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            Copy(source, target, deepProcessing, true, false, nameComparison);
        }

        /// <summary>
        /// Копировать значения свойств из одного объекта в другой с дополнительными параметрами
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="target">Целевой объект</param>
        /// <param name="deepProcessing">Глубокое копирование</param>
        /// <param name="overwritePropertiesWithNullValues">Перезаписывать свойства null значениями</param>
        /// <param name="overwriteOnlyNullProperties">Перезаписывать только null свойства</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        public static void Copy(object source, object target, bool deepProcessing, bool overwritePropertiesWithNullValues, bool overwriteOnlyNullProperties, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (source == null || target == null)
                return;

            if (source is DataRowView drv)
                source = drv.Row;

            var srcType = source.GetType();
            var sourceTypeInfo = srcType.GetMemberInfoEx();
            var targetType = target.GetType();
            var targetTypeInfo = targetType.GetMemberInfoEx();

            // Обработка DataRow
            if (source is DataRow dr)
            {
                var sharedPropNames = Enumerable.Intersect(
                    targetTypeInfo.Properties.Select(x => x.Name),
                    dr.Table.Columns.OfType<DataColumn>().Select(x => x.ColumnName),
                    nameComparison.ToStringComparer()).ToArray();

                foreach (var s in sharedPropNames)
                    Set(target, dr[s], s);
                return;
            }

            if (sourceTypeInfo.IsValueType && targetTypeInfo.IsValueType)
                return;

            // Обработка списков
            if (typeof(IList).IsAssignableFrom(srcType) && typeof(IList).IsAssignableFrom(targetType))
            {
                var srcItemType = srcType.GetElementType() ?? srcType.GenericTypeArguments.FirstOrDefault() ?? typeof(object);
                var srcItemTypeInfo = srcItemType.GetTypeInfo();
                var targetItemType = targetType.GenericTypeArguments.FirstOrDefault() ??
                                     targetType.GetElementType() ?? typeof(object);
                var targetItemTypeInfo = targetItemType.GetTypeInfo();
                var idx = 0;

                foreach (var i in (IList)source)
                {
                    if (targetItemType == typeof(object) || targetItemType.IsImplements(srcItemType) || srcItemTypeInfo.IsValueType && targetItemTypeInfo.IsValueType)
                    {
                        var targetListItem = Cast(i, targetItemType, deepProcessing, nameComparison);
                        if (targetType.IsArray)
                            ((Array)target).SetValue(targetListItem, idx);
                        else
                            Call(target, nameof(IList.Add), targetListItem);
                    }
                    else
                    {
                        var targetListItem = New(targetItemType);
                        Copy(i, targetListItem, deepProcessing, overwritePropertiesWithNullValues, overwriteOnlyNullProperties, nameComparison);
                        if (targetType.IsArray)
                            ((Array)target).SetValue(targetListItem, idx);
                        else
                            Call(target, nameof(IList.Add), targetListItem);
                    }

                    idx++;
                }

                return;
            }

            // Обработка словарей
            if (sourceTypeInfo.IsDictionary && targetTypeInfo.IsDictionary)
            {
                var targetKeyType = targetTypeInfo.Type.GetGenericArguments()[0];
                var targetValueType = targetTypeInfo.Type.GetGenericArguments()[1];
                foreach (var i in (IDictionary)source)
                {
                    ((IDictionary)target)[
                        ChangeType(Get(i, nameComparison, "Key"), targetKeyType) ?? throw new NullReferenceException("TypeHelper.Copy dictionary key is null!")
                    ] = ChangeType(Get(i, nameComparison, "Value"), targetValueType);
                }
                return;
            }

            // Копирование обычных свойств
            var srcMembers = sourceTypeInfo.Properties;
            var targetMembers = targetTypeInfo.Properties;
            var sharedNames = Enumerable.Intersect(
                srcMembers.Select(x => x.Name),
                targetMembers.Select(x => x.Name),
                nameComparison.ToStringComparer()).ToArray();

            foreach (var sn in sharedNames)
            {
                var srcPropValue = Get(source, nameComparison, sn);
                if (TypeExtensions.NullValues.Contains(srcPropValue) && overwritePropertiesWithNullValues)
                    continue;

                var tm = targetMembers.FirstOrDefault(x => x.Name.Equals(sn, nameComparison));
                if (tm == null || !tm.CanWrite)
                    continue;

                if (overwriteOnlyNullProperties)
                {
                    var targetPropValue = Get(target, nameComparison, tm.Name);
                    if (!TypeExtensions.NullValues.Contains(targetPropValue))
                        continue;
                }

                var castedValue = Cast(srcPropValue, tm.PropertyType, deepProcessing, nameComparison);
                Set(target, castedValue, nameComparison, tm.Name);
            }
        }

        /// <summary>
        /// Объединить значения свойств из одного объекта в другой (перезаписываются только null свойства)
        /// </summary>
        /// <param name="source">Источник</param>
        /// <param name="target">Целевой объект</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        public static void Merge(object source, object target, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            Copy(source, target, false, false, true, nameComparison);
        }

        /// <summary>
        /// Вызвать метод объекта
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="methodName">Имя метода</param>
        /// <param name="args">Аргументы метода</param>
        /// <returns>Результат выполнения метода или null</returns>
        public static object Call(object obj, string methodName, params object[] args)
        {
            if (obj == null)
                return null;

            var typeInfoEx = obj.GetType().GetMemberInfoEx();
            var mi = typeInfoEx.Methods.FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals((string)x.Name, methodName));
            if (mi == null)
                return null;
            return mi.Invoke(obj, args);
        }

        /// <summary>
        /// Преобразовать значение в указанный тип
        /// </summary>
        /// <typeparam name="T">Целевой тип</typeparam>
        /// <returns>Преобразованное значение</returns>
        public static T Cast<T>(object value)
        {
            return Cast<T>(value, true);
        }

        /// <summary>
        /// Преобразовать объект в указанный тип
        /// </summary>
        /// <typeparam name="T">Целевой тип</typeparam>
        /// <param name="objInstance">Объект для преобразования</param>
        /// <param name="deepProcessing">Глубокое преобразование</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <param name="ctorArgs">Аргументы конструктора</param>
        /// <returns>Преобразованное значение</returns>
        public static T Cast<T>(object objInstance, bool deepProcessing, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, object[] ctorArgs = null)
        {
            if (objInstance is T direct)
                return direct;

            if (objInstance == null)
                return default;

            return (T)Cast(objInstance, typeof(T), deepProcessing, nameComparison, ctorArgs);
        }

        /// <summary>
        /// Преобразовать объект в указанный тип
        /// </summary>
        /// <param name="objInstance">Объект для преобразования</param>
        /// <param name="toType">Целевой тип</param>
        /// <param name="deepProcessing">Глубокое преобразование</param>
        /// <param name="nameComparison">Способ сравнения имен</param>
        /// <param name="ctorArgs">Аргументы конструктора</param>
        /// <returns>Преобразованное значение</returns>
        public static object Cast(object objInstance, Type toType, bool deepProcessing, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, object[] ctorArgs = null)
        {
            if (objInstance == null)
                return null;

            if (toType == typeof(object))
                return objInstance;

            var objType = objInstance.GetType();

            if (objType == toType && !deepProcessing)
                return objInstance;

            if (objType.FullName == "System.RuntimeType" && toType.FullName == "System.ReaderFieldDataType")
                return objInstance;

            if (toType.IsAssignableFrom(objType) && deepProcessing && objInstance is ICloneable c)
                return c.Clone();

            if (toType.IsAssignableFrom(objType) && !deepProcessing)
                return objInstance;

            var objTypeInfo = objType.GetTypeInfo();
            var toTypeInfo = toType.GetMemberInfoEx();

            if (objType.IsBasic() && toType.IsBasic())
                return ChangeType(objInstance, toType);
            if (objTypeInfo.IsValueType)
            {
                if (objType == typeof(string) && toType.IsCollection())
                {
                    var sl = $"{objInstance}".Split(new[] { ',', ';', ';', '|', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    return ChangeType(sl, toType);
                }

                return ChangeType(objInstance, toType);
            }

            if (toType.IsAssignableFrom(objType) && toType.IsInterface)
                toType = objType;

            if (ctorArgs?.Length == 0)
                ctorArgs = toType.GetConstructor(Type.EmptyTypes) == null ? GetValues(objInstance) : Array.Empty<object>();

            object newInstance;
            if (toType.IsInterface && toType == typeof(IEnumerable) && objType.IsCollection())
                return objInstance;

            if (toType.IsArray && objType.IsCollection())
            {
                newInstance = New(toType, ((IEnumerable)objInstance).CountItems());
            }
            else
            {
                newInstance = New(toType, ctorArgs);
            }

            Copy(objInstance, newInstance, deepProcessing, true, false, nameComparison);

            return newInstance;
        }

        /// <summary>
        /// Попытаться преобразовать значение в указанный тип
        /// </summary>
        /// <typeparam name="T">Целевой тип</typeparam>
        /// <param name="value">Значение для преобразования</param>
        /// <param name="defaultIfFailed">Значение по умолчанию в случае ошибки</param>
        /// <returns>Преобразованное значение или значение по умолчанию</returns>
        public static T TryCast<T>(object value, T defaultIfFailed)
        {
            try
            {
                return Cast<T>(value, true);
            }
            catch
            {
                return defaultIfFailed;
            }
        }

        /// <summary>
        /// Получить значения указанных свойств объекта
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="propertyNames">Имена свойств</param>
        /// <returns>Массив значений</returns>
        public static object[] GetValues(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return Array.Empty<object>();

            if (!propertyNames.Any())
                propertyNames = obj.GetType()
                    .GetMemberInfoEx().Members
                    .Where(x => x.IsProperty && x.IsPublic)
                    .Select(x => x.Name).ToArray();

            return propertyNames.Select(x => Get(obj, x)).ToArray();
        }

        /// <summary>
        /// Получить значения указанных свойств объекта с преобразованием в указанный тип
        /// </summary>
        /// <typeparam name="T">Тип результата</typeparam>
        /// <param name="obj">Объект</param>
        /// <param name="propertyNames">Имена свойств</param>
        /// <returns>Массив значений</returns>
        public static T[] GetValues<T>(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return Array.Empty<T>();

            if (!propertyNames.Any())
                propertyNames = obj.GetType()
                    .GetMemberInfoEx().Members
                    .Where(x => x.IsProperty && x.IsPublic)
                    .Select(x => x.Name).ToArray();

            return propertyNames.Select(x => Get<T>(obj, x)).ToArray();
        }

        /// <summary>
        /// Получить значения свойств объекта, удовлетворяющих условию
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="matchCriteria">Условие выбора свойств</param>
        /// <param name="args">Дополнительные аргументы</param>
        /// <returns>Массив значений</returns>
        public static object[] GetValues(object obj, Func<MemberInfoEx, bool> matchCriteria, params object[] args)
        {
            return GetMembersValues(obj, matchCriteria).Values.ToArray();
        }

        /// <summary>
        /// Получить значения свойств объекта, удовлетворяющих условию, в виде словаря
        /// </summary>
        /// <param name="obj">Объект</param>
        /// <param name="matchCriteria">Условие выбора свойств</param>
        /// <param name="args">Дополнительные аргументы</param>
        /// <returns>Словарь имя-значение</returns>
        public static Dictionary<string, object> GetMembersValues(object obj, Func<MemberInfoEx, bool> matchCriteria, params object[] args)
        {
            var d = new Dictionary<string, object>();
            if (obj == null)
                return d;
            var mi = obj.GetType().GetMemberInfoEx();
            return mi.Members.Where(matchCriteria).ToDictionary(key => key.Name, value => value.GetValue(obj, args));
        }

        /// <summary>
        /// Получить атрибут объекта
        /// </summary>
        /// <typeparam name="T">Тип атрибута</typeparam>
        /// <param name="obj">Объект</param>
        /// <returns>Атрибут или null</returns>
        public static T GetAttribute<T>(object obj) where T : Attribute
        {
            if (obj == null)
                return default(T);
            var objType = obj.GetType().GetMemberInfoEx();
            return objType.Attributes.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Получить атрибут свойства объекта
        /// </summary>
        /// <typeparam name="T">Тип атрибута</typeparam>
        /// <param name="obj">Объект</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <returns>Атрибут или null</returns>
        public static T GetAttribute<T>(object obj, string propertyName) where T : Attribute
        {
            if (obj == null)
                return default(T);
            var mi = GetMember(obj, propertyName);
            if (mi == null)
                return default(T);
            return mi.Attributes.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Проверить существует ли метод с именем
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="stringComparison"></param>
        /// <returns></returns>
        public static bool MethodExists(Type type, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(type, methodName, stringComparison) != null;
        }

        /// <summary>
        /// Проверить существует ли метод с именем
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="stringComparison"></param>
        /// <returns></returns>
        public static MemberInfoEx GetMethodInfo(Type type, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            var mi = type.GetTypeInfo();
            return mi.GetMemberInfoEx().Members.FirstOrDefault(x => x.IsMethod && x.Name.Equals(methodName, stringComparison));
        }

        /// <summary>
        /// Проверить существует ли метод с именем
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="stringComparison"></param>
        /// <returns></returns>
        public static MemberInfoEx GetMethodInfo<T>(string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(typeof(T), methodName, stringComparison);
        }

        /// <summary>
        /// Проверить существует ли метод с именем
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="stringComparison"></param>
        /// <returns></returns>
        public static MemberInfoEx GetMethodInfo<T>(T obj, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(obj?.GetType() ?? typeof(T), methodName, stringComparison);
        }

        /// <summary>
        /// Создать новый экземпляр объекта указанного типа
        /// </summary>
        /// <param name="type"></param>
        /// <param name="args">Аргументы для конструктора</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static object New(Type type, params object[] args)
        {
            return TypeExtensions.Create(type, args);
        }

        /// <summary>
        /// Создать новый экземпляр объекта указанного типа
        /// </summary>
        /// <param name="type"></param>
        /// <param name="args">Аргументы для конструктора</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static T New<T>(Type type, params object[] args) where T : class
        {
            return New(type, args) as T;
        }

        /// <summary>
        /// Создать новый экземпляр объекта указанного типа
        /// </summary>
        /// <param name="args">Аргументы для конструктора</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static T New<T>(params object[] args)
        {
            return (T)New(typeof(T), args);
        }
    }
}