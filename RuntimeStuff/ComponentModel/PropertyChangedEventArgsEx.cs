// <copyright file="PropertyChangedEventArgsEx.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// НЕ ДОБАВЛЯТЬ ПУБЛИЧНЫЕ СВОЙСТВА, Т.К. ЭТО МОЖЕТ СЛОМАТЬ ДИНАМИЧЕСКИЙ МАППИНГ В ENTITY FRAMEWORK И ДР.
namespace System.ComponentModel
{
    /// <summary>
    /// Расширенная версия <see cref="PropertyChangedEventArgs"/>,
    /// содержащая предыдущее и новое значения свойства.
    /// </summary>
    /// <remarks>
    /// Может использоваться в сценариях, где недостаточно только имени изменённого свойства
    /// и требуется передать значения до и после изменения.
    /// </remarks>
    public class PropertyChangedEventArgsEx : PropertyChangedEventArgs
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса
        /// <see cref="PropertyChangedEventArgsEx"/> с указанным именем свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменённого свойства.</param>
        public PropertyChangedEventArgsEx(string propertyName)
            : base(propertyName)
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса
        /// <see cref="PropertyChangedEventArgsEx"/> с именем свойства,
        /// предыдущим и новым значением.
        /// </summary>
        /// <param name="propertyName">Имя изменённого свойства.</param>
        /// <param name="oldValue">Значение свойства до изменения.</param>
        /// <param name="newValue">Значение свойства после изменения.</param>
        public PropertyChangedEventArgsEx(string propertyName, object oldValue, object newValue)
            : this(propertyName)
        {
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>
        /// Получает значение свойства до изменения.
        /// </summary>
        public virtual object OldValue { get; }

        /// <summary>
        /// Получает новое значение свойства после изменения.
        /// </summary>
        public virtual object NewValue { get; }
    }
}