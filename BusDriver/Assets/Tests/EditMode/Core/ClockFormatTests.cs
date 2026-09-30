using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    public class ClockFormatTests {
        [Test]
        public void ShiftStartIsHalfPastMidnight() {
            Assert.AreEqual("12:30 AM", ClockText.Format(1800, ClockFormat.Dash));
            Assert.AreEqual("12:30:00 AM", ClockText.Format(1800, ClockFormat.Cctv));
        }

        [Test]
        public void TerminusTimeFromTheSchedule() {
            Assert.AreEqual("01:26 AM", ClockText.Format(5213.3, ClockFormat.Dash));
            Assert.AreEqual("01:26:53 AM", ClockText.Format(5213.3, ClockFormat.Cctv));
        }

        [TestCase(0, "12:00:00 AM")]
        [TestCase(43199.9, "11:59:59 AM")]
        [TestCase(43200, "12:00:00 PM")]
        [TestCase(46800, "01:00:00 PM")]
        [TestCase(86400 + 1800, "12:30:00 AM")]
        [TestCase(-1, "11:59:59 PM")]
        public void TwelveHourEdges(double seconds, string expected) {
            Assert.AreEqual(expected, ClockText.Format(seconds, ClockFormat.Cctv));
        }
    }
}
