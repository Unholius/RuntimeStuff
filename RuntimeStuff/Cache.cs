// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="Cache.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Определяет стратегию вытеснения элементов из ограниченной коллекции
    /// при превышении максимально допустимого размера.
    /// </summary>
    /// <remarks>Стратегия вытеснения определяет, какой элемент будет удалён первым,
    /// когда в коллекцию добавляется новый элемент сверх установленного лимита.</remarks>
    public enum EvictionPolicy
    {
        /// <summary>
        /// FIFO (First In, First Out).
        /// </summary>
        FIFO,

        /// <summary>
        /// LRU (Least Recently Used).
        /// </summary>
        LRU,
    }

    /// <summary>
    /// Причина удаления элемента из кэша.
    /// </summary>
    public enum RemovalReason
    {
        /// <summary>
        /// Элемент был удалён вручную.
        /// </summary>
        Manual,

        /// <summary>
        /// Элемент был удалён из-за истечения срока жизни.
        /// </summary>
        Expired,

        /// <summary>
        /// Элемент был удалён в результате полной очистки кэша.
        /// </summary>
        Cleared,

        /// <summary>
        /// Превышен лимит на максимальное количество элементов в кэше
        /// </summary>
        SizeLimit,
    }

    /// <summary>
    /// Потокобезопасный кэш значений с поддержкой ленивой инициализации
    /// и опционального времени жизни элементов.
    /// </summary>
    /// <typeparam name="TKey">Тип ключа кэша.</typeparam>
    /// <typeparam name="TValue">Тип значения кэша.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    public class Cache<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        /// <summary>
        /// The asynchronous factory.
        /// </summary>
        private readonly Func<TKey, Task<TValue>> asyncFactory;

        /// <summary>
        /// The cache.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>> cache;

        /// <summary>
        /// The eviction policy.
        /// </summary>
        private readonly EvictionPolicy evictionPolicy;

        /// <summary>
        /// The expiration.
        /// </summary>
        private readonly TimeSpan? expiration;

        /// <summary>
        /// The has factory.
        /// </summary>
        private readonly bool hasFactory;

        /// <summary>
        /// The size limit.
        /// </summary>
        private readonly uint? sizeLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}" /> class.
        /// </summary>
        /// <param name="expiration">The expiration.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="evictionPolicy">The eviction policy.</param>
        public Cache(TimeSpan? expiration = null, uint? sizeLimit = null, EvictionPolicy evictionPolicy = default)
        {
            this.cache = new ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>>();
            this.hasFactory = false;
            this.expiration = expiration;
            this.sizeLimit = sizeLimit;
            this.evictionPolicy = evictionPolicy;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class.
        /// Создаёт кэш с асинхронной фабрикой значений.
        /// </summary>
        /// <param name="valueFactory">Фабрика значений.</param>
        /// <param name="expiration">Опциональное время жизни элементов кэша.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="evictionPolicy">The eviction policy.</param>
        /// <exception cref="System.ArgumentNullException">valueFactory.</exception>
        public Cache(Func<TKey, Task<TValue>> valueFactory, TimeSpan? expiration = null, uint? sizeLimit = null, EvictionPolicy evictionPolicy = default)
        {
            this.cache = new ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>>();
            this.asyncFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            this.expiration = expiration;
            this.hasFactory = true;
            this.sizeLimit = sizeLimit;
            this.evictionPolicy = evictionPolicy;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey, TValue}"/> class.
        /// Создаёт кэш с синхронной фабрикой значений.
        /// </summary>
        /// <param name="syncFactory">Синхронная фабрика значений.</param>
        /// <param name="expiration">Опциональное время жизни элементов кэша.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="evictionPolicy">The eviction policy.</param>
        public Cache(Func<TKey, TValue> syncFactory, TimeSpan? expiration = null, uint? sizeLimit = null, EvictionPolicy evictionPolicy = default)
            : this(WrapSyncFactory(syncFactory), expiration, sizeLimit, evictionPolicy)
        {
        }

        /// <summary>
        /// Событие вызываемое при успешном доступе к элементу
        /// </summary>
        public event Action<TKey> ItemAccessed;

        /// <summary>
        /// Событие, вызываемое при добавлении элемента в кэш.
        /// </summary>
        public event Action<TKey> ItemAdded;

        /// <summary>
        /// Событие, вызываемое при удалении элемента из кэша.
        /// </summary>
        public event Action<TKey, RemovalReason> ItemRemoved;

        /// <summary>
        /// Gets количество актуальных элементов кэша.
        /// Учитываются только успешно созданные и неистёкшие элементы.
        /// </summary>
        /// <value>The count.</value>
        public int Count =>
            this.cache.Count(p =>
                p.Value.IsValueCreated &&
                p.Value.Value.IsCompleted &&
                (this.expiration == null || this.Now() - p.Value.Value.Result.Created < this.expiration));

        /// <summary>
        /// Gets возвращает ключи актуальных элементов кэша.
        /// Не запускает фабрику и не блокирует поток.
        /// </summary>
        /// <value>The keys.</value>
        public IEnumerable<TKey> Keys =>
            this.cache
                .Where(p => p.Value.IsValueCreated &&
                            p.Value.Value.IsCompleted &&
                            (this.expiration == null || this.Now() - p.Value.Value.Result.Created < this.expiration))
                .Select(p => p.Key);

        /// <summary>
        /// Gets возвращает значения актуальных элементов кэша.
        /// Только успешно созданные и неистёкшие элементы.
        /// </summary>
        /// <value>The values.</value>
        public IEnumerable<TValue> Values =>
            this.cache
                .Where(p => p.Value.IsValueCreated &&
                            p.Value.Value.IsCompleted &&
                            (this.expiration == null || this.Now() - p.Value.Value.Result.Created < this.expiration))
                .Select(p => p.Value.Value.Result.Value);

        /// <summary>
        /// Получает значение по ключу.
        /// Синхронная обёртка над асинхронным методом <see cref="GetAsync" />.
        /// ⚠ Не рекомендуется вызывать из UI-потока или ASP.NET.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public TValue this[TKey key] => this.Get(key);

        /// <summary>
        /// Очистка всего кэша.
        /// Вызывает событие <see cref="ItemRemoved" /> для каждого элемента.
        /// </summary>
        public void Clear()
        {
            var keys = this.cache.Keys.ToArray();
            this.cache.Clear();

            foreach (var key in keys)
            {
                this.OnItemRemoved(key, RemovalReason.Cleared);
            }
        }

        /// <summary>
        /// Проверяет наличие актуального (неистёкшего) элемента в кэше.
        /// Фабрика значений не вызывается.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns><c>true</c>, если элемент существует, не истёк и успешно создан.</returns>
        public bool ContainsKey(TKey key) => this.TryGetValue(key, out _);

        /// <summary>
        /// Синхронно получает значение из кэша или создаёт его.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public TValue Get(TKey key) => this.GetAsync(key)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

        /// <summary>
        /// Асинхронно получает значение из кэша или создаёт его.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The given key '{key}' was not present in the cache.</exception>
        public async Task<TValue> GetAsync(TKey key)
        {
            // Режим без фабрики — работаем как обычный словарь
            if (!this.hasFactory)
            {
                if (this.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                throw new KeyNotFoundException(
                    $"The given key '{key}' was not present in the cache.");
            }

            while (true)
            {
                var lazy = this.cache.GetOrAdd(
                    key,
                    k => new Lazy<Task<CacheEntry>>(
                        async () =>
                        {
                            var value = await this.asyncFactory(k).ConfigureAwait(false);
                            this.OnItemAdded(k);
                            this.EnforceSizeLimit();
                            return new CacheEntry(key, value, this.Now());
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));

                CacheEntry entry;

                try
                {
                    entry = await lazy.Value.ConfigureAwait(false);
                }
                catch
                {
                    this.cache.TryRemove(key, out _);
                    this.OnItemRemoved(key, RemovalReason.Manual);
                    throw;
                }

                // TTL проверка
                if (this.expiration != null && this.Now() - entry.Created >= this.expiration)
                {
                    if (this.cache.TryRemove(key, out _))
                    {
                        this.OnItemRemoved(key, RemovalReason.Expired);
                    }

                    continue; // создаём заново
                }

                this.UpdateLastAccess(key, entry);
                return entry.Value;
            }
        }

        /// <summary>
        /// Gets the entries.
        /// </summary>
        /// <returns>IEnumerable&lt;System.ValueTuple&lt;TKey, TValue, DateTime, DateTime&gt;&gt;.</returns>
        public IEnumerable<(TKey Key, TValue Value, DateTime Created, DateTime LastAccess)> GetEntries() => this.cache.Values.Select(x => (x.Value.Result.Key, x.Value.Result.Value, x.Value.Result.Created, x.Value.Result.LastAccess));

        /// <summary>
        /// Возвращает перечислитель по актуальным элементам кэша.
        /// Только успешно созданные и неистёкшие элементы.
        /// </summary>
        /// <returns>Перечислитель ключ-значение.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var entry in this.cache.Values.OrderBy(x => x.Value.Result.Created))
            {
                if (this.TryGetValue(entry.Value.Result.Key, out var value))
                {
                    yield return new KeyValuePair<TKey, TValue>(entry.Value.Result.Key, value);
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <summary>
        /// Получает значение из кэша по указанному ключу
        /// либо возвращает заданное значение по умолчанию.
        /// </summary>
        /// <param name="key">Ключ элемента кэша.</param>
        /// <param name="defaultValue">Значение, которое будет возвращено, если элемент отсутствует в кэше
        /// или не может быть получен.</param>
        /// <returns>Значение из кэша, если оно найдено;
        /// в противном случае — <paramref name="defaultValue" />.</returns>
        /// <remarks>Метод является удобной обёрткой над
        /// <see cref="TryGetValue(TKey, out TValue)" />
        /// и не генерирует исключений при отсутствии элемента.</remarks>
        public TValue GetOrDefault(TKey key, TValue defaultValue)
        {
            if (this.TryGetValue(key, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Асинхронно получает значение из кэша по указанному ключу
        /// либо возвращает заданное значение по умолчанию.
        /// </summary>
        /// <param name="key">Ключ элемента кэша.</param>
        /// <param name="defaultValue">Значение, которое будет возвращено, если элемент отсутствует в кэше,
        /// просрочен или не может быть получен.</param>
        /// <returns>Значение из кэша, если оно найдено и актуально;
        /// в противном случае — <paramref name="defaultValue" />.</returns>
        /// <remarks>Метод является удобной обёрткой над
        /// <see cref="TryGetValueAsync(TKey, System.Threading.CancellationToken)" />
        /// и не генерирует исключений при отсутствии элемента.</remarks>
        public async Task<TValue> GetOrDefaultAsync(TKey key, TValue defaultValue)
        {
            if (!this.cache.TryGetValue(key, out var lazy))
            {
                return defaultValue;
            }

            CacheEntry entry;

            try
            {
                entry = await lazy.Value.ConfigureAwait(false);
            }
            catch
            {
                return defaultValue;
            }

            var elapsed = this.Now() - entry.Created;
            if (this.expiration != null && elapsed >= this.expiration)
            {
                this.cache.TryRemove(key, out _);
                this.OnItemRemoved(key, RemovalReason.Expired);
                return defaultValue;
            }

            this.UpdateLastAccess(key, entry);
            return entry.Value;
        }

        /// <summary>
        /// Удаляет элемент из кэша.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        /// <returns><c>true</c>, если элемент существовал и был удалён; иначе <c>false</c>.</returns>
        public bool Remove(TKey key)
        {
            if (this.cache.TryRemove(key, out _))
            {
                this.OnItemRemoved(key, RemovalReason.Manual);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Добавляет или обновляет значение в кэше по указанному ключу.
        /// </summary>
        /// <param name="key">Ключ элемента кэша.</param>
        /// <param name="value">Значение, которое будет сохранено в кэше.</param>
        /// <remarks>Если элемент с указанным ключом уже существует в кэше, он будет заменён новым значением,
        /// при этом будет вызвано событие <see cref="OnItemRemoved" /> с причиной
        /// <see cref="RemovalReason.Manual" /> и затем <see cref="OnItemAdded" />.
        /// <para />
        /// Если элемента с указанным ключом ещё нет, он будет добавлен,
        /// и будет вызвано событие <see cref="OnItemAdded" />.
        /// <para />
        /// Значение сохраняется в виде <see cref="Lazy{Task}" />, чтобы поддерживать
        /// асинхронный доступ через <see cref="TryGetValueAsync(TKey, CancellationToken)" />.</remarks>
        public void Set(TKey key, TValue value)
        {
            // Создаём Lazy с Task, фиксируя время создания сразу
            var v = new CacheEntry(key, value, this.Now());
            var lazy = new Lazy<Task<CacheEntry>>(
                () => Task.FromResult(v),
                LazyThreadSafetyMode.ExecutionAndPublication);

            // Принудительно создаем значение, чтобы IsValueCreated был true
            var v1 = lazy.Value;

            this.cache.AddOrUpdate(
                key,
                k =>
                {
                    this.OnItemAdded(k);
                    return lazy;
                },
                (k, old) =>
                {
                    this.OnItemRemoved(k, RemovalReason.Manual);
                    this.OnItemAdded(k);
                    return lazy;
                });

            this.EnforceSizeLimit();
        }

        /// <summary>
        /// Асинхронно добавляет или обновляет значение в кэше по указанному ключу.
        /// </summary>
        /// <param name="key">Ключ элемента кэша.</param>
        /// <param name="valueTask">Задача, возвращающая значение для сохранения в кэше.</param>
        /// <returns>Задача <see cref="Task" />, представляющая асинхронную операцию добавления или обновления.</returns>
        /// <exception cref="System.ArgumentNullException">valueTask.</exception>
        /// <remarks>Метод ожидает завершения <paramref name="valueTask" /> и сохраняет результат в кэше,
        /// вызывая внутренний метод <see cref="Set(TKey, TValue)" />.
        /// <para />
        /// Если элемент с указанным ключом уже существует, он будет заменён новым значением,
        /// при этом будет вызвано событие <see cref="OnItemRemoved" /> с причиной
        /// <see cref="RemovalReason.Manual" /> и затем <see cref="OnItemAdded" />.
        /// Если элемента с указанным ключом ещё нет, он будет добавлен,
        /// и будет вызвано событие <see cref="OnItemAdded" />.</remarks>
        public async Task SetAsync(TKey key, Task<TValue> valueTask)
        {
            if (valueTask == null)
            {
                throw new ArgumentNullException(nameof(valueTask));
            }

            var value = await valueTask.ConfigureAwait(false);
            this.Set(key, value);
        }

        /// <summary>
        /// Пытается получить значение из кэша без создания нового элемента.
        /// Не блокирует поток и не вызывает фабрику.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <param name="value">Полученное значение.</param>
        /// <returns><c>true</c>, если значение существует, не истёкло и успешно создано.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;

            if (!this.cache.TryGetValue(key, out var lazy))
            {
                return false;
            }

            CacheEntry entry;

            try
            {
                entry = lazy.Value.GetAwaiter().GetResult(); // синхронно получаем Task
            }
            catch
            {
                return false;
            }

            if (this.expiration != null && this.Now() - entry.Created >= this.expiration)
            {
                if (this.cache.TryRemove(key, out _))
                {
                    this.OnItemRemoved(key, RemovalReason.Expired);
                }

                return false;
            }

            this.UpdateLastAccess(key, entry);
            value = entry.Value;
            return true;
        }

        /// <summary>
        /// Пытается асинхронно получить значение из кэша по указанному ключу.
        /// </summary>
        /// <param name="key">Ключ элемента кэша.</param>
        /// <param name="cancellationToken">Токен отмены, используемый для отмены асинхронной операции.</param>
        /// <returns>Кортеж, содержащий результат операции:
        /// <list type="bullet"><item><description><see langword="true" /> и значение — если элемент найден и актуален;
        /// </description></item><item><description><see langword="false" /> и значение по умолчанию — если элемент отсутствует,
        /// просрочен или при получении произошла ошибка.
        /// </description></item></list></returns>
        /// <exception cref="OperationCanceledException">Выбрасывается, если операция была отменена
        /// через <paramref name="cancellationToken" />.</exception>
        /// <remarks>Метод не генерирует исключений при ошибках получения значения
        /// или истечении срока действия элемента. Все такие ситуации
        /// интерпретируются как неуспешная попытка получения.
        /// <para />
        /// Если срок жизни элемента ограничен и истёк на момент обращения,
        /// элемент будет удалён из кэша с причиной
        /// <see cref="RemovalReason.Expired" />.</remarks>
        public async Task<(bool Success, TValue Value)> TryGetValueAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (!this.cache.TryGetValue(key, out var lazy))
            {
                return (false, default);
            }

            CacheEntry entry;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                entry = await lazy.Value.ConfigureAwait(false);
            }
            catch
            {
                return (false, default);
            }

            if (this.expiration != null && this.Now() - entry.Created >= this.expiration)
            {
                if (this.cache.TryRemove(key, out _))
                {
                    this.OnItemRemoved(key, RemovalReason.Expired);
                }

                return (false, default);
            }

            this.UpdateLastAccess(key, entry);
            return (true, entry.Value);
        }

        /// <summary>
        /// Вызывает событие доступа к элементу.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        protected void OnItemAccessed(TKey key) => this.ItemAccessed?.Invoke(key);

        /// <summary>
        /// Вызывает событие добавления элемента.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        protected void OnItemAdded(TKey key) => this.ItemAdded?.Invoke(key);

        /// <summary>
        /// Вызывает событие удаления элемента.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        /// <param name="reason">Причина удаления.</param>
        protected void OnItemRemoved(TKey key, RemovalReason reason) => this.ItemRemoved?.Invoke(key, reason);

        /// <summary>
        /// Wraps the synchronize factory.
        /// </summary>
        /// <param name="syncFactory">The synchronize factory.</param>
        /// <returns>Func&lt;TKey, Task&lt;TValue&gt;&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">syncFactory.</exception>
        private static Func<TKey, Task<TValue>> WrapSyncFactory(Func<TKey, TValue> syncFactory)
        {
            if (syncFactory == null)
            {
                throw new ArgumentNullException(nameof(syncFactory));
            }

            return key => Task.FromResult(syncFactory(key));
        }

        /// <summary>
        /// Enforces the size limit.
        /// </summary>
        private void EnforceSizeLimit()
        {
            if (this.sizeLimit == null || this.sizeLimit.Value == 0)
            {
                return;
            }

            while (this.cache.Count > this.sizeLimit.Value)
            {
                var candidate = this.cache
                    .Where(p =>
                        p.Value.IsValueCreated &&
                        p.Value.Value.IsCompleted)
                    .Select(p =>
                    {
                        var entry = p.Value.Value.Result;
                        return new
                        {
                            p.Key,
                            entry.Created,
                            entry.LastAccess,
                        };
                    })
                    .OrderBy(p =>
                        this.evictionPolicy == EvictionPolicy.FIFO
                            ? p.Created
                            : p.LastAccess)
                    .FirstOrDefault();

                if (candidate == null)
                {
                    return;
                }

                if (this.cache.TryRemove(candidate.Key, out _))
                {
                    this.OnItemRemoved(candidate.Key, RemovalReason.SizeLimit);
                }
            }
        }

        /// <summary>
        /// Nows this instance.
        /// </summary>
        /// <returns>DateTime.</returns>
        private DateTime Now() => DateTimeHelper.ExactNow();

        /// <summary>
        /// Updates the last access.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="entry">The entry.</param>
        private void UpdateLastAccess(TKey key, CacheEntry entry)
        {
            entry.LastAccess = this.Now();
            this.OnItemAccessed(key);
        }

        /// <summary>
        /// Class CacheEntry. This class cannot be inherited.
        /// </summary>
        private sealed class CacheEntry
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CacheEntry"/> class.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value.</param>
            /// <param name="created">The created.</param>
            public CacheEntry(TKey key, TValue value, DateTime created)
            {
                this.Key = key;
                this.Value = value;
                this.Created = created;
                this.LastAccess = created;
            }

            /// <summary>
            /// Gets the created.
            /// </summary>
            /// <value>The created.</value>
            public DateTime Created { get; }

            /// <summary>
            /// Gets the key.
            /// </summary>
            /// <value>The key.</value>
            public TKey Key { get; }

            /// <summary>
            /// Gets or sets the last access.
            /// </summary>
            /// <value>The last access.</value>
            public DateTime LastAccess { get; internal set; }

            /// <summary>
            /// Gets the value.
            /// </summary>
            /// <value>The value.</value>
            public TValue Value { get; }
        }
    }
}