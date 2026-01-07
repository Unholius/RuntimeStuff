namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public sealed class EntityMapping
    {
        public Type EntityType { get; }

        public string TableName { get; internal set; }

        public string Schema { get; internal set; }

        public IDictionary<PropertyInfo, PropertyMapping> PropertyColumns => this._propertyColumns;

        internal readonly Dictionary<PropertyInfo, PropertyMapping> _propertyColumns;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapping"/> class.
        /// </summary>
        /// <param name="entityType"></param>
        internal EntityMapping(Type entityType)
        {
            this.EntityType = entityType;
            this._propertyColumns = new Dictionary<PropertyInfo, PropertyMapping>();
        }
    }

    public sealed class PropertyMapping
    {
        public PropertyInfo Property { get; }

        public string ColumnName { get; set; }

        public string Alias { get; set; }

        public string Function { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMapping"/> class.
        /// </summary>
        /// <param name="property"></param>
        public PropertyMapping(PropertyInfo property)
        {
            this.Property = property;
        }
    }
}