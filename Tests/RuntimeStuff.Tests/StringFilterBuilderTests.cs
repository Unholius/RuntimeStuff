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

        //[TestMethod]
        //public void Property_Equal_String_EscapesQuotes()
        //{
        //    var b = new StringFilterBuilder();
        //    b.Property("Name").Equal("O'Reilly");

        //    Assert.AreEqual("[Name] == 'O''Reilly'", b.ToString());
        //}

        [TestMethod]
        public void In_With_Integers_FormatsList()
        {
            var b = new StringFilterBuilder();
            b.Property("Age").In([20, 30, 40]);

            Assert.AreEqual("[Age] IN { 20, 30, 40 }", b.ToString());
        }

        [TestMethod]
        public void Between_AddsBetweenClause()
        {
            var b = new StringFilterBuilder();
            b.Add("Price", StringFilterBuilder.Operation.Between, new object[] { 10, 20 });

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
            b.OpenGroup()
             .Property("A").Equal(1)
             .And()
             .Property("B").Equal(2)
             .CloseGroup();

            Assert.AreEqual("([A] == 1 && [B] == 2)", b.ToString());
        }

        [TestMethod]
        public void Equal_Null_FormatsAsNull()
        {
            var b = new StringFilterBuilder();
            b.Property("X").Equal(null);

            Assert.AreEqual("[X] == null", b.ToString());
        }

        [TestMethod]
        public void DateTime_IsFormattedCorrectly()
        {
            var dt = new DateTime(2025, 1, 2, 3, 4, 5);
            var b = new StringFilterBuilder();
            b.Property("Created").Equal(dt);
            Assert.AreEqual($"[Created] == '{string.Format("{0:" + b.Options.FormatOptions.DateFormat + "}", dt)}'", b.ToString());
        }


        [TestMethod]
        public void Bool_True_IsFormattedAs1()
        {
            var b = new StringFilterBuilder();
            b.Property("Active").Equal(true);
            Assert.AreEqual("[Active] == 1", b.ToString());
        }


        [TestMethod]
        public void Bool_False_IsFormattedAs0()
        {
            var b = new StringFilterBuilder();
            b.Property("Active").Equal(false);
            Assert.AreEqual("[Active] == 0", b.ToString());
        }
    }
}
