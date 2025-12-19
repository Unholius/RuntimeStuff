using RuntimeStuff.Builders;
using RuntimeStuff.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff
{
    /// <summary>
    /// Предоставляет типобезопасный клиент базы данных, позволяющий работать с конкретной реализацией подключения к
    /// базе данных, определяемой типом параметра T.
    /// </summary>
    /// <remarks>Используйте этот класс для работы с определённым типом подключения к базе данных, чтобы
    /// получить доступ к специфичным для него свойствам и методам. Это может быть полезно при необходимости
    /// использовать расширенные возможности конкретного провайдера базы данных.</remarks>
    /// <typeparam name="T">Тип подключения к базе данных, реализующий интерфейс IDbConnection и имеющий конструктор без параметров.</typeparam>
    public class DbClient<T> : DbClient where T : IDbConnection, new()
    {
        public DbClient() : base(new T())
        {
        }

        public DbClient(string connectionString)
        {
            Connection = new T
            {
                ConnectionString = connectionString
            };
        }

        public new T Connection
        {
            get => (T)base.Connection;
            set => base.Connection = value;
        }
    }

    /// <summary>
    /// Предоставляет набор статических вспомогательных методов для упрощения работы с реляционными базами данных через
    /// интерфейсы ADO.NET. Включает универсальные методы для создания и выполнения SQL-команд, преобразования
    /// результатов запросов в коллекции объектов, словари, DataTable, а также для выполнения операций вставки,
    /// обновления и удаления как в синхронном, так и в асинхронном режиме.
    /// </summary>
    /// <remarks>Класс предназначен для типовых CRUD-операций и маппинга данных между базой и объектами .NET,
    /// минимизируя ручную работу с SQL и IDataReader. Все методы реализованы как статические и не требуют создания
    /// экземпляра класса. Поддерживается автоматическое открытие и закрытие соединения, а также обработка параметров
    /// команд и сопоставление колонок с помощью выражений и пользовательских маппингов. Для асинхронных операций
    /// используются стандартные Task-методы. Для корректной работы требуется, чтобы используемые типы сущностей
    /// имели публичные конструкторы без параметров и свойства с публичными сеттерами.</remarks>
    public class DbClient : IDisposable
    {
        private static readonly StringComparer IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;
        private readonly AsyncLocal<IDbTransaction> _tr = new AsyncLocal<IDbTransaction>();

        private bool _disposed;

        public DbClient()
        {
            DbReaderValueConvertor = (value, type) => ChangeType(value is string s ? s.Trim() : value, type);
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса DbClient с использованием указанного подключения к базе данных.
        /// </summary>
        /// <param name="con">Подключение к базе данных, которое будет использоваться этим клиентом. Не может быть равно null.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если параметр con имеет значение null.</exception>
        public DbClient(IDbConnection con) : this()
        {
            Connection = con ?? throw new ArgumentNullException(nameof(con));
        }

        ~DbClient()
        {
            Dispose(false);
        }


        public IDbConnection Connection { get; set; }

        /// <summary>
        /// Конвертор значения из DbReader в тип свойства
        /// </summary>
        public Func<object, Type, object> DbReaderValueConvertor { get; set; }

        /// <summary>
        /// Количество секунд по умолчанию для таймаута команд
        /// </summary>
        public int DefaultCommandTimeout { get; set; } = 30;

        /// <summary>
        /// Возвращает значение, указывающее, был ли объект освобождён.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Создаёт новый экземпляр клиента базы данных для указанного типа подключения.
        /// </summary>
        /// <typeparam name="T">Тип подключения к базе данных, реализующий интерфейс IDbConnection и имеющий конструктор без параметров.</typeparam>
        /// <param name="connectionString">Строка подключения, используемая для установления соединения с базой данных. Не может быть null или пустой.</param>
        /// <returns>Экземпляр DbClient<T>, настроенный для работы с указанной строкой подключения.</returns>
        public static DbClient<T> Create<T>(string connectionString) where T : IDbConnection, new()
        {
            return new DbClient<T>(connectionString);
        }

        /// <summary>
        /// Создаёт новый экземпляр класса DbClient, используя предоставлённое подключение к базе данных.
        /// </summary>
        /// <param name="connection">Подключение к базе данных, которое будет использоваться клиентом. Не может быть равно null и должно быть
        /// открыто перед выполнением операций.</param>
        /// <returns>Новый экземпляр DbClient, связанный с указанным подключением.</returns>
        public static DbClient Create(IDbConnection connection)
        {
            return new DbClient(connection);
        }

        /// <summary>
        /// Создаёт новый экземпляр класса DbClient.
        /// </summary>
        /// <returns>Новый экземпляр DbClient, готовый к использованию для взаимодействия с базой данных.</returns>
        public static DbClient Create()
        {
            return new DbClient();
        }

        /// <summary>
        /// Формирует строку SQL-запроса с подстановкой значений параметров
        /// из переданного <see cref="IDbCommand"/>.
        /// </summary>
        /// <param name="command">
        /// Команда <see cref="IDbCommand"/>, содержащая параметры и их значения,
        /// которые будут подставлены в SQL.
        /// </param>
        /// <param name="paramNamePrefix">
        /// Префикс имени параметра в SQL-запросе. 
        /// По умолчанию <c>"@"</c>.
        /// </param>
        /// <param name="dateFormat">
        /// Формат даты при подстановке значений <see cref="DateTime"/>.
        /// По умолчанию <c>"yyyyMMdd"</c>.
        /// </param>
        /// <param name="stringPrefix">
        /// Префикс для строковых значений (например, кавычка в SQL).
        /// По умолчанию <c>"'"</c>.
        /// </param>
        /// <param name="stringSuffix">
        /// Суффикс для строковых значений (например, кавычка в SQL).
        /// По умолчанию <c>"'"</c>.
        /// </param>
        /// <param name="nullValue">
        /// Строковое представление <c>null</c> значения.
        /// По умолчанию <c>"NULL"</c>.
        /// </param>
        /// <param name="trueValue">
        /// Строковое представление логического значения <c>true</c>.
        /// По умолчанию <c>"1"</c>.
        /// </param>
        /// <param name="falseValue">
        /// Строковое представление логического значения <c>false</c>.
        /// По умолчанию <c>"0"</c>.
        /// </param>
        /// <returns>
        /// Строка SQL с подставленными значениями параметров,
        /// готовая к использованию для логирования или анализа.
        /// </returns>
        /// <remarks>
        /// Метод не выполняет команду <see cref="IDbCommand"/> — он только
        /// формирует SQL с текущими значениями параметров.
        /// </remarks>
        public string GetRawSql(IDbCommand command, string paramNamePrefix = "@", string dateFormat = "yyyyMMdd", string stringPrefix = "'", string stringSuffix = "'", string nullValue = "NULL", string trueValue = "1", string falseValue = "0")
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var sql = command.CommandText;

            foreach (IDbDataParameter parameter in command.Parameters)
            {
                var paramToken = paramNamePrefix + parameter.ParameterName;
                var literal = ToSqlLiteral(
                    parameter.Value,
                    dateFormat,
                    stringPrefix,
                    stringSuffix,
                    nullValue,
                    trueValue,
                    falseValue);

                sql = ReplaceParameterToken(sql, paramToken, literal);
            }

            return sql;
        }

        private static string ReplaceParameterToken(string sql, string token, string replacement)
        {
            return Regex.Replace(
                sql,
                $@"(?<![\w@]){Regex.Escape(token)}(?!\w)",
                replacement,
                RegexOptions.CultureInvariant);
        }

        private static string ToSqlLiteral(
            object value,
            string dateFormat,
            string stringPrefix,
            string stringSuffix,
            string nullValue,
            string trueValue,
            string falseValue)
        {
            if (value == null || value == DBNull.Value)
                return nullValue;

            switch (value)
            {
                case string s:
                    return $"{stringPrefix}{EscapeString(s)}{stringSuffix}";

                case char c:
                    return $"{stringPrefix}{EscapeString(c.ToString())}{stringSuffix}";

                case bool b:
                    return b ? trueValue : falseValue;

                case DateTime dt:
                    return $"{stringPrefix}{dt.ToString(dateFormat, CultureInfo.InvariantCulture)}{stringSuffix}";

                case DateTimeOffset dto:
                    return $"{stringPrefix}{dto.ToString(dateFormat, CultureInfo.InvariantCulture)}{stringSuffix}";

                case Guid g:
                    return $"{stringPrefix}{g}{stringSuffix}";

                case Enum e:
                    return Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture);

                case TimeSpan ts:
                    return $"{stringPrefix}{ts.ToString("c", CultureInfo.InvariantCulture)}{stringSuffix}";

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                default:
                    return $"{stringPrefix}{EscapeString(value.ToString())}{stringSuffix}";
            }
        }

        private static string EscapeString(string value)
        {
            return value.Replace("'", "''");
        }


        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="con">Экземпляр соединения с БД.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Параметры команды в формате (имя, значение).</param>
        /// <returns>Готовая команда <see cref="IDbCommand"/>.</returns>
        public IDbCommand CreateCommand(string query, IEnumerable<(string, object)> cmdParams = null)
        {
            return CreateCommand(Connection, query, cmdParams?.ToArray() ?? Array.Empty<(string, object)>());
        }

        public void SetParameterCollection(IDbCommand cmd, Dictionary<string, object> cmdParams)
        {
            foreach (var cp in cmdParams)
            {
                
                IDbDataParameter p = null;
                if (cmd.Parameters.Contains(cp.Key))
                    p = (IDbDataParameter)cmd.Parameters[cp.Key];
                else
                {
                    p = cmd.CreateParameter();
                    cmd.Parameters.Add(p);
                }

                p.ParameterName = cp.Key;
                p.Value = cp.Value ?? DBNull.Value;
            }
        }

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="con">Экземпляр соединения с БД.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Параметры команды в формате (имя, значение).</param>
        /// <returns>Готовая команда <see cref="IDbCommand"/>.</returns>
        public IDbCommand CreateCommand(IDbConnection connection, string query, params (string, object)[] cmdParams)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = DefaultCommandTimeout;

            if (cmdParams != null)
                foreach (var cp in cmdParams)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = cp.Item1;
                    p.Value = cp.Item2 ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

            if (_tr != null)
                cmd.Transaction = _tr.Value;

            LogCommand(cmd);

            return cmd;
        }

        /// <summary>
        /// Создаёт команду для SQL-запроса, принимая параметры в виде словаря.
        /// </summary>
        /// <param name="con">Экземпляр соединения.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Команда <see cref="IDbCommand"/>.</returns>
        public IDbCommand CreateCommand(string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
        {
            return CreateCommand(Connection, query, cmdParams?.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Выполняет удаление записей из таблицы, соответствующей типу <typeparamref name="T"/>,
        /// используя условие, заданное выражением <paramref name="whereExpression"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, таблица которой используется в запросе.</typeparam>
        /// <param name="con">Подключение к базе данных. Если оно закрыто — будет автоматически открыто.</param>
        /// <param name="whereExpression">Lambda-выражение, определяющее условие WHERE. Если null, то удалятся ВСЕ записи!</param>
        /// <remarks>
        /// Метод формирует запрос вида:
        /// <code>
        /// DELETE FROM [Table] WHERE ...
        /// </code>
        /// </remarks>
        public int Delete<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteNonQuery(query);
        }

        /// <summary>
        /// Удаляет запись из базы данных на основе ключевых свойств объекта.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сущности, соответствующий таблице в базе данных.
        /// </typeparam>
        /// <param name="con">
        /// Активное соединение с базой данных.
        /// </param>
        /// <param name="item">
        /// Объект, для которого необходимо выполнить удаление.
        /// Его ключевые свойства используются для формирования условия WHERE.
        /// </param>
        /// <returns>
        /// Количество затронутых строк.
        /// Обычно 1, если удаление выполнено успешно; 0 — если запись не найдена.
        /// </returns>
        /// <remarks>
        /// Метод формирует SQL-запрос вида:
        /// <c>DELETE FROM [Table] WHERE [Key1] = @Key1 AND [Key2] = @Key2 ...</c>
        ///
        /// Предполагается, что значения параметров будут привязаны позже, при выполнении команды.
        /// </remarks>
        public int Delete<T>(T item) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause<T>()).Trim();
            return ExecuteNonQuery(query, GetParams(item));
        }

        /// <summary>
        /// Асинхронно удаляет запись из базы данных на основе ключевых свойств объекта.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сущности, соответствующий таблице в базе данных.
        /// </typeparam>
        /// <param name="con">
        /// Активное соединение с базой данных.
        /// </param>
        /// <param name="item">
        /// Объект, для которого необходимо выполнить удаление.
        /// Его ключевые свойства используются для формирования условия WHERE.
        /// </param>
        /// <returns>
        /// Задача, результат которой — количество затронутых строк.
        /// Обычно 1 при успешном удалении; 0 — если запись отсутствует.
        /// </returns>
        /// <remarks>
        /// Метод формирует SQL-запрос вида:
        /// <c>DELETE FROM [Table] WHERE [Key1] = @Key1 AND [Key2] = @Key2 ...</c>
        ///
        /// Использует асинхронный ExecuteNonQueryAsync.
        /// </remarks>
        public Task<int> DeleteAsync<T>(T item) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause<T>()).Trim();
            return ExecuteNonQueryAsync(query, GetParams(item));
        }

        /// <summary>
        /// Асинхронно выполняет удаление записей из таблицы, соответствующей типу <typeparamref name="T"/>,
        /// используя условие, заданное выражением <paramref name="whereExpression"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, таблица которой используется в запросе.</typeparam>
        /// <param name="whereExpression">Lambda-выражение, определяющее условие WHERE. Если null, то удалятся ВСЕ записи!</param>
        /// <returns>Задача, представляющая асинхронную операцию удаления.</returns>
        /// <remarks>
        /// Формируемый SQL-запрос аналогичен синхронной версии:
        /// <code>
        /// DELETE FROM [Table] WHERE ...
        /// </code>
        /// </remarks>
        public Task<int> DeleteAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteNonQueryAsync(query);
        }

        /// <summary>
        /// Асинхронно удаляет из базы данных все объекты указанного типа <typeparamref name="T"/>, переданные в коллекции <paramref name="list"/>.
        /// </summary>
        /// <typeparam name="T">Тип объектов для удаления. Должен быть ссылочным типом.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод использует транзакцию для выполнения всех удалений.</param>
        /// <param name="list">Коллекция объектов, которые нужно удалить из базы данных.</param>
        /// <returns>
        /// Задача <see cref="Task"/> с результатом в виде числа <see cref="int"/>, равного количеству успешно удалённых объектов.
        /// </returns>
        /// <remarks>
        /// Метод выполняет следующее:
        /// <list type="bullet">
        /// <item>Создаёт транзакцию с помощью <see cref="IDbConnection.BeginTransaction"/>.</item>
        /// <item>Последовательно вызывает асинхронный метод <see cref="DeleteAsync(IDbConnection, T)"/> для каждого элемента коллекции <paramref name="list"/>.</item>
        /// <item>Суммирует количество удалённых записей.</item>
        /// <item>Фиксирует транзакцию после успешного удаления всех элементов.</item>
        /// </list>
        /// Если при удалении любого элемента возникает исключение, транзакция не фиксируется и все изменения откатываются.
        /// </remarks>
        public async Task<int> DeleteRangeAsync<T>(IEnumerable<T> list) where T : class
        {
            try
            {
                var count = 0;
                using (StartTransaction())
                {
                    foreach (var item in list)
                    {
                        count += await DeleteAsync(item);
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }
        
        /// <summary>
        /// Освобождает все ресурсы, используемые данным экземпляром класса.
        /// </summary>
        /// <remarks>Вызывайте этот метод, когда объект больше не нужен, чтобы явно освободить управляемые
        /// и неуправляемые ресурсы. После вызова Dispose объект не должен использоваться повторно.</remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Завершает текущую транзакцию, фиксируя все внесённые изменения.
        /// </summary>
        /// <remarks>После вызова метода все ресурсы, связанные с транзакцией, освобождаются, а соединение
        /// с базой данных закрывается. Повторный вызов метода без начала новой транзакции приведёт к
        /// исключению.</remarks>
        /// <exception cref="InvalidOperationException">Выбрасывается, если транзакция не была начата.</exception>
        public void EndTransaction()
        {
            if (_tr.Value == null)
                throw new InvalidOperationException("Транзакция не была начата.");

            _tr.Value.Commit();
            _tr.Value.Dispose();
            _tr.Value = null;
            CloseConnection();
        }

        /// <summary>
        /// Выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием коллекции параметров в виде <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <param name="con">Открытое или закрытое подключение к базе данных. Если подключение закрыто — будет открыто автоматически.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Коллекция параметров SQL-команды.</param>
        /// <returns>Количество затронутых строк.</returns>
        public int ExecuteNonQuery(string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
        {
            return ExecuteNonQuery(query, cmdParams.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием массива параметров.
        /// </summary>
        /// <param name="con">Открытое или закрытое подключение к базе данных. Если подключение закрыто — будет открыто автоматически.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Массив параметров SQL-команды в формате (имя, значение).</param>
        /// <returns>Количество затронутых строк.</returns>
        public int ExecuteNonQuery(string query, params (string, object)[] cmdParams)
        {
            using (var cmd = CreateCommand(Connection, query, cmdParams))
            {
                BeginConnection(Connection);

                var i = cmd.ExecuteNonQuery();
                CloseConnection(Connection);
                return i;
            }
        }

        /// <summary>
        /// Асинхронно выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием коллекции параметров в виде <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <param name="con">Подключение к базе данных. Если оно закрыто, будет автоматически открыто.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Коллекция параметров SQL-команды.</param>
        /// <returns>Задача, возвращающая количество затронутых строк.</returns>
        public Task<int> ExecuteNonQueryAsync(string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
        {
            return ExecuteNonQueryAsync(query, cmdParams.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Асинхронно выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием массива параметров.
        /// </summary>
        /// <param name="con">Подключение к базе данных. Если оно закрыто, будет автоматически открыто.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Массив параметров SQL-команды в формате (имя, значение).</param>
        /// <returns>Задача, возвращающая количество затронутых строк.</returns>
        public async Task<int> ExecuteNonQueryAsync(string query, params (string, object)[] cmdParams)
        {
            using (var cmd = (DbCommand)CreateCommand(Connection, query, cmdParams))
            {
                try
                {
                    await BeginConnectionAsync();
                    var i = await cmd.ExecuteNonQueryAsync();
                    return i;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает одно скалярное значение.
        /// </summary>
        /// <param name="con">Соединение с БД.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public object ExecuteScalar(string query, params (string, object)[] cmdParams)
        {
            return ExecuteScalar<object>(query, cmdParams);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает одно скалярное значение.
        /// </summary>
        /// <param name="con">Соединение с БД.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public object ExecuteScalar(string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
        {
            return ExecuteScalar<object>(query, cmdParams.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает одно скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение с БД.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public T ExecuteScalar<T>(string query, params (string, object)[] cmdParams)
        {
            using (var cmd = CreateCommand(Connection, query, cmdParams))
            {
                try
                {
                    BeginConnection();
                    var v = cmd.ExecuteScalar();
                    return (T)ChangeType(v, typeof(T));
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает скалярное значение, принимая параметры в виде словаря.
        /// </summary>
        public T ExecuteScalar<T>(Expression<Func<T, bool>> whereExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteScalar<T>(query);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает скалярное значение, принимая параметры в виде словаря.
        /// </summary>
        public T ExecuteScalar<T>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
        {
            return ExecuteScalar<T>(query, cmdParams?.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public Task<T> ExecuteScalarAsync<T>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken ct = default)
        {
            return ExecuteScalarAsync<T>(query, cmdParams?.Select(x => (x.Key, x.Value)), ct);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public async Task<T> ExecuteScalarAsync<T>(string query, IEnumerable<(string, object)> cmdParams = null, CancellationToken ct = default)
        {
            using (var cmd = CreateCommand(Connection, query, cmdParams?.ToArray()))
            {
                try
                {
                    await BeginConnectionAsync();
                    object value;

                    if (cmd is DbCommand dbcmd)
                        value = await dbcmd.ExecuteScalarAsync(ct);
                    else
                        value = cmd.ExecuteScalar();
                    return (T)ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первый объект типа <typeparamref name="T"/> из результата,
        /// или <c>null</c>, если результат пустой.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, создаваемого на основе строки результата.
        /// Должен быть ссылочным типом с публичным конструктором без параметров.
        /// </typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="query">SQL-запрос для выборки данных.</param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса в виде <see cref="KeyValuePair{String, Object}"/>.
        /// Может быть <c>null</c>, если параметры отсутствуют.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Карта сопоставления колонок и свойств объекта: имя колонки → имя свойства.
        /// Если <c>null</c>, используется автомаппинг по совпадению имён.
        /// </param>
        /// <param name="converter">
        /// Пользовательская функция преобразования значения поля в тип свойства.
        /// Если не указано, используется стандартный <c>DefaultConverter</c>.
        /// </param>
        /// <param name="setter">
        /// Пользовательская логика присвоения значения свойству.
        /// Если не указано — используется <c>prop.SetValue(item, value)</c>.
        /// </param>
        /// <returns>Первый объект типа <typeparamref name="T"/> или <c>null</c>, если результат пустой.</returns>
        /// <remarks>
        /// Метод использует <see cref="ToList"/> с ограничением на 1 запись, после чего возвращает <c>FirstOrDefault()</c>.
        /// </remarks>
        public T First<T>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams,
            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, TypeCache, T> setter = null) where T : class, new()
        {
            return ToList(query, cmdParams, columnToPropertyMap, converter, setter, 1)?.FirstOrDefault();
        }

        /// <summary>
        /// Выполняет SQL-запрос с условием <paramref name="whereExpression"/> и возвращает первый объект типа <typeparamref name="T"/> из результата,
        /// или <c>null</c>, если результат пустой.
        /// </summary>
        /// <typeparam name="T">Тип объекта, создаваемого на основе строки результата.</typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="whereExpression">Выражение фильтрации, которое используется для построения SQL-условия WHERE.</param>
        /// <param name="converter">Пользовательская функция преобразования значения поля в тип свойства.</param>
        /// <param name="setter">Пользовательская логика присвоения значения свойству.</param>
        /// <param name="orderByExpression">Порядок сортировки</param>
        /// <returns>Первый объект типа <typeparamref name="T"/> или <c>null</c>, если результат пустой.</returns>
        public T First<T>(Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
        {
            return ToList(whereExpression, converter, setter, 1, orderByExpression)?.FirstOrDefault();
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первый объект типа <typeparamref name="T"/> из результата,
        /// или <c>null</c>, если результат пустой.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, создаваемого на основе строки результата.
        /// Должен быть ссылочным типом с публичным конструктором без параметров.
        /// </typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="query">
        /// SQL-запрос для выборки данных. Если <c>null</c>, используется автоматически сформированный SELECT для типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Параметры запроса в виде последовательности кортежей <c>(string имя, object значение)</c>.
        /// Может быть <c>null</c>, если параметры отсутствуют.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Коллекция сопоставлений колонок результата с именами свойств объекта <typeparamref name="T"/>.
        /// Если <c>null</c>, используется автоматическое сопоставление по совпадению имён.
        /// </param>
        /// <param name="converter">
        /// Пользовательская функция преобразования значения поля в тип свойства.
        /// Если не указана, применяется стандартный <c>DefaultConverter</c>.
        /// </param>
        /// <param name="setter">
        /// Пользовательская функция для установки значения свойства.
        /// Если не указана, используется стандартная установка через <c>prop.SetValue(item, value)</c>.
        /// </param>
        /// <returns>Первый объект типа <typeparamref name="T"/> или <c>null</c>, если результат пустой.</returns>
        /// <remarks>
        /// Метод использует <see cref="ToList{T}"/> с ограничением на одну запись,
        /// после чего возвращает <c>FirstOrDefault()</c>.
        /// </remarks>
        public T First<T>(string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null)
            where T : class, new()
        {
            return ToList(query, cmdParams, columnToPropertyMap, converter, setter, 1)?.FirstOrDefault();
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первый объект типа <typeparamref name="T"/> из результата асинхронно,
        /// или <c>null</c>, если результат пустой.
        /// </summary>
        /// <typeparam name="T">Тип объекта, создаваемого на основе строки результата.</typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="query">SQL-запрос для выборки данных.</param>
        /// <param name="cmdParams">Параметры запроса в виде кортежей или <see cref="KeyValuePair{String, Object}"/>.</param>
        /// <param name="columnToPropertyMap">Сопоставление колонок и свойств объекта.</param>
        /// <param name="converter">Функция преобразования значений полей в свойства объекта.</param>
        /// <param name="setter">Пользовательская логика присвоения значений свойствам.</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/>.</param>
        /// <returns>Задача, результатом которой является первый объект типа <typeparamref name="T"/> или <c>null</c>.</returns>
        /// <remarks>Метод использует <see cref="ToListAsync"/> с ограничением на 1 запись и возвращает <c>FirstOrDefault()</c>.</remarks>
        public async Task<T> FirstAsync<T>(string query,
            IEnumerable<KeyValuePair<string, object>> cmdParams,
            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, TypeCache, T> setter = null) where T : class, new()
        {
            return (await ToListAsync(query, cmdParams, columnToPropertyMap, converter, setter, 1))?.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронная версия метода <see cref="First{T}(IDbConnection, Expression{Func{T, bool}}, Func{object, Type, object}, Action{string, object, TypeCache, T})"/>.
        /// </summary>
        public async Task<T> FirstAsync<T>(Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null,
            Action<string, object, TypeCache, T> setter = null, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
        {
            return (await ToListAsync(whereExpression, converter, setter, 1, orderByExpression))?.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронная версия метода <see cref="First{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T})"/>.
        /// </summary>
        public async Task<T> FirstAsync<T>(string query = null,
            IEnumerable<(string, object)> cmdParams = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, TypeCache, T> setter = null) where T : class, new()
        {
            return (await ToListAsync(query, cmdParams, columnToPropertyMap, converter, setter, 1))?.FirstOrDefault();
        }

        /// <summary>
        /// Создает словарь параметров SQL на основе публичных свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта, для которого извлекаются параметры.</typeparam>
        /// <param name="item">Объект, значения свойств которого используются как значения параметров.</param>
        /// <param name="include">
        /// Если <c>true</c> — включаются только свойства, перечисленные в <paramref name="propertyNames"/>.
        /// Если <c>false</c> — исключаются свойства из <paramref name="propertyNames"/>.
        /// </param>
        /// <param name="propertyNames">Список имен свойств, которые нужно включить или исключить.</param>
        /// <returns>
        /// Словарь параметров, где ключ — имя свойства, а значение — его значение у объекта.
        /// </returns>
        public Dictionary<string, object> GetParams<T>(T item, params string[] propertyNames) where T : class
        {
            return TypeCache.Create<T>().ToDictionary(item, propertyNames);
        }

        /// <summary>
        /// Создает новый объект типа <typeparamref name="T"/>, заполняет его с помощью переданных действий
        /// и выполняет INSERT в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется в базу данных.</typeparam>
        /// <param name="con">Подключение к базе данных. Если закрыто — будет автоматически открыто.</param>
        /// <param name="insertColumns">Делегаты, которые заполняют свойства создаваемого объекта.</param>
        /// <returns>Созданный и вставленный объект.</returns>
        public T Insert<T>(params Action<T>[] insertColumns) where T : class
        {
            var item = TypeHelper.New<T>();
            foreach (var a in insertColumns)
                a(item);
            Insert(item);
            return item;
        }

        /// <summary>
        /// Выполняет INSERT указанного объекта в базу данных и при необходимости
        /// считывает значение первичного ключа.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="item">Объект, который нужно вставить.</param>
        /// <param name="queryGetId">Запрос для получения идентификатора (например, SCOPE_IDENTITY). Если пустой — идентификатор не считывается.</param>
        /// <param name="insertColumns">Список свойств, которые необходимо вставить. Если не указаны — вставляются все свойства, кроме первичного ключа.</param>
        /// <returns>Значение первичного ключа, если получено. Иначе — <c>null</c>.</returns>
        public object Insert<T>(T item, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
            if (string.IsNullOrWhiteSpace(queryGetId))
            {
                ExecuteNonQuery(query, GetParams(item));
            }
            else
            {
                query += $"; {queryGetId}";
                id = ExecuteScalar<object>(query, GetParams(item));
                var mi = TypeCache.Create<T>();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                    mi.PrimaryKeys.First().Value.SetValue(item, TypeHelper.ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
            }

            return id;
        }

        /// <summary>
        /// Асинхронно создает новый объект типа <typeparamref name="T"/> и выполняет INSERT,
        /// возвращая значение первичного ключа, если это возможно.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется в базу.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="queryGetId">Запрос для получения идентификатора, например: "SELECT SCOPE_IDENTITY()".</param>
        /// <param name="insertColumns">Делегаты, которые заполняют свойства создаваемого объекта.</param>
        /// <returns>
        /// Значение первичного ключа, если оно получено, иначе — <c>null</c>.
        /// </returns>
        public Task<object> InsertAsync<T>(string queryGetId = "SELECT SCOPE_IDENTITY()", params Action<T>[] insertColumns) where T : class
        {
            var item = TypeHelper.New<T>();
            foreach (var a in insertColumns)
                a(item);
            return InsertAsync(item, queryGetId);
        }

        /// <summary>
        /// Асинхронно выполняет INSERT указанного объекта в базу данных и при необходимости
        /// получает значение первичного ключа.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="item">Объект, который нужно вставить.</param>
        /// <param name="queryGetId">Запрос для получения идентификатора (например, SCOPE_IDENTITY). Если пустой — идентификатор не считывается.</param>
        /// <param name="insertColumns">Список свойств, которые необходимо вставить. Если не указаны — вставляются все свойства, кроме первичного ключа.</param>
        /// <returns>Задача, возвращающая значение первичного ключа или <c>null</c>.</returns>
        public async Task<object> InsertAsync<T>(T item, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
            if (string.IsNullOrWhiteSpace(queryGetId))
            {
                await ExecuteNonQueryAsync(query, GetParams(item));
            }
            else
            {
                query += $"; {queryGetId}";
                id = await ExecuteScalarAsync<object>(query, GetParams(item));
                var mi = TypeCache.Create<T>();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                    mi.PrimaryKeys.First().Value.SetValue(item, TypeHelper.ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
            }

            return id;
        }

        /// <summary>
        /// Выполняет пакетную вставку коллекции объектов в базу данных внутри одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется в базу.</typeparam>
        /// <param name="con">Подключение к базе данных. Если закрыто — будет автоматически открыто.</param>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="queryGetId">
        /// SQL-запрос для получения идентификатора вставленной записи, например "SELECT SCOPE_IDENTITY()".
        /// Если null, идентификатор не возвращается.
        /// </param>
        /// <param name="insertColumns">Свойства, которые необходимо вставить. Если не указаны, вставляются все свойства, кроме первичного ключа.</param>
        /// <remarks>
        /// Все вставки выполняются в одной транзакции.
        /// Если одна из вставок завершится ошибкой, транзакция не будет зафиксирована.
        /// </remarks>
        public int InsertRange<T>(IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            try
            {
                var count = 0;
                using (StartTransaction())
                {
                    var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
                    if (!string.IsNullOrWhiteSpace(queryGetId))
                        query += $"; {queryGetId}";
                    var typeCache = TypeCache.Create<T>();
                    var pk = typeCache.PrimaryKeys.FirstOrDefault().Value;
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = CreateCommand(Connection, query))
                    {
                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            var id = cmd.ExecuteScalar();
                            if (pk != null && id != null)
                            {
                                pk.SetValue(item, TypeHelper.ChangeType(id, pk.PropertyType));
                            }

                            count++;
                        }
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Асинхронно выполняет пакетную вставку коллекции объектов в базу данных внутри одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется в базу.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="queryGetId">
        /// SQL-запрос для получения идентификатора вставленной записи, например "SELECT SCOPE_IDENTITY()".
        /// Если null, идентификатор не возвращается.
        /// </param>
        /// <param name="insertColumns">Свойства, которые необходимо вставить. Если не указаны, вставляются все свойства, кроме первичного ключа.</param>
        /// <returns>Задача, представляющая асинхронную операцию вставки.</returns>
        /// <remarks>
        /// Все вставки выполняются в одной транзакции.
        /// Если одна из вставок завершится ошибкой, транзакция не будет зафиксирована.
        /// </remarks>
        public async Task<int> InsertRangeAsync<T>(IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            try
            {
                var count = 0;
                using (StartTransaction())
                {
                    foreach (var item in list)
                    {
                        await InsertAsync(item, queryGetId, insertColumns);
                        count++;
                    }

                    EndTransaction();
                }
                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }
        

        /// <summary>
        /// Начинает новую транзакцию базы данных для текущего соединения.
        /// </summary>
        /// <remarks>Перед вызовом этого метода убедитесь, что предыдущая транзакция завершена.
        /// Одновременно может быть активна только одна транзакция на соединение.</remarks>
        /// <returns>Объект, представляющий новую транзакцию базы данных. Возвращаемое значение реализует интерфейс
        /// IDbTransaction и должно быть явно завершено вызовом Commit или Rollback.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если транзакция уже была начата для текущего соединения.</exception>
        public IDbTransaction StartTransaction()
        {
            if (_tr.Value != null)
                throw new InvalidOperationException("Транзакция уже была начата.");

            BeginConnection();
            _tr.Value = Connection.BeginTransaction();
            return _tr.Value;
        }

        /// <summary>
        /// Преобразует результат выполнения SQL-запроса в коллекцию объектов заданного типа.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции, которая будет возвращена. Должна реализовывать <see cref="ICollection{TItem}"/> и иметь публичный конструктор без параметров.</typeparam>
        /// <typeparam name="TItem">Тип элементов коллекции. Должен быть ссылочным типом и иметь публичный конструктор без параметров.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="query">
        /// SQL-запрос для выполнения. Если значение <c>null</c> или пустое, будет автоматически сгенерирован SELECT-запрос для типа <typeparamref name="TItem"/> с помощью <see cref="SqlQueryBuilder.GetSelectQuery{TItem}"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров для SQL-запроса в виде кортежей (имя параметра, значение). Может быть <c>null</c>, если параметры не требуются.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Коллекция сопоставлений между именами столбцов результата SQL-запроса и свойствами объекта <typeparamref name="TItem"/>.
        /// Формат: (имя столбца, имя свойства). Если <c>null</c>, используется автоматическое сопоставление по именам.
        /// </param>
        /// <param name="converter">
        /// Функция для преобразования значения столбца в тип свойства. Принимает исходное значение и <see cref="Type"/> целевого свойства, возвращает преобразованное значение.
        /// Если <c>null</c>, используется <see cref="DbReaderValueConvertor"/>.
        /// </param>
        /// <param name="setter">
        /// Действие для установки значения свойства объекта. Принимает:
        /// <list type="bullet">
        /// <item><description>Имя столбца</description></item>
        /// <item><description>Значение столбца после конвертации</description></item>
        /// <item><description>Информацию о свойстве <see cref="TypeCache"/></description></item>
        /// <item><description>Объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="TypeCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <returns>Коллекция типа <typeparamref name="TList"/>, содержащая объекты <typeparamref name="TItem"/> с заполненными свойствами на основе данных из базы.</returns>
        /// <exception cref="Exception">
        /// Выбрасывается, если установка значения свойства не удалась. Внутри исключения хранится исходное исключение и информация о столбце, значении и свойстве.
        /// </exception>
        /// <remarks>
        /// Метод автоматически открывает подключение к базе данных с помощью <see cref="BeginConnection(IDbConnection)"/> и закрывает его после выполнения запроса <see cref="CloseConnection(IDbConnection)"/>.
        /// Для каждого ряда результата создается новый объект <typeparamref name="TItem"/>. Все свойства заполняются в соответствии с <paramref name="columnToPropertyMap"/> или сопоставлением по имени.
        /// </remarks>
        public TList ToCollection<TList, TItem>(string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, TItem> setter = null, int maxRows = -1) where TList : ICollection<TItem>, new() where TItem : class, new()
        {
            if (string.IsNullOrWhiteSpace(query))
                query = SqlQueryBuilder.GetSelectQuery<TItem>();

            if (maxRows >= 0 && query.StartsWith("SELECT ") && !query.StartsWith("SELECT TOP"))
                query = $"SELECT TOP {maxRows}" + query.Substring(6);

            using (var cmd = CreateCommand(query, cmdParams))
            {
                try
                {
                    BeginConnection();

                    var list = new TList();

                    using (var r = cmd.ExecuteReader())
                    {
                        var map = GetReaderFieldToPropertyMap<TItem>(r, columnToPropertyMap);
                        var valueConverter = converter ?? DbReaderValueConvertor;
                        var valueFactory = setter ?? ((colName, colValue, prop, item) => { prop.SetValue(item, colValue); });

                        var rowCount = 0;
                        while (r.Read())
                        {
                            if (rowCount >= maxRows && maxRows > 0)
                                break;

                            var item = new TItem();

                            foreach (var kv in map)
                            {
                                var colIndex = kv.Key;
                                var mapItem = kv.Value;

                                var raw = r.GetValue(colIndex);

                                if (raw == null || raw == DBNull.Value)
                                {
                                    mapItem.propSetter(item, null);
                                    continue;
                                }

                                var value = valueConverter(raw, mapItem.propInfoEx.PropertyType);
                                try
                                {
                                    valueFactory(r.GetName(kv.Key), value, mapItem.propInfoEx, item);
                                }
                                catch (Exception ex)
                                {
                                    throw HandleDbException(ex, cmd);
                                }
                            }

                            list.Add(item);
                            rowCount++;
                        }
                    }

                    return list;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Асинхронно преобразует результат выполнения SQL-запроса в коллекцию объектов заданного типа.
        /// </summary>
        /// <typeparam name="TList">
        /// Тип коллекции, которая будет возвращена. Должна реализовывать <see cref="ICollection{TItem}"/> и иметь публичный конструктор без параметров.
        /// </typeparam>
        /// <typeparam name="TItem">
        /// Тип элементов коллекции. Должен быть ссылочным типом и иметь публичный конструктор без параметров.
        /// </typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="query">
        /// SQL-запрос для выполнения. Если значение <c>null</c> или пустое, будет автоматически сгенерирован SELECT-запрос для типа <typeparamref name="TItem"/> с помощью <see cref="SqlQueryBuilder.GetSelectQuery{TItem}"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров для SQL-запроса в виде кортежей <c>(имя параметра, значение)</c>. Может быть <c>null</c>, если параметры не требуются.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Коллекция сопоставлений между именами столбцов результата SQL-запроса и свойствами объекта <typeparamref name="TItem"/>.
        /// Формат: <c>(имя столбца, имя свойства)</c>. Если <c>null</c>, используется автоматическое сопоставление по именам.
        /// </param>
        /// <param name="converter">
        /// Функция для преобразования значения столбца в тип свойства. Принимает исходное значение и <see cref="Type"/> целевого свойства, возвращает преобразованное значение.
        /// Если <c>null</c>, используется <see cref="DbReaderValueConvertor"/>.
        /// </param>
        /// <param name="setter">
        /// Действие для установки значения свойства объекта. Принимает:
        /// <list type="bullet">
        /// <item><description>имя столбца</description></item>
        /// <item><description>значение столбца после конвертации</description></item>
        /// <item><description>информацию о свойстве <see cref="TypeCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="TypeCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/> для асинхронной операции.</param>
        /// <returns>
        /// Задача <see cref="Task"/> с результатом в виде коллекции типа <typeparamref name="TList"/>,
        /// содержащей объекты <typeparamref name="TItem"/> с заполненными свойствами на основе данных из базы.
        /// </returns>
        /// <exception cref="Exception">
        /// Выбрасывается, если установка значения свойства не удалась. Внутри исключения хранится исходное исключение и информация о столбце, значении и свойстве.
        /// </exception>
        /// <remarks>
        /// Метод:
        /// <list type="bullet">
        /// <item>Асинхронно открывает подключение к базе данных с помощью <see cref="BeginConnectionAsync(IDbConnection)"/>.</item>
        /// <item>Выполняет SQL-запрос и читает результат с помощью <see cref="DbDataReader"/>.</item>
        /// <item>Для каждой строки создаёт новый объект <typeparamref name="TItem"/> и заполняет его свойства, используя сопоставление столбцов и свойств.</item>
        /// <item>Поддерживает кастомные конвертеры значений и кастомные setter-и для свойств.</item>
        /// <item>Закрывает подключение после выполнения запроса через <see cref="CloseConnection(IDbConnection)"/>.</item>
        /// </list>
        /// </remarks>
        public async Task<TList> ToCollectionAsync<TList, TItem>(string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, TItem> setter = null, int maxRows = -1, CancellationToken ct = default) where TList : ICollection<TItem>, new() where TItem : class, new()
        {
            if (string.IsNullOrWhiteSpace(query))
                query = SqlQueryBuilder.GetSelectQuery<TItem>();

            if (maxRows >= 0 && query.StartsWith("SELECT ") && !query.StartsWith("SELECT TOP"))
                query = $"SELECT TOP {maxRows}" + query.Substring(6);

            using (var cmd = (DbCommand)CreateCommand(query, cmdParams))
            {
                try
                {
                    await BeginConnectionAsync();

                    var list = new TList();

                    using (var r = await cmd.ExecuteReaderAsync(ct))
                    {
                        var map = GetReaderFieldToPropertyMap<TItem>(r, columnToPropertyMap);
                        var valueConverter = converter ?? DbReaderValueConvertor;
                        var valueFactory = setter ?? ((colName, colValue, prop, item) => { prop.SetValue(item, colValue); });

                        var rowCount = 0;
                        while (await r.ReadAsync(ct))
                        {
                            if (rowCount >= maxRows && maxRows > 0)
                                break;

                            var item = new TItem();

                            foreach (var kv in map)
                            {
                                var colIndex = kv.Key;
                                var mapItem = kv.Value;

                                var raw = r.GetValue(colIndex);

                                if (raw == null || raw == DBNull.Value)
                                {
                                    mapItem.propSetter(item, null);
                                    continue;
                                }

                                var value = valueConverter(raw, mapItem.propInfoEx.PropertyType);
                                try
                                {
                                    valueFactory(r.GetName(kv.Key), value, mapItem.propInfoEx, item);
                                }
                                catch (Exception ex)
                                {
                                    throw HandleDbException(ex, cmd);
                                }
                            }

                            list.Add(item);
                            rowCount++;
                        }
                    }

                    return list;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и преобразует результирующий набор данных в <see cref="DataTable"/>.
        /// </summary>
        /// <param name="con">
        /// Подключение к базе данных, через которое будет выполняться запрос.
        /// </param>
        /// <param name="query">
        /// SQL-запрос для выполнения. Не может быть <c>null</c> или пустой строкой.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса в виде кортежей (<c>имя</c>, <c>значение</c>).
        /// Может быть <c>null</c>, если параметры не требуются.
        /// </param>
        /// <param name="columnMap">
        /// Коллекция сопоставлений полей результата с именами столбцов DataTable:
        /// (<c>имя столбца в базе</c>, <c>имя столбца в DataTable</c>).
        /// Если <c>null</c>, используются имена из результата запроса.
        /// </param>
        /// <param name="maxRows">
        /// Максимальное количество строк, которое необходимо загрузить.
        /// Если значение <c>-1</c> (по умолчанию), загружаются все строки.
        /// </param>
        /// <returns>
        /// Заполненный объект <see cref="DataTable"/>, содержащий строки результата запроса.
        /// </returns>
        /// <exception cref="NullReferenceException">
        /// Генерируется, если параметр <paramref name="query"/> не указан.
        /// </exception>
        public DataTable ToDataTable(string query, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new NullReferenceException(nameof(query));

            using (var cmd = CreateCommand(query, cmdParams))
            {
                try
                {
                    BeginConnection();

                    var dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (var r = cmd.ExecuteReader())
                    {
                        var map = GetReaderFieldToPropertyMap(r, columnMap);
                        foreach (var kv in map)
                        {
                            var col = new DataColumn(kv.Value, r.GetFieldType(kv.Key));
                            dataTable.Columns.Add(col);
                        }

                        var rowCount = 0;
                        while (r.Read())
                        {
                            if (rowCount >= maxRows && maxRows > 0)
                                break;

                            var item = dataTable.NewRow();

                            foreach (var kv in map)
                            {
                                var colIndex = kv.Key;

                                var raw = r.GetValue(colIndex);

                                if (raw == null || raw == DBNull.Value)
                                {
                                    continue;
                                }

                                item[kv.Value] = raw;
                            }

                            dataTable.Rows.Add(item);
                            rowCount++;
                        }
                    }

                    dataTable.AcceptChanges();
                    dataTable.EndLoadData();

                    return dataTable;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}"/>,
        /// используя первые два столбца результата, с поддержкой передачи параметров в виде
        /// коллекции <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа словаря. Значение первого столбца результата будет преобразовано в этот тип.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения словаря. Значение второго столбца результата будет преобразовано в этот тип.
        /// </typeparam>
        /// <param name="con">
        /// Подключение к базе данных <see cref="IDbConnection"/>.
        /// Метод самостоятельно открывает и закрывает соединение.
        /// </param>
        /// <param name="query">
        /// SQL-запрос, который должен возвращать как минимум два столбца: ключ и значение.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса, где ключ — имя параметра, а значение — его значение.
        /// Может быть <c>null</c>, если параметры не используются.
        /// </param>
        /// <returns>
        /// Словарь <see cref="Dictionary{TKey, TValue}"/>, где ключи и значения получены
        /// из первых двух колонок результата SQL-запроса.
        /// </returns>
        /// <exception cref="Exception">
        /// Выбрасывается, если значение в первом столбце равно <c>null</c> или <see cref="DBNull.Value"/>.
        /// </exception>
        /// <remarks>
        /// Метод является удобной перегрузкой, преобразующей коллекцию <see cref="KeyValuePair{String, Object}"/>
        /// в массив кортежей <c>(string, object)</c> и передающей его основной реализации.
        /// </remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams = null)
        {
            return ToDictionary<TKey, TValue>(query, cmdParams?.Select(x => (x.Key, x.Value)).ToArray());
        }

        /// <summary>
        /// Выполняет запрос к базе данных и возвращает результаты в виде словаря.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из которой извлекаются данные.</typeparam>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="keySelector">Выражение для выбора ключа из сущности.</param>
        /// <param name="valueSelector">Выражение для выбора значения из сущности.</param>
        /// <param name="whereExpression">
        /// Необязательное выражение для фильтрации данных.
        /// Если <c>null</c>, фильтрация не применяется.
        /// </param>
        /// <returns>Словарь <see cref="Dictionary{TKey, TValue}"/> с результатами запроса.</returns>
        /// <remarks>
        /// Метод строит SQL-запрос на основе переданных селекторов и фильтра (если указан),
        /// выполняет его через подключение <paramref name="con"/> и возвращает результаты в виде словаря.
        /// </remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue, T>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(TypeCache.Create<T>(), ExpressionHelper.GetMemberInfo(keySelector).GetMemberInfoEx(), ExpressionHelper.GetMemberInfo(valueSelector).GetMemberInfoEx()) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ToDictionary<TKey, TValue>(query);
        }

        /// <summary>
        /// Преобразует результат выполнения SQL-запроса в словарь <see cref="Dictionary{TKey, TValue}"/>, используя первые два столбца результата.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря. Первый столбец результата SQL-запроса будет преобразован в этот тип.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря. Второй столбец результата SQL-запроса будет преобразован в этот тип.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="query">SQL-запрос, который должен возвращать как минимум два столбца: ключ и значение.</param>
        /// <param name="cmdParams">
        /// Параметры запроса в виде массива кортежей <c>(имя параметра, значение)</c>. Может быть пустым, если параметры не требуются.
        /// </param>
        /// <returns>Словарь <see cref="Dictionary{TKey, TValue}"/> с данными из первой и второй колонок результата запроса.</returns>
        /// <exception cref="Exception">
        /// Выбрасывается, если ключ (первый столбец) равен <c>null</c> или <see cref="DBNull.Value"/>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет следующие шаги:
        /// <list type="bullet">
        /// <item>Создаёт команду SQL с помощью <see cref="IDbConnection.CreateCommand"/> и переданных параметров.</item>
        /// <item>Открывает подключение к базе данных через <see cref="BeginConnection(IDbConnection)"/>.</item>
        /// <item>Читает результат запроса с помощью <see cref="DbDataReader"/>.</item>
        /// <item>Преобразует первый столбец в тип <typeparamref name="TKey"/> для ключа, второй столбец — в тип <typeparamref name="TValue"/> для значения.</item>
        /// <item>Если значение второго столбца равно <c>null</c> или <see cref="DBNull.Value"/>, используется значение по умолчанию для типа <typeparamref name="TValue"/>.</item>
        /// <item>Добавляет пары ключ-значение в словарь. Если ключ уже существует, значение обновляется.</item>
        /// <item>Закрывает подключение через <see cref="CloseConnection(IDbConnection)"/>.</item>
        /// </list>
        /// </remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(string query, params (string, object)[] cmdParams)
        {
            using (var cmd = CreateCommand(Connection, query, cmdParams))
            {
                try
                {
                    BeginConnection();

                    var dic = new Dictionary<TKey, TValue>();

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var rawKey = r.GetValue(0);
                            if (rawKey == null || rawKey == DBNull.Value)
                                throw HandleDbException(new Exception("Ключ не должен быть null!"), cmd);

                            var rawValue = r.GetValue(1);

                            var key = (TKey)ChangeType(rawKey, typeof(TKey));
                            var value = rawValue == null || rawValue == DBNull.Value
                                ? default
                                : (TValue)ChangeType(rawValue, typeof(TValue));

                            dic[key] = value;
                        }
                    }

                    return dic;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Асинхронно выполняет запрос к базе данных и возвращает результаты в виде словаря.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из которой извлекаются данные.</typeparam>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="con">Подключение к базе данных <see cref="IDbConnection"/>.</param>
        /// <param name="keySelector">Выражение для выбора ключа из сущности.</param>
        /// <param name="valueSelector">Выражение для выбора значения из сущности.</param>
        /// <param name="whereExpression">
        /// Необязательное выражение для фильтрации данных.
        /// Если <c>null</c>, фильтрация не применяется.
        /// </param>
        /// <returns>Словарь <see cref="Dictionary{TKey, TValue}"/> с результатами запроса.</returns>
        /// <remarks>
        /// Метод строит SQL-запрос на основе переданных селекторов и фильтра (если указан),
        /// выполняет его через подключение <paramref name="con"/> и возвращает результаты в виде словаря.
        /// </remarks>
        public Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(TypeCache.Create<T>(), ExpressionHelper.GetMemberInfo(keySelector).GetMemberInfoEx(), ExpressionHelper.GetMemberInfo(valueSelector).GetMemberInfoEx()) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ToDictionaryAsync<TKey, TValue>(query);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и преобразует результат в словарь
        /// <see cref="Dictionary{TKey, TValue}"/>, используя первые два столбца результата,
        /// с поддержкой передачи параметров в виде коллекции <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа словаря. Значение первого столбца результата будет преобразовано в этот тип.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения словаря. Значение второго столбца результата будет преобразовано в этот тип.
        /// </typeparam>
        /// <param name="con">
        /// Подключение к базе данных <see cref="IDbConnection"/>.
        /// Метод самостоятельно открывает и закрывает соединение.
        /// </param>
        /// <param name="query">
        /// SQL-запрос, который должен возвращать как минимум два столбца: ключ и значение.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса, где ключ — имя параметра, а значение — его значение.
        /// Может быть <c>null</c>, если параметры отсутствуют.
        /// </param>
        /// <param name="ct">
        /// Токен отмены <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// Задача, представляющая асинхронную операцию, результатом которой является
        /// заполненный словарь <see cref="Dictionary{TKey, TValue}"/>.
        /// </returns>
        /// <remarks>
        /// Метод является удобной перегрузкой, преобразующей коллекцию
        /// <see cref="KeyValuePair{String, Object}"/> в последовательность кортежей
        /// <c>(string, object)</c> и передающей её основной реализации.
        /// </remarks>
        public Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken ct = default)
        {
            return ToDictionaryAsync<TKey, TValue>(query, cmdParams?.Select(x => (x.Key, x.Value)).ToArray(), ct);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и преобразует результат выбора
        /// в словарь <see cref="Dictionary{TKey, TValue}"/> на основе первых двух столбцов результата.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа словаря. Значение первого столбца результата преобразуется в данный тип
        /// с помощью метода <c>ChangeType</c>.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения словаря. Значение второго столбца результата преобразуется в данный тип.
        /// </typeparam>
        /// <param name="con">
        /// Объект подключения <see cref="IDbConnection"/>.
        /// Метод автоматически открывает соединение посредством <c>BeginConnectionAsync</c>
        /// и закрывает его после завершения операции.
        /// </param>
        /// <param name="query">
        /// SQL-запрос, который должен возвращать как минимум два столбца:
        /// первый используется как ключ, второй — как значение.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров SQL-команды в виде кортежей <c>(string name, object value)</c>.
        /// Может быть <c>null</c>, если параметры не требуются.
        /// </param>
        /// <param name="ct">
        /// Токен отмены <see cref="CancellationToken"/> для прекращения операции.
        /// </param>
        /// <returns>
        /// Задача, представляющая асинхронную операцию, результатом которой
        /// является словарь <see cref="Dictionary{TKey, TValue}"/>, построенный
        /// по данным результата запроса.
        /// </returns>
        /// <exception cref="Exception">
        /// Генерируется, если ключевое значение (первый столбец) равно <c>null</c>
        /// или <see cref="DBNull"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Генерируется, если переданное подключение не поддерживает создание объекта
        /// <see cref="DbCommand"/>, необходимого для асинхронного выполнения.
        /// </exception>
        /// <remarks>
        /// Метод ожидает, что результат запроса содержит не менее двух столбцов:
        /// <list type="number">
        /// <item><description>Первый столбец — ключ словаря (<typeparamref name="TKey"/>).</description></item>
        /// <item><description>Второй столбец — значение словаря (<typeparamref name="TValue"/>).</description></item>
        /// </list>
        /// Если значение второго столбца равно <c>null</c> или <see cref="DBNull"/>,
        /// то в словарь помещается значение по умолчанию (<c>default(TValue)</c>).
        /// </remarks>
        public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(string query, IEnumerable<(string, object)> cmdParams = null, CancellationToken ct = default)
        {
            using (var cmd = CreateCommand(Connection, query, cmdParams?.ToArray()))
            {
                try
                {
                    await BeginConnectionAsync();

                    var dic = new Dictionary<TKey, TValue>();

                    if (cmd is DbCommand dbcmd)
                        using (var r = await dbcmd.ExecuteReaderAsync(ct))
                        {
                            while (await r.ReadAsync(ct))
                            {
                                var rawKey = r.GetValue(0);
                                if (rawKey == null || rawKey == DBNull.Value)
                                    throw HandleDbException(new Exception("Ключ не должен быть null!"), dbcmd);

                                var rawValue = r.GetValue(1);

                                var key = (TKey)ChangeType(rawKey, typeof(TKey));
                                var value = rawValue == null || rawValue == DBNull.Value
                                    ? default
                                    : (TValue)ChangeType(rawValue, typeof(TValue));

                                dic[key] = value;
                            }
                        }
                    else
                        throw new NotSupportedException("Для async требуется DbConnection/DbCommand.");

                    return dic;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и преобразует результат в список объектов типа
        /// <typeparamref name="T"/>, поддерживая передачу параметров и сопоставление колонок
        /// через коллекции <see cref="KeyValuePair{String, Object}"/> и <see cref="KeyValuePair{String, String}"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объектов, создаваемых на основе строк результата запроса.
        /// Должен быть ссылочным типом с публичным конструктором без параметров.
        /// </typeparam>
        /// <param name="con">
        /// Подключение к базе данных <see cref="IDbConnection"/>.
        /// Метод сам открывает и закрывает соединение.
        /// </param>
        /// <param name="query">
        /// SQL-запрос, который должен возвращать данные, используемые
        /// для наполнения объектов типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса, где ключ — имя параметра SQL,
        /// а значение — объект значения параметра.
        /// Может быть <c>null</c>, если параметры отсутствуют.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Карта сопоставления: имя колонки → имя свойства объекта.
        /// Используется, если имена колонок запроса не совпадают с именами свойств типа <typeparamref name="T"/>.
        /// Может быть <c>null</c>.
        /// </param>
        /// <param name="converter">
        /// Пользовательский преобразователь значений.
        /// Если не указан, используется стандартный <c>DefaultConverter</c>.
        /// </param>
        /// <param name="setter">
        /// Пользовательская логика установки значения свойства.
        /// Если не указана — используется стандартная установка через <c>prop.SetValue()</c>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <returns>
        /// Список объектов типа <typeparamref name="T"/>, созданных на основе строк результата запроса.
        /// </returns>
        /// <remarks>
        /// Этот метод является перегрузкой, преобразующей параметры и карту колонок,
        /// переданные в виде <see cref="KeyValuePair{String, Object}"/> и
        /// <see cref="KeyValuePair{String, String}"/>, в последовательности кортежей
        /// <c>(string, object)</c> и <c>(string, string)</c>, и передающей их основной реализации.
        /// </remarks>
        public List<T> ToList<T>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1) where T : class, new()
        {
            return ToList(query, cmdParams?.Select(x => (x.Key, x.Value)), columnToPropertyMap?.Select(x => (x.Key, x.Value)), converter, setter, maxRows);
        }

        /// <summary>
        /// Выполняет SQL-запрос, сформированный на основе выражения фильтрации
        /// <paramref name="whereExpression"/>, и преобразует результат в список объектов
        /// типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объектов, создаваемых на основе строк результата запроса.
        /// Должен быть ссылочным типом с публичным конструктором без параметров.
        /// </typeparam>
        /// <param name="con">
        /// Подключение к базе данных <see cref="IDbConnection"/>.
        /// Метод самостоятельно открывает и закрывает соединение.
        /// </param>
        /// <param name="whereExpression">
        /// Лямбда-выражение, описывающее условие фильтрации для выборки данных.
        /// Используется для генерации SQL-условия WHERE посредством <see cref="SqlQueryBuilder.GetWhereClause"/>.
        /// </param>
        /// <param name="converter">
        /// Пользовательский преобразователь значений полей в типы свойств объекта.
        /// Если не указан, используется стандартный <c>DefaultConverter</c>.
        /// </param>
        /// <param name="setter">
        /// Пользовательская логика установки значения в свойство объекта.
        /// Позволяет перехватывать момент присвоения и выполнять дополнительную обработку.
        /// Если не указан — используется простая установка через <c>prop.SetValue()</c>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="orderByExpression">Порядок сортировки</param>
        /// <returns>
        /// Список объектов типа <typeparamref name="T"/>, созданных на основании строк,
        /// полученных из результата SQL-запроса.
        /// </returns>
        /// <remarks>
        /// Метод формирует SQL-запрос автоматически:
        /// <list type="bullet">
        /// <item><description>Создаёт базовый SELECT через <see cref="SqlQueryBuilder.GetSelectQuery{T}"/>.</description></item>
        /// <item><description>Добавляет условие WHERE, построенное на основе <paramref name="whereExpression"/>.</description></item>
        /// </list>
        /// После формирования запроса управление передаётся основной реализации метода <c>ToList</c>,
        /// работающей с параметрами и сопоставлением колонок.
        /// </remarks>
        public List<T> ToList<T>(Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression) + " " + SqlQueryBuilder.GetOrderBy(orderByExpression)).Trim();

            return ToList(query, null, (IEnumerable<(string, string)>)null, converter, setter, maxRows);
        }

        /// <summary>
        /// Выполняет SQL-запрос и преобразует результат выборки в список объектов
        /// типа <typeparamref name="T"/>, используя универсальный механизм маппинга
        /// через метод <see cref="ToCollection{TList, TItem}"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объектов, создаваемых на основе строк результата запроса.
        /// Должен быть ссылочным типом с публичным конструктором без параметров.
        /// </typeparam>
        /// <param name="con">
        /// Подключение к базе данных <see cref="IDbConnection"/>.
        /// Метод сам открывает и закрывает соединение при необходимости.
        /// </param>
        /// <param name="query">
        /// SQL-запрос для выборки данных. Если значение <c>null</c>,
        /// то запрос формируется автоматически через <c>SqlQueryBuilder.GetSelectQuery&lt;T&gt;()</c>.
        /// </param>
        /// <param name="cmdParams">
        /// Параметры SQL-запроса в виде коллекции кортежей <c>(string name, object value)</c>.
        /// Может быть <c>null</c>, если параметры отсутствуют.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Набор правил сопоставления колонок результата со свойствами типа <typeparamref name="T"/>:
        /// <c>(string columnName, string propertyName)</c>.
        /// Если <c>null</c>, используется автомаппинг по совпадению имён.
        /// </param>
        /// <param name="converter">
        /// Пользовательская функция преобразования значений полей в типы свойств.
        /// Если не указано, применяется стандартный <c>DefaultConverter</c>.
        /// </param>
        /// <param name="setter">
        /// Пользовательская логика присвоения значения свойству.
        /// Если не указано — используется <c>prop.SetValue(item, value)</c>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <returns>
        /// Список объектов типа <typeparamref name="T"/>, созданных на основе результата SQL-запроса.
        /// </returns>
        /// <remarks>
        /// Этот метод является удобной обёрткой вокруг универсального метода
        /// <see cref="ToCollection{TList, TItem}"/>, который содержит полную реализацию механизма:
        /// <list type="bullet">
        /// <item>открытие соединения;</item>
        /// <item>выполнение SQL-запроса;</item>
        /// <item>чтение данных через <see cref="IDataReader"/>;</item>
        /// <item>маппинг значений колонок на свойства объекта;</item>
        /// <item>применение пользовательского конвертера и setter'а.</item>
        /// </list>
        /// </remarks>
        public List<T> ToList<T>(string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1) where T : class, new()
        {
            return ToCollection<List<T>, T>(query, cmdParams, columnToPropertyMap, converter, setter, maxRows);
        }

        /// <summary>
        /// Асинхронно преобразует результат выполнения SQL-запроса в список объектов заданного типа <typeparamref name="T"/>,
        /// используя коллекцию параметров в виде <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <typeparam name="T">Тип элементов списка. Должен быть ссылочным типом и иметь публичный конструктор без параметров.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="query">
        /// SQL-запрос для выполнения. Если значение <c>null</c> или пустое, будет автоматически сгенерирован SELECT-запрос для типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров для SQL-запроса в виде <see cref="IEnumerable{KeyValuePair{String, Object}}"/>.
        /// Если параметров нет, можно передать <c>null</c>.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Коллекция сопоставлений между именами столбцов результата SQL-запроса и свойствами объекта <typeparamref name="T"/>.
        /// Формат: <c>Key = имя столбца, Value = имя свойства</c>. Может быть <c>null</c>, если нужно сопоставление по имени.
        /// </param>
        /// <param name="converter">
        /// Функция для преобразования значения столбца в тип свойства. Принимает исходное значение и <see cref="Type"/> целевого свойства, возвращает преобразованное значение.
        /// Если <c>null</c>, используется стандартный <see cref="DbReaderValueConvertor"/>.
        /// </param>
        /// <param name="setter">
        /// Действие для установки значения свойства объекта. Принимает:
        /// <list type="bullet">
        /// <item><description>имя столбца</description></item>
        /// <item><description>значение столбца после конвертации</description></item>
        /// <item><description>информацию о свойстве <see cref="TypeCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="TypeCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/> для асинхронной операции.</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод является перегрузкой метода <see cref="ToListAsync{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T}, CancellationToken)"/>,
        /// преобразующей <see cref="KeyValuePair{String, Object}"/> параметры в кортежи <c>(string, object)</c> перед вызовом основной реализации.
        /// </remarks>
        public Task<List<T>> ToListAsync<T>(string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1, CancellationToken ct = default) where T : class, new()
        {
            return ToListAsync(query, cmdParams?.Select(x => (x.Key, x.Value)), columnToPropertyMap?.Select(x => (x.Key, x.Value)), converter, setter, maxRows, ct);
        }

        /// <summary>
        /// Асинхронно преобразует результат выполнения SQL-запроса в список объектов заданного типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип элементов списка. Должен быть ссылочным типом и иметь публичный конструктор без параметров.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="query">
        /// SQL-запрос для выполнения. Если значение <c>null</c> или пустое, будет автоматически сгенерирован SELECT-запрос для типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="cmdParams">
        /// Коллекция параметров запроса в виде кортежей <c>(имя параметра, значение)</c>. Может быть <c>null</c>, если параметры не требуются.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Коллекция сопоставлений между именами столбцов результата SQL-запроса и свойствами объекта <typeparamref name="T"/>.
        /// Формат: <c>(имя столбца, имя свойства)</c>. Если <c>null</c>, используется автоматическое сопоставление по именам.
        /// </param>
        /// <param name="converter">
        /// Функция для преобразования значения столбца в тип свойства. Принимает исходное значение и <see cref="Type"/> целевого свойства, возвращает преобразованное значение.
        /// Если <c>null</c>, используется <see cref="DbReaderValueConvertor"/>.
        /// </param>
        /// <param name="setter">
        /// Действие для установки значения свойства объекта. Принимает:
        /// <list type="bullet">
        /// <item><description>имя столбца</description></item>
        /// <item><description>значение столбца после конвертации</description></item>
        /// <item><description>информацию о свойстве <see cref="TypeCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="TypeCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/> для асинхронной операции.</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Внутри используется метод <see cref="ToCollectionAsync{TList, TItem}"/> для выполнения SQL-запроса и построения коллекции.
        /// Каждая строка результата запроса преобразуется в объект <typeparamref name="T"/> с заполнением всех свойств.
        /// </remarks>
        public Task<List<T>> ToListAsync<T>(string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1, CancellationToken ct = default) where T : class, new()
        {
            return ToCollectionAsync<List<T>, T>(query, cmdParams, columnToPropertyMap, converter, setter, maxRows, ct);
        }

        /// <summary>
        /// Асинхронно получает список объектов <typeparamref name="T"/>, фильтруя строки с помощью выражения <paramref name="whereExpression"/>.
        /// </summary>
        /// <typeparam name="T">Тип элементов списка. Должен быть ссылочным типом и иметь публичный конструктор без параметров.</typeparam>
        /// <param name="con">Объект подключения к базе данных <see cref="IDbConnection"/>. Метод автоматически открывает и закрывает соединение.</param>
        /// <param name="whereExpression">
        /// Выражение <see cref="Expression{Func{T, bool}}"/>, задающее условие фильтрации данных (формирует SQL WHERE-клауза).
        /// </param>
        /// <param name="converter">
        /// Функция для преобразования значения столбца в тип свойства. Принимает исходное значение и <see cref="Type"/> целевого свойства, возвращает преобразованное значение.
        /// Если <c>null</c>, используется стандартный <see cref="DbReaderValueConvertor"/>.
        /// </param>
        /// <param name="setter">
        /// Действие для установки значения свойства объекта. Принимает:
        /// <list type="bullet">
        /// <item><description>имя столбца</description></item>
        /// <item><description>значение столбца после конвертации</description></item>
        /// <item><description>информацию о свойстве <see cref="TypeCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="TypeCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="orderByExpression">Порядок сортировки</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Метод генерирует SQL-запрос SELECT с WHERE-клауза на основе <paramref name="whereExpression"/>.
        /// Использует перегрузку <see cref="ToListAsync{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T}, CancellationToken)"/> для выполнения запроса и построения коллекции.
        /// </remarks>
        public Task<List<T>> ToListAsync<T>(Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, TypeCache, T> setter = null, int maxRows = -1, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression) + " " + SqlQueryBuilder.GetOrderBy(orderByExpression)).Trim();

            return ToListAsync(query, null, (IEnumerable<(string, string)>)null, converter, setter, maxRows);
        }

        /// <summary>
        /// Синхронно обновляет один объект, используя указанный набор обновляемых колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="con">Подключение к БД.</param>
        /// <param name="item">Обновляемая сущность.</param>
        /// <param name="updateColumns">Массив обновляемых свойств.</param>
        public int Update<T>(T item, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return Update(item, null, updateColumns);
        }

        /// <summary>
        /// Синхронное обновление одного объекта с возможностью задать выражение WHERE.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="item">Объект для обновления.</param>
        /// <param name="whereExpression">
        /// Условие WHERE, формируемое через лямбда-выражение.
        /// Если отсутствует, WHERE строится по ключевым свойствам сущности.
        /// </param>
        /// <param name="updateColumns">Выражения, определяющие обновляемые колонки.</param>
        public int Update<T>(T item, Expression<Func<T, bool>> whereExpression, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
            query += " " + (whereExpression != null ? SqlQueryBuilder.GetWhereClause(whereExpression)
                                                    : SqlQueryBuilder.GetWhereClause<T>());

            return ExecuteNonQuery(query, GetParams(item));
        }

        /// <summary>
        /// Асинхронно обновляет один объект в базе данных,
        /// используя указанные выражения для выбора обновляемых колонок.
        /// </summary>
        /// <typeparam name="T">Тип обновляемой сущности.</typeparam>
        /// <param name="con">Активное подключение к базе данных.</param>
        /// <param name="item">Экземпляр сущности, значения которого должны быть обновлены.</param>
        /// <param name="updateColumns">
        /// Выражения, определяющие список обновляемых колонок.
        /// Если передано <c>null</c> или массив пуст — обновляются все публичные простые свойства.
        /// </param>
        /// <returns>Объект задачи, представляющий асинхронное выполнение UPDATE.</returns>
        public Task<int> UpdateAsync<T>(T item, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return UpdateAsync(item, null, updateColumns);
        }

        /// <summary>
        /// Асинхронно обновляет один объект с возможностью указания выражения WHERE.
        /// </summary>
        /// <typeparam name="T">Тип обновляемой сущности.</typeparam>
        /// <param name="con">Активное подключение к базе данных.</param>
        /// <param name="item">Объект, данные которого должны быть обновлены.</param>
        /// <param name="whereExpression">
        /// Лямбда-выражение для формирования условия WHERE.
        /// Если <c>null</c>, WHERE формируется на основе ключевых полей сущности (если предусмотрено).</param>
        /// <param name="updateColumns">Список обновляемых колонок.</param>
        /// <returns>Задача, представляющая выполнение команды UPDATE.</returns>
        public Task<int> UpdateAsync<T>(T item, Expression<Func<T, bool>> whereExpression, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
            query += " " + (whereExpression != null ? SqlQueryBuilder.GetWhereClause(whereExpression)
                                                    : SqlQueryBuilder.GetWhereClause<T>());

            return ExecuteNonQueryAsync(query, GetParams(item));
        }

        /// <summary>
        /// Выполняет обновление набора объектов с возможностью указания условия WHERE.
        /// </summary>
        /// <typeparam name="T">Тип обновляемой сущности.</typeparam>
        /// <param name="con">Активное подключение.</param>
        /// <param name="list">Коллекция сущностей для обновления.</param>
        /// <param name="updateColumns">Обновляемые колонки.</param>
        public int UpdateRange<T>(IEnumerable<T> list, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            try
            {
                var count = 0;
                using (StartTransaction())
                {
                    foreach (var item in list)
                    {
                        count += Update(item, null, updateColumns);
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Асинхронно выполняет обновление набора объектов в базе данных с возможностью указать условие WHERE.
        /// </summary>
        /// <typeparam name="T">Тип сущности, данные которой обновляются.</typeparam>
        /// <param name="con">Активное подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей, значения которых должны быть обновлены.</param>
        /// <param name="updateColumns">
        /// Массив выражений <c>x =&gt; x.Property</c>, задающий список обновляемых колонок.
        /// </param>
        /// <returns>Объект задачи, представляющий асинхронную операцию обновления.</returns>
        public async Task<int> UpdateRangeAsync<T>(IEnumerable<T> list, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            try
            {
                var count = 0;
                using (StartTransaction())
                {
                    foreach (var item in list)
                    {
                        count += await UpdateAsync(item, null, updateColumns);
                    }

                    EndTransaction();
                }
                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                CloseConnection();
                Connection?.Dispose();
                _disposed = true;
            }
        }

        private void BeginConnection()
        {
            BeginConnection(Connection);
        }

        private void BeginConnection(IDbConnection con)
        {
            if (con == null)
                throw new NullReferenceException(nameof(con));

            if (con.State != ConnectionState.Open)
                con.Open();
        }

        private Task BeginConnectionAsync()
        {
            return BeginConnectionAsync(Connection);
        }

        private async Task BeginConnectionAsync(IDbConnection con)
        {
            if (!(con is DbConnection dbcon))
                throw new NullReferenceException(nameof(con));

            if (con.State != ConnectionState.Open)
                await dbcon.OpenAsync();
        }

        /// <summary>
        /// Универсальный преобразователь типа, поддерживающий Guid, Enum, Nullable,
        /// строки-даты и примитивы.
        /// Используется во всех методах маппинга и чтения значений.
        /// </summary>
        /// <param name="value">Исходное значение.</param>
        /// <param name="targetType">Тип назначения.</param>
        /// <returns>Сконвертированное значение.</returns>
        private object ChangeType(object value, Type targetType) => TypeHelper.ChangeType(value, targetType);

        private void CloseConnection()
        {
            CloseConnection(Connection);
        }

        private void CloseConnection(IDbConnection con)
        {
            if (_tr.Value != null)
                return;

            if (con == null)
                throw new NullReferenceException(nameof(con));

            if (con.State != ConnectionState.Closed)
                con.Close();
        }

        private Dictionary<int, string> GetReaderFieldToPropertyMap(IDataReader reader, IEnumerable<(string, string)> customMap = null, bool onlyFromCustomMap = true)
        {
            var customMapDic = customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            var map = new Dictionary<int, string>();

            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);

                if (customMapDic.Count > 0 && customMapDic.TryGetValue(colName, out var mappedColumn))
                {
                    map[colIndex] = mappedColumn;
                    if (onlyFromCustomMap)
                        continue;
                }

                map[colIndex] = colName;
            }

            return map;
        }

        private Dictionary<int, (TypeCache propInfoEx, Action<T, object> propSetter)> GetReaderFieldToPropertyMap<T>(IDataReader reader, IEnumerable<(string, string)> customMap = null, bool onlyFromCustomMap = true)
        {
            var customMapDic = customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            var map = new Dictionary<int, (TypeCache propInfoEx, Action<T, object> propSetter)>();
            var typeInfoEx = TypeCache.Create(typeof(T));
            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);
                TypeCache propInfoEx;
                if (customMap != null)
                {
                    propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(customMapDic.GetValueOrDefault(colName, IgnoreCaseComparer), IgnoreCaseComparer);
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    if (map[colIndex].propInfoEx != null || onlyFromCustomMap)
                        continue;
                }

                propInfoEx = typeInfoEx.ColumnProperties.GetValueOrDefault(colName, IgnoreCaseComparer);
                if (propInfoEx != null)
                {
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    continue;
                }

                propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(colName, IgnoreCaseComparer);

                if (propInfoEx != null)
                {
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    continue;
                }

                map.Remove(colIndex);
            }

            return map;
        }

        private InvalidOperationException HandleDbException(Exception ex, IDbCommand cmd, [CallerMemberName] string methodName = "")
        {
            var errorMessage = $"Ошибка в методе {methodName}. " +
                               $"Запрос: {cmd?.CommandText}. " +
                               $"Параметры: {string.Join(", ", cmd == null ? Array.Empty<string>() : cmd.Parameters.Cast<IDbDataParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}";

            return new InvalidOperationException(errorMessage, ex);
        }

        [Conditional("DEBUG")]
        private void LogCommand(IDbCommand cmd)
        {
            Debug.WriteLine($"Executing SQL: {cmd.CommandText}");
            foreach (IDbDataParameter p in cmd.Parameters)
            {
                Debug.WriteLine($"  {p.ParameterName} = {p.Value}");
            }
        }

        private void RollbackTransaction()
        {
            if (_tr.Value == null)
                throw new InvalidOperationException("Транзакция не была начата.");

            _tr.Value?.Rollback();
        }
    }
}