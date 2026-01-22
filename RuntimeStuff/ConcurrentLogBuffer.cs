// <copyright file="ConcurrentLogBuffer.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Потокобезопасный кольцевой буфер фиксированного размера
    /// для хранения последних элементов.
    /// </summary>
    /// <typeparam name="T">
    /// Тип элементов, хранимых в буфере.
    /// </typeparam>
    /// <remarks>
    /// Класс предназначен для сценариев логирования и диагностики,
    /// где требуется хранить ограниченное количество последних записей
    /// без неограниченного роста памяти.
    ///
    /// При добавлении элементов сверх заданной ёмкости
    /// более старые элементы автоматически перезаписываются.
    ///
    /// Класс обеспечивает корректную работу в многопоточной среде
    /// без использования блокировок (<c>lock</c>),
    /// полагаясь на атомарные операции <see cref="Interlocked"/>.
    /// </remarks>
    public sealed class ConcurrentLogBuffer<T> : IEnumerable<T>
    {
        private readonly T[] buffer;
        private int index;
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentLogBuffer{T}"/> class.
        /// Инициализирует новый экземпляр <see cref="ConcurrentLogBuffer{T}"/>
        /// с заданной ёмкостью.
        /// </summary>
        /// <param name="capacity">
        /// Максимальное количество элементов,
        /// одновременно хранимых в буфере.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Может быть сгенерировано, если <paramref name="capacity"/> меньше либо равен нулю
        /// (проверка должна выполняться вызывающим кодом).
        /// </exception>
        public ConcurrentLogBuffer(int capacity)
        {
            buffer = new T[capacity];
        }

        /// <summary>
        /// Добавляет элемент в буфер.
        /// </summary>
        /// <param name="item">
        /// Добавляемый элемент.
        /// </param>
        /// <remarks>
        /// Добавление выполняется атомарно.
        ///
        /// Если буфер заполнен, элемент перезаписывает
        /// самый старый сохранённый элемент.
        ///
        /// Порядок добавления между потоками не гарантируется,
        /// однако сохраняется корректная последовательность индексов.
        /// </remarks>
        public void Add(T item)
        {
            int i = Interlocked.Increment(ref index) - 1;
            buffer[i % buffer.Length] = item;

            if (count < buffer.Length)
                Interlocked.Increment(ref count);
        }

        /// <summary>
        /// Возвращает снимок текущего содержимого буфера.
        /// </summary>
        /// <returns>
        /// Коллекция, содержащая элементы буфера
        /// в порядке их добавления — от самых старых к самым новым.
        /// </returns>
        /// <remarks>
        /// Метод возвращает логический «снимок» данных
        /// на момент вызова без блокировки потоков.
        ///
        /// Возвращаемая коллекция не связана с внутренним буфером
        /// и не изменяется при последующих вызовах <see cref="Add"/>.
        ///
        /// В условиях высокой конкурентности возможно,
        /// что часть элементов будет добавлена параллельно
        /// и не попадёт в текущий снимок.
        /// </remarks>
        public IReadOnlyList<T> Snapshot()
        {
            var result = new List<T>(count);
            int start = Math.Max(0, index - count);

            for (int i = start; i < index; i++)
                result.Add(buffer[i % buffer.Length]);

            return result;
        }

        /// <summary>
        /// Возвращает перечислитель для перебора элементов буфера.
        /// </summary>
        /// <remarks>
        /// Перебор выполняется по снимку данных,
        /// эквивалентному результату вызова <see cref="Snapshot"/>.
        /// </remarks>
        /// <returns>Содержимое буфера.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Snapshot().GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
