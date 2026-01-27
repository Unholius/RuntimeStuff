// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbConnectionExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using RuntimeStuff.Options;

    /// <summary>
    /// Расширения для работы с подключениями к базе данных.
    /// </summary>
    public static class DbConnectionExtensions
    {
        /// <summary>
        /// Добавляет параметр сервера базы данных в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="serverName">Имя или адрес сервера базы данных.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection Server(this IDbConnection con, string serverName)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).ServerParameterName, serverName);
        }

        /// <summary>
        /// Добавляет параметр имени базы данных в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="database">Имя базы данных.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection Database(this IDbConnection con, string database)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).DatabaseParameterName, database);
        }

        /// <summary>
        /// Добавляет параметр имени пользователя в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="userName">Имя пользователя базы данных.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection User(this IDbConnection con, string userName)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).UserParameterName, userName);
        }

        /// <summary>
        /// Добавляет параметр пароля пользователя в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="password">Пароль пользователя базы данных.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection Password(this IDbConnection con, string password)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).PasswordParameterName, password);
        }

        /// <summary>
        /// Добавляет параметр доверия сертификату сервера в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="value">
        /// Значение параметра доверия сертификату сервера
        /// (обычно <c>true</c> или <c>false</c>).
        /// </param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection TrustCertificate(this IDbConnection con, bool value)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).TrustServerCertificateParameterName, value);
        }

        /// <summary>
        /// Добавляет параметр интегрированной безопасности (Windows-аутентификация)
        /// в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="value">
        /// Значение параметра интегрированной безопасности
        /// (обычно <c>true</c> или <c>false</c>).
        /// </param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection IntegratedSecurity(this IDbConnection con, bool value)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).IntegratedSecurityParameterName, value);
        }

        /// <summary>
        /// Добавляет параметр тайм-аута подключения в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="timeoutSeconds">Тайм-аут подключения в секундах.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection Timeout(this IDbConnection con, int timeoutSeconds)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).ConnectTimeoutParameterName, timeoutSeconds);
        }

        /// <summary>
        /// Добавляет параметр имени приложения в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="appName">Имя приложения, устанавливающего соединение.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection ApplicationName(this IDbConnection con, string appName)
        {
            return Param(con, SqlProviderOptions.GetInstance(con).ApplicationNameParameterName, appName);
        }

        /// <summary>
        /// Добавляет параметр в строку подключения.
        /// </summary>
        /// <param name="con">Соединение базы данных.</param>
        /// <param name="paramName">Имя параметра.</param>
        /// <param name="paramValue">Значение параметра.</param>
        /// <returns>Тот же экземпляр <see cref="IDbConnection"/> для цепочного вызова.</returns>
        public static IDbConnection Param(this IDbConnection con, string paramName, object paramValue)
        {
            if (string.IsNullOrEmpty(paramName) || paramValue == null)
            {
                return con;
            }

            con.ConnectionString += $"{paramName}={paramValue};";
            return con;
        }

        /// <summary>
        /// Выполняет указанную агрегирующую функцию для колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="aggFunction">Агрегирующая функция (COUNT, SUM, AVG, MIN, MAX).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelectors">Селекторы колонок.</param>
        /// <returns>Словарь с результатами агрегации (имя колонки → значение).</returns>
        public static Dictionary<string, object> Agg<TFrom>(this IDbConnection connection, string aggFunction, Expression<Func<TFrom, bool>> whereExpression = null, params Expression<Func<TFrom, object>>[] columnSelectors)
            where TFrom : class => connection.AsDbClient().Agg(aggFunction, whereExpression, columnSelectors);

        /// <summary>
        /// Выполняет различные агрегирующие функции для указанных колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelectors">Пары (селектор колонки, агрегирующая функция).</param>
        /// <returns>Словарь с результатами агрегации.</returns>
        public static Dictionary<string, object> Agg<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors)
            where TFrom : class => connection.AsDbClient().Agg(whereExpression, columnSelectors);

        /// <summary>
        /// Асинхронно выполняет указанную агрегирующую функцию для колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="aggFunction">Агрегирующая функция (COUNT, SUM, AVG, MIN, MAX).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="columnSelectors">Селекторы колонок.</param>
        /// <returns>Задача, возвращающая словарь с результатами агрегации.</returns>
        public static Task<Dictionary<string, object>> AggAsync<TFrom>(this IDbConnection connection, string aggFunction, Expression<Func<TFrom, bool>> whereExpression = null, CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelectors)
            where TFrom : class => connection.AsDbClient().AggAsync(aggFunction, whereExpression, token, columnSelectors);

        /// <summary>
        /// Асинхронно выполняет различные агрегирующие функции для указанных колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="columnSelectors">Пары (селектор колонки, агрегирующая функция).</param>
        /// <returns>Задача, возвращающая словарь с результатами агрегации.</returns>
        public static Task<Dictionary<string, object>> AggAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, CancellationToken token = default, params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors)
            where TFrom : class => connection.AsDbClient().AggAsync(whereExpression, token, columnSelectors);

        /// <summary>
        /// Создает типизированный клиент базы данных для указанного подключения.
        /// </summary>
        /// <typeparam name="T">Тип подключения, реализующий IDbConnection.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <returns>Типизированный клиент базы данных.</returns>
        public static DbClient<T> AsDbClient<T>(this T connection)
            where T : IDbConnection, new() => DbClient<T>.Create(connection);

        /// <summary>
        /// Создает клиент базы данных для указанного подключения.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <returns>Клиент базы данных.</returns>
        public static DbClient AsDbClient(this IDbConnection connection) => DbClient.Create(connection);

        /// <summary>
        /// Возвращает среднее значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Среднее значение.</returns>
        public static T Avg<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Avg<TFrom, T>(columnSelector);

        /// <summary>
        /// Возвращает среднее значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Среднее значение.</returns>
        public static object Avg<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Avg(columnSelector);

        /// <summary>
        /// Асинхронно возвращает среднее значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая среднее значение.</returns>
        public static Task<object> AvgAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().AvgAsync(whereExpression, columnSelector, token);

        /// <summary>
        /// Начинает новую транзакцию.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="level">Уровень изоляции транзакции.</param>
        /// <returns>Созданная транзакция.</returns>
        public static IDbTransaction BeginTransaction(this IDbConnection connection, IsolationLevel level = IsolationLevel.ReadCommitted) => connection.AsDbClient().BeginTransaction(level);

        /// <summary>
        /// Возвращает количество записей по результатам выполнения команды.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmd">Команда для выполнения.</param>
        /// <returns>Количество записей.</returns>
        public static object Count(this IDbConnection connection, IDbCommand cmd) => connection.AsDbClient().Count(cmd);

        /// <summary>
        /// Возвращает количество записей по SQL-запросу.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <returns>Количество записей.</returns>
        public static object Count(this IDbConnection connection, string query) => connection.AsDbClient().Count(query);

        /// <summary>
        /// Возвращает количество записей для указанной сущности.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки (опционально).</param>
        /// <returns>Количество записей.</returns>
        public static object Count<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null)
            where TFrom : class => connection.AsDbClient().Count(whereExpression, columnSelector);

        /// <summary>
        /// Возвращает типизированное количество записей для указанной сущности.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки (опционально).</param>
        /// <returns>Типизированное количество записей.</returns>
        public static T Count<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null)
            where TFrom : class => connection.AsDbClient().Count<TFrom, T>(whereExpression, columnSelector);

        /// <summary>
        /// Асинхронно возвращает количество записей по результатам выполнения команды.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmd">Команда для выполнения.</param>
        /// <returns>Задача, возвращающая количество записей.</returns>
        public static Task<object> CountAsync(this IDbConnection connection, IDbCommand cmd) => connection.AsDbClient().CountAsync(cmd);

        /// <summary>
        /// Асинхронно возвращает количество записей по SQL-запросу.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество записей.</returns>
        public static Task<object> CountAsync(this IDbConnection connection, string query, CancellationToken token = default) => connection.AsDbClient().CountAsync(query, token);

        /// <summary>
        /// Асинхронно возвращает количество записей для указанной сущности.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество записей.</returns>
        public static Task<object> CountAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().CountAsync(whereExpression, columnSelector, token);

        /// <summary>
        /// Асинхронно возвращает типизированное количество записей для указанной сущности.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая типизированное количество записей.</returns>
        public static Task<T> CountAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().CountAsync<TFrom, T>(whereExpression, columnSelector, token);

        /// <summary>
        /// Создает команду базы данных.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="commandTimeOut">Таймаут команды в секундах.</param>
        /// <returns>Созданная команда.</returns>
        public static DbCommand CreateCommand(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null, int commandTimeOut = 30) => connection.AsDbClient().CreateCommand(query, cmdParams, dbTransaction, commandTimeOut);

        /// <summary>
        /// Удаляет записи из таблицы по указанному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE для удаления.</param>
        /// <returns>Количество удаленных записей.</returns>
        public static int Delete<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression)
            where T : class => connection.AsDbClient().Delete(whereExpression);

        /// <summary>
        /// Удаляет указанную сущность из таблицы.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для удаления.</param>
        /// <returns>Количество удаленных записей.</returns>
        public static int Delete<T>(this IDbConnection connection, T item)
            where T : class => connection.AsDbClient().Delete(item);

        /// <summary>
        /// Асинхронно удаляет указанную сущность из таблицы.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для удаления.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество удаленных записей.</returns>
        public static Task<int> DeleteAsync<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().DeleteAsync(item, dbTransaction, token);

        /// <summary>
        /// Асинхронно удаляет записи из таблицы по указанному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE для удаления.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество удаленных записей.</returns>
        public static Task<int> DeleteAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction, CancellationToken token = default)
            where T : class => connection.AsDbClient().DeleteAsync(whereExpression, dbTransaction, token);

        /// <summary>
        /// Асинхронно удаляет коллекцию сущностей из таблицы.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей для удаления.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество удаленных записей.</returns>
        public static Task<int> DeleteRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction, CancellationToken token = default)
            where T : class => connection.AsDbClient().DeleteRangeAsync(list, dbTransaction, token);

        /// <summary>
        /// Завершает текущую транзакцию.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        public static void EndTransaction(this IDbConnection connection) => connection.AsDbClient().EndTransaction();

        /// <summary>
        /// Выполняет SQL-команду без возврата результата.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="queryParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <returns>Количество затронутых строк.</returns>
        public static int ExecuteNonQuery(this IDbConnection connection, string query, object queryParams = null, IDbTransaction dbTransaction = null) => connection.AsDbClient().ExecuteNonQuery(query, queryParams, dbTransaction);

        /// <summary>
        /// Асинхронно выполняет SQL-команду без возврата результата.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество затронутых строк.</returns>
        public static Task<int> ExecuteNonQueryAsync(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default) => connection.AsDbClient().ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);

        /// <summary>
        /// Выполняет SQL-команду и возвращает скалярное значение.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <returns>Скалярное значение.</returns>
        public static object ExecuteScalar(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null) => connection.AsDbClient().ExecuteScalar(query, cmdParams, dbTransaction);

        /// <summary>
        /// Выполняет команду и возвращает скалярное значение.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmd">Команда для выполнения.</param>
        /// <returns>Скалярное значение.</returns>
        public static object ExecuteScalar(this IDbConnection connection, IDbCommand cmd) => connection.AsDbClient().ExecuteScalar(cmd);

        /// <summary>
        /// Возвращает значение указанного свойства по условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <typeparam name="TProp">Тип свойства.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="propertySelector">Селектор свойства.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <returns>Значение свойства.</returns>
        public static TProp ExecuteScalar<T, TProp>(this IDbConnection connection, Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression) => connection.AsDbClient().ExecuteScalar(propertySelector, whereExpression);

        /// <summary>
        /// Выполняет SQL-команду и возвращает типизированное скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <returns>Типизированное скалярное значение.</returns>
        public static T ExecuteScalar<T>(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null) => connection.AsDbClient().ExecuteScalar<T>(query, cmdParams, dbTransaction);

        /// <summary>
        /// Выполняет команду и возвращает типизированное скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmd">Команда для выполнения.</param>
        /// <returns>Типизированное скалярное значение.</returns>
        public static T ExecuteScalar<T>(this IDbConnection connection, IDbCommand cmd) => connection.AsDbClient().ExecuteScalar<T>(cmd);

        /// <summary>
        /// Асинхронно выполняет SQL-команду и возвращает скалярное значение.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая скалярное значение.</returns>
        public static Task<object> ExecuteScalarAsync(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default) => connection.AsDbClient().ExecuteScalarAsync(query, cmdParams, dbTransaction, token);

        /// <summary>
        /// Асинхронно выполняет команду и возвращает скалярное значение.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmd">Команда для выполнения.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая скалярное значение.</returns>
        public static Task<object> ExecuteScalarAsync(this IDbConnection connection, IDbCommand cmd, CancellationToken token = default) => connection.AsDbClient().ExecuteScalarAsync(cmd, token);

        /// <summary>
        /// Асинхронно возвращает значение указанного свойства по условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <typeparam name="TProp">Тип свойства.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="propertySelector">Селектор свойства.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая значение свойства.</returns>
        public static Task<TProp> ExecuteScalarAsync<T, TProp>(this IDbConnection connection, Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression, CancellationToken token = default) => connection.AsDbClient().ExecuteScalarAsync(propertySelector, whereExpression, token);

        /// <summary>
        /// Асинхронно выполняет SQL-команду и возвращает типизированное скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая типизированное скалярное значение.</returns>
        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default) => connection.AsDbClient().ExecuteScalarAsync<T>(query, cmdParams, dbTransaction, token);

        /// <summary>
        /// Возвращает первую запись из результата запроса.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <returns>Первая запись.</returns>
        public static T First<T>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null) => connection.AsDbClient().First(query, cmdParams, columns, columnToPropertyMap, converter, offsetRows, itemFactory);

        /// <summary>
        /// Возвращает первую запись по указанному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="orderByExpression">Условия сортировки (опционально).</param>
        /// <returns>Первая запись.</returns>
        public static T First<T>(
            this IDbConnection connection,
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => connection.AsDbClient().First(whereExpression, columnToPropertyMap, converter, offsetRows, itemFactory, orderByExpression);

        /// <summary>
        /// Асинхронно возвращает первую запись из результата запроса.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <returns>Задача, возвращающая первую запись.</returns>
        public static Task<T> FirstAsync<T>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null) => connection.AsDbClient().FirstAsync(query, cmdParams, columns, columnToPropertyMap, converter, offsetRows, itemFactory);

        /// <summary>
        /// Асинхронно возвращает первую запись по указанному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="ct">Токен отмены.</param>
        /// <param name="orderByExpression">Условия сортировки (опционально).</param>
        /// <returns>Задача, возвращающая первую запись.</returns>
        public static Task<T> FirstAsync<T>(
            this IDbConnection connection,
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => connection.AsDbClient().FirstAsync(whereExpression, columnToPropertyMap, converter, offsetRows, itemFactory, ct, orderByExpression);

        /// <summary>
        /// Возвращает агрегированные статистики для указанных колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селекторы колонок.</param>
        /// <returns>Словарь с агрегированными статистиками (имя колонки → количество, минимум, максимум, сумма, среднее).</returns>
        public static Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)> GetAggs<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, params Expression<Func<TFrom, object>>[] columnSelector)
            where TFrom : class => connection.AsDbClient().GetAggs(whereExpression, columnSelector);

        /// <summary>
        /// Асинхронно возвращает агрегированные статистики для указанных колонок.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="columnSelector">Селекторы колонок.</param>
        /// <returns>Задача, возвращающая словарь с агрегированными статистиками.</returns>
        public static Task<Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)>> GetAggsAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelector)
            where TFrom : class => connection.AsDbClient().GetAggsAsync(whereExpression, token, columnSelector);

        /// <summary>
        /// Возвращает информацию о страницах для указанного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="pageSize">Размер страницы.</param>
        /// <returns>Словарь с информацией о страницах (номер страницы → смещение и количество).</returns>
        public static Dictionary<int, (int offset, int count)> GetPages<TFrom>(this IDbConnection connection, int pageSize)
            where TFrom : class => connection.AsDbClient().GetPages<TFrom>(pageSize);

        /// <summary>
        /// Асинхронно возвращает информацию о страницах для указанного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="pageSize">Размер страницы.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая словарь с информацией о страницах.</returns>
        public static Task<Dictionary<int, (int offset, int count)>> GetPagesAsync<TFrom>(this IDbConnection connection, int pageSize, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().GetPagesAsync<TFrom>(pageSize, token);

        /// <summary>
        /// Возвращает количество страниц для указанного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="pageSize">Размер страницы.</param>
        /// <returns>Количество страниц.</returns>
        public static int GetPagesCount<TFrom>(this IDbConnection connection, int pageSize)
            where TFrom : class => connection.AsDbClient().GetPagesCount<TFrom>(pageSize);

        /// <summary>
        /// Асинхронно возвращает количество страниц для указанного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="pageSize">Размер страницы.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество страниц.</returns>
        public static Task<int> GetPagesCountAsync<TFrom>(this IDbConnection connection, int pageSize, Expression<Func<TFrom, bool>> whereExpression = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().GetPagesCountAsync<TFrom>(pageSize, whereExpression, token);

        /// <summary>
        /// Получает параметры из объекта.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="cmdParams">Объект с параметрами.</param>
        /// <param name="propertyNames">Имена свойств для извлечения (опционально).</param>
        /// <returns>Словарь параметров (имя → значение).</returns>
        public static IReadOnlyDictionary<string, object> GetParams(this IDbConnection connection, object cmdParams, params string[] propertyNames) => connection.AsDbClient().GetParams(cmdParams, propertyNames);

        /// <summary>
        /// Возвращает сырой SQL-код команды.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="command">Команда.</param>
        /// <returns>SQL-код команды.</returns>
        public static string GetRawSql(this IDbConnection connection, IDbCommand command) => connection.AsDbClient().GetRawSql(command);

        /// <summary>
        /// Вставляет новую запись в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <returns>Вставленная сущность.</returns>
        public static T Insert<T>(this IDbConnection connection, IDbTransaction dbTransaction = null, params Action<T>[] insertColumns)
            where T : class => connection.AsDbClient().Insert(dbTransaction, insertColumns);

        /// <summary>
        /// Вставляет новую запись в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <returns>Вставленная сущность.</returns>
        public static T Insert<T>(this IDbConnection connection, params Action<T>[] insertColumns)
            where T : class => connection.AsDbClient().Insert(insertColumns);

        /// <summary>
        /// Вставляет указанную сущность в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для вставки.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <returns>Идентификатор вставленной записи.</returns>
        public static object Insert<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns)
            where T : class => connection.AsDbClient().Insert(item, dbTransaction, insertColumns);

        /// <summary>
        /// Вставляет указанную сущность в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для вставки.</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <returns>Идентификатор вставленной записи.</returns>
        public static object Insert<T>(this IDbConnection connection, T item, params Expression<Func<T, object>>[] insertColumns)
            where T : class => connection.AsDbClient().Insert(item, insertColumns);

        /// <summary>
        /// Асинхронно вставляет новую запись в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая идентификатор вставленной записи.</returns>
        public static Task<object> InsertAsync<T>(this IDbConnection connection, Action<T>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().InsertAsync(insertColumns, dbTransaction, token);

        /// <summary>
        /// Асинхронно вставляет указанную сущность в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для вставки.</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая идентификатор вставленной записи.</returns>
        public static Task<object> InsertAsync<T>(this IDbConnection connection, T item, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().InsertAsync(item, insertColumns, dbTransaction, token);

        /// <summary>
        /// Вставляет коллекцию сущностей в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей для вставки.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <returns>Количество вставленных записей.</returns>
        public static int InsertRange<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns)
            where T : class => connection.AsDbClient().InsertRange(list, dbTransaction, insertColumns);

        /// <summary>
        /// Асинхронно вставляет коллекцию сущностей в таблицу.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей для вставки.</param>
        /// <param name="insertColumns">Колонки для вставки (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество вставленных записей.</returns>
        public static Task<int> InsertRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().InsertRangeAsync(list, insertColumns, dbTransaction, token);

        /// <summary>
        /// Возвращает максимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Максимальное значение.</returns>
        public static T Max<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Max<TFrom, T>(columnSelector);

        /// <summary>
        /// Возвращает максимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Максимальное значение.</returns>
        public static object Max<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Max(columnSelector);

        /// <summary>
        /// Асинхронно возвращает максимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая максимальное значение.</returns>
        public static Task<T> MaxAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().MaxAsync<TFrom, T>(whereExpression, columnSelector, token);

        /// <summary>
        /// Асинхронно возвращает максимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая максимальное значение.</returns>
        public static Task<object> MaxAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().MaxAsync(whereExpression, columnSelector, token);

        /// <summary>
        /// Возвращает минимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Минимальное значение.</returns>
        public static T Min<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Min<TFrom, T>(columnSelector);

        /// <summary>
        /// Возвращает минимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Минимальное значение.</returns>
        public static object Min<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Min(columnSelector);

        /// <summary>
        /// Асинхронно возвращает минимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая минимальное значение.</returns>
        public static Task<T> MinAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().MinAsync<TFrom, T>(whereExpression, columnSelector, token);

        /// <summary>
        /// Асинхронно возвращает минимальное значение для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая минимальное значение.</returns>
        public static Task<object> MinAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().MinAsync(whereExpression, columnSelector, token);

        /// <summary>
        /// Открыть соединение.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>IDbConnection.</returns>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        public static bool TryOpen(this IDbConnection connection) => connection.TryOpen(out _);

        /// <summary>
        /// Открыть соединение.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="exception">Исключение при попытке установить соединение.</param>
        /// <returns>IDbConnection.</returns>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        public static bool TryOpen(this IDbConnection connection, out Exception exception)
        {
            exception = null;
            try
            {
                return Open(connection) == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }

        /// <summary>
        /// Открыть соединение.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>IDbConnection.</returns>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        public static ConnectionState Open(this IDbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            try
            {
                if (connection.State == ConnectionState.Broken)
                {
                    connection.Close();
                }

                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }

            return connection.State;
        }

        /// <summary>
        /// Выполняет запрос и возвращает типизированную коллекцию.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции.</typeparam>
        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <returns>Типизированная коллекция.</returns>
        public static TList Query<TList, T>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null)
            where TList : ICollection<T>, IList, new() => connection.AsDbClient().Query<TList, T>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Асинхронно выполняет запрос и возвращает типизированную коллекцию.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции.</typeparam>
        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Задача, возвращающая типизированную коллекцию.</returns>
        public static Task<TList> QueryAsync<TList, T>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = -1,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default)
            where TList : ICollection<T>, IList, new() => connection.AsDbClient().QueryAsync<TList, T>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);

        /// <summary>
        /// Возвращает сумму значений для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Сумма значений.</returns>
        public static T Sum<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Sum<TFrom, T>(columnSelector);

        /// <summary>
        /// Возвращает сумму значений для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <returns>Сумма значений.</returns>
        public static object Sum<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class => connection.AsDbClient().Sum(columnSelector);

        /// <summary>
        /// Асинхронно возвращает сумму значений для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая сумму значений.</returns>
        public static Task<T> SumAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().SumAsync<TFrom, T>(whereExpression, columnSelector, token);

        /// <summary>
        /// Асинхронно возвращает сумму значений для указанной колонки.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Селектор колонки.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая сумму значений.</returns>
        public static Task<object> SumAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default)
            where TFrom : class => connection.AsDbClient().SumAsync(whereExpression, columnSelector, token);

        /// <summary>
        /// Преобразует результат запроса в DataTable.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnSelectors">Селекторы колонок (опционально).</param>
        /// <returns>DataTable с результатами запроса.</returns>
        public static DataTable ToDataTable<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, Func<string, object, DataColumn, object> valueConverter = null, params Expression<Func<TFrom, object>>[] columnSelectors) => connection.AsDbClient().ToDataTable(whereExpression, fetchRows, offsetRows, valueConverter, columnSelectors);

        /// <summary>
        /// Преобразует результат SQL-запроса в DataTable.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnMap">Соответствие колонок (опционально).</param>
        /// <returns>DataTable с результатами запроса.</returns>
        public static DataTable ToDataTable(this IDbConnection connection, string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, params (string, string)[] columnMap) => connection.AsDbClient().ToDataTable(query, cmdParams, valueConverter, columnMap);

        /// <summary>
        /// Асинхронно преобразует результат запроса в DataTable.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="columnSelectors">Селекторы колонок (опционально).</param>
        /// <returns>Задача, возвращающая DataTable с результатами запроса.</returns>
        public static Task<DataTable> ToDataTableAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors) => connection.AsDbClient().ToDataTableAsync(whereExpression, fetchRows, offsetRows, columnSelectors);

        /// <summary>
        /// Асинхронно преобразует результат SQL-запроса в DataTable.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="columnMap">Соответствие колонок (опционально).</param>
        /// <returns>Задача, возвращающая DataTable с результатами запроса.</returns>
        public static Task<DataTable> ToDataTableAsync(this IDbConnection connection, string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, CancellationToken token = default, params (string, string)[] columnMap) => connection.AsDbClient().ToDataTableAsync(query, cmdParams, valueConverter, token, columnMap);

        /// <summary>
        /// Преобразует результат SQL-запроса в массив DataTable (для нескольких результирующих наборов).
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnMap">Соответствие колонок (опционально).</param>
        /// <returns>Массив DataTable с результатами запроса.</returns>
        public static DataTable[] ToDataTables(this IDbConnection connection, string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, params (string, string)[] columnMap) => connection.AsDbClient().ToDataTables(query, cmdParams, valueConverter, columnMap);

        /// <summary>
        /// Асинхронно преобразует результат SQL-запроса в массив DataTable.
        /// </summary>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="columnMap">Соответствие колонок (опционально).</param>
        /// <returns>Задача, возвращающая массив DataTable с результатами запроса.</returns>
        public static Task<DataTable[]> ToDataTablesAsync(this IDbConnection connection, string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, CancellationToken token = default, params (string, string)[] columnMap) => connection.AsDbClient().ToDataTablesAsync(query, cmdParams, valueConverter, token, columnMap);

        /// <summary>
        /// Преобразует результат SQL-запроса в словарь.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания пар ключ-значение (опционально).</param>
        /// <returns>Словарь с результатами запроса.</returns>
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection connection, string query, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => connection.AsDbClient().ToDictionary(query, cmdParams, columns, columnToPropertyMap, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Преобразует результаты запроса сущностей в словарь с использованием селекторов ключа и значения.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="keySelector">Селектор ключа.</param>
        /// <param name="valueSelector">Селектор значения.</param>
        /// <param name="whereExpression">Условие WHERE (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания пар ключ-значение (опционально).</param>
        /// <returns>Словарь с результатами запроса.</returns>
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue, TFrom>(this IDbConnection connection, Expression<Func<TFrom, TKey>> keySelector, Expression<Func<TFrom, TValue>> valueSelector, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => connection.AsDbClient().ToDictionary(keySelector, valueSelector, whereExpression, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Асинхронно преобразует результат SQL-запроса в словарь.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания пар ключ-значение (опционально).</param>
        /// <returns>Задача, возвращающая словарь с результатами запроса.</returns>
        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection connection, string query, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => connection.AsDbClient().ToDictionaryAsync(query, cmdParams, columns, columnToPropertyMap, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Асинхронно преобразует результаты запроса сущностей в словарь с использованием селекторов ключа и значения.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="keySelector">Селектор ключа.</param>
        /// <param name="valueSelector">Селектор значения.</param>
        /// <param name="whereExpression">Условие WHERE (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания пар ключ-значение (опционально).</param>
        /// <returns>Задача, возвращающая словарь с результатами запроса.</returns>
        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue, TFrom>(this IDbConnection connection, Expression<Func<TFrom, TKey>> keySelector, Expression<Func<TFrom, TValue>> valueSelector, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => connection.AsDbClient().ToDictionaryAsync(keySelector, valueSelector, whereExpression, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Преобразует результат запроса в список сущностей.
        /// </summary>
        /// <typeparam name="TItem">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <returns>Список сущностей.</returns>
        public static List<TItem> ToList<TItem>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null) => connection.AsDbClient().ToList(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);

        /// <summary>
        /// Преобразует результат запроса по условию в список сущностей.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="orderByExpression">Условия сортировки (опционально).</param>
        /// <returns>Список сущностей.</returns>
        public static List<T> ToList<T>(
            this IDbConnection connection,
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => connection.AsDbClient().ToList(whereExpression, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, orderByExpression);

        /// <summary>
        /// Асинхронно преобразует результат запроса в список сущностей.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="query">SQL-запрос (опционально).</param>
        /// <param name="cmdParams">Параметры запроса (опционально).</param>
        /// <param name="columns">Колонки для выборки (опционально).</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Задача, возвращающая список сущностей.</returns>
        public static Task<List<T>> ToListAsync<T>(
            this IDbConnection connection,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default) => connection.AsDbClient().ToListAsync(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);

        /// <summary>
        /// Асинхронно преобразует результат запроса по условию в список сущностей.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="columnToPropertyMap">Соответствие колонок свойствам (опционально).</param>
        /// <param name="converter">Конвертер значений (опционально).</param>
        /// <param name="fetchRows">Количество извлекаемых строк (-1 для всех).</param>
        /// <param name="offsetRows">Количество пропускаемых строк.</param>
        /// <param name="itemFactory">Фабрика для создания объектов (опционально).</param>
        /// <param name="ct">Токен отмены.</param>
        /// <param name="orderByExpression">Условия сортировки (опционально).</param>
        /// <returns>Задача, возвращающая список сущностей.</returns>
        public static Task<List<T>> ToListAsync<T>(
            this IDbConnection connection,
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => connection.AsDbClient().ToListAsync(whereExpression, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct, orderByExpression);

        /// <summary>
        /// Обновляет указанную сущность в таблице.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для обновления.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <returns>Количество обновленных записей.</returns>
        public static int Update<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction, params Expression<Func<T, object>>[] updateColumns)
            where T : class => connection.AsDbClient().Update(item, dbTransaction, updateColumns);

        /// <summary>
        /// Обновляет указанную сущность в таблице.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для обновления.</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <returns>Количество обновленных записей.</returns>
        public static int Update<T>(this IDbConnection connection, T item, params Expression<Func<T, object>>[] updateColumns)
            where T : class => connection.AsDbClient().Update(item, null, updateColumns);

        /// <summary>
        /// Обновляет сущность в таблице с указанным условием WHERE.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для обновления.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <returns>Количество обновленных записей.</returns>
        public static int Update<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns)
            where T : class => connection.AsDbClient().Update(item, whereExpression, dbTransaction, updateColumns);

        /// <summary>
        /// Асинхронно обновляет указанную сущность в таблице.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для обновления.</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество обновленных записей.</returns>
        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().UpdateAsync(item, updateColumns, dbTransaction, token);

        /// <summary>
        /// Асинхронно обновляет сущность в таблице с указанным условием WHERE.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="item">Сущность для обновления.</param>
        /// <param name="whereExpression">Условие WHERE.</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество обновленных записей.</returns>
        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class => connection.AsDbClient().UpdateAsync(item, whereExpression, updateColumns, dbTransaction, token);

        /// <summary>
        /// Обновляет коллекцию сущностей в таблице.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей для обновления.</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <returns>Количество обновленных записей.</returns>
        public static int UpdateRange<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns)
            where T : class => connection.AsDbClient().UpdateRange(list, dbTransaction, updateColumns);

        /// <summary>
        /// Асинхронно обновляет коллекцию сущностей в таблице.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="connection">Подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей для обновления.</param>
        /// <param name="updateColumns">Колонки для обновления (опционально).</param>
        /// <param name="dbTransaction">Транзакция (опционально).</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Задача, возвращающая количество обновленных записей.</returns>
        public static Task<int> UpdateRangeAsync<T>(
            this IDbConnection connection,
            IEnumerable<T> list,
            Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class => connection.AsDbClient().UpdateRangeAsync(list, updateColumns, dbTransaction, token);
    }
}