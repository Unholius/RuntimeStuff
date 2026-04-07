namespace RuntimeStuff.MSTests.Beta
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Helpers;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    public class SqlQueryBuilder
    {
        private readonly Dictionary<string, MemberCache> aliases = new DefaultDictionary<string, MemberCache>(StringComparer.OrdinalIgnoreCase);
        private readonly SqlDialect sqlDialect = SqlDialect.SqlServerDialect;
        private readonly List<QueryPart> query = new List<QueryPart>();
        private readonly StringBuilder sb = new StringBuilder();

        public SqlQueryBuilder()
        {
        }

        public SqlQueryBuilder(SqlDialect providerOptions)
        {
            this.sqlDialect = providerOptions;
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
        public bool UseAsKeywordInAliases { get; set; } = true;
        public bool UseFullNames { get; set; }

        public StringHelper.StringCase AliasStringCase { get; set; } = StringHelper.StringCase.Pascal;
        public StringHelper.StringCase ColumnStringCase { get; set; } = StringHelper.StringCase.Pascal;
        public StringHelper.StringCase TableStringCase { get; set; } = StringHelper.StringCase.Pascal;


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
            foreach (PropertyInfo property in properties)
            {
                var mc = (MemberCache)property;
                if (mc.IsBasic)
                {
                    query.Add(new QueryPart(this, queryPartType, property));
                }

                if (mc.IsObject)
                {
                    query.Add(new QueryPart(this, queryPartType, mc.PropertyType));
                }

                if (mc.IsCollection)
                {
                    query.Add(new QueryPart(this, queryPartType, mc.ElementType));
                }
            }

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
            this.aliases[alias] = member.GetMemberCache();
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
            throw new NotImplementedException();
        }

        public SqlQueryBuilder WhereGroup(bool begin)
        {
            throw new NotImplementedException();
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
                    Format(sb, selectParts[i].Member);
                    if (i < selectParts.Length - 1)
                    {
                        sb.Append(", ");
                    }
                }

                sb.Append(" FROM");
                sb.Append(this.Format());
                sb.Append(this.Format(1));
                sb.Append(selectParts[0].Member.GetTableName(this.sqlDialect.NamePrefix, this.sqlDialect.NameSuffix));
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
                sb.Append(member.GetColumnName(this.sqlDialect.NamePrefix, this.sqlDialect.NameSuffix, fullName.Value));
            }
            else
            {
                if (member.IsType)
                {
                    sb.Append(member.GetTableName(this.sqlDialect.NamePrefix, this.sqlDialect.NameSuffix));
                }
                else
                {
                    return;
                }
            }

            if (useAliases.Value)
            {
                if (UseAsKeywordInAliases)
                {
                    sb.Append(" AS ");
                }
                else
                {
                    sb.Append(' ');
                }
                sb.Append(this.sqlDialect.NamePrefix);
                sb.Append(GetAlias(member));
                sb.Append(this.sqlDialect.NameSuffix);
            }
        }

        private string GetAlias(MemberCache member)
        {
            var alias = member.ColumnName;
            var i = 0;
            while (aliases.ContainsKey(alias))
            {
                switch (i)
                {
                    case 0:
                        alias = StringHelper.ConvertCase(member.TableName + member.ColumnName, AliasStringCase);
                        break;

                    default:
                        alias = StringHelper.ConvertCase($"{member.ColumnName}{i + 1}", AliasStringCase);
                        break;
                }

                i++;
            }

            aliases[alias] = member;
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

        internal sealed class QueryPart
        {
            private readonly SqlQueryBuilder sqlQueryBuilder;

            public QueryPart(SqlQueryBuilder sqlQueryBuilder, QueryPartType queryPartType, MemberCache member)
            {
                this.sqlQueryBuilder = sqlQueryBuilder;
                this.QueryPartType = queryPartType;
                this.Member = member;
                
            }

            public QueryPart(SqlQueryBuilder sqlQueryBuilder, QueryPartType queryPartType, string token)
            {
                this.sqlQueryBuilder = sqlQueryBuilder;
                this.QueryPartType = queryPartType;
                this.Token = token;
            }

            public MemberCache Member { get; }

            public string Token { get; }

            public QueryPartType QueryPartType { get; }
        }
    }
}