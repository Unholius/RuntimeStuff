using Microsoft.VisualStudio.TestTools.UnitTesting;
using RuntimeStuff.Extensions;
using System;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class DateTimeExtensionsTests
    {
        [TestMethod]
        public void HasTime_WithTime_ReturnsTrue()
        {
            var dt = new DateTime(2023, 1, 1, 12, 0, 0);
            Assert.IsTrue(dt.HasTime());
        }

        [TestMethod]
        public void HasTime_NullableWithTime_ReturnsTrue()
        {
            DateTime? dt = new DateTime(2023, 1, 1, 12, 0, 0);
            Assert.IsTrue(dt.HasTime());
        }

        [TestMethod]
        public void ParseTimeSpan_ValidString_ReturnsCorrectTimeSpans()
        {
            var result = "1d 2h 30m".ParseTimeSpan();
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(TimeSpan.FromDays(1), result[0]);
            Assert.AreEqual(TimeSpan.FromHours(2), result[1]);
            Assert.AreEqual(TimeSpan.FromMinutes(30), result[2]);
        }

        [TestMethod]
        public void Add_WithTimeSpanString_ReturnsCorrectDate()
        {
            var dt = new DateTime(2023, 1, 1);
            var result = dt.Add("1d 2h");
            Assert.AreEqual(new DateTime(2023, 1, 2, 2, 0, 0), result);
        }

        [TestMethod]
        public void BeginDay_ReturnsStartOfDay()
        {
            var dt = new DateTime(2023, 1, 1, 12, 30, 30);
            var result = dt.BeginDay();
            Assert.AreEqual(new DateTime(2023, 1, 1), result);
        }

        [TestMethod]
        public void BeginDay_Nullable_ReturnsMinValueForNull()
        {
            DateTime? dt = null;
            var result = dt.BeginDay();
            Assert.AreEqual(DateTime.MinValue, result);
        }

        [TestMethod]
        public void EndDay_ReturnsEndOfDay()
        {
            var dt = new DateTime(2023, 1, 1, 12, 30, 30);
            var result = dt.EndDay();
            Assert.AreEqual(new DateTime(2023, 1, 1, 23, 59, 59, 999), result);
        }

        [TestMethod]
        public void EndDay_Nullable_ReturnsMaxValueForNull()
        {
            DateTime? dt = null;
            var result = dt.EndDay();
            Assert.AreEqual(DateTime.MaxValue, result);
        }

        [TestMethod]
        public void BeginMonth_ReturnsFirstDayOfMonth()
        {
            var dt = new DateTime(2023, 1, 15);
            var result = dt.BeginMonth();
            Assert.AreEqual(new DateTime(2023, 1, 1), result);
        }

        [TestMethod]
        public void BeginMonth_Nullable_ReturnsMinValueForNull()
        {
            DateTime? dt = null;
            var result = dt.BeginMonth();
            Assert.AreEqual(DateTime.MinValue, result);
        }

        [TestMethod]
        public void EndMonth_ReturnsLastDayOfMonth()
        {
            var dt = new DateTime(2023, 2, 15);
            var result = dt.EndMonth();
            Assert.AreEqual(new DateTime(2023, 2, 28, 23, 59, 59, 999), result);
        }

        [TestMethod]
        public void EndMonth_Nullable_ReturnsMaxValueForNull()
        {
            DateTime? dt = null;
            var result = dt.EndMonth();
            Assert.AreEqual(DateTime.MaxValue, result);
        }

        [TestMethod]
        public void BeginYear_ReturnsFirstDayOfYear()
        {
            var dt = new DateTime(2023, 6, 15);
            var result = dt.BeginYear();
            Assert.AreEqual(new DateTime(2023, 1, 1), result);
        }

        [TestMethod]
        public void BeginYear_Nullable_ReturnsMinValueForNull()
        {
            DateTime? dt = null;
            var result = dt.BeginYear();
            Assert.AreEqual(DateTime.MinValue, result);
        }

        [TestMethod]
        public void EndYear_ReturnsLastDayOfYear()
        {
            var dt = new DateTime(2023, 6, 15);
            var result = dt.EndYear();
            Assert.AreEqual(new DateTime(2023, 12, 31, 23, 59, 59, 999), result);
        }

        [TestMethod]
        public void EndYear_Nullable_ReturnsMaxValueForNull()
        {
            DateTime? dt = null;
            var result = dt.EndYear();
            Assert.AreEqual(DateTime.MaxValue, result);
        }

        [TestMethod]
        public void Yesterday_ReturnsPreviousDay()
        {
            var dt = new DateTime(2023, 1, 2);
            var result = dt.Yesterday();
            Assert.AreEqual(new DateTime(2023, 1, 1), result);
        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public void Yesterday_NullableWithNull_ThrowsException()
        {
            DateTime? dt = null;
            var result = dt.Yesterday();
        }

        [TestMethod]
        public void NowTicks_ReturnsUniqueValues()
        {
            var tick1 = DateTimeExtensions.NowTicks;
            var tick2 = DateTimeExtensions.NowTicks;
            Assert.AreNotEqual(tick1, tick2);
        }

        [TestMethod]
        public void ExactNow_ReturnsCurrentDateTime()
        {
            var before = DateTime.Now;
            var result = DateTime.Now.ExactNow();
            Assert.IsTrue(result >= before);
        }

        [TestMethod]
        public void ExactTicks_ReturnsCurrentTicks()
        {
            var before = DateTime.Now.Ticks;
            var result = DateTime.Now.ExactTicks();
            Assert.IsTrue(result >= before);
        }
    }
}