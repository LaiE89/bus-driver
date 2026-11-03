using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The Starer's hop / kill-hop numbers
    public class StarerRulesTests {
        [Test]
        public void NextRow_StepsTowardTheFront() {
            Assert.AreEqual(8, StarerRules.NextRow(9));
            Assert.AreEqual(1, StarerRules.NextRow(2));
            Assert.AreEqual(1, StarerRules.NextRow(1));
        }

        [Test]
        public void ShouldHop_AfterTheIntervalUnseen() {
            Assert.IsTrue(StarerRules.ShouldHop(3f, 3f));
            Assert.IsFalse(StarerRules.ShouldHop(2.99f, 3f));
            Assert.IsFalse(StarerRules.ShouldHop(10f, 0f));
        }

        [Test]
        public void IsKillHop_AtOrAheadOfTheKillRow() {
            Assert.IsTrue(StarerRules.IsKillHop(2, 2));
            Assert.IsTrue(StarerRules.IsKillHop(1, 2));
            Assert.IsFalse(StarerRules.IsKillHop(3, 2));
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
