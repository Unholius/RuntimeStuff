using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;



/// <summary>
/// Обеспечивает потокобезопасный кэш с автоматическим созданием и устареванием значений по ключу. Поддерживает
/// синхронные и асинхронные фабрики значений, а также события для отслеживания изменений в кэше.
/// </summary>
/// <remarks>Класс предназначен для хранения и автоматического создания значений по ключу с возможностью задания
/// времени жизни элементов. Если элемент устарел или отсутствует, он создаётся с помощью заданной фабрики.
/// Поддерживаются события для отслеживания добавления, удаления и очистки элементов. Кэш реализует интерфейс
/// IReadOnlyDictionary для удобного доступа к коллекции ключей и значений. Все операции потокобезопасны благодаря
/// использованию ConcurrentDictionary.</remarks>
/// <typeparam name="TKey">Тип ключа, используемого для идентификации элементов в кэше.</typeparam>
/// <typeparam name="TValue">Тип значения, хранимого в кэше.</typeparam>
[DebuggerDisplay("Count = {Count}")]
public class Cache<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>
    ///     Асинхронная фабрика значений (опционально). Используется в асинхронном конструкторе.
    /// </summary>
    private readonly Func<TKey, Task<TValue>> _asyncValueFactory;

    /// <summary>
    ///     Внутренний словарь: ключ → пара (значение, время создания).
    /// </summary>
    protected readonly ConcurrentDictionary<TKey, (TValue Value, DateTime Created)> _cache;

    /// <summary>
    ///     Время жизни элемента в кэше. Если <c>null</c>, элементы не устаревают.
    /// </summary>
    protected readonly TimeSpan? _expiration;

    /// <summary>
    ///     Синхронная фабрика значений, используемая для создания значения по ключу.
    /// </summary>
    private readonly Func<TKey, TValue> _valueFactory;

    /// <summary>
    ///     Инициализирует новый экземпляр кэша с заданной функцией преобразования ключа в значение.
    /// </summary>
    /// <param name="valueFactory">Функция для генерации значения по ключу.</param>
    /// <param name="expiration">Время жизни элементов (null — без истечения срока).</param>
    /// <param name="concurrencyLevel">Ожидаемый уровень параллелизма для <see cref="_cache" />.</param>
    /// <param name="capacity">Начальная ёмкость словаря.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="valueFactory" /> равен <c>null</c>.</exception>
    public Cache(Func<TKey, TValue> valueFactory, TimeSpan? expiration = null, int concurrencyLevel = 4,
        int capacity = 31)
    {
        _cache = new ConcurrentDictionary<TKey, (TValue Value, DateTime Created)>(concurrencyLevel, capacity);
        _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        _expiration = expiration;
    }

    /// <summary>
    ///     Создаёт асинхронный кэш с автоматическим созданием значений.
    ///     Асинхронная фабрика хранится и используется в методе <see cref="GetAsync" />
    /// </summary>
    /// <param name="asyncValueFactory">Асинхронная функция генерации значения по ключу.</param>
    /// <param name="expiration">Время жизни элементов (null — без истечения срока).</param>
    /// <param name="concurrencyLevel">Уровень параллельности <see cref="_cache" />.</param>
    /// <param name="capacity">Начальная ёмкость словаря.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="asyncValueFactory" /> равен <c>null</c>.</exception>
    public Cache(Func<TKey, Task<TValue>> asyncValueFactory, TimeSpan? expiration = null, int concurrencyLevel = 4, int capacity = 31)
        : this(key => asyncValueFactory(key).Result, expiration, concurrencyLevel, capacity)
    {
        _asyncValueFactory = asyncValueFactory ?? throw new ArgumentNullException(nameof(asyncValueFactory));
    }

    /// <summary>
    ///     Возвращает значение, указывающее, является ли коллекция доступной только для чтения.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    ///     Определяет, содержится ли указанный ключ в кэше.
    /// </summary>
    /// <param name="key">Ключ для поиска.</param>
    /// <returns><see langword="true" />, если ключ найден; иначе — <see langword="false" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key)
    {
        return _cache.ContainsKey(key);
    }

    /// <summary>
    ///     Пытается получить значение, связанное с указанным ключом.
    /// </summary>
    /// <param name="key">Ключ значения, которое требуется получить.</param>
    /// <param name="value">
    ///     Выходной параметр: значение, связанное с ключом, если оно найдено и не истекло; иначе значение по
    ///     умолчанию для типа.
    /// </param>
    /// <returns><see langword="true" />, если значение найдено и действительно; иначе — <see langword="false" />.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var entry))
            if (_expiration == null || DateTime.UtcNow - entry.Created < _expiration)
            {
                value = entry.Value;
                return true;
            }

        value = default;
        return false;
    }

    /// <summary>
    ///     Возвращает перечислитель, выполняющий итерацию по кэшу.
    /// </summary>
    /// <returns>Перечислитель для элементов кэша (ключ, значение).</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _cache.Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Количество элементов, содержащихся в кэше.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    ///     Коллекция всех ключей, содержащихся в кэше.
    /// </summary>
    public IEnumerable<TKey> Keys => _cache.Keys;

    /// <summary>
    ///     Коллекция всех значений, содержащихся в кэше.
    /// </summary>
    public IEnumerable<TValue> Values => _cache.Values.Select(x => x.Value);

    /// <summary>
    ///     Получает или задаёт элемент, связанный с указанным ключом.
    ///     Если элемент отсутствует, используется фабрика значений <see cref="_valueFactory" />.
    /// </summary>
    /// <param name="key">Ключ элемента.</param>
    /// <returns>Значение, связанное с ключом.</returns>
    public TValue this[TKey key] => Get(key);

    /// <summary>
    ///     Событие вызывается при добавлении нового элемента в кэш.
    /// </summary>
    public event Action<TKey> ItemAdded;

    /// <summary>
    ///     Событие вызывается при удалении элемента из кэша.
    /// </summary>
    public event Action<TKey> ItemRemoved;

    /// <summary>
    ///     Событие вызывается при очистке всего кэша.
    /// </summary>
    public event Action CacheCleared;

    /// <summary>
    ///     Получает значение по ключу. Если значение отсутствует, оно создаётся с помощью <see cref="_valueFactory" />.
    /// </summary>
    /// <param name="key">Ключ, по которому нужно получить значение.</param>
    /// <returns>Значение, связанное с ключом.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Get(TKey key)
    {
        if (_cache.TryGetValue(key, out var entry))
            if (_expiration == null || DateTime.UtcNow - entry.Created < _expiration)
                return entry.Value;

        var value = _valueFactory(key);
        _cache[key] = (value, DateTime.UtcNow);
        OnItemAdded(key);
        return value;
    }

    /// <summary>
    ///     Асинхронно получает значение по ключу.
    ///     Если значения нет или оно устарело — вызывает асинхронную фабрику.
    /// </summary>
    /// <param name="key">Ключ, по которому требуется получить значение.</param>
    /// <returns>Задача, возвращающая значение, связанное с ключом.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<TValue> GetAsync(TKey key)
    {
        if (_cache.TryGetValue(key, out var entry))
            if (_expiration == null || DateTime.UtcNow - entry.Created < _expiration)
                return entry.Value;

        var value = await _asyncValueFactory(key);
        _cache[key] = (value, DateTime.UtcNow);
        OnItemAdded(key);
        return value;
    }

    /// <summary>
    ///     Вызывает событие <see cref="ItemAdded" /> для указанного ключа.
    /// </summary>
    /// <param name="key">Ключ добавленного элемента.</param>
    protected void OnItemAdded(TKey key)
    {
        ItemAdded?.Invoke(key);
    }

    /// <summary>
    ///     Вызывает событие <see cref="CacheCleared" /> при очистке кэша.
    /// </summary>
    protected void OnCacheCleared()
    {
        CacheCleared?.Invoke();
    }

    /// <summary>
    ///     Вызывает событие <see cref="ItemRemoved" /> для указанного ключа.
    /// </summary>
    /// <param name="key">Ключ удалённого элемента.</param>
    protected void OnItemRemoved(TKey key)
    {
        ItemRemoved?.Invoke(key);
    }

    /// <summary>
    ///     Удаляет значение из кэша по ключу.
    /// </summary>
    /// <param name="key">Ключ, который нужно удалить.</param>
    /// <returns><c>true</c>, если элемент был удалён; иначе <c>false</c>.</returns>
    public bool Remove(TKey key)
    {
        if (_cache.TryRemove(key, out _))
        {
            OnItemRemoved(key);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Очищает весь кэш и вызывает событие очистки.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        OnCacheCleared();
    }
}