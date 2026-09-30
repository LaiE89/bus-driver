using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The Starer's advance numbers (§2.10, T-M4-08)
    public class StarerRulesTests {
        [Test]
        public void TargetRow_ClosesFromTheBoardRowToR1() {
            Assert.AreEqual(9, StarerRules.TargetRow(9, 0f), "Dormant at 0: its own row");
            Assert.AreEqual(1, StarerRules.TargetRow(9, 100f), "R1 at Lethal");
            Assert.AreEqual(5, StarerRules.TargetRow(9, 50f), "9 − round(8 × 0.5)");
            Assert.AreEqual(7, StarerRules.TargetRow(9, 25f), "9 − round(8 × 0.25)");
            Assert.AreEqual(4, StarerRules.TargetRow(9, 60f), "9 − round(4.8): back after an escape");
            Assert.AreEqual(1, StarerRules.TargetRow(1, 70f), "a front-row Starer stays in R1");
        }

        [Test]
        public void TargetRow_RoundsHalvesUpAndClamps() {
            // 8 × 0.0625 = 0.5 rounds to 1, not to the even 0
            Assert.AreEqual(8, StarerRules.TargetRow(9, 6.25f));
            // 8 × 0.1875 = 1.5 rounds to 2
            Assert.AreEqual(7, StarerRules.TargetRow(9, 18.75f));
            Assert.AreEqual(9, StarerRules.TargetRow(9, -10f));
            Assert.AreEqual(1, StarerRules.TargetRow(9, 150f));
        }

        [Test]
        public void ShouldAdvance_OnlyBehindTheTargetAfterASecondUnseen() {
            Assert.IsTrue(StarerRules.ShouldAdvance(8, 5, 1f, 1f));
            Assert.IsFalse(StarerRules.ShouldAdvance(8, 5, 0.99f, 1f), "watched within the last second");
            Assert.IsFalse(StarerRules.ShouldAdvance(5, 5, 10f, 1f), "already in its target row");
            Assert.IsFalse(StarerRules.ShouldAdvance(3, 5, 10f, 1f), "it never moves back");
        }

        [Test]
        public void Stillness_IsStageOverThree() {
            Assert.AreEqual(0f, StarerRules.Stillness(0));
            Assert.AreEqual(1f / 3f, StarerRules.Stillness(1), 1e-6f);
            Assert.AreEqual(2f / 3f, StarerRules.Stillness(2), 1e-6f);
            Assert.AreEqual(1f, StarerRules.Stillness(3));
        }
    }
}
