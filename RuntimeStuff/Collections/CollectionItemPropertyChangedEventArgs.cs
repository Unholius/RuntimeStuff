// <copyright file="CollectionItemPropertyChangedEventArgs.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Collections
{
    using System;

    /// <summary>
    /// Содержит данные события об изменении свойства элемента коллекции.
    /// </summary>
    /// <remarks>
    /// Используется в коллекциях, отслеживающих изменения объектов-элементов,
    /// когда необходимо определить, какой именно элемент изменился
    /// и какое его свойство было обновлено.
    /// </remarks>
    public class CollectionItemPropertyChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса
        /// <see cref="CollectionItemPropertyChangedEventArgs"/>.
        /// </summary>
        /// <param name="item">Элемент коллекции, у которого изменилось свойство.</param>
        /// <param name="propertyName">Имя изменённого свойства элемента.</param>
        public CollectionItemPropertyChangedEventArgs(object item, string propertyName)
        {
            this.PropertyName = propertyName;
            this.Item = item;
        }

        /// <summary>
        /// Получает элемент коллекции, у которого произошло изменение.
        /// </summary>
        public virtual object Item { get; }

        /// <summary>
        /// Получает имя изменённого свойства элемента коллекции.
        /// </summary>
        public virtual string PropertyName { get; }
    }
}