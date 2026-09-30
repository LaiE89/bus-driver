using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    public class MoneyTests {
        [TestCase(350, "$3.50")]
        [TestCase(0, "$0.00")]
        [TestCase(5, "$0.05")]
        [TestCase(100000, "$1,000.00")]
        [TestCase(-350, "−$3.50")]
        [TestCase(-5, "−$0.05")]
        public void Format(long cents, string expected) {
            Assert.AreEqual(expected, Money.Format(cents));
        }

        [TestCase(350, "+$3.50")]
        [TestCase(-350, "−$3.50")]
        [TestCase(0, "$0.00")]
        public void FormatDeltaAlwaysCarriesASign(long cents, string expected) {
            Assert.AreEqual(expected, Money.FormatDelta(cents));
        }

        [Test]
        public void MinusIsTheTrueMinusSign() {
            StringAssert.StartsWith("−", Money.Format(-1));
            StringAssert.DoesNotContain("-", Money.Format(-1));
        }

        [Test]
        public void ExtremeValuesDontOverflow() {
            Assert.AreEqual("−$92,233,720,368,547,758.08", Money.Format(long.MinValue));
        }
    }
}
