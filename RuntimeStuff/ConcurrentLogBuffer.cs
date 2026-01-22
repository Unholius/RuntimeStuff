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
    where T : class
    {
        private readonly T[] buffer;
        private int index;
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentLogBuffer{T}"/> class.
        /// Инициализирует новый экземпляр <see cref="ConcurrentLogBuffer{T}"/>
        /// с указанной ёмкостью.
        /// </summary>
        /// <param name="capacity">
        /// Максимальное количество элементов, которые может хранить буфер.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Генерируется, если <paramref name="capacity"/> меньше или равно нулю.
        /// </exception>
        public ConcurrentLogBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            buffer = new T[capacity];
        }

        /// <summary>
        /// Добавляет элемент в буфер.
        /// </summary>
        /// <param name="item">Элемент для добавления.</param>
        /// <remarks>
        /// При достижении ёмкости буфера старые элементы перезаписываются.
        /// Метод потокобезопасен и не использует блокировки.
        /// </remarks>
        public void Add(T item)
        {
            int i = Interlocked.Increment(ref index) - 1;
            Volatile.Write(ref buffer[i % buffer.Length], item);

            int c;
            do
            {
                c = count;
                if (c >= buffer.Length) break;
            }
            while (Interlocked.CompareExchange(ref count, c + 1, c) != c);
        }

        /// <summary>
        /// Создаёт снимок текущего состояния буфера.
        /// </summary>
        /// <returns>
        /// Коллекция <see cref="IReadOnlyList{T}"/> с элементами
        /// в порядке добавления — от старых к новым.
        /// </returns>
        /// <remarks>
        /// Снимок изолирован от внутреннего буфера, и последующие вызовы <see cref="Add"/>
        /// не влияют на уже полученный результат.
        /// </remarks>
        public IReadOnlyList<T> Snapshot()
        {
            var snapshot = new List<T>(Volatile.Read(ref count));
            int c = Volatile.Read(ref count);
            int startIndex = Math.Max(0, Volatile.Read(ref index) - c);

            for (int i = startIndex; i < startIndex + c; i++)
            {
                snapshot.Add(Volatile.Read(ref buffer[i % buffer.Length]));
            }

            return snapshot;
        }

        /// <summary>
        /// Возвращает перечислитель для перебора элементов буфера.
        /// </summary>
        /// <returns>Перечислитель элементов в порядке добавления.</returns>
        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <returns>Возвращает список элементов из Snapshot().</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
