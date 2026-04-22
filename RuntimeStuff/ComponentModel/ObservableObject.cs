// <copyright file="ObservableObject.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// НЕ ДОБАВЛЯТЬ ПУБЛИЧНЫЕ СВОЙСТВА, Т.К. ЭТО МОЖЕТ СЛОМАТЬ ДИНАМИЧЕСКИЙ МАППИНГ В ENTITY FRAMEWORK И ДР.
namespace System.ComponentModel
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// Базовый класс для объектов с поддержкой уведомлений об изменении свойств,
    /// динамических свойств и интеграции с механизмами привязки данных (data binding).
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Класс объединяет следующие возможности:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>INotifyPropertyChanged — уведомление об изменении свойства.</description>
    /// </item>
    /// <item>
    /// <description>INotifyPropertyChanging — уведомление до изменения свойства.</description>
    /// </item>
    /// <item>
    /// <description>ICustomTypeDescriptor — предоставление метаданных свойств для UI.</description>
    /// </item>
    /// </list>
    ///
    /// <para>
    /// Поддерживаются два типа свойств:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Статические свойства (через Get&lt;T&gt; / Set&lt;T&gt;).</description>
    /// </item>
    /// <item>
    /// <description>Динамические свойства (через индексатор string).</description>
    /// </item>
    /// </list>
    ///
    /// <para>
    /// Динамические свойства становятся доступны UI (WinForms / WPF DataGrid)
    /// через реализацию ICustomTypeDescriptor.
    /// </para>
    ///
    /// <para>
    /// Рекомендуется:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Использовать Set(ref field, value) для явных полей.</description>
    /// </item>
    /// <item>
    /// <description>Использовать Get/Set без поля для простых VM-свойств.</description>
    /// </item>
    /// <item>
    /// <description>Использовать SuspendNotifications при массовых обновлениях.</description>
    /// </item>
    /// <item>
    /// <description>Использовать On{PropertyName}Changed для бизнес-логики.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public class ObservableObject :
        INotifyPropertyChanged,
        INotifyPropertyChanging,
        ICustomTypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Action<object>>> ChangedHandlers = new();
        private static readonly ConcurrentDictionary<Type, PropertyDescriptorCollection> DescriptorCache = new();
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
        /// Индексатор для свойств.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        public object this[string propertyName]
        {
            get => this.Get<object>(propertyName);
            set => this.Set<object>(value, propertyName: propertyName);
        }

        /// <summary>
        /// Возвращает значение свойства.
        /// </summary>
        /// <typeparam name="T">Тип значения свойства.</typeparam>
        /// <param name="propertyName">Имя свойства (заполняется автоматически компилятором).</param>
        /// <returns>Текущее значение свойства или значение по умолчанию для типа <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentNullException">Генерируется, если имя свойства не указано.</exception>
        public T Get<T>(
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

        /// <inheritdoc/>
        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this);

        /// <inheritdoc/>
        public string GetClassName() => TypeDescriptor.GetClassName(this);

        /// <inheritdoc/>
        public string GetComponentName() => TypeDescriptor.GetComponentName(this);

        /// <inheritdoc/>
        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this);

        /// <inheritdoc/>
        public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this);

        /// <inheritdoc/>
        public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this);

        /// <inheritdoc/>
        public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType);

        /// <inheritdoc/>
        public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this);

        /// <inheritdoc/>
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes);

        /// <inheritdoc/>
        public PropertyDescriptorCollection GetProperties()
            => this.GetProperties(Array.Empty<Attribute>());

        /// <inheritdoc/>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var type = this.GetType();

            return DescriptorCache.GetOrAdd(type, _ =>
            {
                var staticProps = TypeDescriptor.GetProperties(type)
                    .Cast<PropertyDescriptor>();

                var dynamicProps = Maps[type].GetNames().Where(x => staticProps.Any(sp => !sp.Name.Equals(x)))
                    .Select(k => new DynamicPropertyDescriptor(k));

                return new PropertyDescriptorCollection(
                    staticProps.Concat(dynamicProps).ToArray(),
                    true);
            });
        }

        /// <inheritdoc/>
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

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
        public bool Set<T>(
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
        public bool Set<T>(
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

        /// <summary>
        /// Управляет состоянием рассылки уведомлений об изменениях.
        /// </summary>
        /// <param name="suspend">Значение <see langword="true"/> для приостановки уведомлений, и <see langword="false"/> для возобновления.</param>
        public void SuspendNotifications(bool suspend)
        {
            this.notificationsSuspended = suspend;
        }

        /// <summary>
        /// Вызывается после изменения значения свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменённого свойства.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            this.InvokeChangedHandler(propertyName);
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

        private static Action<object> CreateChangedHandler(Type type, string propertyName)
        {
            var method = type.GetMethod(
                "On" + propertyName + "Changed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
            {
                return null;
            }

            return instance => method.Invoke(instance, null);
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

        private void InvokeChangedHandler(string propertyName)
        {
            var type = this.GetType();

            var map = ChangedHandlers.GetOrAdd(
                type,
                _ => new ConcurrentDictionary<string, Action<object>>());

            var handler = map.GetOrAdd(
                propertyName,
                name => CreateChangedHandler(type, name));

            handler?.Invoke(this);
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

        /// <inheritdoc/>
        public sealed class DynamicPropertyDescriptor : PropertyDescriptor
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DynamicPropertyDescriptor"/> class.
            /// </summary>
            /// <param name="name">Property name.</param>
            public DynamicPropertyDescriptor(string name)
                : base(name, null)
            {
            }

            /// <inheritdoc/>
            public override Type ComponentType => typeof(ObservableObject);

            /// <inheritdoc/>
            public override bool IsReadOnly => false;

            /// <inheritdoc/>
            public override Type PropertyType => typeof(object);

            /// <inheritdoc/>
            public override bool CanResetValue(object component) => false;

            /// <inheritdoc/>
            public override object GetValue(object component)
                => ((ObservableObject)component)[this.Name];

            /// <inheritdoc/>
            public override void ResetValue(object component)
            {
            }

            /// <inheritdoc/>
            public override void SetValue(object component, object value)
                => ((ObservableObject)component)[this.Name] = value;

            /// <inheritdoc/>
            public override bool ShouldSerializeValue(object component) => true;
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

            public string[] GetNames() => this.map.Keys.ToArray();
        }
    }
}