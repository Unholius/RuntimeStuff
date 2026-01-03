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
        private readonly ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>> _cache;
        private readonly TimeSpan? _expiration;
        private readonly bool _hasFactory;

        public Cache(TimeSpan? expiration = null)
        {
            _cache = new ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>>();
            _expiration = null;
            _hasFactory = false;
            _expiration = expiration;
        }

        /// <summary>
        /// Создаёт кэш с асинхронной фабрикой значений.
        /// </summary>
        /// <param name="valueFactory">Фабрика значений.</param>
        /// <param name="expiration">Опциональное время жизни элементов кэша.</param>
        public Cache(Func<TKey, Task<TValue>> valueFactory, TimeSpan? expiration = null)
        {
            _cache = new ConcurrentDictionary<TKey, Lazy<Task<CacheEntry>>>();
            _asyncFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _expiration = expiration;
            _hasFactory = true;
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
        /// Получает значение из кэша по указанному ключу
        /// либо возвращает заданное значение по умолчанию.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа, используемого для идентификации значения в кэше.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения, хранящегося в кэше.
        /// </typeparam>
        /// <param name="key">
        /// Ключ элемента кэша.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, которое будет возвращено, если элемент отсутствует в кэше
        /// или не может быть получен.
        /// </param>
        /// <returns>
        /// Значение из кэша, если оно найдено;
        /// в противном случае — <paramref name="defaultValue"/>.
        /// </returns>
        /// <remarks>
        /// Метод является удобной обёрткой над
        /// <see cref="TryGetValue(TKey, out TValue)"/>
        /// и не генерирует исключений при отсутствии элемента.
        /// </remarks>
        public TValue GetOrDefault(TKey key, TValue defaultValue)
        {
            if (TryGetValue(key, out var value))
                return value;

            return defaultValue;
        }

        /// <summary>
        /// Асинхронно получает значение из кэша или создаёт его.
        /// </summary>
        /// <param name="key">Ключ.</param>
        /// <returns>Значение кэша.</returns>
        public async Task<TValue> GetAsync(TKey key)
        {
            // Режим без фабрики — работаем как обычный словарь
            if (!_hasFactory)
            {
                if (TryGetValue(key, out var existing))
                    return existing;

                throw new KeyNotFoundException(
                    $"The given key '{key}' was not present in the cache.");
            }

            while (true)
            {
                var lazy = _cache.GetOrAdd(
                    key,
                    k => new Lazy<Task<CacheEntry>>(
                        async () =>
                        {
                            var value = await _asyncFactory(k).ConfigureAwait(false);
                            OnItemAdded(k);
                            return new CacheEntry(value, DateTime.UtcNow);
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication));

                CacheEntry entry;

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
        /// Асинхронно получает значение из кэша по указанному ключу
        /// либо возвращает заданное значение по умолчанию.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа, используемого для идентификации значения в кэше.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения, хранящегося в кэше.
        /// </typeparam>
        /// <param name="key">
        /// Ключ элемента кэша.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, которое будет возвращено, если элемент отсутствует в кэше,
        /// просрочен или не может быть получен.
        /// </param>
        /// <returns>
        /// Значение из кэша, если оно найдено и актуально;
        /// в противном случае — <paramref name="defaultValue"/>.
        /// </returns>
        /// <remarks>
        /// Метод является удобной обёрткой над
        /// <see cref="TryGetValueAsync(TKey, System.Threading.CancellationToken)"/>
        /// и не генерирует исключений при отсутствии элемента.
        /// </remarks>
        public async Task<TValue> GetOrDefaultAsync(TKey key, TValue defaultValue)
        {
            if (!_cache.TryGetValue(key, out var lazy))
                return defaultValue;

            CacheEntry entry;

            try
            {
                entry = await lazy.Value.ConfigureAwait(false);
            }
            catch
            {
                return defaultValue;
            }

            var elapsed = DateTime.UtcNow - entry.Created;
            if (_expiration != null && elapsed >= _expiration)
            {
                _cache.TryRemove(key, out _);
                OnItemRemoved(key, RemovalReason.Expired);
                return defaultValue;
            }

            return entry.Value;
        }

        /// <summary>
        /// Добавляет или обновляет значение в кэше по указанному ключу.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа, используемого для идентификации значения в кэше.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения, хранящегося в кэше.
        /// </typeparam>
        /// <param name="key">
        /// Ключ элемента кэша.
        /// </param>
        /// <param name="value">
        /// Значение, которое будет сохранено в кэше.
        /// </param>
        /// <remarks>
        /// Если элемент с указанным ключом уже существует в кэше, он будет заменён новым значением,
        /// при этом будет вызвано событие <see cref="OnItemRemoved"/> с причиной
        /// <see cref="RemovalReason.Manual"/> и затем <see cref="OnItemAdded"/>.
        /// <para/>
        /// Если элемента с указанным ключом ещё нет, он будет добавлен,
        /// и будет вызвано событие <see cref="OnItemAdded"/>.
        /// <para/>
        /// Значение сохраняется в виде <see cref="Lazy{Task}"/>, чтобы поддерживать
        /// асинхронный доступ через <see cref="TryGetValueAsync(TKey, CancellationToken)"/>.
        /// </remarks>
        public void Set(TKey key, TValue value)
        {
            // Создаём Lazy с Task, фиксируя время создания сразу
            var v = new CacheEntry(value, DateTime.UtcNow);
            var lazy = new Lazy<Task<CacheEntry>>(
                () => Task.FromResult(v),
                LazyThreadSafetyMode.ExecutionAndPublication);

            _cache.AddOrUpdate(
                key,
                k =>
                {
                    OnItemAdded(k);
                    return lazy;
                },
                (k, old) =>
                {
                    OnItemRemoved(k, RemovalReason.Manual);
                    OnItemAdded(k);
                    return lazy;
                });
        }

        /// <summary>
        /// Асинхронно добавляет или обновляет значение в кэше по указанному ключу.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа, используемого для идентификации значения в кэше.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения, хранящегося в кэше.
        /// </typeparam>
        /// <param name="key">
        /// Ключ элемента кэша.
        /// </param>
        /// <param name="valueTask">
        /// Задача, возвращающая значение для сохранения в кэше.
        /// </param>
        /// <returns>
        /// Задача <see cref="Task"/>, представляющая асинхронную операцию добавления или обновления.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="valueTask"/> равна <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Метод ожидает завершения <paramref name="valueTask"/> и сохраняет результат в кэше,
        /// вызывая внутренний метод <see cref="Set(TKey, TValue)"/>.
        /// <para/>
        /// Если элемент с указанным ключом уже существует, он будет заменён новым значением,
        /// при этом будет вызвано событие <see cref="OnItemRemoved"/> с причиной
        /// <see cref="RemovalReason.Manual"/> и затем <see cref="OnItemAdded"/>.
        /// Если элемента с указанным ключом ещё нет, он будет добавлен,
        /// и будет вызвано событие <see cref="OnItemAdded"/>.
        /// </remarks>
        public async Task SetAsync(TKey key, Task<TValue> valueTask)
        {
            if (valueTask == null)
                throw new ArgumentNullException(nameof(valueTask));

            var value = await valueTask.ConfigureAwait(false);
            Set(key, value);
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

            if (!_cache.TryGetValue(key, out var lazy))
                return false;

            CacheEntry entry;

            try
            {
                entry = lazy.Value.GetAwaiter().GetResult(); // синхронно получаем Task
            }
            catch
            {
                return false;
            }

            if (_expiration != null && DateTime.UtcNow - entry.Created >= _expiration)
            {
                if (_cache.TryRemove(key, out _))
                    OnItemRemoved(key, RemovalReason.Expired);

                return false;
            }

            value = entry.Value;
            return true;
        }

        /// <summary>
        /// Пытается асинхронно получить значение из кэша по указанному ключу.
        /// </summary>
        /// <typeparam name="TKey">
        /// Тип ключа, используемого для идентификации значения в кэше.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// Тип значения, хранящегося в кэше.
        /// </typeparam>
        /// <param name="key">
        /// Ключ элемента кэша.
        /// </param>
        /// <param name="cancellationToken">
        /// Токен отмены, используемый для отмены асинхронной операции.
        /// </param>
        /// <returns>
        /// Кортеж, содержащий результат операции:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see langword="true"/> и значение — если элемент найден и актуален;
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see langword="false"/> и значение по умолчанию — если элемент отсутствует,
        /// просрочен или при получении произошла ошибка.
        /// </description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Метод не генерирует исключений при ошибках получения значения
        /// или истечении срока действия элемента. Все такие ситуации
        /// интерпретируются как неуспешная попытка получения.
        /// <para/>
        /// Если срок жизни элемента ограничен и истёк на момент обращения,
        /// элемент будет удалён из кэша с причиной
        /// <see cref="RemovalReason.Expired"/>.
        /// </remarks>
        /// <exception cref="OperationCanceledException">
        /// Выбрасывается, если операция была отменена
        /// через <paramref name="cancellationToken"/>.
        /// </exception>
        public async Task<(bool Success, TValue Value)> TryGetValueAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (!_cache.TryGetValue(key, out var lazy))
                return (false, default);

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

            if (_expiration != null &&
                DateTime.UtcNow - entry.Created >= _expiration)
            {
                if (_cache.TryRemove(key, out _))
                    OnItemRemoved(key, RemovalReason.Expired);

                return (false, default);
            }

            return (true, entry.Value);
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

        private sealed class CacheEntry
        {
            public TValue Value { get; }
            public DateTime Created { get; }
            public DateTime LastAccess;

            public CacheEntry(TValue value, DateTime created)
            {
                Value = value;
                Created = created;
                LastAccess = created;
            }
        }
    }
}
