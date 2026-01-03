using RuntimeStuff;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace RuntimeStuff
{
    public sealed class PropertyMapBuilder<T, TProperty>
    {
        private readonly PropertyMapping _propertyMapping;
        private readonly EntityMapBuilder<T> _entityMapBuilder;

        internal PropertyMapBuilder(EntityMapBuilder<T> entityMapBuilder, PropertyInfo propertyInfo)
        {
            _entityMapBuilder = entityMapBuilder;
            _propertyMapping = new PropertyMapping(propertyInfo);
        }

        public PropertyMapBuilder<T, TProperty> HasColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException(nameof(columnName));

            _propertyMapping.ColumnName = columnName;
            MapColumnName(_propertyMapping.Property, columnName);
            return this;
        }

        public PropertyMapBuilder<T, TProperty> HasAlias(string alias)
        {
            _propertyMapping.Alias = alias;
            MapAlias(_propertyMapping.Property, alias);
            return this;
        }

        public PropertyMapBuilder<T, TNewProperty> Property<TNewProperty>(Expression<Func<T, TNewProperty>> selector, string columnName, string alias = null)
        {
            var property = EntityMapBuilder<T>.GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TNewProperty>(_entityMapBuilder, property);
            pb.HasColumn(columnName);
            pb.HasAlias(alias);
            return pb;
        }

        public EntityMapBuilder<TTable> Table<TTable>(string tableName) where TTable : class
        {
            return new EntityMapBuilder<TTable>(_entityMapBuilder._map, tableName);
        }

        internal void MapColumnName(PropertyInfo property, string columnName)
        {
            var propMapping = GetOrAdd(property);
            propMapping.ColumnName = columnName;
        }

        internal void MapAlias(PropertyInfo property, string alias)
        {
            var propMapping = GetOrAdd(property);
            propMapping.Alias = alias;
        }

        private PropertyMapping GetOrAdd(PropertyInfo property)
        {
            if (!_entityMapBuilder._entityMapping.PropertyColumns.TryGetValue(property, out var propMapping))
            {
                propMapping = new PropertyMapping(property);
                _entityMapBuilder._entityMapping.PropertyColumns[property] = propMapping;
            }
            return propMapping;
        }
    }
}
