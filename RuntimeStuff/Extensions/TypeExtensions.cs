// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 10-13-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="TypeExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using RuntimeStuff;

    /// <summary>
    /// Class TypeExtensions.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Вызывает указанный метод через отражение, автоматически подставляя значения аргументов.
        /// </summary>
        /// <param name="methodInfo">
        /// Объект <see cref="MethodInfo"/>, представляющий вызываемый метод.
        /// </param>
        /// <param name="instance">
        /// Экземпляр объекта, для которого вызывается метод.
        /// Для статических методов может быть <c>null</c>.
        /// </param>
        /// <returns>
        /// Результат выполнения метода. Если метод ничего не возвращает (<c>void</c>),
        /// возвращаемое значение будет <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Для каждого параметра метода:
        /// <list type="bullet">
        /// <item>
        /// Если параметр имеет значение по умолчанию, используется <see cref="Type.Missing"/>,
        /// чтобы позволить механизму отражения применить значение по умолчанию.
        /// </item>
        /// <item>
        /// Если значение по умолчанию отсутствует, используется значение по умолчанию
        /// для типа параметра (например, <c>0</c> для числовых типов или <c>null</c> для ссылочных).
        /// </item>
        /// </list>
        /// После подготовки аргументов метод вызывается через <see cref="MethodBase.Invoke(object, object[])"/>.
        /// </remarks>
        /// <exception cref="TargetInvocationException">
        /// Возникает, если вызываемый метод выбрасывает исключение.
        /// </exception>
        /// <exception cref="TargetParameterCountException">
        /// Возникает при несовпадении количества параметров.
        /// </exception>
        public static object InvokeDefault(this MethodInfo methodInfo, object instance)
        {
            var parameters = methodInfo.GetParameters();
            var args = parameters
                .Select(p => p.IsOptional ? Type.Missing : p.ParameterType.DefaultValue())
                .ToArray();
            return methodInfo.Invoke(instance, args);
        }

        /// <summary>
        /// Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is tuple; otherwise, <c>false</c>.</returns>
        public static bool IsTuple(this Type type) => Obj.IsTuple(type);

        /// <summary>
        /// Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is numeric; otherwise, <c>false</c>.</returns>
        public static bool IsNumeric(this Type type) => Obj.IsNumeric(type);

        /// <summary>
        /// Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is nullable; otherwise, <c>false</c>.</returns>
        public static bool IsNullable(this Type type) => Obj.IsNullable(type);

        /// <summary>
        /// Проверяет, является ли тип словарём.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is dictionary; otherwise, <c>false</c>.</returns>
        public static bool IsDictionary(this Type type) => Obj.IsDictionary(type);

        /// <summary>
        /// Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is float; otherwise, <c>false</c>.</returns>
        public static bool IsFloat(this Type type) => Obj.IsFloat(type);

        /// <summary>
        /// Проверяет, является ли тип базовым (примитивным, строка, дата и т.д.).
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is basic; otherwise, <c>false</c>.</returns>
        public static bool IsBasic(this Type type) => Obj.IsBasic(type);

        /// <summary>
        /// Проверяет, является ли тип логическим.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is boolean; otherwise, <c>false</c>.</returns>
        public static bool IsBoolean(this Type type) => Obj.IsBoolean(type);

        /// <summary>
        /// Проверяет, является ли тип коллекцией.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is collection; otherwise, <c>false</c>.</returns>
        public static bool IsCollection(this Type type) => Obj.IsCollection(type);

        /// <summary>
        /// Проверяет, является ли тип датой/временем.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is date; otherwise, <c>false</c>.</returns>
        public static bool IsDate(this Type type) => Obj.IsDate(type);

        /// <summary>
        /// Проверяет, является ли тип делегатом.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is delegate; otherwise, <c>false</c>.</returns>
        public static bool IsDelegate(this Type type) => Obj.IsDelegate(type);

        /// <summary>
        /// Проверяет, реализует ли тип указанный интерфейс.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="interfaceType">Type of the interface.</param>
        /// <returns><c>true</c> if the specified interface type is implements; otherwise, <c>false</c>.</returns>
        public static bool IsImplements(this Type type, Type interfaceType) => Obj.IsImplements(type, interfaceType);

        /// <summary>
        /// Проверяет, реализует ли тип указанный generic-интерфейс.
        /// </summary>
        /// <typeparam name="TInterface">The type of the t interface.</typeparam>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type is implements; otherwise, <c>false</c>.</returns>
        public static bool IsImplements<TInterface>(this Type type) => Obj.IsImplements<TInterface>(type);

        /// <summary>
        /// Возвращает значение по умолчанию для типа.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.Object.</returns>
        public static object DefaultValue(this Type type) => Obj.Default(type);

        /// <summary>
        /// Возвращает цепочку базовых типов и/или интерфейсов.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="includeThis">if set to <c>true</c> [include this].</param>
        /// <param name="getInterfaces">if set to <c>true</c> [get interfaces].</param>
        /// <returns>Type[].</returns>
        public static Type[] GetBaseTypes(this Type type, bool includeThis = false, bool getInterfaces = false) => Obj.GetBaseTypes(type, includeThis, getInterfaces);

        /// <summary>
        /// Возвращает тип элементов коллекции.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Type.</returns>
        public static Type GetCollectionItemType(this Type type) => Obj.GetCollectionItemType(type);

        /// <summary>
        /// Получает все публичные свойства типа.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>PropertyInfo[].</returns>
        public static PropertyInfo[] GetProperties(this Type type) => Obj.GetProperties(type);

        /// <summary>
        /// Получает свойство по имени.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="stringComparison">The string comparison.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty(this Type type, string propertyName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase) => Obj.GetProperty(type, propertyName, stringComparison);

        /// <summary>
        /// Получает имена всех публичных свойств типа.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String[].</returns>
        public static string[] GetPropertyNames(this Type type) => Obj.GetPropertyNames(type);

        /// <summary>
        /// Получает первый пользовательский атрибут по имени типа.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <param name="attributeName">Name of the attribute.</param>
        /// <param name="stringComparison">The string comparison.</param>
        /// <returns>Attribute.</returns>
        public static Attribute GetCustomAttribute(this MemberInfo member, string attributeName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase) => Obj.GetCustomAttribute(member, attributeName, stringComparison);

        /// <summary>
        /// Возвращает поле по условию фильтрации.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="matchCriteria">The match criteria.</param>
        /// <returns>FieldInfo.</returns>
        public static FieldInfo GetField(this Type type, Func<FieldInfo, bool> matchCriteria) => Obj.GetField(type, matchCriteria);

        /// <summary>
        /// Возвращает все типы из указанной сборки, которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Type of the base.</param>
        /// <param name="fromAssembly">From assembly.</param>
        /// <returns>Type[].</returns>
        public static Type[] GetImplementationsOf(this Type baseType, Assembly fromAssembly) => Obj.GetImplementationsOf(baseType, fromAssembly);

        /// <summary>
        /// Возвращает все типы из всех загруженных сборок, которые реализуют интерфейс или наследуются от указанного базового
        /// типа.
        /// </summary>
        /// <param name="baseType">Type of the base.</param>
        /// <returns>Type[].</returns>
        public static Type[] GetImplementationsOf(this Type baseType) => Obj.GetImplementationsOf(baseType);

        /// <summary>
        /// Получает событие с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="name">The name.</param>
        /// <returns>EventInfo.</returns>
        public static EventInfo GetLowestEvent(this Type type, string name) => Obj.GetLowestEvent(type, name);

        /// <summary>
        /// Получает поле с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="name">The name.</param>
        /// <returns>FieldInfo.</returns>
        public static FieldInfo GetLowestField(this Type type, string name) => Obj.GetLowestField(type, name);

        /// <summary>
        /// Получает метод с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="name">The name.</param>
        /// <returns>MethodInfo.</returns>
        public static MethodInfo GetLowestMethod(this Type type, string name) => Obj.GetLowestMethod(type, name);

        /// <summary>
        /// Получает свойство с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="name">The name.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetLowestProperty(this Type type, string name) => Obj.GetLowestProperty(type, name);

        /// <summary>
        /// Расширение для <see cref="Type"/>, возвращает методы типа, соответствующие указанным типам параметров.
        /// </summary>
        /// <param name="type">Тип, для которого выполняется поиск методов.</param>
        /// <param name="args">Массив типов параметров для фильтрации методов.
        /// Если массив пуст или равен <c>null</c>, возвращаются все методы типа.</param>
        /// <returns>
        /// Массив <see cref="MethodInfo"/> методов типа, совместимых с указанными типами параметров.
        /// </returns>
        /// <remarks>
        /// Используется кэширование через <see cref="MemberCache"/> для ускорения повторных вызовов.
        /// </remarks>
        public static MethodInfo[] GetMethods(this Type type, params Type[] args) => MemberCache.Create(type).GetMethods(args);

        /// <summary>
        /// Уведомляет подписчиков о изменении одного или нескольких свойств объекта.
        /// </summary>
        /// <param name="observable">Объект, реализующий <see cref="INotifyPropertyChanged"/>.</param>
        /// <param name="propertyNames">
        /// Имена свойств, для которых необходимо отправить уведомление.
        /// Если не указаны, уведомление будет отправлено для всех публичных свойств.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="observable"/> равен <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если у типа объекта не найден метод <c>OnPropertyChanged</c>.
        /// </exception>
        /// <remarks>
        /// Метод использует отражение и кэш <see cref="MemberCache"/> для поиска метода
        /// <c>OnPropertyChanged</c> и его вызова.
        /// </remarks>
        public static void NotifyPropertyChanged(this INotifyPropertyChanged observable, params string[] propertyNames)
        {
            if (observable == null)
            {
                throw new ArgumentNullException(nameof(observable));
            }

            var type = MemberCache.Create(observable.GetType());

            if (propertyNames == null || propertyNames.Length == 0)
            {
                propertyNames = type.PublicProperties.Select(x => x.Name).ToArray();
            }

            if (type.PropertyChanged?.GetValue(observable) is PropertyChangedEventHandler handler)
            {
                foreach (var propertyName in propertyNames)
                {
                    handler(observable, new PropertyChangedEventArgs(propertyName));
                }
            }
        }
    }
}