// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DynamicPropertyObject.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using RuntimeStuff.Extensions;

    /// <summary>
    /// Динамический объект с возможностью добавлять свойства во время выполнения.
    /// Поддерживает Expando-подобный доступ через индексатор и типобезопасные методы.
    /// </summary>
    public class DynamicPropertyObject : ICustomTypeDescriptor
    {
        /// <summary>
        /// The properties.
        /// </summary>
        private readonly Dictionary<string, PropertyData> properties = new Dictionary<string, PropertyData>(StringComparison.OrdinalIgnoreCase.ToStringComparer());

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPropertyObject"/> class.
        /// Создает пустой объект.
        /// </summary>
        public DynamicPropertyObject()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPropertyObject"/> class.
        /// Создает объект с копированием свойств из другого объекта.
        /// </summary>
        /// <param name="source">Исходный объект для копирования свойств.</param>
        public DynamicPropertyObject(object source)
        {
            var props = TypeDescriptor.GetProperties(source);
            foreach (PropertyDescriptor prop in props)
            {
                this.AddProperty(prop.Name, prop.PropertyType, prop.GetValue(source));
            }
        }

        /// <summary>
        /// Gets or sets функция для разрешения редактора по типу свойства.
        /// </summary>
        /// <value>The editor resolver.</value>
        public Func<Type, Type> EditorResolver { get; set; }

        /// <summary>
        /// Gets словарь соответствий типов свойства и типов редакторов.
        /// </summary>
        /// <value>The editors.</value>
        public Dictionary<Type, Type> Editors { get; private set; } = new Dictionary<Type, Type>();

        /// <summary>
        /// Индексатор для доступа к свойствам по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>System.Object.</returns>
        /// <example>
        /// obj["Name"] = "John";
        /// var age = obj["Age"];.
        /// </example>
        public object this[string name]
        {
            get => this.GetValue(name);
            set
            {
                if (this.properties.TryGetValue(name, out var property))
                {
                    property.Value = value;
                }
                else
                {
                    this.AddProperty(name, value?.GetType() ?? typeof(object), value);
                }
            }
        }

        /// <summary>
        /// Добавляет новое свойство или заменяет существующее.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <param name="type">Тип свойства.</param>
        /// <param name="value">Начальное значение свойства.</param>
        public void AddProperty(string name, Type type, object value = null) => this.properties[name] = new PropertyData(type, value);

        /// <summary>
        /// Очищает все свойства объекта.
        /// </summary>
        public void ClearProperties() => this.properties.Clear();

        /// <summary>
        /// Возвращает коллекцию атрибутов компонента.
        /// </summary>
        /// <returns>An <see cref="T:System.ComponentModel.AttributeCollection"></see> containing the attributes for this object.</returns>
        public AttributeCollection GetAttributes() => AttributeCollection.Empty;

        /// <summary>
        /// Возвращает имя класса компонента.
        /// </summary>
        /// <returns>The class name of the object, or null if the class does not have a name.</returns>
        public string GetClassName() => nameof(DynamicPropertyObject);

        /// <summary>
        /// Возвращает имя компонента.
        /// </summary>
        /// <returns>The name of the object, or null if the object does not have a name.</returns>
        public string GetComponentName() => null;

        /// <summary>
        /// Возвращает конвертер типа компонента.
        /// </summary>
        /// <returns>A <see cref="T:System.ComponentModel.TypeConverter"></see> that is the converter for this object, or null if there is no <see cref="T:System.ComponentModel.TypeConverter"></see> for this object.</returns>
        public TypeConverter GetConverter() => null;

        /// <summary>
        /// Возвращает событие по умолчанию.
        /// </summary>
        /// <returns>An <see cref="T:System.ComponentModel.EventDescriptor"></see> that represents the default event for this object, or null if this object does not have events.</returns>
        public EventDescriptor GetDefaultEvent() => null;

        /// <summary>
        /// Возвращает свойство по умолчанию.
        /// </summary>
        /// <returns>A <see cref="T:System.ComponentModel.PropertyDescriptor"></see> that represents the default property for this object, or null if this object does not have properties.</returns>
        public PropertyDescriptor GetDefaultProperty() => null;

        /// <summary>
        /// Возвращает редактор типа.
        /// </summary>
        /// <param name="editorBaseType">Базовый тип редактора.</param>
        /// <returns>An <see cref="T:System.Object"></see> of the specified type that is the editor for this object, or null if the editor cannot be found.</returns>
        public object GetEditor(Type editorBaseType) => null;

        /// <summary>
        /// Возвращает коллекцию событий компонента с фильтром атрибутов.
        /// </summary>
        /// <param name="attributes">Массив атрибутов.</param>
        /// <returns>An <see cref="T:System.ComponentModel.EventDescriptorCollection"></see> that represents the filtered events for this component instance.</returns>
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => this.GetEvents();

        /// <summary>
        /// Возвращает коллекцию всех событий компонента.
        /// </summary>
        /// <returns>An <see cref="T:System.ComponentModel.EventDescriptorCollection"></see> that represents the events for this component instance.</returns>
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;

        /// <summary>
        /// Возвращает коллекцию динамических свойств компонента.
        /// </summary>
        /// <returns>A <see cref="T:System.ComponentModel.PropertyDescriptorCollection"></see> that represents the properties for this component instance.</returns>
        public PropertyDescriptorCollection GetProperties() => new PropertyDescriptorCollection(
                this.properties.Keys.Select(name => new DynamicPropertyDescriptor(name, this))
                    .ToArray<PropertyDescriptor>());

        /// <summary>
        /// Возвращает коллекцию динамических свойств компонента с фильтром атрибутов.
        /// </summary>
        /// <param name="attributes">An array of type <see cref="T:System.Attribute"></see> that is used as a filter.</param>
        /// <returns>A <see cref="T:System.ComponentModel.PropertyDescriptorCollection"></see> that represents the filtered properties for this component instance.</returns>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => this.GetProperties();

        /// <summary>
        /// Возвращает владельца свойства.
        /// </summary>
        /// <param name="pd">Описание свойства.</param>
        /// <returns>An <see cref="T:System.Object"></see> that represents the owner of the specified property.</returns>
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        /// <summary>
        /// Получает тип свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Тип свойства.</returns>
        public Type GetPropertyType(string name) => this.properties[name].Type;

        /// <summary>
        /// Типобезопасный getter значения свойства.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Property '{name}' not found.</exception>
        public T GetValue<T>(string name)
        {
            if (this.properties.TryGetValue(name, out var p))
            {
                if (p.Value is T t)
                {
                    return t;
                }

                return (T)Convert.ChangeType(p.Value, typeof(T));
            }

            throw new KeyNotFoundException($"Property '{name}' not found.");
        }

        /// <summary>
        /// Получает значение свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        public object GetValue(string name) => this.properties[name].Value;

        /// <summary>
        /// Типобезопасный setter значения свойства.
        /// </summary>
        /// <typeparam name="T">Тип значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        public void SetValue<T>(string name, T value)
        {
            if (this.properties.ContainsKey(name))
            {
                this.properties[name].Value = value;
                this.properties[name].Type = typeof(T);
            }
            else
            {
                this.AddProperty(name, typeof(T), value);
            }
        }

        /// <summary>
        /// Устанавливает значение существующего свойства.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        public void SetValue(string name, object value)
        {
            if (this.properties.TryGetValue(name, out var p))
            {
                p.Value = value;
            }
        }

        /// <summary>
        /// Дескриптор динамического свойства.
        /// </summary>
        public class DynamicPropertyDescriptor : PropertyDescriptor
        {
            /// <summary>
            /// The owner.
            /// </summary>
            private readonly DynamicPropertyObject owner;

            /// <summary>
            /// Initializes a new instance of the <see cref="DynamicPropertyDescriptor"/> class.
            /// Создает дескриптор свойства.
            /// </summary>
            /// <param name="name">Имя свойства.</param>
            /// <param name="owner">Владелец свойства.</param>
            public DynamicPropertyDescriptor(string name, DynamicPropertyObject owner)
                : base(name, null)
            {
                this.owner = owner;
            }

            /// <summary>
            /// Gets словарь редакторов для всех свойств.
            /// </summary>
            /// <value>The editors.</value>
            public static Dictionary<Type, Type> Editors { get; } = new Dictionary<Type, Type>();

            /// <summary>
            /// Gets тип компонента.
            /// </summary>
            /// <value>The type of the component.</value>
            public override Type ComponentType => typeof(DynamicPropertyObject);

            /// <summary>
            /// Gets конвертер свойства.
            /// </summary>
            /// <value>The converter.</value>
            public override TypeConverter Converter =>
                this.PropertyType.IsEnum ? new EnumConverter(this.PropertyType) : base.Converter;

            /// <summary>
            /// Gets a value indicating whether признак доступности для изменения.
            /// </summary>
            /// <value><c>true</c> if this instance is read only; otherwise, <c>false</c>.</value>
            public override bool IsReadOnly => false;

            /// <summary>
            /// Gets тип свойства.
            /// </summary>
            /// <value>The type of the property.</value>
            public override Type PropertyType => this.owner.GetPropertyType(this.Name);

            /// <summary>
            /// Можно ли сбросить значение свойства.
            /// </summary>
            /// <param name="component">The component to test for reset capability.</param>
            /// <returns>true if resetting the component changes its value; otherwise, false.</returns>
            public override bool CanResetValue(object component) => false;

            /// <summary>
            /// Получение редактора свойства.
            /// </summary>
            /// <param name="editorBaseType">Базовый тип редактора.</param>
            /// <returns>An instance of the requested editor type, or null if an editor cannot be found.</returns>
            public override object GetEditor(Type editorBaseType)
            {
                if (this.owner.properties[this.Name].EditorType != null)
                {
                    return Activator.CreateInstance(this.owner.properties[this.Name].EditorType);
                }

                if (this.owner.Editors.TryGetValue(this.PropertyType, out var editorType))
                {
                    return Activator.CreateInstance(editorType);
                }

                if (this.owner.EditorResolver != null)
                {
                    return Activator.CreateInstance(this.owner.EditorResolver(this.PropertyType));
                }

                return base.GetEditor(editorBaseType);
            }

            /// <summary>
            /// Получение значения свойства.
            /// </summary>
            /// <param name="component">The component with the property for which to retrieve the value.</param>
            /// <returns>The value of a property for a given component.</returns>
            public override object GetValue(object component) => this.owner.GetValue(this.Name);

            /// <summary>
            /// Сброс значения свойства (не реализован).
            /// </summary>
            /// <param name="component">The component with the property value that is to be reset to the default value.</param>
            public override void ResetValue(object component)
            {
            }

            /// <summary>
            /// Установка значения свойства.
            /// </summary>
            /// <param name="component">The component with the property value that is to be set.</param>
            /// <param name="value">The new value.</param>
            public override void SetValue(object component, object value) => this.owner.SetValue(this.Name, value);

            /// <summary>
            /// Нужно ли сериализовать значение свойства.
            /// </summary>
            /// <param name="component">The component.</param>
            /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
            public override bool ShouldSerializeValue(object component) => true;
        }

        /// <summary>
        /// Внутренние данные одного свойства.
        /// </summary>
        private sealed class PropertyData
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PropertyData"/> class.
            /// Создает данные для свойства.
            /// </summary>
            /// <param name="type">Тип свойства.</param>
            /// <param name="value">Значение свойства.</param>
            public PropertyData(Type type, object value)
            {
                this.Type = type;
                this.Value = value;
            }

            /// <summary>
            /// Gets or sets тип редактора свойства.
            /// </summary>
            /// <value>The type of the editor.</value>
            public Type EditorType { get; set; }

            /// <summary>
            /// Gets or sets тип свойства.
            /// </summary>
            /// <value>The type.</value>
            public Type Type { get; set; }

            /// <summary>
            /// Gets or sets значение свойства.
            /// </summary>
            /// <value>The value.</value>
            public object Value { get; set; }
        }
    }
}