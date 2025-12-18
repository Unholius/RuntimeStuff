using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using RuntimeStuff.Extensions;

namespace RuntimeStuff
{
    /// <summary>
    /// Динамический объект с возможностью добавлять свойства во время выполнения.
    /// Поддерживает Expando-подобный доступ через индексатор и типобезопасные методы.
    /// </summary>
    public class DynamicPropertyObject : ICustomTypeDescriptor
    {
        private readonly Dictionary<string, PropertyData> _properties = new Dictionary<string, PropertyData>(StringComparison.OrdinalIgnoreCase.ToStringComparer());

        /// <summary>
        /// Создает пустой объект.
        /// </summary>
        public DynamicPropertyObject()
        { }

        /// <summary>
        /// Создает объект с копированием свойств из другого объекта.
        /// </summary>
        /// <param name="source">Исходный объект для копирования свойств.</param>
        public DynamicPropertyObject(object source)
        {
            var props = TypeDescriptor.GetProperties(source);
            foreach (PropertyDescriptor prop in props)
            {
                AddProperty(prop.Name, prop.PropertyType, prop.GetValue(source));
            }
        }

        /// <summary>
        /// Функция для разрешения редактора по типу свойства.
        /// </summary>
        public Func<Type, Type> EditorResolver { get; set; }

        /// <summary>
        /// Словарь соответствий типов свойства и типов редакторов.
        /// </summary>
        public Dictionary<Type, Type> Editors { get; private set; } = new Dictionary<Type, Type>();

        /// <summary>
        /// Внутренние данные одного свойства.
        /// </summary>
        private class PropertyData
        {
            /// <summary>
            /// Создает данные для свойства.
            /// </summary>
            /// <param name="type">Тип свойства.</param>
            /// <param name="value">Значение свойства.</param>
            public PropertyData(Type type, object value)
            {
                Type = type;
                Value = value;
            }

            /// <summary>
            /// Тип редактора свойства.
            /// </summary>
            public Type EditorType { get; set; }

            /// <summary>
            /// Тип свойства.
            /// </summary>
            public Type Type { get; set; }

            /// <summary>
            /// Значение свойства.
            /// </summary>
            public object Value { get; set; }
        }

        #region Expando-подобный API

        /// <summary>
        /// Индексатор для доступа к свойствам по имени.
        /// </summary>
        /// <example>
        /// obj["Name"] = "John";
        /// var age = obj["Age"];
        /// </example>
        /// <param name="name">Имя свойства.</param>
        public object this[string name]
        {
            get => GetValue(name);
            set
            {
                if (_properties.TryGetValue(name, out var property))
                    property.Value = value;
                else
                    AddProperty(name, value?.GetType() ?? typeof(object), value);
            }
        }

        /// <summary>
        /// Типобезопасный getter значения свойства.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        /// <exception cref="KeyNotFoundException">Выбрасывается, если свойство не найдено.</exception>
        public T GetValue<T>(string name)
        {
            if (_properties.TryGetValue(name, out var p))
            {
                if (p.Value is T t)
                    return t;

                return (T)Convert.ChangeType(p.Value, typeof(T));
            }
            throw new KeyNotFoundException($"Property '{name}' not found.");
        }

        /// <summary>
        /// Типобезопасный setter значения свойства.
        /// </summary>
        /// <typeparam name="T">Тип значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        public void SetValue<T>(string name, T value)
        {
            if (_properties.ContainsKey(name))
            {
                _properties[name].Value = value;
                _properties[name].Type = typeof(T);
            }
            else
            {
                AddProperty(name, typeof(T), value);
            }
        }

        #endregion Expando-подобный API

        #region Динамические свойства

        /// <summary>
        /// Добавляет новое свойство или заменяет существующее.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <param name="type">Тип свойства.</param>
        /// <param name="value">Начальное значение свойства.</param>
        public void AddProperty(string name, Type type, object value = null)
        {
            _properties[name] = new PropertyData(type, value);
        }

        /// <summary>
        /// Очищает все свойства объекта.
        /// </summary>
        public void ClearProperties() => _properties.Clear();

        /// <summary>
        /// Получает тип свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Тип свойства.</returns>
        public Type GetPropertyType(string name) => _properties[name].Type;

        /// <summary>
        /// Получает значение свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        public object GetValue(string name) => _properties[name].Value;

        /// <summary>
        /// Устанавливает значение существующего свойства.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        public void SetValue(string name, object value)
        {
            if (_properties.TryGetValue(name, out var p))
                p.Value = value;
        }

        #endregion Динамические свойства

        #region ICustomTypeDescriptor

