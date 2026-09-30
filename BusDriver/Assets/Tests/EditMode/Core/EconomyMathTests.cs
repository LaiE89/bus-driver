using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The money numbers of §2.7 and the sanity carry-over of §2.15 (T-M3-05)
    public class EconomyMathTests {
        [TestCase(350, 50, 175)]
        [TestCase(350, 0, 0)]
        [TestCase(350, 100, 350)]
        [TestCase(333, 50, 167)]   // 166.5 rounds half up
        [TestCase(1, 49, 0)]
        [TestCase(0, 50, 0)]
        public void Tip(int fare, int percent, int expected) {
            Assert.AreEqual(expected, EconomyMath.Tip(fare, percent));
        }

        [Test]
        public void TipOnlyForTheRidersOwnStopReachedEarly() {
            Assert.IsTrue(EconomyMath.EarnsTip(false, false, "church", "church", ArrivalRating.Early));
            Assert.IsFalse(EconomyMath.EarnsTip(false, false, "church", "church", ArrivalRating.OnTime));
            Assert.IsFalse(EconomyMath.EarnsTip(false, false, "church", "church", ArrivalRating.Late));
            Assert.IsFalse(EconomyMath.EarnsTip(false, false, "church", "church", ArrivalRating.None));
            // Carried on from a missed stop: no tip even at an Early end stop (§2.4)
            Assert.IsFalse(EconomyMath.EarnsTip(false, true, "campground", "church", ArrivalRating.Early));
            Assert.IsFalse(EconomyMath.EarnsTip(false, false, "campground", "church", ArrivalRating.Early));
            // Monsters never earn one
            Assert.IsFalse(EconomyMath.EarnsTip(true, false, "church", "church", ArrivalRating.Early));
            Assert.IsFalse(EconomyMath.EarnsTip(false, false, "church", "", ArrivalRating.Early));
        }

        [TestCase(100f, 100f)]
        [TestCase(80f, 100f)]
        [TestCase(48.5f, 78.5f)]
        [TestCase(20f, 60f)]
        [TestCase(0f, 60f)]
        public void CarriedSanity(float end, float expected) {
            Assert.AreEqual(expected, EconomyMath.CarriedSanity(end, 30f, 60f), 1e-4f);
        }

        [Test]
        public void BalanceDefaultsAreTheRoadmapNumbers() {
            BalanceConfig balance = UnityEngine.ScriptableObject.CreateInstance<BalanceConfig>();
            Assert.AreEqual(350, balance.fareCents);
            Assert.AreEqual(50, balance.tipPercent);
            Assert.AreEqual(500, balance.defaultBountyCents);
            Assert.AreEqual(10, balance.maxAboard);
            Assert.AreEqual(4, balance.hallucinationIntervals.Length);
            UnityEngine.Object.DestroyImmediate(balance);
        }
    }
}
