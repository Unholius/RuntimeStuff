using RuntimeStuff.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace RuntimeStuff
{
    /// <summary>
    /// Статический вспомогательный класс для расширенной работы с объектами, их свойствами, полями, методами и типами в .NET с использованием рефлексии.
    /// <para>
    /// <b>Основные возможности:</b>
    /// <list type="bullet">
    /// <item>— Универсальный доступ к значениям свойств и полей по имени или пути (в том числе вложенным и индексированным), с возможностью преобразования типов.</item>
    /// <item>— Гибкое преобразование типов, поддержка пользовательских конвертеров, безопасные и fallback-преобразования.</item>
    /// <item>— Копирование и слияние свойств между объектами, включая глубокое копирование, работу с коллекциями и словарями, а также правила перезаписи значений.</item>
    /// <item>— Динамический вызов методов по имени, получение и установка значений через <see cref="MemberInfoEx"/>.</item>
    /// <item>— Получение атрибутов для объектов и их членов, расширенная работа с метаданными .NET.</item>
    /// <item>— Универсальное создание экземпляров объектов, поддержка DataRow/DataRowView, кортежей, перечислений и других специальных случаев.</item>
    /// <item>— Сжатие и распаковка массивов байт с помощью GZip, копирование потоков.</item>
    /// <item>— Кэширование результатов операций Split и рефлексии для повышения производительности при повторных вызовах.</item>
    /// </list>
    /// <b>Типовые сценарии использования:</b>
    /// <list type="bullet">
    /// <item>— Маппинг и преобразование данных между различными объектными моделями (DTO, ORM, сериализация/десериализация).</item>
    /// <item>— Динамическое связывание данных в UI, редакторы свойств, построение универсальных инспекторов объектов.</item>
    /// <item>— ETL, интеграция, обработка данных, где требуется универсальный доступ к структурам объектов.</item>
    /// <item>— Генерация кода, скриптовые движки, плагин-системы, где необходима работа с объектами на этапе выполнения.</item>
    /// </list>
    /// <b>Потокобезопасность:</b> Все статические члены класса потокобезопасны. Для кэширования и пользовательских конвертеров используются concurrent-коллекции.<br/>
    /// <b>Производительность:</b> Класс оптимизирован для частого использования в динамических сценариях за счёт кэширования и эффективных паттернов работы с рефлексией.
    /// </para>
    /// </summary>
    public static class Obj
    {
        /// <summary>
        /// Словарь пользовательских преобразователей типов
        /// </summary>
        public static ConcurrentDictionary<Type, ConcurrentDictionary<Type, Func<object, object>>> CustomTypeConverters =
            new ConcurrentDictionary<Type, ConcurrentDictionary<Type, Func<object, object>>>();

        /// <summary>
        /// Получает расширенную информацию о свойстве, поле, методе или конструкторе объекта по имени или пути.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="obj">Экземпляр объекта.</param>
        /// <param name="memberName">Имя члена или путь через точку.</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx"/> или null, если член не найден.</returns>
        public static MemberInfoEx GetMember<T>(T obj, string memberName, MemberNameType memberNameType = MemberNameType.Any)
        {
            var objType = obj?.GetType() ?? typeof(T);

            return GetMember(objType, memberName, memberNameType);
        }

        /// <summary>
        /// Получает расширенную информацию о свойстве, поле, методе или конструкторе типа по имени или пути.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberName">Имя или путь до свойства, поля или метода</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <returns></returns>
        public static MemberInfoEx GetMember<T>(string memberName, MemberNameType memberNameType = MemberNameType.Any)
        {
            return GetMember(typeof(T), memberName, memberNameType);
        }

        /// <summary>
        /// Получает расширенную информацию о свойстве, поле, методе или конструкторе типа по имени или пути.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="memberName">Имя или путь до свойства, поля или метода</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static MemberInfoEx GetMember(Type type, string memberName, MemberNameType memberNameType = MemberNameType.Any)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var memberNames = SplitCache.Get(memberName);
            var memberInfoEx = type.GetMemberInfoEx();
            if (memberInfoEx == null)
                return null;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var name = memberNames[i];
                var mi = memberInfoEx.GetMember(name, memberNameType);
                if (mi == null)
                    return null;

                mi = mi.IsCollection && i < memberNames.Length - 1 && !memberNames[i + 1].Equals("Item", StringComparison.OrdinalIgnoreCase)
                    ? mi.ElementType.GetMemberInfoEx()
                    : mi.GetMemberInfoEx();
                memberInfoEx = mi;
            }

            return memberInfoEx;
        }

        /// <summary>
        /// Увеличивает значение переменной на указанный шаг.
        /// </summary>
        /// <param name="value">Значение для увеличения (передается по ссылке).</param>
        /// <param name="step">Шаг увеличения (по умолчанию 1).</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="value"/> равен null.</exception>
        /// <exception cref="NotImplementedException">Выбрасывается, если тип значения не поддерживается для увеличения.</exception>
        public static void Increase(ref object value, int step = 1)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var valueType = value.GetType();

            if (valueType.IsEnum || valueType.IsNumeric(false))
            {
                value = ChangeType(ChangeType<long>(value) + step, valueType);
                return;
            }

            if (valueType.IsNumeric())
            {
                value = ChangeType(ChangeType<decimal>(value) + step, valueType);
                return;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="toType">Тип, в который нужно преобразовать.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture"/>).</param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="InvalidCastException">Если преобразование невозможно.</exception>
        /// <exception cref="FormatException">Если формат значения некорректен.</exception>
        /// <exception cref="ArgumentNullException">Если <paramref name="toType"/> равен null.</exception>
        public static object ChangeType(object value, Type toType, IFormatProvider formatProvider = null)
        {
            if (value?.Equals(DBNull.Value) != false)
                return toType.Default();

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
                return toType.Create(GetValues(value).ToArray());

            // Обработка строковых значений
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return toType.Default();
                if (toType.IsEnum)
                    return Enum.Parse(toType, s, true);
                if (toType == typeof(DateTime))
                    return Converters.RSConverters.StringToDateTimeConverter(s);
            }

            // Стандартное преобразование
            return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Получает пользовательский преобразователь типов между двумя типами, если он зарегистрирован.
        /// </summary>
        /// <param name="typeFrom">Исходный тип.</param>
        /// <param name="typeTo">Целевой тип.</param>
        /// <returns>Функция преобразования или null, если не найдено.</returns>
        public static Func<object, object> GetCustomTypeConverter(Type typeFrom, Type typeTo)
        {
            if (!CustomTypeConverters.TryGetValue(typeFrom, out var typeConverters) || typeConverters == null)
                return null;

            if (!typeConverters.TryGetValue(typeTo, out var converter) || converter == null)
                return null;

            return converter;
        }

        /// <summary>
        /// Регистрирует пользовательский преобразователь типов между двумя типами.
        /// </summary>
        /// <typeparam name="TFrom">Исходный тип.</typeparam>
        /// <typeparam name="TTo">Целевой тип.</typeparam>
        /// <param name="converter">Функция преобразования.</param>
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
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Целевой тип.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="formatProvider">Провайдер формата.</param>
        /// <returns>Преобразованное значение типа <typeparamref name="T"/>.</returns>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null)
        {
            var toType = typeof(T);
            return (T)ChangeType(value, toType, formatProvider);
        }

        /// <summary>
        /// Пытается преобразовать значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Целевой тип.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="convert">Результат преобразования.</param>
        /// <param name="formatProvider">Провайдер формата.</param>
        /// <returns>True, если преобразование успешно; иначе false.</returns>
        public static bool TryChangeType<T>(object value, out T convert, IFormatProvider formatProvider = null)
        {
            convert = default;
            if (TryChangeType(value, new[] { typeof(T) }, out var convert2, formatProvider))
            {
                convert = (T)convert2;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Пытается преобразовать значение к одному из указанных типов.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="toTypes">Массив возможных целевых типов.</param>
        /// <param name="convert">Результат преобразования.</param>
        /// <param name="formatProvider">Провайдер формата.</param>
        /// <returns>True, если преобразование успешно; иначе false.</returns>
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
        /// Пытается преобразовать значение к указанному типу.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="toType">Целевой тип.</param>
        /// <param name="convert">Результат преобразования.</param>
        /// <param name="formatProvider">Провайдер формата.</param>
        /// <returns>True, если преобразование успешно; иначе false.</returns>
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
        /// Сжимает массив байт с использованием GZip.
        /// </summary>
        /// <param name="bytes">Исходные данные.</param>
        /// <returns>Сжатый массив байт.</returns>
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
        /// Распаковывает массив байт, сжатый с помощью GZip.
        /// </summary>
        /// <param name="bytes">Сжатые данные.</param>
        /// <returns>Распакованный массив байт.</returns>
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
        /// Копирует данные из одного потока в другой.
        /// </summary>
        /// <param name="src">Исходный поток.</param>
        /// <param name="target">Целевой поток.</param>
        public static void CopyTo(Stream src, Stream target)
        {
            byte[] bytes = new byte[4096];
            int cnt;
            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                target.Write(bytes, 0, cnt);
            }
        }

        /// <summary>
        /// Кеш для результатов string.Split
        /// </summary>
        public static readonly Cache<string, string[]> SplitCache =
            new Cache<string, string[]>(x => x.Split(new[] { '.', '\\', '/', '[', ']', '(', ')' }, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Получает значение свойства объекта по имени с преобразованием в указанный тип.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="obj">Объект.</param>
        /// <param name="propName">Имя свойства или путь через точки.</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <returns>Значение свойства или значение по умолчанию для типа <typeparamref name="T"/>.</returns>
        public static T Get<T>(object obj, string propName, MemberNameType memberNameType = MemberNameType.Any)
        {
            return (T)Get(obj, propName, typeof(T), memberNameType);
        }

        /// <summary>
        /// Получает значение свойства объекта по имени.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="propName">Имя свойства или путь через точки.</param>
        /// <param name="convertToType">Тип для преобразования результата.</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <returns>Значение свойства или null.</returns>
        public static object Get(object obj, string propName, Type convertToType = null, MemberNameType memberNameType = MemberNameType.Any)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propName))
                return null;

            object getValue;
            switch (obj)
            {
                case DataRow dr:
                    getValue = dr.RowState == DataRowState.Detached ? null : dr[propName];
                    break;
                case DataRowView drv:
                    return Get(drv.Row, propName, convertToType, memberNameType);
                default:
                    getValue = Get(obj, SplitCache.Get(propName), convertToType, memberNameType);
                    break;
            }
            return convertToType == null ? getValue : ChangeType(getValue, convertToType);
        }

        /// <summary>
        /// Получает значения свойства, поля или метода объекта по пути.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="propertyNames">Путь к свойству или полю (массив имен до дочернего свойства).</param>
        /// <param name="convertToType">Конвертировать значение в указанный тип, null - не конвертировать</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <param name="nameComparison">Сравнение имен</param>
        /// <returns>Значение свойства или null.</returns>
        public static object Get(object obj, string[] propertyNames, Type convertToType = null, MemberNameType memberNameType = MemberNameType.Any, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (obj == null || !propertyNames.Any())
                return null;

            switch (obj)
            {
                case DataRow dr:
                    return dr[propertyNames[0]];
                case DataRowView drv:
                    return Get(drv.Row, new[]{propertyNames[0]}, convertToType, memberNameType, nameComparison);
                default:

                    if (string.IsNullOrWhiteSpace(propertyNames[0]))
                        return null;

                    var objTypeInfo = obj.GetType().GetMemberInfoEx();

                    var objMember = objTypeInfo.GetMember(propertyNames[0], memberNameType, x=>x.IsProperty || x.IsField || x.IsMethod, nameComparison);

                    if (objMember == null)
                        return null;

                    return propertyNames.Length == 1
                        ? (convertToType == null ? objMember.Getter(obj) : ChangeType(objMember.Getter(obj), convertToType))
                        : Get(objMember.Getter(obj), propertyNames.Skip(1).ToArray(), convertToType, memberNameType, nameComparison);
            }
        }

        /// <summary>
        /// Устанавливает значение свойства объекта по имени.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="propName">Имя свойства или путь до свойства через точки.</param>
        /// <param name="value">Новое значение.</param>
        /// <param name="memberNameType">Тип имени</param>
        /// <returns>True, если операция выполнена успешно; иначе false.</returns>
        public static bool Set(object obj, string propName, object value, MemberNameType memberNameType = MemberNameType.Any)
        {
            return Set(obj, SplitCache.Get(propName), value, memberNameType);
        }

        /// <summary>
        /// Устанавливает несколько значений свойств объекта.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="values">Коллекция пар имя-значение.</param>
        /// <param name="memberNameType">Тип имени</param>
        public static void Set(object obj, IEnumerable<KeyValuePair<string, object>> values, MemberNameType memberNameType = MemberNameType.Any)
        {
            foreach (var kv in values)
                Set(obj, kv.Key, kv.Value, memberNameType);
        }

        /// <summary>
        /// Устанавливает значение свойства или поля объекта по указанному пути с учетом способа сравнения имен.<br/>
        /// Поддерживает вложенные свойства, автоматическое создание промежуточных объектов и преобразование типов.<br/>
        /// Для DataRow и DataRowView выполняет преобразование значения к типу столбца.<br/>
        /// </summary>
        /// <param name="obj">Объект, в котором требуется установить значение.</param>
        /// <param name="propertyNames">Путь к свойству или полю в виде массива имен (поддерживаются вложенные свойства).</param>
        /// <param name="value">Новое значение для установки.</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <param name="nameComparison">Сравнение имен</param>
        /// <returns>True, если значение успешно установлено; иначе false.</returns>
        public static bool Set(object obj, string[] propertyNames, object value, MemberNameType memberNameType = MemberNameType.Any, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (obj == null || !propertyNames.Any())
                return false;

            switch (obj)
            {
                case DataRowView drv:
                    drv.Row[propertyNames[0]] = ChangeType(value,
                        drv.Row.Table.Columns[propertyNames[0]].DataType ?? typeof(object));
                    return true;

                case DataRow dr:
                    dr[propertyNames[0]] =
                        ChangeType(value, dr.Table.Columns[propertyNames[0]].DataType ?? typeof(object));
                    return true;

                default:
                    var objTypeInfo = obj.GetType().GetMemberInfoEx();
                    var propertyName = propertyNames[0];
                    var propertyOrField = objTypeInfo.GetMember(propertyName, memberNameType, x => x.IsProperty || x.IsField, nameComparison);

                    if (propertyOrField == null)
                        return false;

                    if (propertyNames.Length == 1)
                    {
                        var valueType = value?.GetType();
                        return propertyOrField.SetValue(obj,
                            valueType == null || valueType == propertyOrField.Type || propertyOrField.IsCollection
                                ? value
                                : ChangeType(value, propertyOrField.Type));
                    }

                    var propValue = Get(obj, new[]{propertyName}, null, memberNameType);
                    if (propValue == null)
                    {
                        propValue = propertyOrField.Type.Create();
                        var result = Set(obj, new[]{propertyName}, propValue, memberNameType);
                        if (!result)
                            return false;
                    }

                    return Set(propValue, propertyNames.Skip(1).ToArray(), value, memberNameType);
            }
        }

        /// <summary>
        /// Устанавливает значение свойства или поля объекта по имени с учетом регистра с минимальным количеством проверок.<br/>
        /// Не конвертирует тип значения в тип свойства.
        /// Нет поддержки вложенных свойств и полей.<br/>
        /// Нет поддержки DataRow и DataRowView.<br/>
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="propertyName">Имя свойство, регистрозависимое.</param>
        /// <param name="value">Новое значение.</param>
        /// <returns>True, если операция выполнена успешно; иначе false.</returns>
        public static bool FastSet(object obj, string propertyName, object value)
        {
            var objTypeInfo = obj.GetType().GetMemberInfoEx();
            var propertyOrField = objTypeInfo.GetMember(propertyName, MemberNameType.Name, x => x.IsProperty || x.IsField, StringComparison.Ordinal);

            return propertyOrField?.SetValue(obj, value) ?? false;
        }

        /// <summary>
        /// Копирует значения свойств из одного объекта в другой.
        /// </summary>
        /// <param name="source">Исходный объект.</param>
        /// <param name="target">Целевой объект.</param>
        /// <param name="deepProcessing">Выполнять глубокое копирование (создавать новые экземпляры для ссылочных типов).</param>
        /// <param name="nameComparison">Способ сравнения имен.</param>
        public static void Copy(object source, object target, bool deepProcessing = false, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            Copy(source, target, deepProcessing, true, false, MemberNameType.Name, nameComparison);
        }

        /// <summary>
        /// Копирует значения свойств из одного объекта в другой с дополнительными параметрами.
        /// </summary>
        /// <param name="source">Исходный объект.</param>
        /// <param name="target">Целевой объект.</param>
        /// <param name="deepProcessing">Выполнять глубокое копирование.</param>
        /// <param name="overwritePropertiesWithNullValues">Перезаписывать свойства null значениями.</param>
        /// <param name="overwriteOnlyNullProperties">Перезаписывать только null свойства.</param>
        /// <param name="memberNameType">Тип имени члена.</param>
        /// <param name="nameComparison">Сравнение имен</param>
        public static void Copy(object source, object target, bool deepProcessing, bool overwritePropertiesWithNullValues, bool overwriteOnlyNullProperties, MemberNameType memberNameType = MemberNameType.Name, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
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
                var sharedPropNames = targetTypeInfo.Properties.Select(x => x.Name).Intersect(dr.Table.Columns.OfType<DataColumn>().Select(x => x.ColumnName),
                    nameComparison.ToStringComparer()).ToArray();

                foreach (var s in sharedPropNames)
                    Set(target, new[]{s}, dr[s]);
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
                    if (targetItemType == typeof(object) || targetItemType.IsImplements(srcItemType) || (srcItemTypeInfo.IsValueType && targetItemTypeInfo.IsValueType))
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
                        Copy(i, targetListItem, deepProcessing, overwritePropertiesWithNullValues, overwriteOnlyNullProperties, memberNameType, nameComparison);
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
                        Get(i, new[]{"Key"}, targetKeyType, memberNameType) ?? throw new NullReferenceException("TypeHelper.Copy dictionary key is null!")
                    ] = Get(i, new[]{"Value"}, targetValueType, memberNameType);
                }
                return;
            }

            // Копирование обычных свойств
            var srcMembers = sourceTypeInfo.Properties;
            var targetMembers = targetTypeInfo.Properties;
            var sharedNames = srcMembers.Select(x => x.Name).Intersect(targetMembers.Select(x => x.Name),
                nameComparison.ToStringComparer()).ToArray();

            foreach (var sn in sharedNames)
            {
                var srcPropValue = Get(source, new[]{sn}, null, memberNameType, nameComparison);
                if (RSTypeExtensions.NullValues.Contains(srcPropValue) && overwritePropertiesWithNullValues)
                    continue;

                var tm = targetMembers.FirstOrDefault(x => x.Name.Equals(sn, nameComparison));
                if (tm?.CanWrite != true)
                    continue;

                if (overwriteOnlyNullProperties)
                {
                    var targetPropValue = Get(target, new[]{tm.Name}, null, memberNameType, nameComparison);
                    if (!RSTypeExtensions.NullValues.Contains(targetPropValue))
                        continue;
                }

                var castedValue = Cast(srcPropValue, tm.PropertyType, deepProcessing, nameComparison);
                Set(target, new[] { tm.Name }, castedValue, memberNameType, nameComparison);
            }
        }

        /// <summary>
        /// Объединяет значения свойств из одного объекта в другой (перезаписываются только null свойства).
        /// </summary>
        /// <param name="source">Исходный объект.</param>
        /// <param name="target">Целевой объект.</param>
        /// <param name="nameComparison">Способ сравнения имен.</param>
        public static void Merge(object source, object target, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            Copy(source, target, false, false, true, MemberNameType.Name, nameComparison);
        }

        /// <summary>
        /// Вызывает метод объекта по имени.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="methodName">Имя метода.</param>
        /// <param name="args">Аргументы метода.</param>
        /// <returns>Результат выполнения метода или null, если метод не найден.</returns>
        public static object Call(object obj, string methodName, params object[] args)
        {
            if (obj == null)
                return null;

            var typeInfoEx = obj.GetType().GetMemberInfoEx();
            var mi = typeInfoEx.Methods.FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, methodName));
            if (mi == null)
                return null;
            return mi.Invoke(obj, args);
        }

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Целевой тип.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <returns>Преобразованное значение.</returns>
        public static T Cast<T>(object value)
        {
            return Cast<T>(value, true);
        }

        /// <summary>
        /// Преобразует объект к указанному типу с возможностью глубокого преобразования.
        /// </summary>
        /// <typeparam name="T">Целевой тип.</typeparam>
        /// <param name="objInstance">Объект для преобразования.</param>
        /// <param name="deepProcessing">Выполнять глубокое преобразование.</param>
        /// <param name="nameComparison">Способ сравнения имен.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Преобразованный объект типа <typeparamref name="T"/>.</returns>
        public static T Cast<T>(object objInstance, bool deepProcessing, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase, object[] ctorArgs = null)
        {
            if (objInstance is T direct)
                return direct;

            if (objInstance == null)
                return default;

            return (T)Cast(objInstance, typeof(T), deepProcessing, nameComparison, ctorArgs);
        }

        /// <summary>
        /// Преобразует объект к указанному типу с возможностью глубокого преобразования.
        /// </summary>
        /// <param name="objInstance">Объект для преобразования.</param>
        /// <param name="toType">Целевой тип.</param>
        /// <param name="deepProcessing">Выполнять глубокое преобразование.</param>
        /// <param name="nameComparison">Способ сравнения имен.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Преобразованный объект.</returns>
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
            //var toTypeInfo = toType.GetMemberInfoEx();

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

            Copy(objInstance, newInstance, deepProcessing, true, false, MemberNameType.Name, nameComparison);

            return newInstance;
        }

        /// <summary>
        /// Пытается преобразовать значение к указанному типу, возвращая значение по умолчанию в случае ошибки.
        /// </summary>
        /// <typeparam name="T">Целевой тип.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="defaultIfFailed">Значение по умолчанию, если преобразование не удалось.</param>
        /// <returns>Преобразованное значение или <paramref name="defaultIfFailed"/>.</returns>
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
        /// Получает значения указанных свойств объекта.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="propertyNames">Имена свойств.</param>
        /// <returns>Массив значений свойств.</returns>
        public static object[] GetValues(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return Array.Empty<object>();

            if (!propertyNames.Any())
            {
                propertyNames = obj.GetType()
                    .GetMemberInfoEx().Members
                    .Where(x => x.IsProperty && x.IsPublic)
                    .Select(x => x.Name).ToArray();
            }

            return propertyNames.Select(x => Get(obj, x)).ToArray();
        }

        /// <summary>
        /// Получает значения указанных свойств объекта с преобразованием в указанный тип.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="obj">Объект.</param>
        /// <param name="propertyNames">Имена свойств.</param>
        /// <returns>Массив значений свойств типа <typeparamref name="T"/>.</returns>
        public static T[] GetValues<T>(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return Array.Empty<T>();

            if (!propertyNames.Any())
            {
                propertyNames = obj.GetType()
                    .GetMemberInfoEx().Members
                    .Where(x => x.IsProperty && x.IsPublic)
                    .Select(x => x.Name).ToArray();
            }

            return propertyNames.Select(x => Get<T>(obj, x)).ToArray();
        }

        /// <summary>
        /// Получает значения свойств объекта, удовлетворяющих заданному критерию.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="matchCriteria">Функция фильтрации членов.</param>
        /// <param name="args">Дополнительные аргументы.</param>
        /// <returns>Массив значений свойств.</returns>
        public static object[] GetValues(object obj, Func<MemberInfoEx, bool> matchCriteria, params object[] args)
        {
            return GetMembersValues(obj, matchCriteria, args).Values.ToArray();
        }

        /// <summary>
        /// Получает значения свойств объекта, удовлетворяющих заданному критерию, в виде словаря.
        /// </summary>
        /// <param name="obj">Объект.</param>
        /// <param name="matchCriteria">Функция фильтрации членов.</param>
        /// <param name="args">Дополнительные аргументы.</param>
        /// <returns>Словарь имя-значение.</returns>
        public static Dictionary<string, object> GetMembersValues(object obj, Func<MemberInfoEx, bool> matchCriteria, params object[] args)
        {
            var d = new Dictionary<string, object>();
            if (obj == null)
                return d;
            var mi = obj.GetType().GetMemberInfoEx();
            return mi.Members.Where(matchCriteria).ToDictionary(key => key.Name, value => value.GetValue(obj, args));
        }

        /// <summary>
        /// Получает атрибут заданного типа для объекта.
        /// </summary>
        /// <typeparam name="T">Тип атрибута.</typeparam>
        /// <param name="obj">Объект.</param>
        /// <returns>Экземпляр атрибута или null, если не найден.</returns>
        public static T GetAttribute<T>(object obj) where T : Attribute
        {
            if (obj == null)
                return default;
            var objType = obj.GetType().GetMemberInfoEx();
            return objType.Attributes.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Получает атрибут заданного типа для свойства объекта.
        /// </summary>
        /// <typeparam name="T">Тип атрибута.</typeparam>
        /// <param name="obj">Объект.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Экземпляр атрибута или null, если не найден.</returns>
        public static T GetAttribute<T>(object obj, string propertyName) where T : Attribute
        {
            if (obj == null)
                return default;
            var mi = GetMember(obj, propertyName);
            if (mi == null)
                return default;
            return mi.Attributes.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Проверяет, существует ли метод с указанным именем у типа.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <param name="methodName">Имя метода.</param>
        /// <param name="stringComparison">Способ сравнения имен.</param>
        /// <returns>True, если метод существует; иначе false.</returns>
        public static bool MethodExists(Type type, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(type, methodName, stringComparison) != null;
        }

        /// <summary>
        /// Получает расширенную информацию о методе по имени для типа.
        /// </summary>
        /// <param name="type">Тип для поиска.</param>
        /// <param name="methodName">Имя метода.</param>
        /// <param name="stringComparison">Способ сравнения имен.</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx"/> или null, если метод не найден.</returns>
        public static MemberInfoEx GetMethodInfo(Type type, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            var mi = type.GetTypeInfo();
            return mi.GetMemberInfoEx()?.Members.FirstOrDefault(x => x.IsMethod && x.Name.Equals(methodName, stringComparison));
        }

        /// <summary>
        /// Получает расширенную информацию о методе по имени для типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип для поиска.</typeparam>
        /// <param name="methodName">Имя метода.</param>
        /// <param name="stringComparison">Способ сравнения имен.</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx"/> или null, если метод не найден.</returns>
        public static MemberInfoEx GetMethodInfo<T>(string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(typeof(T), methodName, stringComparison);
        }

        /// <summary>
        /// Получает расширенную информацию о методе по имени для экземпляра объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="obj">Экземпляр объекта.</param>
        /// <param name="methodName">Имя метода.</param>
        /// <param name="stringComparison">Способ сравнения имен.</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx"/> или null, если метод не найден.</returns>
        public static MemberInfoEx GetMethodInfo<T>(T obj, string methodName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetMethodInfo(obj?.GetType() ?? typeof(T), methodName, stringComparison);
        }

        /// <summary>
        /// Создает новый экземпляр объекта указанного типа.
        /// </summary>
        /// <param name="type">Тип объекта.</param>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Новый экземпляр объекта.</returns>
        /// <exception cref="NotImplementedException">Если создание экземпляра не поддерживается.</exception>
        public static object New(Type type, params object[] args)
        {
            return type.Create(args);
        }

        /// <summary>
        /// Создает новый экземпляр объекта указанного типа и приводит его к типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="type">Тип объекта.</param>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Новый экземпляр объекта типа <typeparamref name="T"/> или null.</returns>
        /// <exception cref="NotImplementedException">Если создание экземпляра не поддерживается.</exception>
        public static T New<T>(Type type, params object[] args) where T : class
        {
            return New(type, args) as T;
        }

        /// <summary>
        /// Создает новый экземпляр объекта типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Новый экземпляр объекта типа <typeparamref name="T"/>.</returns>
        /// <exception cref="NotImplementedException">Если создание экземпляра не поддерживается.</exception>
        public static T New<T>(params object[] args)
        {
            return (T)New(typeof(T), args);
        }
    }
}