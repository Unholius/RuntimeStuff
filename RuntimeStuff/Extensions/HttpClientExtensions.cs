// <copyright file="HttpClientExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>
    /// Класс расширений для <see cref="HttpClient"/>, предоставляющий удобные методы для выполнения HTTP-запросов.
    /// Поддерживает GET, POST, PUT, DELETE методы, параметры запроса, различные типы содержимого и авторизацию.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Выполняет асинхронный HTTP DELETE запрос.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <returns>Тело ответа в виде строки.</returns>
        public static async Task<string> DeleteAsync(this HttpClient client, string requestUri, Dictionary<string, object> query = null)
        {
            return (await SendAsync(client, HttpMethod.Delete, requestUri, query)).Result;
        }

        /// <summary>
        /// Выполняет асинхронный HTTP GET запрос.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <returns>Тело ответа в виде строки.</returns>
        public static async Task<string> GetAsync(this HttpClient client, string requestUri, Dictionary<string, object> query = null)
        {
            return (await SendAsync(client, HttpMethod.Get, requestUri, query)).Result;
        }

        /// <summary>
        /// Выполняет асинхронный HTTP POST запрос.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="content">Тело запроса. Может быть строкой, словарем или объектом.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <param name="dateFormat">Формат даты для сериализации JSON (если content является объектом).</param>
        /// <returns>Тело ответа в виде строки.</returns>
        public static async Task<string> PostAsync(this HttpClient client, string requestUri, object content = null, Dictionary<string, object> query = null, string dateFormat = null)
        {
            return (await SendAsync(client, HttpMethod.Post, requestUri, query, content, dateFormat)).Result;
        }

        /// <summary>
        /// Выполняет асинхронный HTTP PUT запрос.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="content">Тело запроса. Может быть строкой, словарем или объектом.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <param name="dateFormat">Формат даты для сериализации JSON (если content является объектом).</param>
        /// <returns>Тело ответа в виде строки.</returns>
        public static async Task<string> PutAsync(this HttpClient client, string requestUri, object content = null, Dictionary<string, object> query = null, string dateFormat = null)
        {
            return (await SendAsync(client, HttpMethod.Put, requestUri, query, content, dateFormat)).Result;
        }

        /// <summary>
        /// Выполняет асинхронный HTTP запрос с указанным методом.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="method">HTTP метод (GET, POST, PUT, DELETE).</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <param name="content">Тело запроса. Может быть строкой, словарем или объектом.</param>
        /// <param name="dateFormat">Формат даты для сериализации JSON (если content является объектом).</param>
        /// <param name="enumAsStrings">Сериализовать enum как строки, иначе как числа.</param>
        /// <param name="ensureSuccessStatusCode">Выбрасывать исключение, если ответ не OK.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Тело ответа в виде строки.</returns>
        /// <remarks>
        /// Автоматически определяет тип содержимого: - Строка: если начинается с '{' или '[', то 'application/json',
        /// иначе 'text/plain' - Словарь: 'application/x-www-form-urlencoded' - Объект: сериализуется в JSON с
        /// 'application/json'.
        /// </remarks>
        public static Task<HttpResponse> SendAsync(
            this HttpClient client,
            HttpMethod method,
            string requestUri,
            Dictionary<string, object> query = null,
            object content = null,
            string dateFormat = null,
            bool enumAsStrings = false,
            bool ensureSuccessStatusCode = false,
            CancellationToken token = default)
        {
            var typeFormats = new Dictionary<Type, string>();
            if (dateFormat != null)
            {
                typeFormats[typeof(DateTime)] = dateFormat;
            }

            return SendAsync(client, method, requestUri, content, query, typeFormats, enumAsStrings, ensureSuccessStatusCode);
        }

        /// <summary>
        /// Выполняет асинхронный HTTP запрос с указанным методом.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="method">HTTP метод (GET, POST, PUT, DELETE).</param>
        /// <param name="requestUri">URI запроса.</param>
        /// <param name="content">Тело запроса. Может быть строкой, словарем или объектом.</param>
        /// <param name="query">Параметры запроса (query string).</param>
        /// <param name="additionalFormats">Дополнительные Форматы типов для сериализации JSON (если content является объектом).</param>
        /// <param name="enumAsStrings">Сериализовать enum как строки, иначе как числа.</param>
        /// <param name="ensureSuccessStatusCode">Выбрасывать исключение, если ответ не OK.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Тело ответа в виде строки.</returns>
        /// <remarks>
        /// Автоматически определяет тип содержимого: - Строка: если начинается с '{' или '[', то 'application/json',
        /// иначе 'text/plain' - Словарь: 'application/x-www-form-urlencoded' - Объект: сериализуется в JSON с
        /// 'application/json'.
        /// </remarks>
        public static async Task<HttpResponse> SendAsync(
            this HttpClient client,
            HttpMethod method,
            string requestUri,
            object content,
            Dictionary<string, object> query,
            Dictionary<Type, string> additionalFormats,
            bool enumAsStrings,
            bool ensureSuccessStatusCode,
            CancellationToken token = default)
        {
            if (query != null && query.Count > 0)
            {
                var queryString = BuildQueryString(query);
                requestUri += requestUri.Contains("?") ? "&" + queryString : "?" + queryString;
            }

            HttpContent httpContent = null;

            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                string body;
                string contentType;

                if (content == null)
                {
                    body = string.Empty;
                    contentType = "text/plain";
                }
                else if (content is string s)
                {
                    body = s;
                    s = s.Trim();
                    contentType = (s.StartsWith("{") || s.StartsWith("[")) ? "application/json" : "text/plain";
                }
                else if (content is Dictionary<string, object> dict)
                {
                    body = BuildQueryString(dict);
                    contentType = "application/x-www-form-urlencoded";
                }
                else
                {
                    body = Helpers.JsonSerializerHelper.Serialize(content, null, enumAsStrings, additionalFormats);
                    contentType = "application/json";
                }

                httpContent = new StringContent(body, Encoding.UTF8, contentType);
            }

            using (var request = new HttpRequestMessage(method, requestUri) { Content = httpContent })
            {
                using (var response = await client.SendAsync(request, token).ConfigureAwait(false))
                {
                    if (ensureSuccessStatusCode)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var httpResponse = new HttpResponse(response, text);

                    return httpResponse;
                }
            }
        }

        /// <summary>
        /// Добавляет заголовок авторизации Bearer к клиенту.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="token">Токен авторизации.</param>
        /// <returns>Тот же экземпляр <see cref="HttpClient"/> для цепочки вызовов.</returns>
        public static HttpClient WithAuth(this HttpClient client, string token)
        {
            if (client.DefaultRequestHeaders.Contains("Authorization"))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>
        /// Устанавливает базовый URL для всех запросов, выполняемых этим клиентом.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="baseUrl">Базовый URL в абсолютном формате (например, "https://api.example.com/v1/").</param>
        /// <returns>Тот же экземпляр <see cref="HttpClient"/> для цепочки вызовов.</returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если <paramref name="baseUrl"/> равен null, пустой строке или состоит только из пробельных символов.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// Выбрасывается, если <paramref name="baseUrl"/> не является допустимым абсолютным URI.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Базовый URL используется как префикс для всех относительных URI, передаваемых в методы запросов.
        /// После установки базового URL можно использовать относительные пути в запросах.
        /// </para>
        /// <para>
        /// Пример использования:
        /// <code>
        /// var client = new HttpClient()
        ///     .WithBaseUrl("https://api.example.com/v1/");
        /// // Отправит запрос на https://api.example.com/v1/users
        /// var response = await client.GetAsync("users");
        /// // Отправит запрос на https://api.example.com/v1/products/123
        /// var response = await client.GetAsync("products/123");
        /// </code>
        /// </para>
        /// <para>
        /// Если базовый URL уже установлен, этот метод перезапишет его новым значением.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Создание и настройка клиента с базовым URL
        /// var client = new HttpClient()
        ///     .WithBaseUrl("https://jsonplaceholder.typicode.com/")
        ///     .WithHeader("Accept", "application/json");
        /// // Выполнение запросов с относительными путями
        /// var posts = await client.GetAsync("posts");
        /// var post = await client.GetAsync("posts/1");
        /// // Можно комбинировать с другими методами расширения
        /// var authClient = new HttpClient()
        ///     .WithBaseUrl("https://api.example.com/")
        ///     .WithAuth("your_token_here")
        ///     .WithHeader("X-API-Version", "1.0");
        /// </code>
        /// </example>
        public static HttpClient WithBaseUrl(this HttpClient client, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException(@"Base URL cannot be null or empty", nameof(baseUrl));
            }

            // Используем UriKind.Absolute для явного указания, что ожидается абсолютный URL
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            return client;
        }

        /// <summary>
        /// Добавляет произвольный заголовок к клиенту.
        /// Если заголовок уже существует, он будет заменен.
        /// </summary>
        /// <param name="client">Экземпляр <see cref="HttpClient"/>.</param>
        /// <param name="name">Имя заголовка.</param>
        /// <param name="value">Значение заголовка.</param>
        /// <returns>Тот же экземпляр <see cref="HttpClient"/> для цепочки вызовов.</returns>
        public static HttpClient WithHeader(this HttpClient client, string name, string value)
        {
            if (client.DefaultRequestHeaders.Contains(name))
            {
                client.DefaultRequestHeaders.Remove(name);
            }

            client.DefaultRequestHeaders.Add(name, value);
            return client;
        }

        /// <summary>
        /// Строит строку запроса (query string) из словаря параметров.
        /// </summary>
        /// <param name="query">Словарь параметров запроса.</param>
        /// <returns>Строка запроса в формате key1=value1&amp;key2=value2.</returns>
        private static string BuildQueryString(Dictionary<string, object> query)
        {
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            foreach (var kvp in query)
            {
                if (kvp.Value != null)
                {
                    queryParams[kvp.Key] = kvp.Value.ToString();
                }
            }

            return queryParams.ToString();
        }

        /// <summary>
        /// Представляет HTTP-ответ, содержащий как исходное сообщение ответа, так и результат в виде строки.
        /// </summary>
        /// <remarks>
        /// Этот класс полезен, когда требуется сохранить как исходные метаданные HTTP-ответа (статус код, заголовки и
        /// т.д.), так и тело ответа в строковом формате.
        /// </remarks>
        public class HttpResponse
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="HttpResponse"/> class. Инициализирует новый экземпляр
            /// класса <see cref="HttpResponse"/> с указанным сообщением ответа и результатом.
            /// </summary>
            /// <param name="responseMessage">Исходное сообщение HTTP-ответа, содержащее метаданные ответа.</param>
            /// <param name="result">Тело ответа в виде строки.</param>
            /// <exception cref="ArgumentNullException">
            /// Выбрасывается, когда <paramref name="responseMessage"/> равен null.
            /// </exception>
            internal HttpResponse(HttpResponseMessage responseMessage, string result)
            {
                this.StatusCode = responseMessage.StatusCode;
                this.IsSuccessStatusCode = responseMessage.IsSuccessStatusCode;
                this.Result = result;
                this.RequestString = responseMessage.RequestMessage?.RequestUri?.ToString();
            }

            /// <summary>
            /// Gets получает тело HTTP-ответа в виде строки.
            /// </summary>
            /// <value>
            /// Строка, содержащая тело ответа. Может быть пустой строкой или null, если ответ не содержит тела.
            /// </value>
            public string Result { get; }

            /// <summary>
            /// Gets the status code.
            /// </summary>
            /// <value>The status code.</value>
            public HttpStatusCode StatusCode { get; }

            /// <summary>
            /// Gets a value indicating whether this instance is success status code.
            /// </summary>
            /// <value><c>true</c> if this instance is success status code; otherwise, <c>false</c>.</value>
            public bool IsSuccessStatusCode { get; }

            /// <summary>
            /// Gets the request string.
            /// </summary>
            /// <value>The request string.</value>
            public string RequestString { get; }
        }
    }
}