using System;
using System.Collections.Generic;
using System.Text;

namespace RuntimeStuff.MSTests.Beta
{
    internal class Query<TFlags> : LinkedList<Query<TFlags>.QueryToken>
        where TFlags : struct, Enum
    {
        public class QueryToken
        {
            public TFlags Flags { get; set; } = default;
            public object? Item { get; set; }
            public List<QueryToken> Links { get; set; }
        }
    }
}
