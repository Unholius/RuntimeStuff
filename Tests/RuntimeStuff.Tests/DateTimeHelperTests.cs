using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DateTimeHelperTests
    {
        [TestMethod]
        public void GetTimeIntervalString()
        {
            var s = DateTimeHelper.GetElapsedTimeString(4000, DateTimeHelper.DateTimeInterval.Second);
            Assert.AreEqual("1 час. 6 мин. 40 с.", s);
        }
    }
}