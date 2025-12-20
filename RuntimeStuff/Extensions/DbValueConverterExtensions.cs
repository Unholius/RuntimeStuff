using System;
using System.Reflection;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Содержит методы расширения для преобразования
    /// делегатов форматирования значений базы данных.
    /// </summary>
    public static class DbValueConverterExtensions
    {
        /// <summary>
        /// Преобразует делегат <see cref="DbClient.DbValueConverter"/>
        /// в универсальный <see cref="Func{T1,T2,T3,T4,TResult}"/>.
        /// </summary>
        /// <param name="d">
        /// Делегат преобразования значения базы данных.
        /// </param>
        /// <returns>
        /// Функция, эквивалентная переданному делегату
        /// <see cref="DbClient.DbValueConverter"/>.
        /// </returns>
        /// <remarks>
        /// Метод используется для унификации API и упрощения
        /// работы с конвертерами значений в обобщённом виде,
        /// например при передаче или хранении в коллекциях.
        /// </remarks>
        public static Func<string, object, PropertyInfo, object, object> ToFunc(
            this DbClient.DbValueConverter d)
        {
            return (f, v, p, i) => d(f, v, p, i);
        }

        /// <summary>
        /// Преобразует универсальную функцию в делегат
        /// <see cref="DbClient.DbValueConverter"/>.
        /// </summary>
        /// <param name="func">
        /// Функция преобразования значения базы данных.
        /// </param>
        /// <returns>
        /// Экземпляр делегата <see cref="DbClient.DbValueConverter"/>,
        /// оборачивающий указанную функцию.
        /// </returns>
        /// <remarks>
        /// Данный метод позволяет использовать стандартные
        /// <see cref="Func{T1,T2,T3,T4,TResult}"/> в местах,
        /// где требуется тип <see cref="DbClient.DbValueConverter"/>.
        /// </remarks>
        public static DbClient.DbValueConverter ToDbValueConverter(
            this Func<string, object, PropertyInfo, object, object> func)
        {
            return new DbClient.DbValueConverter(func);
        }
    }
}
