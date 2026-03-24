using RuntimeStuff.Builders;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class StringFilterBuilderTests
    {
        private class TestEntity
        {
            public string? Name { get; set; }
            public bool Active { get; set; }
            public int Age { get; set; }
            public DateTime Created { get; set; }
        }

        [TestMethod]
        public void In_With_Integers_FormatsList()
        {
            var b = new StringFilterBuilder();
            b.Property("Age").In([20, 30, 40]);

            Assert.AreEqual("[Age] IN ( 20, 30, 40 )", b.ToString());
        }

        [TestMethod]
        public void Between_AddsBetweenClause()
        {
            var b = new StringFilterBuilder();
            b.Add("Price", StringFilterBuilder.Token.Between, new object[] { 10, 20 });

            Assert.AreEqual("[Price] BETWEEN 10 AND 20", b.ToString());
        }

        [TestMethod]
        public void Like_WorksAndFormats()
        {
            var b = new StringFilterBuilder();
            b.Property("Title").Like("prefix%");

            Assert.AreEqual("[Title] LIKE 'prefix%'", b.ToString());
        }

        [TestMethod]
        public void NotLike_Works()
        {
            var b = new StringFilterBuilder();
            b.Property("Name").NotLike("abc");

            Assert.AreEqual("[Name] NOT LIKE 'abc'", b.ToString());
        }

        [TestMethod]
        public void Where_With_Expression_ConvertsToFilterString()
        {
            var b = new StringFilterBuilder();
            b.Where<TestEntity>(x => x.Active && x.Name != null && x.Name.Contains("abc"));

            // ExpressionVisitor создает: ([Active] && [Name] LIKE '%abc%')
            Assert.AreEqual("(([Active] && ([Name] != null)) && [Name] LIKE '%abc%')", b.ToString());
        }

        [TestMethod]
        public void Grouping_And_LogicalOperators_ProduceCorrectString()
        {
            var b = new StringFilterBuilder();
            b.BeginGroup()
             .Property("A").Equal(1)
             .And()
             .Property("B").Equal(2)
             .EndGroup();

            Assert.AreEqual($"( [A] {b.Syntax[StringFilterBuilder.Token.Equal]} 1 {b.Syntax[StringFilterBuilder.Token.And]} [B] {b.Syntax[StringFilterBuilder.Token.Equal]} 2 )", b.ToString());
        }

        [TestMethod]
        public void Equal_Null_FormatsAsNull()
        {
            var b = new StringFilterBuilder();
            b.Property("X").Equal(null);

            Assert.AreEqual($"[X] {b.Syntax[StringFilterBuilder.Token.Equal]} null", b.ToString());
        }

        [TestMethod]
        public void DateTime_IsFormattedCorrectly()
        {
            var dt = new DateTime(2025, 1, 2, 3, 4, 5);
            var b = new StringFilterBuilder();
            b.Property("Created").Equal(dt);
            Assert.AreEqual($"[Created] {b.Syntax[StringFilterBuilder.Token.Equal]} {string.Format(b.Options.Formatter.DateTimeFormat, dt)}", b.ToString());
        }

        [TestMethod]
        public void Bool_True_IsFormattedAs1()
        {
            var b = new StringFilterBuilder();
            b.Property("Active").Equal(true);
            Assert.AreEqual($"[Active] {b.Syntax[StringFilterBuilder.Token.Equal]} 1", b.ToString());
        }

        [TestMethod]
        public void Bool_False_IsFormattedAs0()
        {
            var b = new StringFilterBuilder();
            b.Property("Active").Equal(false);
            Assert.AreEqual($"[Active] {b.Syntax[StringFilterBuilder.Token.Equal]} 0", b.ToString());
        }
    }
}