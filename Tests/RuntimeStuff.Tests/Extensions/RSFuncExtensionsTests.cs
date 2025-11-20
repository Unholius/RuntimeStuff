using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class RSFuncExtensionsTests
    {
        [TestMethod]
        public void ConvertFunc_Test_01()
        {
            Func<int, string> intToString = i => i.ToString();
            Func<object, object> objFunc = intToString.ConvertFunc();
            Func<int, string> backToTyped = objFunc.ConvertFunc<object, object, int, string>();
            Assert.AreEqual("1", intToString(1));
            object o1 = 1;
            Assert.AreEqual(intToString(1), objFunc(o1));
            Assert.AreEqual(objFunc(1), backToTyped(1));
        }
    }
}
