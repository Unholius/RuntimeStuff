using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RuntimeStuff
{
    /// <summary>
    ///     Динамический объект с возможностью добавлять свойства во время выполнения.
    ///     Поддерживает Expando-подобный доступ через индексатор и типобезопасные методы.
    /// </summary>
    public class DynamicPropertyObject : ICustomTypeDescriptor
    {
        private readonly Dictionary<string, PropertyData> _properties = new Dictionary<string, PropertyData>(StringComparer.Ordinal);

        public Dictionary<Type, Type> Editors { get; private set; } = new Dictionary<Type, Type>();

        /// <summary>
        /// Функция для разрешения редактора по типу свойства.
        /// </summary>
        public Func<Type, Type> EditorResolver { get; set; }

        /// <summary>
        ///     Внутренние данные одного свойства.
        /// </summary>
        private class PropertyData
        {
            public Type Type { get; set; }
            public object Value { get; set; }
            public Type EditorType { get; set; }

            public PropertyData(Type type, object value)
            {
                Type = type;
                Value = value;
            }
        }

        /// <summary>
        ///     Создает пустой объект.
        /// </summary>
        public DynamicPropertyObject() { }

        /// <summary>
        ///     Создает объект с копированием свойств из другого объекта.
        /// </summary>
        public DynamicPropertyObject(object source)
        {
            var props = TypeDescriptor.GetProperties(source);
            foreach (PropertyDescriptor prop in props)
            {
                AddProperty(prop.Name, prop.PropertyType, prop.GetValue(source));
            }
        }

        #region Expando-подобный API

        /// <summary>
        ///     Индексатор для доступа к свойствам по имени.
        /// </summary>
        /// <example>
        ///     obj["Name"] = "John";
        ///     var age = obj["Age"];
        /// </example>
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
        ///     Типобезопасный setter.
        /// </summary>
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

        /// <summary>
        ///     Типобезопасный getter.
        /// </summary>
        public T GetValue<T>(string name)
        {
            if (_properties.TryGetValue(name, out var p))
            {
                if (p.Value is T t)
                    return t;
                // попытка преобразовать
                return (T)Convert.ChangeType(p.Value, typeof(T));
            }
            throw new KeyNotFoundException($"Property '{name}' not found.");
        }

        #endregion

        #region Динамические свойства

        public void AddProperty(string name, Type type, object value = null)
        {
            _properties[name] = new PropertyData(type, value);
        }

        public object GetValue(string name) => _properties[name].Value;

        public void SetValue(string name, object value)
        {
            if (_properties.TryGetValue(name, out var p))
                p.Value = value;
        }

        public Type GetPropertyType(string name) => _properties[name].Type;

        public void ClearProperties() => _properties.Clear();

        #endregion

        #region ICustomTypeDescriptor

        public PropertyDescriptorCollection GetProperties()
        {
            return new PropertyDescriptorCollection(
                _properties.Keys.Select(name => new DynamicPropertyDescriptor(name, this))
                    .ToArray<PropertyDescriptor>());
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) =>
            GetProperties();

        public AttributeCollection GetAttributes() => AttributeCollection.Empty;
        public string GetClassName() => nameof(DynamicPropertyObject);
        public string GetComponentName() => null;
        public TypeConverter GetConverter() => null;
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => GetEvents();
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        public class DynamicPropertyDescriptor : PropertyDescriptor
        {
            private readonly DynamicPropertyObject _owner;
            public static Dictionary<Type, Type> Editors { get; } = new Dictionary<Type, Type>();

            public DynamicPropertyDescriptor(string name, DynamicPropertyObject owner)
                : base(name, null)
            {
                _owner = owner;
            }

            public override Type PropertyType => _owner.GetPropertyType(Name);
            public override bool IsReadOnly => false;
            public override Type ComponentType => typeof(DynamicPropertyObject);
            public override TypeConverter Converter =>
                PropertyType.IsEnum ? new EnumConverter(PropertyType) : base.Converter;

            public override void SetValue(object component, object value) =>
                _owner.SetValue(Name, value);

            public override object GetValue(object component) =>
                _owner.GetValue(Name);

            public override bool CanResetValue(object component) => false;
            public override void ResetValue(object component) { }
            public override bool ShouldSerializeValue(object component) => true;

            public override object GetEditor(Type editorBaseType)
            {
                // 1. Сначала проверяем локальный Editor конкретного свойства
                if (_owner._properties[Name].EditorType != null)
                    return Activator.CreateInstance(_owner._properties[Name].EditorType);

                // 2. Потом проверяем редактор на уровне объекта
                if (_owner.Editors.TryGetValue(PropertyType, out var editorType))
                    return Activator.CreateInstance(editorType);

                if (_owner.EditorResolver != null)
                    return Activator.CreateInstance(_owner.EditorResolver(PropertyType));

                return base.GetEditor(editorBaseType);
            }
        }

        #endregion
    }
}
