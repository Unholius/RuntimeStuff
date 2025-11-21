using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class StringTokensTests
    {
        [TestMethod]
        public void GetTokens_Test_01()
        {
            var s = "ахаха[In32] >= 99 AND [Str [SubProp] ] LIKE '%123%' OR [Str] == '345'[] бвахаха".RepeatString(1);
            var masks = new List<StringHelper.TokenMask>();
            masks.Add(new StringHelper.TokenMask("[", "]", _ => "property"));
            masks.Add(new StringHelper.TokenMask(" >= ", null, _ => "ge"));
            masks.Add(new StringHelper.TokenMask(" == ", null, _ => "eq"));
            masks.Add(new StringHelper.TokenMask(" AND ", null, _ => "and"));
            masks.Add(new StringHelper.TokenMask(" OR ", null, _ => "or"));
            masks.Add(new StringHelper.TokenMask("'", "'", _ => "string_value"));
            var tokens = StringHelper.GetTokens(s, masks, true, t=> int.TryParse(t.Body, out var intval) ? intval : t.Body).Flatten();
        }

        [TestMethod]
        public void GetTokens_Test_02()
        {
            var s = "([Id] >= 2 || [Id] < 100)".RepeatString(1);
            var masks = new List<StringHelper.TokenMask>();
            masks.Add(new StringHelper.TokenMask("[", "]", _ => "property"));
            masks.Add(new StringHelper.TokenMask("(", ")", _ => "group"));
            masks.Add(new StringHelper.TokenMask(" >= ", null, _ => "ge"));
            masks.Add(new StringHelper.TokenMask(" < ", null, _ => "lt"));
            masks.Add(new StringHelper.TokenMask("'", "'", _ => "string_value") { AllowChildrenTokens = false });
            var tokens = StringHelper.GetTokens(s, masks, true, t => int.TryParse(t.Body, out var intval) ? intval : t.Body);
            StringHelper.TokenizeNotMatched(tokens[0].Children, null);
        }
    }
}
