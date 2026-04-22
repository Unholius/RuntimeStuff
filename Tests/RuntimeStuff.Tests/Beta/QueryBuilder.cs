using System;
using System.Collections.Generic;
using System.Text;

namespace RuntimeStuff.MSTests.Beta
{
    internal class Query<TFlags> : LinkedList<Query<TFlags>.Token>
        where TFlags : struct, Enum
    {
        private readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

        public Token? this[string key] => this.FirstOrDefault(x => comparer.Equals(x, key));

        public bool Contains(string key) => this[key] != null;
        public Token? LastOrDefault(string key) => this.LastOrDefault(x => comparer.Equals(x, key));

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
