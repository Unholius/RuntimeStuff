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
            var v = TypeHelper.GetValue(o, "Int32");
        }

        [TestMethod]
        public void Test2()
        {
            var o = new TestClassWithMethods() {Int32 = 666};
            var v1 = TypeHelper.GetValue(o, "_str");
            var v2 = TypeHelper.GetValue<string>(o, "int32", true, true);
            var v3 = TypeHelper.GetValue(o, "GetString");
            var v4 = TypeHelper.GetValue<string>(o, "GetString");
            var g = TypeHelper.Getter<TestClassWithMethods, string>("_str");
        }
    }
}
