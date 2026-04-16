// <copyright file="PropertyChangedBase.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// НЕ ДОБАВЛЯТЬ ПУБЛИЧНЫЕ СВОЙСТВА, Т.К. ЭТО МОЖЕТ СЛОМАТЬ ДИНАМИЧЕСКИЙ МАППИНГ В ENTITY FRAMEWORK И ДР.
namespace System.ComponentModel
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// Базовый класс для объектов, реализующих уведомления об изменении свойств.
    /// Оптимизирован для снижения аллокаций памяти за счет использования индексированного хранилища значений.
    /// </summary>
    public abstract class PropertyChangedBase :
        INotifyPropertyChanged,
        INotifyPropertyChanging
    {
        private static readonly ConcurrentDictionary<Type, PropertyMap> Maps = new();
        private bool notificationsSuspended;
        private object[] values;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Управляет состоянием рассылки уведомлений об изменениях.
        /// </summary>
        /// <param name="suspend">Значение <see langword="true"/> для приостановки уведомлений, и <see langword="false"/> для возобновления.</param>
        public void SuspendNotifications(bool suspend)
        {
            this.notificationsSuspended = suspend;
        }

        /// <summary>
        /// Возвращает значение свойства.
        /// </summary>
        /// <typeparam name="T">Тип значения свойства.</typeparam>
        /// <param name="propertyName">Имя свойства (заполняется автоматически компилятором).</param>
        /// <returns>Текущее значение свойства или значение по умолчанию для типа <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentNullException">Генерируется, если имя свойства не указано.</exception>
        protected T Get<T>(
            [CallerMemberName] string propertyName = null)
        {
            if (propertyName is null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var index = GetIndex(this.GetType(), propertyName);

            var local = this.values;

            if (local == null || index >= local.Length)
            {
                return default;
            }

            var value = local[index];

            if (value is null)
            {
                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            throw new InvalidCastException(
                $"Property '{propertyName}' contains value of type '{value.GetType().FullName}', expected '{typeof(T).FullName}'.");
        }

        /// <summary>
        /// Вызывается после изменения значения свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменённого свойства.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Вызывается перед изменением значения свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменяемого свойства.</param>
        protected virtual void OnPropertyChanging(string propertyName)
        {
            var handler = this.PropertyChanging;
            handler?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }

        /// <summary>
        /// Устанавливает значение поля с уведомлением об изменении свойства.
        /// </summary>
        /// <typeparam name="T">Тип значения свойства.</typeparam>
        /// <param name="field">Ссылка на поле, в которое будет записано новое значение.</param>
        /// <param name="value">Новое значение.</param>
        /// <param name="onChanged">
        /// Дополнительное действие, выполняемое после изменения значения и до вызова уведомления
        /// <see cref="PropertyChanged"/>.
        /// </param>
        /// <param name="propertyName">
        /// Имя свойства. Подставляется автоматически вызывающим кодом.
        /// </param>
        /// <returns>
        /// <see langword="true"/>, если значение было изменено;
        /// <see langword="false"/>, если новое значение совпадает с текущим.
        /// </returns>
        protected bool Set<T>(
            ref T field,
            T value,
            Action onChanged = null,
            [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            this.RaiseChanging(propertyName);

            field = value;

            onChanged?.Invoke();

            this.RaiseChanged(propertyName);

            return true;
        }

        /// <summary>
        /// Устанавливает значение свойства, хранящегося во внутреннем массиве значений,
        /// с уведомлением об изменении.
        /// </summary>
        /// <typeparam name="T">Тип значения свойства.</typeparam>
        /// <param name="value">Новое значение.</param>
        /// <param name="onChanged">
        /// Дополнительное действие, выполняемое после изменения значения и до вызова уведомления
        /// <see cref="PropertyChanged"/>.
        /// </param>
        /// <param name="propertyName">
        /// Имя свойства. Подставляется автоматически вызывающим кодом.
        /// </param>
        /// <returns>
        /// <see langword="true"/>, если значение было изменено;
        /// <see langword="false"/>, если новое значение совпадает с текущим.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Возникает, если имя свойства не указано.
        /// </exception>
        protected bool Set<T>(
            T value,
            Action onChanged = null,
            [CallerMemberName] string propertyName = null)
        {
            if (propertyName is null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var index = GetIndex(this.GetType(), propertyName);

            var current = this.Get<T>(propertyName);

            if (EqualityComparer<T>.Default.Equals(current, value))
            {
                return false;
            }

            this.RaiseChanging(propertyName);

            this.EnsureCapacity(index);
            this.values[index] = value;

            onChanged?.Invoke();

            this.RaiseChanged(propertyName);

            return true;
        }

        private static int GetIndex(Type type, string propertyName)
        {
            var map = Maps.GetOrAdd(type, _ => new PropertyMap());
            return map.GetIndex(propertyName);
        }

        private void EnsureCapacity(int index)
        {
            if (this.values == null)
            {
                this.values = new object[index + 1];
                return;
            }

            if (index < this.values.Length)
            {
                return;
            }

            lock (this)
            {
                if (index < this.values.Length)
                {
                    return;
                }

                Array.Resize(
                    ref this.values,
                    Math.Max(this.values.Length * 2, index + 1));
            }
        }

        private void RaiseChanged(string propertyName)
        {
            if (!this.notificationsSuspended)
            {
                this.OnPropertyChanged(propertyName);
            }
        }

        private void RaiseChanging(string propertyName)
        {
            if (!this.notificationsSuspended)
            {
                this.OnPropertyChanging(propertyName);
            }
        }

        private sealed class PropertyMap
        {
            private readonly ConcurrentDictionary<string, int> map = new();
            private int counter = -1;

            public int GetIndex(string propertyName)
            {
                return this.map.GetOrAdd(
                    propertyName,
                    _ => Interlocked.Increment(ref this.counter));
            }
        }
    }
}