        /// <summary>
        /// Возвращает коллекцию атрибутов компонента.
        /// </summary>
        public AttributeCollection GetAttributes() => AttributeCollection.Empty;

        /// <summary>
        /// Возвращает имя класса компонента.
        /// </summary>
        public string GetClassName() => nameof(DynamicPropertyObject);

        /// <summary>
        /// Возвращает имя компонента.
        /// </summary>
        public string GetComponentName() => null;

        /// <summary>
        /// Возвращает конвертер типа компонента.
        /// </summary>
        public TypeConverter GetConverter() => null;

        /// <summary>
        /// Возвращает событие по умолчанию.
        /// </summary>
        public EventDescriptor GetDefaultEvent() => null;

        /// <summary>
        /// Возвращает свойство по умолчанию.
        /// </summary>
        public PropertyDescriptor GetDefaultProperty() => null;

        /// <summary>
        /// Возвращает редактор типа.
        /// </summary>
        /// <param name="editorBaseType">Базовый тип редактора.</param>
        public object GetEditor(Type editorBaseType) => null;

        /// <summary>
        /// Возвращает коллекцию событий компонента с фильтром атрибутов.
        /// </summary>
        /// <param name="attributes">Массив атрибутов.</param>
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => GetEvents();

        /// <summary>
        /// Возвращает коллекцию всех событий компонента.
        /// </summary>
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;

        /// <summary>
        /// Возвращает коллекцию динамических свойств компонента.
        /// </summary>
        public PropertyDescriptorCollection GetProperties()
        {
            return new PropertyDescriptorCollection(
                _properties.Keys.Select(name => new DynamicPropertyDescriptor(name, this))
                    .ToArray<PropertyDescriptor>());
        }

        /// <summary>
        /// Возвращает коллекцию динамических свойств компонента с фильтром атрибутов.
        /// </summary>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => GetProperties();

        /// <summary>
        /// Возвращает владельца свойства.
        /// </summary>
        /// <param name="pd">Описание свойства.</param>
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        /// <summary>
        /// Дескриптор динамического свойства.
        /// </summary>
        public class DynamicPropertyDescriptor : PropertyDescriptor
        {
            private readonly DynamicPropertyObject _owner;

            /// <summary>
            /// Создает дескриптор свойства.
            /// </summary>
            /// <param name="name">Имя свойства.</param>
            /// <param name="owner">Владелец свойства.</param>
            public DynamicPropertyDescriptor(string name, DynamicPropertyObject owner)
                : base(name, null)
            {
                _owner = owner;
            }

            /// <summary>
            /// Словарь редакторов для всех свойств.
            /// </summary>
            public static Dictionary<Type, Type> Editors { get; } = new Dictionary<Type, Type>();

            /// <summary>
            /// Тип компонента.
            /// </summary>
            public override Type ComponentType => typeof(DynamicPropertyObject);

            /// <summary>
            /// Конвертер свойства.
            /// </summary>
            public override TypeConverter Converter =>
                PropertyType.IsEnum ? new EnumConverter(PropertyType) : base.Converter;

            /// <summary>
            /// Признак доступности для изменения.
            /// </summary>
            public override bool IsReadOnly => false;

            /// <summary>
            /// Тип свойства.
            /// </summary>
            public override Type PropertyType => _owner.GetPropertyType(Name);

            /// <summary>
            /// Можно ли сбросить значение свойства.
            /// </summary>
            public override bool CanResetValue(object component) => false;

            /// <summary>
            /// Получение редактора свойства.
            /// </summary>
            /// <param name="editorBaseType">Базовый тип редактора.</param>
            public override object GetEditor(Type editorBaseType)
            {
                if (_owner._properties[Name].EditorType != null)
                    return Activator.CreateInstance(_owner._properties[Name].EditorType);

                if (_owner.Editors.TryGetValue(PropertyType, out var editorType))
                    return Activator.CreateInstance(editorType);

                if (_owner.EditorResolver != null)
                    return Activator.CreateInstance(_owner.EditorResolver(PropertyType));

                return base.GetEditor(editorBaseType);
            }

            /// <summary>
            /// Получение значения свойства.
            /// </summary>
            public override object GetValue(object component) => _owner.GetValue(Name);

            /// <summary>
            /// Сброс значения свойства (не реализован).
            /// </summary>
            public override void ResetValue(object component) { }

            /// <summary>
            /// Установка значения свойства.
            /// </summary>
            public override void SetValue(object component, object value) => _owner.SetValue(Name, value);

            /// <summary>
            /// Нужно ли сериализовать значение свойства.
            /// </summary>
            public override bool ShouldSerializeValue(object component) => true;
        }

        #endregion ICustomTypeDescriptor
    }
}