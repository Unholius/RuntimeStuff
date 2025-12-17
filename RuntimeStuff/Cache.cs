using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff
{
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
        /// Асинхронная фабрика значений.
        /// Используется в методе <see cref="GetAsync"/>.
        /// </summary>
        private readonly Func<TKey, Task<TValue>> _asyncValueFactory;

        /// <summary>
        /// Внутреннее хранилище кэша.
        /// Значения обёрнуты в <see cref="Lazy{T}"/> для ленивого создания.
        /// </summary>
        protected readonly ConcurrentDictionary<TKey, Lazy<(TValue Value, DateTime Created)>> _cache;

        /// <summary>
        /// Время жизни элементов кэша.
        /// Если <c>null</c>, элементы не истекают.
        /// </summary>
        protected readonly TimeSpan? _expiration;

        /// <summary>
        /// Синхронная фабрика значений.
        /// </summary>
        private readonly Func<TKey, TValue> _valueFactory;

        /// <summary>
        /// Создаёт экземпляр кэша с синхронной фабрикой значений.
        /// </summary>
        /// <param name="valueFactory">Функция создания значения по ключу.</param>
        /// <param name="expiration">Время жизни элемента кэша.</param>
        /// <param name="concurrencyLevel">Ожидаемое количество параллельных потоков.</param>
        /// <param name="capacity">Начальная ёмкость кэша.</param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="valueFactory"/> равен <c>null</c>.
        /// </exception>
        public Cache(
            Func<TKey, TValue> valueFactory,
            TimeSpan? expiration = null,
            int concurrencyLevel = 4,
            int capacity = 31)
        {
            _cache = new ConcurrentDictionary<TKey, Lazy<(TValue Value, DateTime Created)>>(
                concurrencyLevel, capacity);

            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _expiration = expiration;
        }

        /// <summary>
        /// Создаёт экземпляр кэша с асинхронной фабрикой значений.
        /// </summary>
        /// <param name="asyncValueFactory">Асинхронная функция создания значения.</param>
        /// <param name="expiration">Время жизни элемента кэша.</param>
        /// <param name="concurrencyLevel">Ожидаемое количество параллельных потоков.</param>
        /// <param name="capacity">Начальная ёмкость кэша.</param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="asyncValueFactory"/> равен <c>null</c>.
        /// </exception>
        public Cache(
            Func<TKey, Task<TValue>> asyncValueFactory,
            TimeSpan? expiration = null,
            int concurrencyLevel = 4,
            int capacity = 31)
            : this(key => asyncValueFactory(key).Result, expiration, concurrencyLevel, capacity)
        {
            _asyncValueFactory = asyncValueFactory
                ?? throw new ArgumentNullException(nameof(asyncValueFactory));
        }

        /// <summary>
        /// Возвращает <c>false</c>, так как кэш поддерживает изменение.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Проверяет наличие ключа в кэше.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns><c>true</c>, если ключ существует.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }

        /// <summary>
        /// Пытается получить значение по ключу без создания нового элемента.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <param name="value">Полученное значение.</param>
        /// <returns>
        /// <c>true</c>, если значение найдено и не истекло.
        /// </returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var lazyEntry))
            {
                var entry = lazyEntry.Value;
                if (_expiration == null || DateTime.UtcNow - entry.Created < _expiration)
                {
                    value = entry.Value;
                    return true;
                }

                // Элемент истёк
                if (_cache.TryRemove(key, out _))
                {
                    OnItemExpired(key);
                    OnItemRemoved(key, RemovalReason.Expired);
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Возвращает перечислитель по актуальным (неистекшим) элементам кэша.
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _cache
                .Select(x =>
                {
                    var entry = x.Value.Value;
                    if (_expiration == null || DateTime.UtcNow - entry.Created < _expiration)
                    {
                        return new KeyValuePair<TKey, TValue>(x.Key, entry.Value);
                    }

                    return new KeyValuePair<TKey, TValue>(x.Key, default);
                })
                .Where(x => x.Value != null || default(TValue) == null)
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Количество элементов в кэше (включая устаревшие).
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Коллекция ключей кэша.
        /// </summary>
        public IEnumerable<TKey> Keys => _cache.Keys;

        /// <summary>
        /// Коллекция актуальных (неистекших) значений кэша.
        /// </summary>
        public IEnumerable<TValue> Values => _cache.Values
            .Select(x => x.Value)
            .Where(entry => _expiration == null || DateTime.UtcNow - entry.Created < _expiration)
            .Select(entry => entry.Value);

        /// <summary>
        /// Получает значение по ключу, создавая его при необходимости.
        /// </summary>
        /// <param name="key">Ключ.</param>
        public TValue this[TKey key] => Get(key);

        /// <summary>
        /// Событие вызывается при добавлении нового элемента в кэш.
        /// </summary>
        public event Action<TKey> ItemAdded;

        /// <summary>
        /// Событие вызывается при удалении элемента из кэша.
        /// </summary>
        public event Action<TKey, RemovalReason> ItemRemoved;

        /// <summary>
        /// Событие вызывается при полной очистке кэша.
        /// </summary>
        public event Action CacheCleared;

        /// <summary>
        /// Событие вызывается при истечении срока жизни элемента кэша.
        /// </summary>
        public event Action<TKey> ItemExpired;

        /// <summary>
        /// Получает значение по ключу.
        /// Если значение отсутствует или истекло — создаёт новое.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue Get(TKey key)
        {
            var lazyEntry = _cache.GetOrAdd(key, k =>
                new Lazy<(TValue Value, DateTime Created)>(() =>
                {
                    var value = _valueFactory(k);
                    OnItemAdded(k);
                    return (value, DateTime.UtcNow);
                }, LazyThreadSafetyMode.ExecutionAndPublication));

            var entry = lazyEntry.Value;

            if (_expiration != null && DateTime.UtcNow - entry.Created >= _expiration)
            {
                if (_cache.TryRemove(key, out _))
                {
                    OnItemExpired(key);
                    OnItemRemoved(key, RemovalReason.Expired);
                }

                return Get(key);
            }

            return entry.Value;
        }

        /// <summary>
        /// Асинхронно получает значение по ключу.
        /// Если значение отсутствует или истекло — создаёт новое.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<TValue> GetAsync(TKey key)
        {
            var lazyEntry = _cache.GetOrAdd(key, k =>
                new Lazy<(TValue Value, DateTime Created)>(() =>
                {
                    var task = _asyncValueFactory(k);
                    var value = task.Result;
                    OnItemAdded(k);
                    return (value, DateTime.UtcNow);
                }, LazyThreadSafetyMode.ExecutionAndPublication));

            var entry = lazyEntry.Value;

            if (_expiration != null && DateTime.UtcNow - entry.Created >= _expiration)
            {
                if (_cache.TryRemove(key, out _))
                {
                    OnItemExpired(key);
                    OnItemRemoved(key, RemovalReason.Expired);
                }

                return await GetAsync(key);
            }

            return entry.Value;
        }

        /// <summary>
        /// Вызывает событие <see cref="ItemAdded"/>.
        /// </summary>
        protected void OnItemAdded(TKey key) => ItemAdded?.Invoke(key);

        /// <summary>
        /// Вызывает событие <see cref="CacheCleared"/>.
        /// </summary>
        protected void OnCacheCleared() => CacheCleared?.Invoke();

        /// <summary>
        /// Вызывает событие <see cref="ItemRemoved"/>.
        /// </summary>
        /// <param name="key">Ключ удалённого элемента.</param>
        /// <param name="reason">Причина удаления.</param>
        protected void OnItemRemoved(TKey key, RemovalReason reason)
        {
            ItemRemoved?.Invoke(key, reason);
        }

        /// <summary>
        /// Вызывает событие <see cref="ItemExpired"/>.
        /// </summary>
        protected void OnItemExpired(TKey key)
        {
            ItemExpired?.Invoke(key);
        }

        /// <summary>
        /// Удаляет элемент из кэша по ключу.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns><c>true</c>, если элемент был удалён.</returns>
        public bool Remove(TKey key)
        {
            if (_cache.TryRemove(key, out _))
            {
                OnItemRemoved(key, RemovalReason.Manual);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Полностью очищает кэш.
        /// </summary>
        public void Clear()
        {
            var keys = _cache.Keys.ToArray();

            _cache.Clear();

            foreach (var key in keys)
            {
                OnItemRemoved(key, RemovalReason.Cleared);
            }
            OnCacheCleared();
        }
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
        Cleared
    }
}