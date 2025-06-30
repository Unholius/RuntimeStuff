using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class ActionExtensionsTests
    {
        [TestMethod]
        public void ConvertAction_Test_01()
        {
            // Строго типизированный делегат
            Action<string> printMessage = Console.WriteLine;

            // Преобразование в делегат с object
            Action<object> objAction = printMessage.ConvertAction();

            // Обратное преобразование
            Action<string> backToTyped = objAction.ConvertAction<string>();
        }
    }
}
