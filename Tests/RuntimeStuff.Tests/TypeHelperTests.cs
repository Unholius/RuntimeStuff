using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuntimeStuff.Helpers;
using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class TypeHelperTests
    {
        [TestMethod]
        public void Test1()
        {
            var o = new TestClassWithBasicProperties(666);
            var v = TypeHelper.GetMemberValue(o, "Int32");
        }

        [TestMethod]
        public void Test2()
        {
            var o = new TestClassWithMethods() {Int32 = 666};
            var v1 = TypeHelper.GetMemberValue(o, "_str");
            var v2 = TypeHelper.GetMemberValue<string>(o, "int32", true, true);
            var v3 = TypeHelper.GetMemberValue(o, "GetString");
            var v4 = TypeHelper.GetMemberValue<string>(o, "GetString");
            var g = TypeHelper.Getter<TestClassWithMethods, string>("_str");
        }
    }
}
