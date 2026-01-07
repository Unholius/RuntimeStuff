// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbValueConverterExtensions.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Содержит методы расширения для преобразования
    /// делегатов форматирования значений базы данных.
    /// </summary>
    public static class DbValueConverterExtensions
    {
        /// <summary>
        /// Преобразует делегат <see cref="DbClient.DbValueConverter" />
        /// в универсальный <see cref="Func{T1,T2,T3,T4,TResult}" />.
        /// </summary>
        /// <param name="d">Делегат преобразования значения базы данных.</param>
        /// <returns>Функция, эквивалентная переданному делегату
        /// <see cref="DbClient.DbValueConverter" />.</returns>
        /// <remarks>Метод используется для унификации API и упрощения
        /// работы с конвертерами значений в обобщённом виде,
        /// например при передаче или хранении в коллекциях.</remarks>
        public static Func<string, object, PropertyInfo, object, object> ToFunc(this DbClient.DbValueConverter d) => (f, v, p, i) => d(f, v, p, i);

        /// <summary>
        /// Converts to objectconverter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter">The converter.</param>
        /// <returns>DbClient.DbValueConverter&lt;System.Object&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">converter.</exception>
        public static DbClient.DbValueConverter<object> ToObjectConverter<T>(this DbClient.DbValueConverter<T> converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            return (fieldName, fieldValue, propertyInfo, item) =>
            {
                if (!(item is T typedItem))
                {
                    throw new InvalidCastException(
                        $"Item must be of type {typeof(T).FullName}");
                }

                return converter(fieldName, fieldValue, propertyInfo, typedItem);
            };
        }

        /// <summary>
        /// Converts to typedconverter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter">The converter.</param>
        /// <returns>DbClient.DbValueConverter&lt;T&gt;.</returns>
        public static DbClient.DbValueConverter<T> ToTypedConverter<T>(this DbClient.DbValueConverter converter) => (f, v, p, item) => converter(f, v, p, item);

        /// <summary>
        /// Converts to typedconverter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter">The converter.</param>
        /// <returns>DbClient.DbValueConverter&lt;T&gt;.</returns>
        public static DbClient.DbValueConverter<T> ToTypedConverter<T>(this DbClient.DbValueConverter<object> converter) => (f, v, p, item) => converter(f, v, p, item);

        /// <summary>
        /// Converts to typedconverter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter">The converter.</param>
        /// <returns>DbClient.DbValueConverter&lt;T&gt;.</returns>
        public static DbClient.DbValueConverter<T> ToTypedConverter<T>(this Func<string, object, PropertyInfo, object, object> converter) => (f, v, p, item) => converter(f, v, p, item);

        /// <summary>
        /// Преобразует универсальную функцию в делегат
        /// <see cref="DbClient.DbValueConverter" />.
        /// </summary>
        /// <param name="func">Функция преобразования значения базы данных.</param>
        /// <returns>Экземпляр делегата <see cref="DbClient.DbValueConverter" />,
        /// оборачивающий указанную функцию.</returns>
        /// <remarks>Данный метод позволяет использовать стандартные
        /// <see cref="Func{T1,T2,T3,T4,TResult}" /> в местах,
        /// где требуется тип <see cref="DbClient.DbValueConverter" />.</remarks>
        public static DbClient.DbValueConverter ToDbValueConverter(this Func<string, object, PropertyInfo, object, object> func) => new DbClient.DbValueConverter(func);
    }
}