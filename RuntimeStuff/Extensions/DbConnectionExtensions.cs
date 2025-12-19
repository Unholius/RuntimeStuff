using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Набор расширений для <see cref="IDbConnection"/>, упрощающих выполнение команд,
    /// получение скалярных значений, списков, словарей и автоматическое маппирование данных.
    /// </summary>
    public static class DbConnectionExtensions
    {

        /// <summary>
        /// Открывает соединение, если оно ещё не открыто.
        /// При состоянии Broken соединение сначала закрывается и открывается заново.
        /// </summary>
        /// <param name="connection">Соединение с базой данных.</param>
        /// <returns>Открытое соединение.</returns>
        /// <exception cref="ArgumentNullException">Если соединение равно null.</exception>
        /// <exception cref="InvalidOperationException">Если соединение не может быть открыто.</exception>
        public static IDbConnection OpenConnection(this IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

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

                return connection;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="con">Экземпляр соединения с БД.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Параметры команды в формате (имя, значение).</param>
        /// <returns>Готовая команда <see cref="IDbCommand"/>.</returns>
        public static IDbCommand CreateCommand(this IDbConnection con, string query, IEnumerable<(string, object)> cmdParams = null) => DbClient.Create(con).CreateCommand(query, cmdParams);

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="con">Экземпляр соединения с БД.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Параметры команды в формате (имя, значение).</param>
        /// <returns>Готовая команда <see cref="IDbCommand"/>.</returns>
        public static IDbCommand CreateCommand(this IDbConnection con, string query, params (string, object)[] cmdParams) => DbClient.Create(con).CreateCommand(query, cmdParams);

        /// <summary>
        /// Создаёт команду для SQL-запроса, принимая параметры в виде словаря.
        /// </summary>
        /// <param name="con">Экземпляр соединения.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Команда <see cref="IDbCommand"/>.</returns>
        public static IDbCommand CreateCommand(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams = null) => DbClient.Create(con).CreateCommand(query, cmdParams);

        /// <summary>
        /// Выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием коллекции параметров в виде <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <param name="con">Открытое или закрытое подключение к базе данных. Если подключение закрыто — будет открыто автоматически.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Коллекция параметров SQL-команды.</param>
        /// <returns>Количество затронутых строк.</returns>
        public static int ExecuteNonQuery(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams) => DbClient.Create(con).ExecuteNonQuery(query, cmdParams);

        /// <summary>
        /// Выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием массива параметров.
        /// </summary>
        /// <param name="con">Открытое или закрытое подключение к базе данных. Если подключение закрыто — будет открыто автоматически.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Массив параметров SQL-команды в формате (имя, значение).</param>
        /// <returns>Количество затронутых строк.</returns>
        public static int ExecuteNonQuery(this IDbConnection con, string query, params (string, object)[] cmdParams) => DbClient.Create(con).ExecuteNonQuery(query, cmdParams);

        /// <summary>
        /// Асинхронно выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием коллекции параметров в виде <see cref="KeyValuePair{String, Object}"/>.
        /// </summary>
        /// <param name="con">Подключение к базе данных. Если оно закрыто, будет автоматически открыто.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Коллекция параметров SQL-команды.</param>
        /// <returns>Задача, возвращающая количество затронутых строк.</returns>
        public static Task<int> ExecuteNonQueryAsync(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken token = default) => DbClient.Create(con).ExecuteNonQueryAsync(query, cmdParams, token);

        /// <summary>
        /// Асинхронно выполняет SQL-команду, не возвращающую результирующий набор (INSERT, UPDATE, DELETE),
        /// с использованием массива параметров.
        /// </summary>
        /// <param name="con">Подключение к базе данных. Если оно закрыто, будет автоматически открыто.</param>
        /// <param name="query">Текст SQL-команды.</param>
        /// <param name="cmdParams">Массив параметров SQL-команды в формате (имя, значение).</param>
        /// <returns>Задача, возвращающая количество затронутых строк.</returns>
        public static Task<int> ExecuteNonQueryAsync(this IDbConnection con, string query, (string, object)[] cmdParams, CancellationToken token = default) => DbClient.Create(con).ExecuteNonQueryAsync(query, cmdParams, token);

        /// <summary>
        /// Выполняет SQL-запрос и возвращает одно скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение с БД.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public static T ExecuteScalar<T>(this IDbConnection con, string query, params (string, object)[] cmdParams) => DbClient.Create(con).ExecuteScalar<T>(query, cmdParams);

        /// <summary>
        /// Выполняет SQL-запрос и возвращает скалярное значение, принимая параметры в виде словаря.
        /// </summary>
        public static T ExecuteScalar<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression) => DbClient.Create(con).ExecuteScalar(whereExpression);

        /// <summary>
        /// Выполняет SQL-запрос и возвращает скалярное значение, принимая параметры в виде словаря.
        /// </summary>
        public static T ExecuteScalar<T>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams) => DbClient.Create(con).ExecuteScalar<T>(query, cmdParams);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken ct = default)
            => DbClient.Create(con).ExecuteScalarAsync<T>(query, cmdParams, ct);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает скалярное значение.
        /// </summary>
        /// <typeparam name="T">Тип результата.</typeparam>
        /// <param name="con">Соединение.</param>
        /// <param name="query">SQL-запрос.</param>
        /// <param name="cmdParams">Параметры команды.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Значение типа <typeparamref name="T"/>.</returns>
        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection con, string query, IEnumerable<(string, object)> cmdParams = null, CancellationToken ct = default)
            => DbClient.Create(con).ExecuteScalarAsync<T>(query, cmdParams, ct);

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
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams = null)
            => DbClient.Create(con).ToDictionary<TKey, TValue>(query, cmdParams);

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
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue, T>(this IDbConnection con, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null)
            => DbClient.Create(con).ToDictionary(keySelector, valueSelector, whereExpression);

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
        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(this IDbConnection con, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null, CancellationToken token = default)
            => DbClient.Create(con).ToDictionaryAsync(keySelector, valueSelector, whereExpression, token);

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
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection con, string query, params (string, object)[] cmdParams)
            => DbClient.Create(con).ToDictionary<TKey, TValue>(query, cmdParams);

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
        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken ct = default)
            => DbClient.Create(con).ToDictionaryAsync<TKey, TValue>(query, cmdParams, ct);

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
        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection con, string query, IEnumerable<(string, object)> cmdParams = null, CancellationToken ct = default)
            => DbClient.Create(con).ToDictionaryAsync<TKey, TValue>(query, cmdParams, ct);

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
        public static List<T> ToList<T>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1) where T : class, new()
            => DbClient.Create(con).ToList(query, cmdParams, columnToPropertyMap, converter, setter, maxRows);

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
        public static List<T> ToList<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
            => DbClient.Create(con).ToList(whereExpression, converter, setter, maxRows, orderByExpression);

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
        public static List<T> ToList<T>(this IDbConnection con, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1) where T : class, new()
            => DbClient.Create(con).ToList(query, cmdParams, columnToPropertyMap, converter, setter, maxRows);

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
        /// <item><description>Информацию о свойстве <see cref="MemberCache"/></description></item>
        /// <item><description>Объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="MemberCache.SetValue"/>.
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
        public static TList ToCollection<TList, TItem>(this IDbConnection con, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, TItem> setter = null, int maxRows = -1) where TList : ICollection<TItem>, new() where TItem : class, new()
            => DbClient.Create(con).ToCollection<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, setter, maxRows);

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
        public static DataTable ToDataTable(this IDbConnection con, string query, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
            => DbClient.Create(con).ToDataTable(query, cmdParams, columnMap, maxRows);

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
        public static Task<DataTable> ToDataTableAsync(this IDbConnection con, string query, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnMap = null, int maxRows = -1, CancellationToken token = default)
            => DbClient.Create(con).ToDataTableAsync(query, cmdParams, columnMap, maxRows, token);

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
        /// <item><description>информацию о свойстве <see cref="MemberCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="MemberCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/> для асинхронной операции.</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод является перегрузкой метода <see cref="ToListAsync{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T}, CancellationToken)"/>,
        /// преобразующей <see cref="KeyValuePair{String, Object}"/> параметры в кортежи <c>(string, object)</c> перед вызовом основной реализации.
        /// </remarks>
        public static Task<List<T>> ToListAsync<T>(this IDbConnection con, string query = null, IEnumerable<KeyValuePair<string, object>> cmdParams = null, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1, CancellationToken ct = default) where T : class, new()
            => DbClient.Create(con).ToListAsync(query, cmdParams, columnToPropertyMap, converter, setter, maxRows, ct);

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
        /// <item><description>информацию о свойстве <see cref="MemberCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="MemberCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="ct">Токен отмены <see cref="CancellationToken"/> для асинхронной операции.</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Внутри используется метод <see cref="ToCollectionAsync{TList, TItem}"/> для выполнения SQL-запроса и построения коллекции.
        /// Каждая строка результата запроса преобразуется в объект <typeparamref name="T"/> с заполнением всех свойств.
        /// </remarks>
        public static Task<List<T>> ToListAsync<T>(this IDbConnection con, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1, CancellationToken ct = default) where T : class, new()
            => DbClient.Create(con).ToListAsync(query, cmdParams, columnToPropertyMap, converter, setter, maxRows, ct);

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
        /// <item><description>информацию о свойстве <see cref="MemberCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="MemberCache.SetValue"/>.
        /// </param>
        /// <param name="maxRows">Максимальное количество строк для возврата, -1 - все</param>
        /// <param name="orderByExpression">Порядок сортировки</param>
        /// <returns>Задача <see cref="Task"/> с результатом в виде списка <see cref="List{T}"/> объектов <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Метод генерирует SQL-запрос SELECT с WHERE-клауза на основе <paramref name="whereExpression"/>.
        /// Использует перегрузку <see cref="ToListAsync{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T}, CancellationToken)"/> для выполнения запроса и построения коллекции.
        /// </remarks>
        public static Task<List<T>> ToListAsync<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, int maxRows = -1, (Expression<Func<T, object>>, bool)[] orderByExpression = null, CancellationToken token = default) where T : class, new()
            => DbClient.Create(con).ToListAsync(whereExpression, converter, setter, maxRows, orderByExpression ?? Array.Empty<(Expression<Func<T, object>>, bool)>(), token);

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
        public static T First<T>(this IDbConnection con, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, MemberCache, T> setter = null) where T : class, new()
            => DbClient.Create(con).First(query, cmdParams, columnToPropertyMap, converter, setter);

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
        public static T First<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null, params (Expression<Func<T, object>>, bool)[] orderByExpression) where T : class, new()
            => DbClient.Create(con).First(whereExpression, converter, setter, orderByExpression);

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
        public static T First<T>(this IDbConnection con, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            Func<object, Type, object> converter = null, Action<string, object, MemberCache, T> setter = null)
            where T : class, new()
            => DbClient.Create(con).First(query, cmdParams, columnToPropertyMap, converter, setter);

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
        public static Task<T> FirstAsync<T>(this IDbConnection con, string query,
            IEnumerable<KeyValuePair<string, object>> cmdParams,
            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, MemberCache, T> setter = null, CancellationToken token = default) where T : class, new()
            => DbClient.Create(con).FirstAsync(query, cmdParams, columnToPropertyMap, converter, setter, token);

        /// <summary>
        /// Асинхронная версия метода <see cref="First{T}(IDbConnection, Expression{Func{T, bool}}, Func{object, Type, object}, Action{string, object, MemberCache, T})"/>.
        /// </summary>
        public static Task<T> FirstAsync<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression, Func<object, Type, object> converter = null,
            Action<string, object, MemberCache, T> setter = null, (Expression<Func<T, object>>, bool)[] orderByExpression = null, CancellationToken token = default) where T : class, new()
            => DbClient.Create(con).FirstAsync(whereExpression, converter, setter, orderByExpression, token);

        /// <summary>
        /// Асинхронная версия метода <see cref="First{T}(IDbConnection, string, IEnumerable{(string, object)}, IEnumerable{(string, string)}, Func{object, Type, object}, Action{string, object, TypeCache, T})"/>.
        /// </summary>
        public static Task<T> FirstAsync<T>(this IDbConnection con, string query = null,
            IEnumerable<(string, object)> cmdParams = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            Func<object, Type, object> converter = null,
            Action<string, object, MemberCache, T> setter = null, CancellationToken token = default) where T : class, new()
            => DbClient.Create(con).FirstAsync(query, cmdParams, columnToPropertyMap, converter, setter, token);

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
        /// <item><description>информацию о свойстве <see cref="MemberCache"/></description></item>
        /// <item><description>объект, в который нужно установить значение</description></item>
        /// </list>
        /// Если <c>null</c>, используется стандартный setter, который вызывает <see cref="MemberCache.SetValue"/>.
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
        public static Task<TList> ToCollectionAsync<TList, TItem>(this IDbConnection con, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, Func<object, Type, object> converter = null, Action<string, object, MemberCache, TItem> setter = null, int maxRows = -1, CancellationToken ct = default) where TList : ICollection<TItem>, new() where TItem : class, new()
            => DbClient.Create(con).ToCollectionAsync<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, setter, maxRows, ct);

        /// <summary>
        /// Асинхронно выполняет обновление набора объектов в базе данных с возможностью указать условие WHERE.
        /// </summary>
        /// <typeparam name="T">Тип сущности, данные которой обновляются.</typeparam>
        /// <param name="con">Активное подключение к базе данных.</param>
        /// <param name="list">Коллекция сущностей, значения которых должны быть обновлены.</param>
        /// <param name="whereExpression">
        /// Лямбда-выражение, формирующее условие WHERE.
        /// Если значение <c>null</c>, WHERE формируется по ключевым свойствам (если реализовано в <c>SqlQueryBuilder</c>).
        /// </param>
        /// <param name="updateColumns">
        /// Массив выражений <c>x =&gt; x.Property</c>, задающий список обновляемых колонок.
        /// </param>
        /// <returns>Объект задачи, представляющий асинхронную операцию обновления.</returns>
        public static Task<int> UpdateRangeAsync<T>(this IDbConnection con, IEnumerable<T> list, Expression<Func<T, object>>[] updateColumns, CancellationToken token) where T : class
            => DbClient.Create(con).UpdateRangeAsync(list, updateColumns, token);

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
        public static Task<int> UpdateAsync<T>(this IDbConnection con, T item, Expression<Func<T, object>>[] updateColumns = null, CancellationToken token = default) where T : class
            => DbClient.Create(con).UpdateAsync(item, updateColumns, token);

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
        public static Task<int> UpdateAsync<T>(this IDbConnection con, T item, Expression<Func<T, bool>> whereExpression, Expression<Func<T, object>>[] updateColumns = null, CancellationToken token = default) where T : class
            => DbClient.Create(con).UpdateAsync(item, whereExpression, updateColumns, token);

        /// <summary>
        /// Выполняет обновление набора объектов с возможностью указания условия WHERE.
        /// </summary>
        /// <typeparam name="T">Тип обновляемой сущности.</typeparam>
        /// <param name="con">Активное подключение.</param>
        /// <param name="list">Коллекция сущностей для обновления.</param>
        /// <param name="updateColumns">Обновляемые колонки.</param>
        public static int UpdateRange<T>(this IDbConnection con, IEnumerable<T> list, params Expression<Func<T, object>>[] updateColumns) where T : class
            => DbClient.Create(con).UpdateRange(list, updateColumns);

        /// <summary>
        /// Синхронно обновляет один объект, используя указанный набор обновляемых колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="con">Подключение к БД.</param>
        /// <param name="item">Обновляемая сущность.</param>
        /// <param name="updateColumns">Массив обновляемых свойств.</param>
        public static int Update<T>(this IDbConnection con, T item, params Expression<Func<T, object>>[] updateColumns) where T : class
            => DbClient.Create(con).Update(item, updateColumns);

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
        public static int Update<T>(this IDbConnection con, T item, Expression<Func<T, bool>> whereExpression, params Expression<Func<T, object>>[] updateColumns) where T : class
            => DbClient.Create(con).Update(item, updateColumns);

        /// <summary>
        /// Создает новый объект типа <typeparamref name="T"/>, заполняет его с помощью переданных действий
        /// и выполняет INSERT в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности, которая вставляется в базу данных.</typeparam>
        /// <param name="con">Подключение к базе данных. Если закрыто — будет автоматически открыто.</param>
        /// <param name="insertColumns">Делегаты, которые заполняют свойства создаваемого объекта.</param>
        /// <returns>Созданный и вставленный объект.</returns>
        public static T Insert<T>(this IDbConnection con, params Action<T>[] insertColumns) where T : class
            => DbClient.Create(con).Insert(insertColumns);

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
        public static Task<object> InsertAsync<T>(this IDbConnection con, string queryGetId = "SELECT SCOPE_IDENTITY()", Action<T>[] insertColumns = null, CancellationToken token = default) where T : class
            => DbClient.Create(con).InsertAsync(queryGetId, insertColumns, token);

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
        public static object Insert<T>(this IDbConnection con, T item, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
            => DbClient.Create(con).Insert(item, queryGetId, insertColumns);

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
        public static Task<object> InsertAsync<T>(this IDbConnection con, T item, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, CancellationToken token = default) where T : class
            => DbClient.Create(con).InsertAsync(item, queryGetId, insertColumns, token);

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
        public static int InsertRange<T>(this IDbConnection con, IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", params Expression<Func<T, object>>[] insertColumns) where T : class
            => DbClient.Create(con).InsertRange(list, queryGetId, insertColumns);

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
        public static Task<int> InsertRangeAsync<T>(this IDbConnection con, IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, CancellationToken token = default) where T : class
            => DbClient.Create(con).InsertRangeAsync(list, queryGetId, insertColumns, token);

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
        public static int Delete<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression) where T : class
            => DbClient.Create(con).Delete(whereExpression);

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
        public static int Delete<T>(this IDbConnection con, T item) where T : class
            => DbClient.Create(con).Delete(item);

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
        public static Task<int> DeleteAsync<T>(this IDbConnection con, T item, CancellationToken token = default) where T : class
            => DbClient.Create(con).DeleteAsync(item, token);

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
        public static Task<int> DeleteRangeAsync<T>(this IDbConnection con, IEnumerable<T> list, CancellationToken token = default) where T : class
            => DbClient.Create(con).DeleteRangeAsync(list, token);

        /// <summary>
        /// Асинхронно выполняет удаление записей из таблицы, соответствующей типу <typeparamref name="T"/>,
        /// используя условие, заданное выражением <paramref name="whereExpression"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, таблица которой используется в запросе.</typeparam>
        /// <param name="con">Подключение к базе данных.</param>
        /// <param name="whereExpression">Lambda-выражение, определяющее условие WHERE. Если null, то удалятся ВСЕ записи!</param>
        /// <returns>Задача, представляющая асинхронную операцию удаления.</returns>
        /// <remarks>
        /// Формируемый SQL-запрос аналогичен синхронной версии:
        /// <code>
        /// DELETE FROM [Table] WHERE ...
        /// </code>
        /// </remarks>
        public static Task<int> DeleteAsync<T>(this IDbConnection con, Expression<Func<T, bool>> whereExpression, CancellationToken token = default) where T : class
            => DbClient.Create(con).DeleteAsync(whereExpression, token);
    }
}