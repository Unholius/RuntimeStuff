namespace RuntimeStuff.Extensions
{
    using System;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет методы расширения для <see cref="System.Type"/> и связанных типов отражения (reflection),
    /// упрощающие типичные операции анализа типов, доступа к свойствам и запросов метаданных.
    /// </summary>
    /// <remarks>
    /// Класс <c>TypeExtensions</c> содержит набор статических методов для определения характеристик типов
    /// (таких как числовой тип, кортеж или коллекция), получения членов типа (свойств, полей, событий и методов),
    /// доступа к пользовательским атрибутам, а также поиска реализаций интерфейсов или базовых типов.
    /// Эти методы предназначены для упрощения операций, основанных на отражении (reflection),
    /// и повышения читаемости кода при работе с типами во время выполнения.
    /// Все методы являются статическими и предназначены для использования в качестве методов расширения,
    /// что обеспечивает удобный и выразительный синтаксис при работе с экземплярами
    /// <see cref="System.Type"/>, <see cref="System.Reflection.MemberInfo"/> и <see cref="System.Object"/>.
    public static class TypeExtensions
    {
        /// <summary>
        ///     Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        public static bool IsTuple(this Type type) => Obj.IsTuple(type);

        /// <summary>
        ///     Проверяет, является ли тип числовым.
        /// </summary>
        public static bool IsNumeric(this Type type) => Obj.IsNumeric(type);

        /// <summary>
        ///     Проверяет, является ли тип nullable.
        /// </summary>
        public static bool IsNullable(this Type type) => Obj.IsNullable(type);

        /// <summary>
        ///     Проверяет, является ли тип словарём.
        /// </summary>
        public static bool IsDictionary(this Type type) => Obj.IsDictionary(type);

        /// <summary>
        ///     Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        public static bool IsFloat(this Type type) => Obj.IsFloat(type);

        /// <summary>
        ///     Проверяет, является ли тип базовым (примитивным, строка, дата и т.д.).
        /// </summary>
        public static bool IsBasic(this Type type) => Obj.IsBasic(type);

        /// <summary>
        ///     Проверяет, является ли тип логическим.
        /// </summary>
        public static bool IsBoolean(this Type type) => Obj.IsBoolean(type);

        /// <summary>
        ///     Проверяет, является ли тип коллекцией.
        /// </summary>
        public static bool IsCollection(this Type type) => Obj.IsCollection(type);

        /// <summary>
        ///     Проверяет, является ли тип датой/временем.
        /// </summary>
        public static bool IsDate(this Type type) => Obj.IsDate(type);

        /// <summary>
        ///     Проверяет, является ли тип делегатом.
        /// </summary>
        public static bool IsDelegate(this Type type) => Obj.IsDelegate(type);

        /// <summary>
        ///     Проверяет, реализует ли тип указанный интерфейс.
        /// </summary>
        public static bool IsImplements(this Type type, Type interfaceType) => Obj.IsImplements(type, interfaceType);

        /// <summary>
        ///     Проверяет, реализует ли тип указанный generic-интерфейс.
        /// </summary>
        public static bool IsImplements<TInterface>(this Type type) => Obj.IsImplements<TInterface>(type);

        /// <summary>
        ///     Возвращает значение по умолчанию для типа.
        /// </summary>
        public static object DefaultValue(this Type type) => Obj.Default(type);

        /// <summary>
        ///     Возвращает цепочку базовых типов и/или интерфейсов.
        /// </summary>
        public static Type[] GetBaseTypes(this Type type, bool includeThis = false, bool getInterfaces = false) => Obj.GetBaseTypes(type, includeThis, getInterfaces);

        /// <summary>
        ///     Возвращает тип элементов коллекции.
        /// </summary>
        public static Type GetCollectionItemType(this Type type) => Obj.GetCollectionItemType(type);

        /// <summary>
        ///     Получает все публичные свойства типа.
        /// </summary>
        public static PropertyInfo[] GetProperties(this Type type) => Obj.GetProperties(type);

        /// <summary>
        ///     Получает свойство по имени.
        /// </summary>
        public static PropertyInfo GetProperty(this Type type, string propertyName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase) => Obj.GetProperty(type, propertyName, stringComparison);

        /// <summary>
        ///     Получает имена всех публичных свойств типа.
        /// </summary>
        public static string[] GetPropertyNames(this Type type) => Obj.GetPropertyNames(type);

        /// <summary>
        ///     Получает первый пользовательский атрибут по имени типа.
        /// </summary>
        public static Attribute GetCustomAttribute(this MemberInfo member, string attributeName,
            StringComparison stringComparison = StringComparison.OrdinalIgnoreCase) => Obj.GetCustomAttribute(member, attributeName, stringComparison);

        /// <summary>
        ///     Возвращает поле по условию фильтрации.
        /// </summary>
        public static FieldInfo GetField(this Type type, Func<FieldInfo, bool> matchCriteria) => Obj.GetField(type, matchCriteria);

        /// <summary>
        ///     Возвращает все типы из указанной сборки, которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        public static Type[] GetImplementationsOf(this Type baseType, Assembly fromAssembly) => Obj.GetImplementationsOf(baseType, fromAssembly);

        /// <summary>
        ///     Возвращает все типы из всех загруженных сборок, которые реализуют интерфейс или наследуются от указанного базового
        ///     типа.
        /// </summary>
        public static Type[] GetImplementationsOf(this Type baseType) => Obj.GetImplementationsOf(baseType);

        /// <summary>
        ///     Получает событие с наименьшего уровня иерархии.
        /// </summary>
        public static EventInfo GetLowestEvent(this Type type, string name) => Obj.GetLowestEvent(type, name);

        /// <summary>
        ///     Получает поле с наименьшего уровня иерархии.
        /// </summary>
        public static FieldInfo GetLowestField(this Type type, string name) => Obj.GetLowestField(type, name);

        /// <summary>
        ///     Получает метод с наименьшего уровня иерархии.
        /// </summary>
        public static MethodInfo GetLowestMethod(this Type type, string name) => Obj.GetLowestMethod(type, name);

        /// <summary>
        ///     Получает свойство с наименьшего уровня иерархии.
        /// </summary>
        public static PropertyInfo GetLowestProperty(this Type type, string name) => Obj.GetLowestProperty(type, name);
    }
}