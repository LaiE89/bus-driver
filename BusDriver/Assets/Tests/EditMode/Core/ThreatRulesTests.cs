using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // Threat rules (§2.9, T-M4-02): first match wins, multipliers on positive rates only
    public class ThreatRulesTests {
        // The Starer's list (§2.10, D106): watching freezes it
        static readonly ThreatRule[] Starer = {
            new ThreatRule(ThreatCondition.Observed, 0f),
            new ThreatRule(ThreatCondition.Always, 10f),
        };
        // The Whisperer's list (§2.11)
        static readonly ThreatRule[] Whisperer = {
            new ThreatRule(ThreatCondition.ObservedByCctv, -3f),
            new ThreatRule(ThreatCondition.AttentionOnRoad, 1.2f),
            new ThreatRule(ThreatCondition.Always, 0.4f),
        };

        [Test]
        public void EachConditionMatchesItsFlag() {
            ThreatContext none = new ThreatContext();
            Assert.IsTrue(ThreatRules.Matches(ThreatCondition.Always, none));
            Assert.IsFalse(ThreatRules.Matches(ThreatCondition.Observed, none));
            Assert.IsTrue(ThreatRules.Matches(ThreatCondition.Observed, new ThreatContext { Observed = true }));
            Assert.IsTrue(ThreatRules.Matches(ThreatCondition.ObservedByCctv, new ThreatContext { ObservedByCctv = true }));
            Assert.IsTrue(ThreatRules.Matches(ThreatCondition.AttentionOnRoad, new ThreatContext { AttentionOnRoad = true }));
            Assert.IsTrue(ThreatRules.Matches(ThreatCondition.PlayerOnFoot, new ThreatContext { PlayerOnFoot = true }));
        }

        [Test]
        public void TheFirstMatchingRuleWins() {
            // Observed matches first, so its 0 wins over Always even though Always also holds
            Assert.AreEqual(0f, ThreatRules.BaseRate(Starer, new ThreatContext { Observed = true }));
            Assert.AreEqual(0, ThreatRules.FirstMatch(Starer, new ThreatContext { Observed = true }));
            Assert.AreEqual(10f, ThreatRules.BaseRate(Starer, new ThreatContext()));
            // Watched on CCTV while the head is on the road: the CCTV rule comes first
            Assert.AreEqual(-3f, ThreatRules.BaseRate(Whisperer, new ThreatContext { ObservedByCctv = true, AttentionOnRoad = true }));
            Assert.AreEqual(1.2f, ThreatRules.BaseRate(Whisperer, new ThreatContext { AttentionOnRoad = true }));
            Assert.AreEqual(0.4f, ThreatRules.BaseRate(Whisperer, new ThreatContext { PlayerOnFoot = true }));
            Assert.AreEqual(1, ThreatRules.FirstMatch(Whisperer, new ThreatContext { AttentionOnRoad = true }));
        }

        [Test]
        public void NoMatchingRuleMeansNoMovement() {
            ThreatRule[] onlyObserved = { new ThreatRule(ThreatCondition.Observed, 2.5f) };
            Assert.AreEqual(-1, ThreatRules.FirstMatch(onlyObserved, new ThreatContext()));
            Assert.AreEqual(0f, ThreatRules.Rate(onlyObserved, new ThreatContext(), 1.35f, 1.5f));
            Assert.AreEqual(0f, ThreatRules.BaseRate(null, new ThreatContext()));
        }

        [TestCase(100f, 1f)]
        [TestCase(60f, 1f)]
        [TestCase(30f, 1.25f)]
        [TestCase(0f, 1.5f)]
        [TestCase(-3f, 1.5f)]
        public void SanityFactor(float sanity, float expected) {
            Assert.AreEqual(expected, ThreatRules.SanityFactor(sanity, 1.5f), 1e-5f);
        }

        [Test]
        public void MultipliersScalePositiveRatesOnly() {
            ThreatContext unwatched = new ThreatContext();
            ThreatContext onCctv = new ThreatContext { ObservedByCctv = true };
            // Night 5's ×1.35 and sanity 30's ×1.25
            Assert.AreEqual(10f * 1.35f * 1.25f, ThreatRules.Rate(Starer, unwatched, 1.35f, 1.25f), 1e-4f);
            Assert.AreEqual(0f, ThreatRules.Rate(Starer, new ThreatContext { Observed = true }, 1.35f, 1.25f), "a frozen Starer stays frozen");
            Assert.AreEqual(-3f, ThreatRules.Rate(Whisperer, onCctv, 1.35f, 1.25f), "watching never gets better with a worse night");
            Assert.AreEqual(0f, ThreatRules.ApplyMultipliers(0f, 2f, 2f));
        }

        [Test]
        public void TheStarerIgnoredReachesLethalInAbout10Seconds() {
            ThreatMeterCore meter = new ThreatMeterCore();
            float seconds = 0f;
            while (!meter.IsLethal && seconds < 200f) {
                meter.Tick(0.02f, ThreatRules.Rate(Starer, new ThreatContext(), 1f, ThreatRules.SanityFactor(100f, 1.5f)));
                seconds += 0.02f;
            }
            Assert.AreEqual(10f, seconds, 0.05f);
        }

        [Test]
        public void TheStarersMeterNeverFalls() {
            // D106: watching only stops the rise. Flicker between watched and unwatched frames at the
            // worst multipliers and check every tick
            ThreatMeterCore meter = new ThreatMeterCore();
            float previous = meter.Value;
            for (int frame = 0; frame < 1000 && !meter.IsLethal; frame++) {
                ThreatContext context = new ThreatContext { Observed = frame % 3 != 0, ObservedByCctv = frame % 2 == 0 };
                meter.Tick(0.02f, ThreatRules.Rate(Starer, context, 1.35f, 1.5f));
                Assert.GreaterOrEqual(meter.Value, previous, "frame " + frame);
                previous = meter.Value;
            }
            Assert.IsTrue(meter.IsLethal, "unwatched frames still add up to Lethal");
        }
    }
}
