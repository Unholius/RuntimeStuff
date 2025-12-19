using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class TypeCacheTests
    {

        public class TestClass
        {
            [Column("Name")]
            public int Id { get; set; }

            [Column("EventId")] public string Name { get; set; } = "";
        }

        public class TestClass02
        {
            public string Prop { get; set; }
            public string PROP { get; set; }
            private string prop;
        }

        [TestMethod]
        public void GetNonExistedMemberByNameShouldReturnNull()
        {
            var memberInfo = typeof(TestClass).GetMemberInfoEx();
            var m = memberInfo.GetMember("Имя");
            Assert.IsNull(m);
        }

        [TestMethod]
        public void GetMemberByColumnName()
        {
            var memberInfo = typeof(TestClass).GetMemberInfoEx();
            var m = memberInfo.GetMember("namE", MemberNameType.ColumnName);
            Assert.IsNotNull(m);
            Assert.AreEqual(nameof(TestClass.Id), m.Name);
        }

        [TestMethod]
        public void GetMemberByColumnNameWithDifferentCase()
        {
            var memberInfo = typeof(TestClass).GetMemberInfoEx();
            var m = memberInfo.GetMember("name");
        }

        [TestMethod]
        public void MemberSameNames_Test_01()
        {
            var mi = typeof(TestClass02).GetMemberInfoEx();
            var x = new TestClass02();
            mi.SetValue(x, "prop", "123");
        }
    }
}
