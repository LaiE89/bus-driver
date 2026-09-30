using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // Threat rules (§2.9, T-M4-02): first match wins, multipliers on positive rates only
    public class ThreatRulesTests {
        // The Starer's list (§2.10)
        static readonly ThreatRule[] Starer = {
            new ThreatRule(ThreatCondition.Observed, -5f),
            new ThreatRule(ThreatCondition.Always, 1.5f),
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
            Assert.AreEqual(-5f, ThreatRules.BaseRate(Starer, new ThreatContext { Observed = true }));
            Assert.AreEqual(1.5f, ThreatRules.BaseRate(Starer, new ThreatContext()));
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
            ThreatContext watched = new ThreatContext { Observed = true };
            // Night 5's ×1.35 and sanity 30's ×1.25
            Assert.AreEqual(1.5f * 1.35f * 1.25f, ThreatRules.Rate(Starer, unwatched, 1.35f, 1.25f), 1e-5f);
            Assert.AreEqual(-5f, ThreatRules.Rate(Starer, watched, 1.35f, 1.25f), "watching never gets better with a worse night");
            Assert.AreEqual(0f, ThreatRules.ApplyMultipliers(0f, 2f, 2f));
        }

        [Test]
        public void TheStarerIgnoredReachesLethalInAbout67Seconds() {
            ThreatMeterCore meter = new ThreatMeterCore();
            float seconds = 0f;
            while (!meter.IsLethal && seconds < 200f) {
                meter.Tick(0.02f, ThreatRules.Rate(Starer, new ThreatContext(), 1f, ThreatRules.SanityFactor(100f, 1.5f)));
                seconds += 0.02f;
            }
            Assert.AreEqual(66.7f, seconds, 0.1f);
        }
    }
}
