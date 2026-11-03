using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // Sanity math (§2.15, T-M5-01)
    public class SanityCoreTests {
        [Test]
        public void TierBoundaries_MatchSpec() {
            Assert.AreEqual(SanityTier.T0, SanityCore.TierOf(100f));
            Assert.AreEqual(SanityTier.T0, SanityCore.TierOf(75f));
            Assert.AreEqual(SanityTier.T1, SanityCore.TierOf(74.9f));
            Assert.AreEqual(SanityTier.T1, SanityCore.TierOf(50f));
            Assert.AreEqual(SanityTier.T2, SanityCore.TierOf(49.9f));
            Assert.AreEqual(SanityTier.T2, SanityCore.TierOf(25f));
            Assert.AreEqual(SanityTier.T3, SanityCore.TierOf(24.9f));
            Assert.AreEqual(SanityTier.T3, SanityCore.TierOf(10f));
            Assert.AreEqual(SanityTier.T4, SanityCore.TierOf(9.9f));
            Assert.AreEqual(SanityTier.T4, SanityCore.TierOf(0f));
        }

        [Test]
        public void Apply_ClampsAndReportsTierChange() {
            SanityCore core = new SanityCore(80f);
            Assert.IsTrue(core.Apply(-40f, out SanityTier previous));
            Assert.AreEqual(SanityTier.T0, previous);
            Assert.AreEqual(40f, core.Value, 1e-4f);
            Assert.AreEqual(SanityTier.T2, core.Tier);
            Assert.IsFalse(core.Apply(-5f, out _));
            Assert.AreEqual(35f, core.Value, 1e-4f);
            core.Apply(-100f, out _);
            Assert.AreEqual(0f, core.Value, 1e-4f);
            Assert.IsTrue(core.IsDepleted);
            core.Apply(200f, out _);
            Assert.AreEqual(100f, core.Value, 1e-4f);
        }

        [Test]
        public void Drains_StackAndRemoveCleanly() {
            SanityCore core = new SanityCore(100f);
            object a = new object();
            object b = new object();
            core.SetDrain(a, -0.05f);
            core.SetDrain(b, -0.5f);
            Assert.AreEqual(-0.55f, core.DrainSum(), 1e-4f);
            Assert.IsFalse(core.Tick(1f, out _));
            Assert.AreEqual(99.45f, core.Value, 1e-3f);
            core.RemoveDrain(b);
            Assert.AreEqual(-0.05f, core.DrainSum(), 1e-4f);
            core.SetDrain(a, 0f);
            Assert.AreEqual(0f, core.DrainSum(), 1e-4f);
        }

        [TestCase(20f, 60f)]
        [TestCase(50f, 80f)]
        [TestCase(90f, 100f)]
        public void CarryOver_MatchesEconomyMath(float end, float expected) {
            Assert.AreEqual(expected, EconomyMath.CarriedSanity(end, 30f, 60f), 1e-4f);
        }
    }
}
