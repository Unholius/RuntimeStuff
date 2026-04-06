namespace RuntimeStuff.MSTests.Beta
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    public class SqlQueryBuilder
    {
        private readonly Dictionary<MemberCache, string> aliases = new DefaultDictionary<MemberCache, string>();
        private readonly SqlProviderOptions providerOptions = SqlProviderOptions.SqlServerOptions;
        private readonly List<QueryPart> query = new List<QueryPart>();

        public SqlQueryBuilder()
        {
        }

        public SqlQueryBuilder(SqlProviderOptions providerOptions)
        {
            this.providerOptions = providerOptions;
        }

        public enum ParamType
        {
            Column,
            Operator,
            Values,
            Prefix,
            Suffix,
        }

        public enum QueryPartType
        {
            Select,
            Update,
            Insert,
            Delete,
            Join,
            InnerJoin,
            LeftJoin,
            RightJoin,
            OuterJoin,
            CrossJoin,
            Where,
            OrderByAsc,
            OrderByDesc,
            GroupBy,
            Pagination,
            Union,
        }

        public enum SqlOperator
        {
            [Description("=")]
            Equal,

            [Description("<>")]
            NotEqual,

            [Description(">")]
            Greater,

            [Description(">=")]
            GreaterOrEqual,

            [Description("<")]
            Less,

            [Description("<=")]
            LessOrEqual,

            [Description("NOT")]
            Not,

            [Description("IN")]
            In,

            [Description("BETWEEN")]
            Between,
        }

        public bool IndentFormat { get; set; }

        public bool UseAliases { get; set; }

        public bool UseFullNames { get; set; }

        public SqlQueryBuilder Add<T>(QueryPartType queryPartType, params Expression<Func<T, object>>[] propertySelectors)
            where T : class
        {
            return this.Add(QueryPartType.Select, propertySelectors.Select(x => x.GetPropertyInfo()).ToArray());
        }

        public SqlQueryBuilder Add(QueryPartType queryPartType, Type type)
        {
            return this.Add(queryPartType, type.GetMemberCache().PublicBasicProperties.Select(x => (PropertyInfo)x).ToArray());
        }

        public SqlQueryBuilder Add(QueryPartType queryPartType, params PropertyInfo[] properties)
        {
            foreach (var property in properties)
            {
                var mc = (MemberCache)property;
                if (mc.IsBasic)
                {
                    this.Add(queryPartType, (ParamType.Column, property));
                }

                if (mc.IsObject)
                {
                    this.Add(queryPartType, mc.PropertyType);
                }

                if (mc.IsCollection)
                {
                    this.Add(queryPartType, mc.ElementType);
                }
            }

            return this;
        }

        public SqlQueryBuilder Add(QueryPartType queryPartType, params (ParamType, object)[] args)
        {
            this.query.Add(new QueryPart(this, queryPartType, args));
            return this;
        }

        public SqlQueryBuilder ClearAliases()
        {
            aliases.Clear();
            return this;
        }

        public SqlQueryBuilder ClearQuery()
        {
            query.Clear();
            return this;
        }

        public SqlQueryBuilder Delete(Type type)
        {
            return this.Add(QueryPartType.Delete, type);
        }

        public SqlQueryBuilder Delete<T>()
        {
            return this.Add(QueryPartType.Delete, typeof(T));
        }

        public string GetQuery()
        {
            var sb = new StringBuilder();

            this.BuildSelect(sb);

            this.BuildWhere(sb);

            return sb.ToString();
        }

        public SqlQueryBuilder Insert(Type type)
        {
            return this.Add(QueryPartType.Insert, type);
        }

        public SqlQueryBuilder Insert(params PropertyInfo[] properties)
        {
            return this.Add(QueryPartType.Insert, properties);
        }

        public SqlQueryBuilder Insert<T>(params Expression<Func<T, object>>[] propertySelectors)
            where T : class
        {
            return this.Add<T>(QueryPartType.Insert, propertySelectors);
        }

        public SqlQueryBuilder Select(Type type)
        {
            return this.Add(QueryPartType.Select, type);
        }

        public SqlQueryBuilder Select(params PropertyInfo[] properties)
        {
            return this.Add(QueryPartType.Select, properties);
        }

        public SqlQueryBuilder Select<T>(params Expression<Func<T, object>>[] propertySelectors)
            where T : class
        {
            return this.Add<T>(QueryPartType.Select, propertySelectors);
        }

        public SqlQueryBuilder SetAlias<T>(Expression<Func<T, object>> propertySelector, string alias)
        {
            return SetAlias(propertySelector.GetPropertyInfo(), alias);
        }

        public SqlQueryBuilder SetAlias(MemberInfo member, string alias)
        {
            this.aliases[member.GetMemberCache()] = alias;
            return this;
        }

        public override string ToString()
        {
            return this.GetQuery();
        }

        public SqlQueryBuilder Update(Type type)
        {
            return this.Add(QueryPartType.Update, type);
        }

        public SqlQueryBuilder Update(params PropertyInfo[] properties)
        {
            return this.Add(QueryPartType.Update, properties);
        }

        public SqlQueryBuilder Update<T>(params Expression<Func<T, object>>[] propertySelectors)
            where T : class
        {
            return this.Add<T>(QueryPartType.Update, propertySelectors);
        }

        public SqlQueryBuilder Where<T>(Expression<Func<T, object>> propertySelector, SqlOperator sqlOperator, params object[] args)
        {
            return this.Where(true, propertySelector.GetPropertyInfo(), sqlOperator, args);
        }

        public SqlQueryBuilder Where(bool and, PropertyInfo property, SqlOperator sqlOperator, params object[] args)
        {
            if (this.query.Count(x => x.QueryPartType == QueryPartType.Where) == 0)
            {
                this.Add(
                    QueryPartType.Where,
                    (ParamType.Prefix, and ? " AND " : " OR "));
            }

            return this.Add(
                QueryPartType.Where,
                (ParamType.Column, property),
                (ParamType.Operator, sqlOperator),
                (ParamType.Values, args));
        }

        public SqlQueryBuilder WhereGroup(bool begin)
        {
            if (begin)
            {
                this.Add(QueryPartType.Where, (ParamType.Prefix, "( "));
            }
            else
            {
                this.Add(QueryPartType.Where, (ParamType.Suffix, " )"));
            }

            return this;
        }

        private void BuildSelect(StringBuilder sb)
        {
            var selectParts = this.query.Where(x => x.QueryPartType == QueryPartType.Select).ToArray();

            if (selectParts.Length > 0)
            {
                sb.Append("SELECT");
                sb.Append(this.Format());
                sb.Append(this.Format(1));
                for (int i = 0; i < selectParts.Length; i++)
                {
                    var pi = selectParts[i].Params[ParamType.Column] as PropertyInfo;
                    var mc = pi.GetMemberCache();
                    Format(sb, mc); //mc.GetColumnName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix, this.UseFullNames));
                    if (i < selectParts.Length - 1)
                    {
                        sb.Append(", ");
                    }
                }

                var mc0 = (selectParts[0].Params[ParamType.Column] as PropertyInfo)?.GetMemberCache();
                sb.Append(" FROM");
                sb.Append(this.Format());
                sb.Append(this.Format(1));
                sb.Append(mc0.GetTableName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix));
            }
        }

        private void BuildWhere(StringBuilder sb)
        {
            var whereParts = this.query.Where(x => x.QueryPartType == QueryPartType.Where).ToArray();

            if (whereParts.Length > 0)
            {
                sb.Append(this.Format());
                sb.Append("WHERE");
                sb.Append(this.Format());
                sb.Append(this.Format(1));

                for (int i = 0; i < whereParts.Length; i++)
                {
                    this.Prefix(sb, whereParts[i], i);
                    var pi = whereParts[i].Params[ParamType.Column] as PropertyInfo;
                    if (pi != null)
                    {
                        var mc = pi.GetMemberCache();
                        sb.Append(mc.GetColumnName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix, this.UseFullNames));
                        sb.Append(' ');
                        sb.Append(((SqlOperator)whereParts[i].Params[ParamType.Operator]).GetDescription());
                        sb.Append(' ');
                        sb.Append(this.providerOptions.ValueFormatter.Format(whereParts[i].Params[ParamType.Values]));
                    }

                    this.Suffix(sb, whereParts[i], i);
                }
            }
        }

        private string Format(int tabsCount = 0)
        {
            var f = this.IndentFormat ? Environment.NewLine : (tabsCount > 0 ? string.Empty : " ");
            if (tabsCount > 0 && this.IndentFormat)
            {
                f = (new string('\t', tabsCount) + f).Trim();
            }

            return f;
        }

        private void Format(StringBuilder sb, MemberCache member, bool? useAliases = null, bool? fullName = null)
        {
            if (useAliases == null)
            {
                useAliases = this.UseAliases;
            }

            if (fullName == null)
            {
                fullName = this.UseFullNames;
            }

            if (member.IsProperty)
            {
                sb.Append(member.GetColumnName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix, fullName.Value));
            }
            else
            {
                if (member.IsType)
                {
                    sb.Append(member.GetTableName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix));
                }
                else
                {
                    return;
                }
            }

            if (useAliases.Value)
            {
                sb.Append(" AS ");
                sb.Append(this.providerOptions.NamePrefix);
                sb.Append(GetAlias(member));
                sb.Append(this.providerOptions.NameSuffix);
            }
        }

        private string GetAlias(MemberCache member)
        {
            if (this.aliases.TryGetValue(member, out var alias))
            {
                return alias;
            }

            var names = query
                .Where(x => x.QueryPartType == QueryPartType.Select)
                .Select(x => (x.Params[ParamType.Column] as MemberCache)?.ColumnName)
                .Concat(aliases.Values).ToArray();

            alias = member.ColumnName;
            var i = 0;
            while (names.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                switch (i)
                {
                    case 0:
                        alias = member.TableName + member.ColumnName;
                        break;

                    default:
                        alias = $"{member.ColumnName}_{i}";
                        break;
                }

                i++;
            }

            aliases[member] = alias;
            return alias;
        }

        private SqlOperator GetOperator(Expression e)
        {
            if (e == null)
            {
                throw new ArgumentNullException("e");
            }

            if (!(e is BinaryExpression be))
            {
                throw new Exception("Выражение должно быть BinaryExpression!");
            }

            switch (be.NodeType)
            {
                case ExpressionType.Equal: return SqlOperator.Equal;
                case ExpressionType.NotEqual: return SqlOperator.NotEqual;
                case ExpressionType.GreaterThan: return SqlOperator.Greater;
                case ExpressionType.GreaterThanOrEqual: return SqlOperator.GreaterOrEqual;
                case ExpressionType.LessThan: return SqlOperator.Less;
                case ExpressionType.LessThanOrEqual: return SqlOperator.LessOrEqual;
                default: throw new NotImplementedException();
            }
        }

        private void Prefix(StringBuilder sb, QueryPart qp, int index)
        {
            var s = qp.Params[ParamType.Prefix];
            if (s != null)
            {
                sb.Append($"{s}");
            }
        }

        private void Suffix(StringBuilder sb, QueryPart qp, int index)
        {
            var s = qp.Params[ParamType.Suffix];
            if (s != null)
            {
                sb.Append($"{s}");
            }
        }

        internal sealed class QueryPart
        {
            private readonly SqlQueryBuilder sqlQueryBuilder;

            public QueryPart(SqlQueryBuilder sqlQueryBuilder, QueryPartType queryPartType, params (ParamType, object)[] args)
            {
                this.sqlQueryBuilder = sqlQueryBuilder;
                this.QueryPartType = queryPartType;
                if (args != null && args.Length > 0)
                {
                    this.Params = args.ToDefaultDictionary(x => x.Item1, x => x.Item2);
                }
            }

            public DefaultDictionary<ParamType, object> Params { get; set; } = new DefaultDictionary<ParamType, object>();
            public QueryPartType QueryPartType { get; }
        }
    }
}