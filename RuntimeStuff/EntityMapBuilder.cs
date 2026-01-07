namespace RuntimeStuff
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public sealed class EntityMapBuilder<T>
    {
        internal readonly EntityMapping _entityMapping;

        internal readonly EntityMap _map;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}"/> class.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="tableName"></param>
        internal EntityMapBuilder(EntityMap map, string tableName)
            : this(map, new EntityMapping(typeof(T)))
        {
            this.Table(tableName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}"/> class.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="mapping"></param>
        internal EntityMapBuilder(EntityMap map, EntityMapping mapping)
        {
            this._map = map;
            this._entityMapping = mapping;
            this._map._entityMapping[typeof(T)] = this._entityMapping;
        }

        public EntityMapBuilder<T> Table(string tableName, string schema = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException(nameof(tableName));
            }

            this._entityMapping.TableName = tableName;
            this._entityMapping.Schema = schema;
            return this;
        }

        public PropertyMapBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            var property = GetProperty(selector);
            return new PropertyMapBuilder<T, TProperty>(this, property);
        }

        public PropertyMapBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> selector, string columnName)
        {
            var property = GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TProperty>(this, property);
            pb.HasColumn(columnName);
            return pb;
        }

        public static PropertyInfo GetProperty<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            if (!(selector.Body is MemberExpression member))
            {
                throw new ArgumentException("Expression must be a property.");
            }

            if (!(member.Member is PropertyInfo property))
            {
                throw new ArgumentException("Member is not a property.");
            }

            return property;
        }

        internal void MapTableName(string tableName) => this._entityMapping.TableName = tableName;
    }
}