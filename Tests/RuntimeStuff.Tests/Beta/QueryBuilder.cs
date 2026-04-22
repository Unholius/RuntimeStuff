using System;
using System.Collections.Generic;
using System.Text;
using static RuntimeStuff.MSTests.Beta.SqlQueryBuilder;

namespace RuntimeStuff.MSTests.Beta
{
    internal class TokenList<TFlags> : LinkedList<TokenList<TFlags>.Token>
        where TFlags : struct, Enum
    {
        private readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

        public Token? this[string key] => this.FirstOrDefault(x => comparer.Equals(x, key));
        public Token[] this[params TFlags[] flags] => this.Where(x => flags.Any(f=> x.Flags.HasFlag(f))).ToArray();
        public Token this[int index] => this.FirstOrDefault((x, i) => i == index);

        public bool Contains(string key) => this[key] != null;

        public Token? FirstOrDefault(string key) => this.FirstOrDefault(x => comparer.Equals(x, key));
        public Token? LastOrDefault(string key) => this.LastOrDefault(x => comparer.Equals(x, key));
        public bool IfLast(params QueryPartFlag[] hasAnyFlag)
        {
            return this.Count == 0 ? false : hasAnyFlag.Any(x => this[this.Count - 1].Flags.HasFlag(x));
        }

        public class Token
        {
            public TFlags Flags { get; set; } = default;
            public object? Item { get; set; }
            public string? Key { get; set; }
            public string? ParentKey { get; set; }
            public List<Token> Links { get; set; }
        }
    }
}
