using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // Scare arbitration (§2.17, T-M4-04), every rule against a fake clock
    public class ScareArbiterTests {
        double now;
        ScareArbiter arbiter;

        [SetUp]
        public void SetUp() {
            now = 100.0;
            arbiter = new ScareArbiter(ScareArbiterSettings.Default, () => now) { Driving = true };
        }

        ScareDecision Ask(string id, ScareTier tier, object payload = null) {
            return arbiter.Request(id, tier, payload);
        }

        // Plays a scare to its end
        void Play(string id, ScareTier tier) {
            Assert.IsTrue(Ask(id, tier).Accepted, id + " should play");
            arbiter.NotifyFinished();
        }

        static void AssertDecision(ScareDecision decision, ScareOutcome outcome, ScareReason reason) {
            Assert.AreEqual(outcome, decision.Outcome, decision.ToString());
            Assert.AreEqual(reason, decision.Reason, decision.ToString());
        }

        [Test]
        public void NoScaresOutsideDriving() {
            arbiter.Driving = false;
            foreach (ScareTier tier in new[] { ScareTier.Ambient, ScareTier.Startle, ScareTier.Monster, ScareTier.Kill }) {
                AssertDecision(Ask("s", tier), ScareOutcome.Dropped, ScareReason.NotDriving);
            }
        }

        [Test]
        public void NoScaresWhilePaused() {
            arbiter.Paused = true;
            foreach (ScareTier tier in new[] { ScareTier.Ambient, ScareTier.Startle, ScareTier.Monster, ScareTier.Kill }) {
                AssertDecision(Ask("s", tier), ScareOutcome.Dropped, ScareReason.Paused);
            }
            Assert.IsFalse(arbiter.HasQueued, "a paused Monster scare isn't queued either");
        }

        [Test]
        public void GlobalGapOf6SecondsBetweenStartleOrAbove() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 5.9;
            AssertDecision(Ask("scare.starer.lens", ScareTier.Monster), ScareOutcome.Queued, ScareReason.GlobalGap);
            AssertDecision(Ask("hal.horn", ScareTier.Startle), ScareOutcome.Dropped, ScareReason.GlobalGap);
        }

        [Test]
        public void TheGapIsMeasuredAcrossTiers() {
            Play("scare.starer.lens", ScareTier.Monster);
            now += 6.0;
            Assert.IsTrue(Ask("hal.window_knock", ScareTier.Startle).Accepted, "a Startle 6 s after a Monster scare is fine");
        }

        [Test]
        public void StartleCooldownIs10Seconds() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 9.0;
            AssertDecision(Ask("hal.horn", ScareTier.Startle), ScareOutcome.Dropped, ScareReason.Cooldown);
            now += 1.0;
            Assert.IsTrue(Ask("hal.horn", ScareTier.Startle).Accepted);
        }

        [Test]
        public void MonsterCooldownIs20Seconds() {
            Play("scare.starer.lens", ScareTier.Monster);
            now += 19.0;
            AssertDecision(Ask("scare.mimic.turn", ScareTier.Monster), ScareOutcome.Queued, ScareReason.Cooldown);
            now += 1.0;
            Assert.IsTrue(arbiter.Update(out ScareDecision released, out _));
            AssertDecision(released, ScareOutcome.Accepted, ScareReason.Released);
            Assert.AreEqual("scare.mimic.turn", released.Id);
        }

        [Test]
        public void AStartleDoesntStartOverOneStillPlaying() {
            Assert.IsTrue(Ask("scare.starer.lens", ScareTier.Monster).Accepted);
            now += 30.0;
            AssertDecision(Ask("hal.window_knock", ScareTier.Startle), ScareOutcome.Dropped, ScareReason.Busy);
            arbiter.NotifyFinished();
            Assert.IsTrue(Ask("hal.window_knock", ScareTier.Startle).Accepted);
        }

        [Test]
        public void KillPreemptsARunningScareAndIgnoresGapsAndCooldowns() {
            Assert.IsTrue(Ask("scare.starer.lens", ScareTier.Monster).Accepted);
            now += 0.5;
            ScareDecision kill = Ask("scare.starer.kill", ScareTier.Kill);
            AssertDecision(kill, ScareOutcome.Accepted, ScareReason.Preempts);
            Assert.AreEqual(ScareTier.Kill, arbiter.RunningTier);
        }

        [Test]
        public void KillWithNothingRunningIsAPlainAccept() {
            AssertDecision(Ask("scare.starer.kill", ScareTier.Kill), ScareOutcome.Accepted, ScareReason.None);
        }

        [Test]
        public void ABlockedMonsterScareWaitsUpTo5SecondsThenIsDropped() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 1.0;
            AssertDecision(Ask("scare.starer.lens", ScareTier.Monster), ScareOutcome.Queued, ScareReason.GlobalGap);
            Assert.AreEqual("scare.starer.lens", arbiter.QueuedId);
            now += 4.0;
            Assert.IsFalse(arbiter.Update(out _, out _), "still inside the gap: keeps waiting");
            now += 1.01;
            Assert.IsTrue(arbiter.Update(out ScareDecision expired, out _));
            AssertDecision(expired, ScareOutcome.Dropped, ScareReason.Expired);
            Assert.IsFalse(arbiter.HasQueued);
        }

        [Test]
        public void AQueuedMonsterScarePlaysOnceTheGapIsOverWithItsPayload() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 2.0;
            object payload = new object();
            Ask("scare.starer.lens", ScareTier.Monster, payload);
            now += 3.5;
            Assert.IsFalse(arbiter.Update(out _, out _));
            now += 0.5;
            Assert.IsTrue(arbiter.Update(out ScareDecision released, out object back));
            AssertDecision(released, ScareOutcome.Accepted, ScareReason.Released);
            Assert.AreSame(payload, back);
            Assert.IsTrue(arbiter.IsRunning);
        }

        [Test]
        public void TheQueueHoldsOneMonsterScare() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 1.0;
            AssertDecision(Ask("scare.starer.lens", ScareTier.Monster), ScareOutcome.Queued, ScareReason.GlobalGap);
            AssertDecision(Ask("scare.mimic.turn", ScareTier.Monster), ScareOutcome.Dropped, ScareReason.QueueFull);
            Assert.AreEqual("scare.starer.lens", arbiter.QueuedId, "the first one keeps its place");
        }

        [Test]
        public void ABlockedStartleIsDroppedNotQueued() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 1.0;
            AssertDecision(Ask("hal.horn", ScareTier.Startle), ScareOutcome.Dropped, ScareReason.GlobalGap);
            Assert.IsFalse(arbiter.HasQueued);
        }

        [Test]
        public void AmbientIgnoresGapsCooldownsAndRunningScares() {
            Assert.IsTrue(Ask("scare.starer.lens", ScareTier.Monster).Accepted);
            now += 0.1;
            Assert.IsTrue(Ask("hal.static_burst", ScareTier.Ambient).Accepted);
            Assert.IsTrue(Ask("hal.cabin_flicker", ScareTier.Ambient).Accepted);
            arbiter.NotifyFinished();
            now += 6.0;
            Assert.IsTrue(Ask("hal.window_knock", ScareTier.Startle).Accepted, "Ambient scares don't count toward the gap");
        }

        [Test]
        public void DuringATelegraphOnlyKillIsAllowed() {
            arbiter.TelegraphActive = true;
            AssertDecision(Ask("hal.static_burst", ScareTier.Ambient), ScareOutcome.Dropped, ScareReason.Telegraph);
            AssertDecision(Ask("hal.window_knock", ScareTier.Startle), ScareOutcome.Dropped, ScareReason.Telegraph);
            AssertDecision(Ask("scare.starer.lens", ScareTier.Monster), ScareOutcome.Queued, ScareReason.Telegraph);
            Assert.IsFalse(arbiter.Update(out _, out _), "the queued scare waits out the telegraph");
            AssertDecision(Ask("scare.starer.kill", ScareTier.Kill), ScareOutcome.Accepted, ScareReason.None);
        }

        [Test]
        public void AKillCancelsTheQueuedScare() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 1.0;
            Ask("scare.starer.lens", ScareTier.Monster);
            Ask("scare.starer.kill", ScareTier.Kill);
            Assert.IsFalse(arbiter.HasQueued);
            Assert.AreEqual(ScareReason.Cancelled, arbiter.Recent(1).Reason);
        }

        [Test]
        public void AQueuedScareIsReleasedWhenTheTelegraphEndsInTime() {
            arbiter.TelegraphActive = true;
            Ask("scare.starer.lens", ScareTier.Monster);
            now += 4.0;
            arbiter.TelegraphActive = false;
            Assert.IsTrue(arbiter.Update(out ScareDecision released, out _));
            Assert.IsTrue(released.Accepted);
        }

        [Test]
        public void LeavingDrivingDropsTheQueuedScare() {
            Play("hal.window_knock", ScareTier.Startle);
            now += 1.0;
            Ask("scare.starer.lens", ScareTier.Monster);
            arbiter.Driving = false;
            Assert.IsFalse(arbiter.HasQueued);
            AssertDecision(arbiter.Recent(0), ScareOutcome.Dropped, ScareReason.NotDriving);
        }

        [Test]
        public void TheLogKeepsTheLast8DecisionsNewestFirst() {
            for (int i = 0; i < 10; i++) {
                Ask("a" + i, ScareTier.Ambient);
            }
            Assert.AreEqual(ScareArbiter.LogSize, arbiter.LogCount);
            Assert.AreEqual("a9", arbiter.Recent(0).Id);
            Assert.AreEqual("a2", arbiter.Recent(7).Id);
        }

        [Test]
        public void SettingsComeFromBalance() {
            BalanceConfig balance = UnityEngine.ScriptableObject.CreateInstance<BalanceConfig>();
            balance.scareGlobalGap = 3f;
            ScareArbiterSettings settings = ScareArbiterSettings.From(balance);
            Assert.AreEqual(3f, settings.GlobalGap);
            Assert.AreEqual(20f, settings.MonsterCooldown);
            Assert.AreEqual(10f, settings.StartleCooldown);
            Assert.AreEqual(5f, settings.MonsterQueueSeconds);
            UnityEngine.Object.DestroyImmediate(balance);
        }
    }
}
