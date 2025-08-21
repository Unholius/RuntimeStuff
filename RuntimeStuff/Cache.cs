namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Потокобезопасный кэш, который автоматически создаёт значение для заданного ключа при отсутствии.
    /// Использует ConcurrentDictionary для высокой производительности при множественном доступе.
    /// </summary>
    /// <typeparam name="TKey">Тип ключа.</typeparam>
    /// <typeparam name="TValue">Тип значения.</typeparam>
    public class Cache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _cache;
        private readonly Func<TKey, TValue> _valueFactory;

        /// <summary>
        /// Инициализирует новый экземпляр кэша с заданной функцией преобразования ключа в значение.
        /// </summary>
        /// <param name="valueFactory">Функция для генерации значения по ключу.</param>
        public Cache(Func<TKey, TValue> valueFactory)
        {
            _cache = new ConcurrentDictionary<TKey, TValue>();
            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        /// <summary>
        /// Получает значение по ключу. Если значение отсутствует, оно создаётся с помощью valueFactory.
        /// </summary>
        /// <param name="key">Ключ, по которому нужно получить значение.</param>
        /// <returns>Значение, связанное с ключом.</returns>
        public TValue Get(TKey key)
        {
            return _cache.GetOrAdd(key, _valueFactory);
        }

        /// <summary>
        /// Пытается получить значение по ключу, без создания нового.
        /// </summary>
        /// <param name="key">Ключ, по которому нужно найти значение.</param>
        /// <param name="value">Результат, если значение найдено.</param>
        /// <returns>true, если значение найдено; иначе false.</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            return _cache.TryGetValue(key, out value);
        }

        /// <summary>
        /// Устанавливает значение по ключу, заменяя старое при наличии.
        /// </summary>
        /// <param name="key">Ключ, по которому устанавливается значение.</param>
        /// <param name="value">Новое значение.</param>
        public void Set(TKey key, TValue value)
        {
            _cache[key] = value;
        }

        /// <summary>
        /// Удаляет значение из кэша по ключу.
        /// </summary>
        /// <param name="key">Ключ, который нужно удалить.</param>
        /// <returns>true, если элемент был удалён; иначе false.</returns>
        public bool Remove(TKey key)
        {
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Очищает весь кэш.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Количество элементов, содержащихся в кэше.
        /// </summary>
        public int Count => _cache.Count;
    }
}