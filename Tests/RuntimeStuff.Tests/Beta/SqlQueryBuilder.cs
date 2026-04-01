namespace System.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    public class SqlQueryBuilder
    {
        private readonly List<QueryPart> queryParts = new List<QueryPart>();
        private readonly List<string> aliasesInUse = new List<string>();
        private readonly SqlProviderOptions providerOptions = SqlProviderOptions.SqlServerOptions;

        public bool IndentFormat { get; set; }

        public bool UseAliases { get; set; }

        public bool UseFullNames { get; set; }

        public SqlQueryBuilder()
        {
        }

        public SqlQueryBuilder(SqlProviderOptions providerOptions)
        {
            this.providerOptions = providerOptions;
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

        public SqlQueryBuilder Delete(Type type)
        {
            return this.Add(QueryPartType.Delete, type);
        }

        public SqlQueryBuilder Delete<T>()
        {
            return this.Add(QueryPartType.Delete, typeof(T));
        }

        public SqlQueryBuilder Where<T>(Expression<Func<T, object>> propertySelector, SqlOperator sqlOperator, params object[] args)
        {
            return this.Where(true, propertySelector.GetPropertyInfo(), sqlOperator, args);
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

        public SqlQueryBuilder Where(bool and, PropertyInfo property, SqlOperator sqlOperator, params object[] args)
        {
            if (this.queryParts.Count(x => x.QueryPartType == QueryPartType.Where) == 0)
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
            this.queryParts.Add(new QueryPart(this, queryPartType, args));
            return this;
        }

        public override string ToString()
        {
            return this.GetQuery();
        }

        public string GetQuery()
        {
            var sb = new StringBuilder();

            this.BuildSelect(sb);

            this.BuildWhere(sb);

            return sb.ToString();
        }

        private void BuildSelect(StringBuilder sb)
        {
            var selectParts = this.queryParts.Where(x => x.QueryPartType == QueryPartType.Select).ToArray();

            if (selectParts.Length > 0)
            {
                var from = (selectParts[0].Params[ParamType.Column] as PropertyInfo)?.GetMemberCache();
                sb.Append(SqlQueryHelper.GetSelectQuery(this.providerOptions, this.UseFullNames, from, selectParts.Select(x => (x.Params[ParamType.Column] as PropertyInfo)?.GetMemberCache()).ToArray()));

                //sb.Append("SELECT");
                //sb.Append(this.Format());
                //sb.Append(this.Format(1));
                //for (int i = 0; i < selectParts.Length; i++)
                //{
                //    var pi = selectParts[i].Params[ParamType.Column] as PropertyInfo;
                //    var mc = pi.GetMemberCache();
                //    sb.Append(mc.GetColumnName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix, this.UseFullNames));
                //    if (i < selectParts.Length - 1)
                //    {
                //        sb.Append(", ");
                //    }
                //}

                //var mc0 = (selectParts[0].Params[ParamType.Column] as PropertyInfo)?.GetMemberCache();
                //sb.Append(" FROM");
                //sb.Append(this.Format());
                //sb.Append(this.Format(1));
                //sb.Append(mc0.GetTableName(this.providerOptions.NamePrefix, this.providerOptions.NameSuffix));
            }
        }

        private void BuildWhere(StringBuilder sb)
        {
            var whereParts = this.queryParts.Where(x => x.QueryPartType == QueryPartType.Where).ToArray();

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

        private string Format(int tabsCount = 0)
        {
            var f = this.IndentFormat ? Environment.NewLine : (tabsCount > 0 ? string.Empty : " ");
            if (tabsCount > 0 && this.IndentFormat)
            {
                f = (new string('\t', tabsCount) + f).Trim();
            }

            return f;
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

        public enum ParamType
        {
            Column,
            Operator,
            Values,
            Prefix,
            Suffix,
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

            public QueryPartType QueryPartType { get; }

            public DefaultDictionary<ParamType, object> Params { get; set; } = new DefaultDictionary<ParamType, object>();
        }
    }
}
