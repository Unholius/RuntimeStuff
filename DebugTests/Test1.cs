namespace DebugTests
{
    using System;

    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod2()
        {
            var x = new TestClass1();
            var json = "{ 'Id':1, 'Name': 'MyName', 'Child': { 'Id': 2, 'Name': 'ChildName'} }".Replace("'", "\"");
            x.FromJson(json);
            Assert.AreEqual(1, x.Id);
            Assert.AreEqual("MyName", x.Name);
            Assert.IsNotNull(x.Child);
            Assert.AreEqual(2, x.Child.Id);
            Assert.AreEqual("ChildName", x.Child.Name);
            Obj.ClearCaches();
        }
    }
}