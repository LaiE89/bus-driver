using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Core {
    // A kick's sanity effect (§2.13, §2.15, T-M4-11)
    public class KickRulesTests {
        [Test]
        public void MonsterKickRaisesSanity_InnocentKickLowersIt() {
            BalanceConfig balance = ScriptableObject.CreateInstance<BalanceConfig>();
            Assert.AreEqual(10f, KickRules.SanityDelta(true, balance));
            Assert.AreEqual(-8f, KickRules.SanityDelta(false, balance));
            Assert.AreEqual(0f, KickRules.SanityDelta(true, null));
            Object.DestroyImmediate(balance);
        }
    }
}
