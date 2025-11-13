using System;
using System.Reflection;

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
    public static bool IsTuple(this Type type)
    {
        return TypeHelper.IsTuple(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип числовым.
    /// </summary>
    public static bool IsNumeric(this Type type)
    {
        return TypeHelper.IsNumeric(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип nullable.
    /// </summary>
    public static bool IsNullable(this Type type)
    {
        return TypeHelper.IsNullable(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип словарём.
    /// </summary>
    public static bool IsDictionary(this Type type)
    {
        return TypeHelper.IsDictionary(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип числом с плавающей точкой.
    /// </summary>
    public static bool IsFloat(this Type type)
    {
        return TypeHelper.IsFloat(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип базовым (примитивным, строка, дата и т.д.).
    /// </summary>
    public static bool IsBasic(this Type type)
    {
        return TypeHelper.IsBasic(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип логическим.
    /// </summary>
    public static bool IsBoolean(this Type type)
    {
        return TypeHelper.IsBoolean(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип коллекцией.
    /// </summary>
    public static bool IsCollection(this Type type)
    {
        return TypeHelper.IsCollection(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип датой/временем.
    /// </summary>
    public static bool IsDate(this Type type)
    {
        return TypeHelper.IsDate(type);
    }

    /// <summary>
    ///     Проверяет, является ли тип делегатом.
    /// </summary>
    public static bool IsDelegate(this Type type)
    {
        return TypeHelper.IsDelegate(type);
    }

    /// <summary>
    ///     Проверяет, реализует ли тип указанный интерфейс.
    /// </summary>
    public static bool IsImplements(this Type type, Type interfaceType)
    {
        return TypeHelper.IsImplements(type, interfaceType);
    }

    /// <summary>
    ///     Проверяет, реализует ли тип указанный generic-интерфейс.
    /// </summary>
    public static bool IsImplements<TInterface>(this Type type)
    {
        return TypeHelper.IsImplements<TInterface>(type);
    }

    /// <summary>
    ///     Возвращает значение по умолчанию для типа.
    /// </summary>
    public static object DefaultValue(this Type type)
    {
        return TypeHelper.Default(type);
    }

    /// <summary>
    ///     Возвращает цепочку базовых типов и/или интерфейсов.
    /// </summary>
    public static Type[] GetBaseTypes(this Type type, bool includeThis = false, bool getInterfaces = false)
    {
        return TypeHelper.GetBaseTypes(type, includeThis, getInterfaces);
    }

    /// <summary>
    ///     Возвращает тип элементов коллекции.
    /// </summary>
    public static Type GetCollectionItemType(this Type type)
    {
        return TypeHelper.GetCollectionItemType(type);
    }

    /// <summary>
    ///     Получает все публичные свойства типа.
    /// </summary>
    public static PropertyInfo[] GetProperties(this Type type)
    {
        return TypeHelper.GetProperties(type);
    }

    /// <summary>
    ///     Получает свойство по имени.
    /// </summary>
    public static PropertyInfo GetProperty(this Type type, string propertyName,
        StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
    {
        return TypeHelper.GetProperty(type, propertyName, stringComparison);
    }

    /// <summary>
    ///     Получает имена всех публичных свойств типа.
    /// </summary>
    public static string[] GetPropertyNames(this Type type)
    {
        return TypeHelper.GetPropertyNames(type);
    }

    /// <summary>
    ///     Получает значение свойства по имени.
    /// </summary>
    public static object GetPropertyValue(this object source, string propertyName)
    {
        return TypeHelper.GetPropertyValue(source, propertyName);
    }

    /// <summary>
    ///     Получает первый пользовательский атрибут по имени типа.
    /// </summary>
    public static Attribute GetCustomAttribute(this MemberInfo member, string attributeName,
        StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
    {
        return TypeHelper.GetCustomAttribute(member, attributeName, stringComparison);
    }

    /// <summary>
    ///     Возвращает поле по условию фильтрации.
    /// </summary>
    public static FieldInfo GetField(this Type type, Func<FieldInfo, bool> matchCriteria)
    {
        return TypeHelper.GetField(type, matchCriteria);
    }

    /// <summary>
    ///     Возвращает все типы из указанной сборки, которые реализуют интерфейс или наследуются от указанного базового типа.
    /// </summary>
    public static Type[] GetImplementationsOf(this Type baseType, Assembly fromAssembly)
    {
        return TypeHelper.GetImplementationsOf(baseType, fromAssembly);
    }

    /// <summary>
    ///     Возвращает все типы из всех загруженных сборок, которые реализуют интерфейс или наследуются от указанного базового
    ///     типа.
    /// </summary>
    public static Type[] GetImplementationsOf(this Type baseType)
    {
        return TypeHelper.GetImplementationsOf(baseType);
    }

    /// <summary>
    ///     Получает событие с наименьшего уровня иерархии.
    /// </summary>
    public static EventInfo GetLowestEvent(this Type type, string name)
    {
        return TypeHelper.GetLowestEvent(type, name);
    }

    /// <summary>
    ///     Получает поле с наименьшего уровня иерархии.
    /// </summary>
    public static FieldInfo GetLowestField(this Type type, string name)
    {
        return TypeHelper.GetLowestField(type, name);
    }

    /// <summary>
    ///     Получает метод с наименьшего уровня иерархии.
    /// </summary>
    public static MethodInfo GetLowestMethod(this Type type, string name)
    {
        return TypeHelper.GetLowestMethod(type, name);
    }

    /// <summary>
    ///     Получает свойство с наименьшего уровня иерархии.
    /// </summary>
    public static PropertyInfo GetLowestProperty(this Type type, string name)
    {
        return TypeHelper.GetLowestProperty(type, name);
    }
}