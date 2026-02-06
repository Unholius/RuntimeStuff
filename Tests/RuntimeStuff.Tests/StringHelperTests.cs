using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        public void SplitToList_Test_01()
        {
            var s = "E01-WIN-2513DI\tE01-WIN-2513PR\r\n";
            var list = s.SplitToList<KeyValuePair<string, string>>();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("E01-WIN-2513DI", list[0].Key);
            Assert.AreEqual("E01-WIN-2513PR", list[0].Value);
        }

        [TestMethod]
        public void SplitToList_Test_02()
        {
            var s = "E01-WIN-2513DI\tE01-WIN-2513PR\r\n";
            var list = s.SplitToList<(string, string)>();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("E01-WIN-2513DI", list[0].Item1);
            Assert.AreEqual("E01-WIN-2513PR", list[0].Item2);
        }
    }
}
