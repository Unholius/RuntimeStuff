using System.ComponentModel.DataAnnotations.Schema;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class MemberInfoExTests
    {

        public class TestClass
        {
            [Column("Name")]
            public int Id { get; set; }

            [Column("Id")] public string Name { get; set; } = "";
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
    }
}
