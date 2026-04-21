namespace RuntimeStuff.MSTests.Beta
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Helpers;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    public partial class SqlQueryBuilder
    {
        private readonly SqlOptions options = SqlOptions.SqlServer;
        private readonly List<QueryPart> query = [];
        private readonly StringBuilder sb = new StringBuilder();

        public SqlQueryBuilder()
        {
        }

        public SqlQueryBuilder(SqlOptions providerOptions)
        {
            this.options = providerOptions;
        }

        [Flags]
        public enum QueryPartFlag
        {
            None = 0,
            Select = 1 << 0,
            Update = 1 << 1,
            Insert = 1 << 2,
            Delete = 1 << 3,
            Join = 1 << 4,
            InnerJoin = 1 << 5,
            LeftJoin = 1 << 6,
            RightJoin = 1 << 7,
            OuterJoin = 1 << 8,
            CrossJoin = 1 << 9,
            Where = 1 << 10,
            OrderByAsc = 1 << 11,
            OrderByDesc = 1 << 12,
            GroupBy = 1 << 13,
            Pagination = 1 << 14,
            Union = 1 << 15,
            BeginGroup = 1 << 16,
            EndGroup = 1 << 17,
            Table = 1 << 18,
            Column = 1 << 19,
            From = 1 << 20,
            Operator = 1 << 21,
            Parameter = 1 << 22,
        }

        public enum DuplicateNameHandling
        {
            ThrowException,
            GenerateAlias,
            Ignore,
        }

        public bool IndentFormat { get; set; }
        public DuplicateNameHandling DuplicateColumnNameHandling { get; set; } = DuplicateNameHandling.ThrowException;
        public DuplicateNameHandling EmptyColumnNameHandling { get; set; } = DuplicateNameHandling.ThrowException;
        public bool UseAliases { get; set; }
        public bool UseAsKeywordInAliases { get; set; } = true;
        public bool UseFullNames { get; set; }
        public StringHelper.StringCase AliasNameCase { get; set; } = StringHelper.StringCase.Pascal;
        public StringHelper.StringCase ColumnNameCase { get; set; } = StringHelper.StringCase.Pascal;
        public StringHelper.StringCase TableNameCase { get; set; } = StringHelper.StringCase.Pascal;
        public string SetParamNameTemplate = "set_{0}";
        public string WhereParamNameTemplate = "{0}";
        public bool UseParams;

        public SqlQueryBuilder Select(string columnName, string? alias = null)
        {
            AddColumn(QueryPartFlag.Select, columnName, alias);
            return this;
        }

        public SqlQueryBuilder SelectMany(params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                Select(columnName);
            }
            return this;
        }

        public SqlQueryBuilder From(string tableName, string? alias = null)
        {
            return From(null, tableName, alias);
        }

        public SqlQueryBuilder From(string? schemaName, string tableName, string? alias = null)
        {
            return From(null, null, schemaName, tableName, alias);
        }

        public SqlQueryBuilder From(string? serverName, string? databaseName, string? schemaName, string tableName, string? alias = null)
        {
            AddTable(QueryPartFlag.From, serverName, databaseName, schemaName, tableName, alias);
            return this;
        }

        public SqlQueryBuilder InnerJoin(string childTableName, string childAlias, string childColumnName, string parentAlias, string parentColumnName)
        {
            return Join(JoinType.Inner, null, null, null, childTableName, childAlias, childColumnName, SqlOperator.Equal, parentAlias, parentColumnName);
        }

        public SqlQueryBuilder Join(JoinType joinType, string? serverName, string? databaseName, string? schemaName, string childTableName, string childAlias, string childColumnName, SqlOperator op, string parentAlias, string parentColumnName)
        {
            var joinTable = AddTable(QueryPartFlag.Join, serverName, databaseName, schemaName, childTableName, childAlias);
            var parentTable = GetByAlias(childAlias);
            var pkCol = AddColumn(QueryPartFlag.Join, parentColumnName, null, joinTable);
            var con = AddCondition(op);
            var fkCol = AddColumn(QueryPartFlag.Join, childColumnName, null, parentTable);
            joinTable.AddLinks(pkCol, fkCol);
            con.AddLinks(pkCol, fkCol);
            return this;
        }

        public SqlQueryBuilder BeginGroup()
        {
            AddQueryPart(QueryPartFlag.BeginGroup, "(");
            return this;
        }

        public SqlQueryBuilder EndGroup()
        {
            AddQueryPart(QueryPartFlag.EndGroup, ")");
            return this;
        }

        public SqlQueryBuilder And()
        {
            AddQueryPart(QueryPartFlag.Operator, "AND");
            return this;
        }

        public SqlQueryBuilder Or()
        {
            AddQueryPart(QueryPartFlag.Operator, "OR");
            return this;
        }

        public SqlQueryBuilder Where(string tableAlias, string columnName, SqlOperator op, object value)
        {
            if (IfLast(QueryPartFlag.Where, QueryPartFlag.EndGroup))
            {
                And();
            }
            var table = GetByAlias(tableAlias);
            AddColumn(QueryPartFlag.Where, columnName, null, table);
            AddCondition(op);
            AddParam(value, string.Format(WhereParamNameTemplate, columnName));
            return this;
        }

        private bool IfLast(params QueryPartFlag[] hasAnyFlag)
        {
            return query.Count == 0 ? false : hasAnyFlag.Any(x => query[query.Count - 1].Flags.HasFlag(x));
        }

        private QueryPart? Last()
        {
            return query.Count > 0 ? query[query.Count - 1] : null;
        }

        private void CheckAlias(string? alias)
        {
            if (string.IsNullOrEmpty(alias))
            {
                return;
            }

            if (query.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x, alias)))
            {
                throw new InvalidOperationException($"В запросе уже существует псевдоним '{alias}'");
            }
        }

        private QueryPart AddParam(object value, string alias)
        {
            return AddQueryPart(QueryPartFlag.Parameter, null, alias, value);
        }

        private QueryPart AddTable(QueryPartFlag tableFlag, string? serverName, string? databaseName, string? schemaName, string tableName, string? alias = null)
        {
            var fromPart = AddQueryPart(QueryPartFlag.Table | tableFlag, tableName, alias, null, schemaName, databaseName, serverName);
            foreach (var q in query.TakeWhile(query.Count - 1, EnumerableExtensions.ListDirection.Backward, (x, i) => x.Flags.HasFlag(QueryPartFlag.Column)))
            {
                q.AddLinks(fromPart);
            }
            return fromPart;
        }

        private QueryPart GetByAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException(nameof(alias));
            }

            var qp = query.FirstOrDefault(x => x.Flags.HasFlag(QueryPartFlag.Table) && StringComparer.InvariantCultureIgnoreCase.Equals(x.Alias, alias));

            if (qp == null)
            {
                throw new InvalidOperationException($"В запросе не найден объект с псевдонимом '{alias}'!");
            }

            return qp;
        }

        private QueryPart AddCondition(SqlOperator op)
        {
            var condition = AddQueryPart(QueryPartFlag.Operator, null, null, op);
            return condition;
        }

        private QueryPart AddColumn(QueryPartFlag columnFlags, string columnName, string? alias = null, params QueryPart[] links)
        {
            var columnPart = AddQueryPart(QueryPartFlag.Column | columnFlags, columnName, alias);
            columnPart.AddLinks(links);
            return columnPart;
        }

        private QueryPart AddQueryPart(QueryPartFlag flags, string? objectName = null, string? alias = null, object? value = null, string? schemaName = null, string? databaseName = null, string? serverName = null)
        {
            CheckAlias(alias);
            var qp = new QueryPart(this, flags);
            qp.ObjectName = objectName;
            qp.Alias = alias;
            qp.Value = value;
            qp.SchemaName = schemaName;
            qp.DatabaseName = databaseName;
            qp.ServerName = serverName;
            query.Add(qp);
            return qp;
        }

        //private void BuildSelect()
        //{
        //    var selectParts = this.query.Where(x => x.QueryPartType == QueryPartFlag.Select).ToArray();

        //    if (selectParts.Length > 0)
        //    {
        //        sb.Append("SELECT");
        //        sb.Append(this.Format());
        //        sb.Append(this.Format(1));
        //        for (int i = 0; i < selectParts.Length; i++)
        //        {
        //            Format(selectParts[i].Member, selectParts[i].Alias);
        //            if (i < selectParts.Length - 1)
        //            {
        //                sb.Append(", ");
        //            }
        //        }

        //        sb.Append(" FROM");
        //        sb.Append(this.Format());
        //        sb.Append(this.Format(1));
        //        sb.Append(selectParts[0].Member?.GetTableName(this.options.NamePrefix, this.options.NameSuffix));
        //    }
        //}

        //private void BuildWhere()
        //{
        //    var whereParts = this.query.Where(x => x.QueryPartType == QueryPartFlag.Where).ToArray();

        //    if (whereParts.Length > 0)
        //    {
        //        sb.Append(this.Format());
        //        sb.Append("WHERE");
        //        sb.Append(this.Format());
        //        sb.Append(this.Format(1));

        //        for (int i = 0; i < whereParts.Length; i++)
        //        {
        //        }
        //    }
        //}

        private string Format(int tabsCount = 0)
        {
            var f = this.IndentFormat ? Environment.NewLine : (tabsCount > 0 ? string.Empty : " ");
            if (tabsCount > 0 && this.IndentFormat)
            {
                f = (new string('\t', tabsCount) + f).Trim();
            }

            return f;
        }

        internal sealed class QueryPart
        {
            private readonly SqlQueryBuilder sqlQueryBuilder;
            private List<QueryPart> links;

            public QueryPart(SqlQueryBuilder sqlQueryBuilder, QueryPartFlag queryPartType)
            {
                this.sqlQueryBuilder = sqlQueryBuilder;
                this.Flags = queryPartType;
            }

            public string? ServerName { get; set; }
            public string? DatabaseName { get; set; }
            public string? SchemaName { get; set; }
            public string? ObjectName { get; set; }
            public string? Alias { get; set; }
            public object? Value { get; set; }
            public QueryPartFlag Flags { get; }

            internal void AddLinks(params QueryPart[] links)
            {
                if (links == null ||  links.Length == 0)
                {
                    return;
                }

                this.links ??= new List<QueryPart>();
                this.links.AddRange(links);
            }

            public override string ToString()
            {
                return $"{Flags} [{ObjectName}] AS {Alias}";
            }
        }
    }
}