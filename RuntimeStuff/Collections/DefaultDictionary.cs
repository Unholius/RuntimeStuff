// <copyright file="DefaultDictionary.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Словарь, возвращающий значение по умолчанию при обращении
    /// к отсутствующему ключу вместо генерации исключения.
    /// </summary>
    /// <typeparam name="TKey">
    /// Тип ключей в словаре.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// Тип значений в словаре.
    /// </typeparam>
    public class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует пустой словарь со значением по умолчанию для типа <typeparamref name="TValue"/>.
        /// </summary>
        public DefaultDictionary()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует пустой словарь с заданным значением,
        /// возвращаемым при отсутствии ключа.
        /// </summary>
        /// <param name="keyNotFoundValue">
        /// Значение, возвращаемое при обращении к отсутствующему ключу.
        /// </param>
        public DefaultDictionary(TValue keyNotFoundValue)
            : base()
        {
            DefaultValue = keyNotFoundValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует словарь начальными значениями и заданным значением по умолчанию.
        /// </summary>
        /// <param name="initValues">
        /// Начальный набор пар ключ–значение.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, возвращаемое при отсутствии ключа.
        /// </param>
        public DefaultDictionary(IEnumerable<KeyValuePair<TKey, TValue>> initValues, TValue defaultValue)
            : this(defaultValue)
        {
            foreach (var kv in initValues)
                this[kv.Key] = kv.Value;

            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует словарь на основе другого словаря
        /// с заданным значением по умолчанию.
        /// </summary>
        /// <param name="initValues">
        /// Исходный словарь для копирования данных.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, возвращаемое при отсутствии ключа.
        /// </param>
        public DefaultDictionary(IDictionary<TKey, TValue> initValues, TValue defaultValue)
            : this(defaultValue)
        {
            foreach (var kv in initValues)
                this[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует пустой словарь с пользовательским компаратором ключей
        /// и значением по умолчанию.
        /// </summary>
        /// <param name="comparer">
        /// Компаратор для сравнения ключей.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, возвращаемое при отсутствии ключа.
        /// </param>
        public DefaultDictionary(IEqualityComparer<TKey> comparer, TValue defaultValue = default)
            : base(comparer)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDictionary{TKey, TValue}"/> class.
        /// Инициализирует словарь на основе другого словаря
        /// с пользовательским компаратором и значением по умолчанию.
        /// </summary>
        /// <param name="initValues">
        /// Исходный словарь для копирования данных.
        /// </param>
        /// <param name="comparer">
        /// Компаратор для сравнения ключей.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, возвращаемое при отсутствии ключа.
        /// </param>
        public DefaultDictionary(
            IDictionary<TKey, TValue> initValues,
            IEqualityComparer<TKey> comparer = null,
            TValue defaultValue = default)
            : base(initValues, comparer)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Значение, возвращаемое при обращении к отсутствующему ключу.
        /// </summary>
        private TValue DefaultValue { get; }

        /// <summary>
        /// Получает или задаёт значение по указанному ключу.
        /// </summary>
        /// <remarks>
        /// В отличие от стандартного <see cref="Dictionary{TKey, TValue}"/>,
        /// при отсутствии ключа возвращает значение по умолчанию,
        /// а не выбрасывает исключение <see cref="KeyNotFoundException"/>.
        /// </remarks>
        /// <param name="key">
        /// Ключ элемента.
        /// </param>
        /// <returns>
        /// Значение, связанное с ключом, либо значение по умолчанию.
        /// </returns>
        public new TValue this[TKey key]
        {
            get => this.TryGetValue(key, out var val) ? val : DefaultValue;
            set => base[key] = value;
        }

        /// <summary>
        /// Возвращает значение по ключу либо указанное альтернативное значение,
        /// если ключ отсутствует.
        /// </summary>
        /// <param name="key">
        /// Ключ элемента.
        /// </param>
        /// <param name="defaultValue">
        /// Значение, возвращаемое при отсутствии ключа.
        /// </param>
        /// <returns>
        /// Значение, связанное с ключом, либо <paramref name="defaultValue"/>.
        /// </returns>
        public TValue this[TKey key, TValue defaultValue]
        {
            get
            {
                if (!this.TryGetValue(key, out var result))
                    result = defaultValue;

                return result;
            }
        }
    }
}