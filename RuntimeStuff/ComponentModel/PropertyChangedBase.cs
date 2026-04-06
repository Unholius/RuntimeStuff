// <copyright file="PropertyChangedBase.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.ComponentModel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Базовый класс для объектов, поддерживающих уведомления об изменении свойств.
    /// Реализует интерфейсы <see cref="INotifyPropertyChanged"/> и <see cref="INotifyPropertyChanging"/>.
    /// </summary>
    public abstract class PropertyChangedBase : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private bool suspendNotifications;
        private Dictionary<string, IValueHolder> values;

        /// <summary>
        /// Событие, возникающее после изменения значения свойства.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Событие, возникающее перед изменением значения свойства.
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Внутренний интерфейс для хранения значения свойства.
        /// </summary>
        private interface IValueHolder
        {
        }

        /// <summary>
        /// При установке в <c>true</c> временно приостанавливает уведомления об изменении свойств.
        /// </summary>
        public bool SuspendNotifications
        {
            get => this.suspendNotifications;
            set
            {
                if (this.suspendNotifications != value)
                {
                    this.suspendNotifications = value;
                }
            }
        }

        /// <summary>
        /// Вызывает событие <see cref="PropertyChanged"/> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменяемого свойства. Если не указано, используется имя вызывающего метода.</param>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Вызывает событие <see cref="PropertyChanging"/> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменяемого свойства. Если не указано, используется имя вызывающего метода.</param>
        public virtual void OnPropertyChanging([CallerMemberName] string propertyName = null) =>
            this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

        /// <summary>
        /// Устанавливает новое значение свойства и вызывает уведомления при необходимости.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="field">Ссылка на поле, хранящее значение свойства.</param>
        /// <param name="value">Новое значение свойства.</param>
        /// <param name="onChanged">Действие, выполняемое после изменения значения свойства.</param>
        /// <param name="propertyName">Имя свойства. Если не указано, используется имя вызывающего метода.</param>
        /// <returns>Возвращает <c>true</c>, если значение изменилось, иначе <c>false</c>.</returns>
        public virtual bool Set<T>(ref T field, T value, Action onChanged = null, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            var notify = !this.suspendNotifications;
            if (notify)
            {
                this.OnPropertyChanging(propertyName);
            }

            field = value;
            onChanged?.Invoke();

            if (notify)
            {
                this.OnPropertyChanged(propertyName);
            }

            return true;
        }

        /// <summary>
        /// Устанавливает новое значение свойства, используя внутренний словарь значений, и вызывает уведомления при необходимости.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="value">Новое значение свойства.</param>
        /// <param name="onChanged">Действие, выполняемое после изменения значения свойства.</param>
        /// <param name="propertyName">Имя свойства. Если не указано, используется имя вызывающего метода.</param>
        /// <returns>Возвращает <c>true</c>, если значение изменилось, иначе <c>false</c>.</returns>
        public virtual bool Set<T>(T value, Action onChanged = null, [CallerMemberName] string propertyName = null)
        {
            this.values ??= new Dictionary<string, IValueHolder>();

            if (!this.values.TryGetValue(propertyName, out var holder) || !(holder is ValueHolder<T> typed))
            {
                typed = new ValueHolder<T>();
                this.values[propertyName] = typed;
            }

            if (typed.ValueEquals(value))
            {
                return false;
            }

            var notify = !this.suspendNotifications;
            if (notify)
            {
                this.OnPropertyChanging(propertyName);
            }

            typed.Value = value;

            onChanged?.Invoke();

            if (notify)
            {
                this.OnPropertyChanged(propertyName);
            }

            return true;
        }

        /// <summary>
        /// Получает текущее значение свойства из внутреннего словаря.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="propertyName">Имя свойства. Если не указано, используется имя вызывающего метода.</param>
        /// <returns>Возвращает текущее значение свойства или значение по умолчанию для типа.</returns>
        protected T Get<T>([CallerMemberName] string propertyName = null)
        {
            var dict = this.values;

            if (dict != null && dict.TryGetValue(propertyName, out var holder) && holder is ValueHolder<T> typed)
            {
                return typed.Value;
            }

            return default;
        }

        /// <summary>
        /// Внутренний класс для хранения значения свойства.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        private sealed class ValueHolder<T> : IValueHolder
        {
            /// <summary>
            /// Компаратор для сравнения значений свойства.
            /// </summary>
            public static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;

            /// <summary>
            /// Значение свойства.
            /// </summary>
            public T Value { get; set; }

            /// <summary>
            /// Сравнивает текущее значение с другим значением.
            /// </summary>
            /// <param name="other">Значение для сравнения.</param>
            /// <returns>Возвращает <c>true</c>, если значения равны, иначе <c>false</c>.</returns>
            public bool ValueEquals(T other) => Comparer.Equals(this.Value, other);
        }
    }
}