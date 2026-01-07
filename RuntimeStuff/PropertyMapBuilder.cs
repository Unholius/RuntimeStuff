namespace RuntimeStuff
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public sealed class PropertyMapBuilder<T, TProperty>
    {
        private readonly PropertyMapping _propertyMapping;
        private readonly EntityMapBuilder<T> _entityMapBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMapBuilder{T, TProperty}"/> class.
        /// </summary>
        /// <param name="entityMapBuilder"></param>
        /// <param name="propertyInfo"></param>
        internal PropertyMapBuilder(EntityMapBuilder<T> entityMapBuilder, PropertyInfo propertyInfo)
        {
            this._entityMapBuilder = entityMapBuilder;
            this._propertyMapping = new PropertyMapping(propertyInfo);
        }

        public PropertyMapBuilder<T, TProperty> HasColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(nameof(columnName));
            }

            this._propertyMapping.ColumnName = columnName;
            this.MapColumnName(this._propertyMapping.Property, columnName);
            return this;
        }

        public PropertyMapBuilder<T, TProperty> HasAlias(string alias)
        {
            this._propertyMapping.Alias = alias;
            this.MapAlias(this._propertyMapping.Property, alias);
            return this;
        }

        public PropertyMapBuilder<T, TNewProperty> Property<TNewProperty>(Expression<Func<T, TNewProperty>> selector, string columnName, string alias = null)
        {
            var property = EntityMapBuilder<T>.GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TNewProperty>(this._entityMapBuilder, property);
            pb.HasColumn(columnName);
            pb.HasAlias(alias);
            return pb;
        }

        public EntityMapBuilder<TTable> Table<TTable>(string tableName) where TTable : class => new EntityMapBuilder<TTable>(this._entityMapBuilder._map, tableName);

        internal void MapColumnName(PropertyInfo property, string columnName)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.ColumnName = columnName;
        }

        internal void MapAlias(PropertyInfo property, string alias)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.Alias = alias;
        }

        private PropertyMapping GetOrAdd(PropertyInfo property)
        {
            if (!this._entityMapBuilder._entityMapping.PropertyColumns.TryGetValue(property, out var propMapping))
            {
                propMapping = new PropertyMapping(property);
                this._entityMapBuilder._entityMapping.PropertyColumns[property] = propMapping;
            }

            return propMapping;
        }
    }
}