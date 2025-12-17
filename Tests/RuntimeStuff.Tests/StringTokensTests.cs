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
            var s = "([EventId] >= 2 || [EventId] < 100)";
            var masks = new List<StringHelper.TokenMask>();
            masks.Add(new StringHelper.TokenMask("[", "]", _ => "property"));
            masks.Add(new StringHelper.TokenMask("(", ")", _ => "group"));
            masks.Add(new StringHelper.TokenMask(" >= ", null, _ => "ge"));
            masks.Add(new StringHelper.TokenMask(" < ", null, _ => "lt"));
            masks.Add(new StringHelper.TokenMask(" || ", null, _ => "or"));
            masks.Add(new StringHelper.TokenMask("'", "'", _ => "string_value") { AllowChildrenTokens = false });
            var tokens = StringHelper.GetTokens(s, masks, true, t => int.TryParse(t.Body, out var intval) ? intval : t.Body).Flatten();
        }

        [TestMethod]
        public void GetTokens_Test_03()
        {
            var s = "(1(2()A()3)4(5()6)7)".RepeatString(1);
            var masks = new List<StringHelper.TokenMask>();
            masks.Add(new StringHelper.TokenMask("(", ")", _ => "group"));
            var tokens = StringHelper.GetTokens(s, masks, false, t => int.TryParse(t.Body, out var intval) ? intval : t.Body);
            //StringHelper.TokenizeNotMatched(tokens[0].Children, null);
            var c = tokens[0].Content;
            Assert.AreEqual("12A34567", c);
        }
    }
}