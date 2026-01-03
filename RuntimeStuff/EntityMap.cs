using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RuntimeStuff
{
    public class EntityMap
    {
        internal readonly Dictionary<Type, EntityMapping> _entityMapping = new Dictionary<Type, EntityMapping>();
        public EntityMapBuilder<T> Table<T>() where T: class
        {
            return new EntityMapBuilder<T>(this, GetOrAdd(typeof(T)));
        }

        private EntityMapping GetOrAdd(Type type)
        {
            if (!_entityMapping.TryGetValue(type, out var typeProps))
            {
                typeProps = new EntityMapping(type);
                _entityMapping.Add(type, typeProps);
            }

            return typeProps;
        }

        public EntityMapBuilder<T> Table<T>(string tableName) where T : class
        {
            var entityMapping = GetOrAdd(typeof(T));
            var builder = new EntityMapBuilder<T>(this, entityMapping);
            builder.MapTableName(tableName);
            return builder;
        }

        public string ResolveTableName(Type type, string namePrefix, string nameSuffix)
        {
            return _entityMapping.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.TableName}{nameSuffix}" : null;
        }

        public string ResolveSchemaName(Type type, string namePrefix, string nameSuffix)
        {
            return _entityMapping.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.Schema}{nameSuffix}" : null;
        }

        public string ResolveColumnName(PropertyInfo property, string namePrefix, string nameSuffix)
        {
            return _entityMapping.TryGetValue(property.DeclaringType, out var typeMapping) ? (typeMapping.PropertyColumns.TryGetValue(property, out var propertyMapping) ? $"{namePrefix}{propertyMapping.ColumnName}{nameSuffix}" : null) : null;
        }

        public Type ResolveType(string tableName)
        {
            return _entityMapping.FirstOrDefault(x => x.Value.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)).Key;
        }

        public PropertyInfo ResolveProperty(Type type, string columnName)
        {
            if (!_entityMapping.TryGetValue(type, out var typeMapping))
                return null;

            return typeMapping.PropertyColumns.FirstOrDefault(x => x.Value.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)).Key;
        }

        public IEnumerable<(string ColumnName, string PropertyName)> GetColumnToPropertyMap(Type type)
        {
            if (!_entityMapping.TryGetValue(type, out var typeMapping))
                return null;

            return typeMapping.PropertyColumns.Select(x => (x.Value.ColumnName, x.Value.Property.Name)).ToArray();
        }

        public IEnumerable<(string PropertyName, string ColumnName)> GetPropertyToColumnMap(Type type)
        {
            if (!_entityMapping.TryGetValue(type, out var typeMapping))
                return null;

            return typeMapping.PropertyColumns.Select(x => (x.Value.Property.Name, x.Value.ColumnName)).ToArray();
        }
    }
}
