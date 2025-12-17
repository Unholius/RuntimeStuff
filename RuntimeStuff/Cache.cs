using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff
{
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

    /// <summary>
    /// Потокобезопасный кэш значений с поддержкой ленивой инициализации
    /// и опционального времени жизни элементов.
    /// </summary>
    /// <typeparam name="TKey">Тип ключа кэша.</typeparam>
    /// <typeparam name="TValue">Тип значения кэша.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    public class Cache<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly Func<TKey, Task<TValue>> _asyncFactory;
        private readonly ConcurrentDictionary<TKey, Lazy<Task<(TValue Value, DateTime Created)>>> _cache;
        private readonly TimeSpan? _expiration;

        /// <summary>
        /// Создаёт кэш с асинхронной фабрикой значений.
        /// </summary>
        /// <param name="valueFactory">Фабрика значений.</param>
        /// <param name="expiration">Опциональное время жизни элементов кэша.</param>
        public Cache(Func<TKey, Task<TValue>> valueFactory, TimeSpan? expiration = null)
        {
            _cache = new ConcurrentDictionary<TKey, Lazy<Task<(TValue Value, DateTime Created)>>>();
            _asyncFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _expiration = expiration;
        }

        /// <summary>
        /// Создаёт кэш с синхронной фабрикой значений.
        /// </summary>
        /// <param name="syncFactory">Синхронная фабрика значений.</param>
        /// <param name="expiration">Опциональное время жизни элементов кэша.</param>
        public Cache(Func<TKey, TValue> syncFactory, TimeSpan? expiration = null)
            : this(WrapSyncFactory(syncFactory), expiration)
        {
        }

        /// <summary>
        /// Событие, вызываемое при добавлении элемента в кэш.
        /// </summary>
        public event Action<TKey> ItemAdded;

        /// <summary>
        /// Событие, вызываемое при удалении элемента из кэша.
        /// </summary>
        public event Action<TKey, RemovalReason> ItemRemoved;

        /// <summary>
        /// Количество актуальных элементов кэша.
        /// Учитываются только успешно созданные и неистёкшие элементы.
        /// </summary>
        public int Count =>
            _cache.Count(p =>
                p.Value.IsValueCreated &&
                p.Value.Value.IsCompleted &&
                (_expiration == null || DateTime.UtcNow - p.Value.Value.Result.Created < _expiration));

        /// <summary>
        /// Возвращает ключи актуальных элементов кэша.
        /// Не запускает фабрику и не блокирует поток.
        /// </summary>
        public IEnumerable<TKey> Keys =>
            _cache
                .Where(p => p.Value.IsValueCreated &&
                            p.Value.Value.IsCompleted &&
                            (_expiration == null || DateTime.UtcNow - p.Value.Value.Result.Created < _expiration))
                .Select(p => p.Key);

        /// <summary>
        /// Возвращает значения актуальных элементов кэша.
        /// Только успешно созданные и неистёкшие элементы.
        /// </summary>
        public IEnumerable<TValue> Values =>
            _cache
                .Where(p => p.Value.IsValueCreated &&
                            p.Value.Value.IsCompleted &&
                            (_expiration == null || DateTime.UtcNow - p.Value.Value.Result.Created < _expiration))
                .Select(p => p.Value.Value.Result.Value);

        /// <summary>
        /// Получает значение по ключу.
        /// Синхронная обёртка над асинхронным методом <see cref="GetAsync"/>.
        /// ⚠ Не рекомендуется вызывать из UI-потока или ASP.NET.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public TValue this[TKey key] => Get(key);

        /// <summary>
        /// Очистка всего кэша.
        /// Вызывает событие <see cref="ItemRemoved"/> для каждого элемента.
        /// </summary>
        public void Clear()
        {
            var keys = _cache.Keys.ToArray();
            _cache.Clear();

            foreach (var key in keys)
            {
                OnItemRemoved(key, RemovalReason.Cleared);
            }
        }

        /// <summary>
        /// Проверяет наличие актуального (неистёкшего) элемента в кэше.
        /// Фабрика значений не вызывается.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>
        /// <c>true</c>, если элемент существует, не истёк и успешно создан.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Синхронно получает значение из кэша или создаёт его.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public TValue Get(TKey key)
        {
            return GetAsync(key)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Асинхронно получает значение из кэша или создаёт его.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public async Task<TValue> GetAsync(TKey key)
        {
            while (true)
            {
                var lazy = _cache.GetOrAdd(
                    key,
                    k => new Lazy<Task<(TValue, DateTime)>>(
                        async () =>
                        {
                            var value = await _asyncFactory(k).ConfigureAwait(false);
                            OnItemAdded(k);
                            return (value, DateTime.UtcNow);
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));

                (TValue Value, DateTime Created) entry;

                try
                {
                    entry = await lazy.Value.ConfigureAwait(false);
                }
                catch
                {
                    _cache.TryRemove(key, out _);
                    OnItemRemoved(key, RemovalReason.Manual);
                    throw;
                }

                // TTL проверка
                if (_expiration != null && DateTime.UtcNow - entry.Created >= _expiration)
                {
                    if (_cache.TryRemove(key, out _))
                        OnItemRemoved(key, RemovalReason.Expired);

                    continue; // создаём заново
                }

                return entry.Value;
            }
        }

        /// <summary>
        /// Возвращает перечислитель по актуальным элементам кэша.
        /// Только успешно созданные и неистёкшие элементы.
        /// </summary>
        /// <returns>Перечислитель ключ-значение.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in Keys)
            {
                if (TryGetValue(key, out var value))
                    yield return new KeyValuePair<TKey, TValue>(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Удаляет элемент из кэша.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        /// <returns>
        /// <c>true</c>, если элемент существовал и был удалён; иначе <c>false</c>.
        /// </returns>
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
        /// Пытается получить значение из кэша без создания нового элемента.
        /// Не блокирует поток и не вызывает фабрику.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <param name="value">Полученное значение.</param>
        /// <returns>
        /// <c>true</c>, если значение существует, не истёкло и успешно создано.
        /// </returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;
            if (_cache.TryGetValue(key, out var lazy))
            {
                if (!lazy.IsValueCreated)
                    return false;

                var task = lazy.Value;
                if (!task.IsCompleted)
                    return false;

                var entry = task.Result;
                if (_expiration != null && DateTime.UtcNow - entry.Created >= _expiration)
                    return false;

                value = entry.Value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Вызывает событие добавления элемента.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        protected void OnItemAdded(TKey key) => ItemAdded?.Invoke(key);

        /// <summary>
        /// Вызывает событие удаления элемента.
        /// </summary>
        /// <param name="key">Ключ элемента.</param>
        /// <param name="reason">Причина удаления.</param>
        protected void OnItemRemoved(TKey key, RemovalReason reason) => ItemRemoved?.Invoke(key, reason);

        private static Func<TKey, Task<TValue>> WrapSyncFactory(Func<TKey, TValue> syncFactory)
        {
            if (syncFactory == null)
                throw new ArgumentNullException(nameof(syncFactory));

            return key => Task.FromResult(syncFactory(key));
        }
    }
}
