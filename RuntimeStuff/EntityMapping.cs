using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace RuntimeStuff
{
    public sealed class EntityMapping
    {
        public Type EntityType { get; }
        public string TableName { get; internal set; }
        public string Schema { get; internal set; }

        public IDictionary<PropertyInfo, PropertyMapping> PropertyColumns => _propertyColumns;

        internal readonly Dictionary<PropertyInfo, PropertyMapping> _propertyColumns;

        internal EntityMapping(Type entityType)
        {
            EntityType = entityType;
            _propertyColumns = new Dictionary<PropertyInfo, PropertyMapping>();
        }
    }

    public sealed class PropertyMapping
    {
        public PropertyInfo Property { get; }
        public string ColumnName { get; set; }
        public string Alias { get; set; }
        public string Function { get; set; }
        public PropertyMapping(PropertyInfo property) 
        {
            Property = property;
        }
    }
}
