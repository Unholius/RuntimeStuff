using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class ActionExtensionsTests
    {
        [TestMethod]
        public void ConvertAction_TypedToObject_1Param()
        {
            var called = 0;
            Action<int> act = x => called = x;
            var objAct = act.ConvertAction();
            objAct(42);
            Assert.AreEqual(42, called);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_2Params()
        {
            var sum = 0;
            Action<int, int> act = (a, b) => sum = a + b;
            var objAct = act.ConvertAction();
            objAct(3, 4);
            Assert.AreEqual(7, sum);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_3Params()
        {
            string? result = null;
            Action<string, int, bool> act = (s, i, b) => result = $"{s}-{i}-{b}";
            var objAct = act.ConvertAction();
            objAct("A", 5, true);
            Assert.AreEqual("A-5-True", result);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_4Params()
        {
            string? result = null;
            Action<int, int, int, int> act = (a, b, c, d) => result = $"{a},{b},{c},{d}";
            var objAct = act.ConvertAction();
            objAct(1, 2, 3, 4);
            Assert.AreEqual("1,2,3,4", result);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_5Params()
        {
            string? result = null;
            Action<int, int, int, int, int> act = (a, b, c, d, e) => result = $"{a}{b}{c}{d}{e}";
            var objAct = act.ConvertAction();
            objAct(1, 2, 3, 4, 5);
            Assert.AreEqual("12345", result);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_6Params()
        {
            var sum = 0;
            Action<int, int, int, int, int, int> act = (a, b, c, d, e, f) => sum = a + b + c + d + e + f;
            var objAct = act.ConvertAction();
            objAct(1, 2, 3, 4, 5, 6);
            Assert.AreEqual(21, sum);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_7Params()
        {
            string? result = null;
            Action<string, string, string, string, string, string, string> act =
                (a, b, c, d, e, f, g) => result = $"{a}{b}{c}{d}{e}{f}{g}";
            var objAct = act.ConvertAction();
            objAct("a", "b", "c", "d", "e", "f", "g");
            Assert.AreEqual("abcdefg", result);
        }

        [TestMethod]
        public void ConvertAction_TypedToObject_8Params()
        {
            string? result = null;
            Action<int, int, int, int, int, int, int, int> act =
                (a, b, c, d, e, f, g, h) => result = $"{a}{b}{c}{d}{e}{f}{g}{h}";
            var objAct = act.ConvertAction();
            objAct(1, 2, 3, 4, 5, 6, 7, 8);
            Assert.AreEqual("12345678", result);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_1Param()
        {
            var called = 0;
            Action<object> act = o => called = (int)o;
            var typedAct = act.ConvertAction<int>();
            typedAct(99);
            Assert.AreEqual(99, called);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_2Params()
        {
            var sum = 0;
            Action<object, object> act = (a, b) => sum = (int)a + (int)b;
            var typedAct = act.ConvertAction<int, int>();
            typedAct(10, 20);
            Assert.AreEqual(30, sum);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_3Params()
        {
            string? result = null;
            Action<object, object, object> act = (a, b, c) => result = $"{a}-{b}-{c}";
            var typedAct = act.ConvertAction<string, int, bool>();
            typedAct("X", 7, false);
            Assert.AreEqual("X-7-False", result);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_4Params()
        {
            string? result = null;
            Action<object, object, object, object> act = (a, b, c, d) => result = $"{a}{b}{c}{d}";
            var typedAct = act.ConvertAction<int, int, int, int>();
            typedAct(1, 2, 3, 4);
            Assert.AreEqual("1234", result);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_5Params()
        {
            string? result = null;
            Action<object, object, object, object, object> act = (a, b, c, d, e) => result = $"{a}{b}{c}{d}{e}";
            var typedAct = act.ConvertAction<int, int, int, int, int>();
            typedAct(1, 2, 3, 4, 5);
            Assert.AreEqual("12345", result);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_6Params()
        {
            var sum = 0;
            Action<object, object, object, object, object, object> act =
                (a, b, c, d, e, f) => sum = (int)a + (int)b + (int)c + (int)d + (int)e + (int)f;
            var typedAct = act.ConvertAction<int, int, int, int, int, int>();
            typedAct(1, 2, 3, 4, 5, 6);
            Assert.AreEqual(21, sum);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_7Params()
        {
            string? result = null;
            Action<object, object, object, object, object, object, object> act =
                (a, b, c, d, e, f, g) => result = $"{a}{b}{c}{d}{e}{f}{g}";
            var typedAct = act.ConvertAction<string, string, string, string, string, string, string>();
            typedAct("a", "b", "c", "d", "e", "f", "g");
            Assert.AreEqual("abcdefg", result);
        }

        [TestMethod]
        public void ConvertAction_ObjectToTyped_8Params()
        {
            string? result = null;
            Action<object, object, object, object, object, object, object, object> act =
                (a, b, c, d, e, f, g, h) => result = $"{a}{b}{c}{d}{e}{f}{g}{h}";
            var typedAct = act.ConvertAction<int, int, int, int, int, int, int, int>();
            typedAct(1, 2, 3, 4, 5, 6, 7, 8);
            Assert.AreEqual("12345678", result);
        }
    }
}