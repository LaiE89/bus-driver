using BusDriver.Core.Data;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // The seeded monster definitions carry the §2.10–§2.12b numbers, and each has its generated
    // prefab (T-M4-03)
    public class MonsterContentTests {
        static GameRootConfig Config {
            get { return AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath); }
        }

        static MonsterDefinition Monster(string id) {
            MonsterDefinition monster = Config.Monster(id);
            Assert.IsNotNull(monster, "GameRootConfig.monsters has no " + id);
            return monster;
        }

        static void AssertRules(MonsterDefinition monster, params object[] conditionsAndRates) {
            Assert.AreEqual(conditionsAndRates.Length / 2, monster.rules.Length, monster.id + " rule count");
            for (int i = 0; i < monster.rules.Length; i++) {
                Assert.AreEqual((ThreatCondition)conditionsAndRates[i * 2], monster.rules[i].condition, $"{monster.id} rule {i}");
                Assert.AreEqual((float)conditionsAndRates[i * 2 + 1], monster.rules[i].ratePerSecond, 1e-5f, $"{monster.id} rule {i} rate");
            }
        }

        const ObserverKinds All = ObserverKinds.Cctv | ObserverKinds.Driver | ObserverKinds.OnFoot | ObserverKinds.Mirror;

        [Test]
        public void Starer_MatchesSection2_10() {
            MonsterDefinition m = Monster("starer");
            AssertRules(m, ThreatCondition.Observed, -5f, ThreatCondition.Always, 1.5f);
            Assert.AreEqual(All, m.observerKinds);
            Assert.AreEqual(SeatZone.Rear, m.seatZonePreference);
            Assert.AreEqual(EscapeKind.ObserveFor, m.escape.kind);
            Assert.AreEqual(1.5f, m.escape.seconds);
            Assert.AreEqual("It only moves when you aren't looking.", m.journal.hint);
            Assert.IsInstanceOf<StarerAdvanceConfig>(m.ability);
        }

        [Test]
        public void Whisperer_MatchesSection2_11() {
            MonsterDefinition m = Monster("whisperer");
            AssertRules(m, ThreatCondition.ObservedByCctv, -3f, ThreatCondition.AttentionOnRoad, 1.2f, ThreatCondition.Always, 0.4f);
            Assert.AreEqual(ObserverKinds.Cctv, m.observerKinds);
            Assert.AreEqual(EscapeKind.None, m.escape.kind, "it never starts a kill sequence");
            CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.6f, 1.2f }, m.Ability<WhispererDrainConfig>().drainPerSecondByStage);
        }

        [Test]
        public void Mimic_MatchesSection2_12() {
            MonsterDefinition m = Monster("mimic");
            AssertRules(m, ThreatCondition.Observed, 2.5f, ThreatCondition.Always, -1f);
            Assert.AreEqual(ObserverKinds.Cctv | ObserverKinds.Driver | ObserverKinds.Mirror, m.observerKinds, "not OnFoot (D22)");
            Assert.AreEqual(EscapeKind.UnobservedFor, m.escape.kind);
            Assert.AreEqual(2f, m.escape.seconds);
            Assert.AreEqual(120f, m.Ability<MimicCopyConfig>().replaceIntervalSeconds);
        }

        [Test]
        public void WeepingAngel_MatchesSection2_12b() {
            MonsterDefinition m = Monster("weeping_angel");
            AssertRules(m, ThreatCondition.Observed, 0f, ThreatCondition.Always, 1.2f);
            Assert.AreEqual(All, m.observerKinds);
            Assert.AreEqual(SeatZone.Rear, m.seatZonePreference);
            Assert.AreEqual(SeatZone.Mid, m.seatZoneFallback);
            Assert.AreEqual(EscapeKind.ObserveFor, m.escape.kind);
            Assert.AreEqual(2f, m.escape.seconds);
        }

        [Test]
        public void EveryMonster_SharesTheFrameworkDefaults_AndHasItsPrefab() {
            foreach (string id in new[] { "starer", "whisperer", "mimic", "weeping_angel" }) {
                MonsterDefinition m = Monster(id);
                Assert.AreEqual(10f, m.graceSeconds, id + " grace");
                Assert.AreEqual(4f, m.killTelegraphSeconds, id + " telegraph");
                Assert.AreEqual(500, m.bountyCents, id + " bounty");
                Assert.AreEqual(60f, m.escape.resetThreat, id + " escape reset");
                Assert.IsNotNull(m.logicPrefab, id + " has no generated prefab");
                MonsterBrain brain = m.logicPrefab.GetComponent<MonsterBrain>();
                Assert.IsNotNull(brain, id + " prefab has no MonsterBrain");
                Assert.AreSame(m, brain.Definition);
                Assert.IsNotNull(m.logicPrefab.GetComponent<ThreatMeter>());
                Assert.IsNotNull(m.logicPrefab.GetComponent<Passenger>());
                Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(m.logicPrefab), id + " is a variant of Passenger.prefab");
            }
        }
    }
}
