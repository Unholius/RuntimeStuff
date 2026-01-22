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

        public ConcurrentLogBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            buffer = new T[capacity];
        }

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

        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
