using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // The Starer (T-M4-08, §2.10): it only moves unwatched, it kills when ignored, it never gets
    // anywhere while watched, and watching it through the telegraph sends it back to R2
    public class StarerTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static IEnumerator Night(string saveRoot, ShiftServices[] result) {
            yield return KillSequenceTests.StartNight(saveRoot, result);
            result[0].Cctv.ShowHome();
        }

        static MonsterBrain Starer(ShiftServices shift, int row, int column) {
            return KillSequenceTests.SpawnSeated(shift, "starer", KillSequenceTests.Seat(shift, row, column), "look06");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_AdvancesOnlyUnobserved() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 9, 1);
            StarerAdvance advance = brain.GetComponent<StarerAdvance>();
            Assert.IsNotNull(advance, "the Starer's prefab carries its ability");
            Assert.AreEqual(9, advance.BoardRow, "the row it sat down in");
            // On camera before the grace runs out: unwatched at +10/s it would be moving up a row
            // within a second of grace ending (D106)
            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees R9");
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the grace period");
            Assert.AreEqual(0f, brain.Threat, 0.001f, "watched through the end of grace: still at 0");
            brain.Meter.Core.SetValue(50f);
            Assert.AreEqual(5, advance.TargetRow, "9 − round(8 × 0.5)");
            float watched = 0f;
            while (watched < 3f) {
                yield return null;
                watched += Time.deltaTime;
                Assert.IsTrue(brain.IsObserved, "the camera keeps it in view");
                Assert.AreEqual(9, advance.CurrentRow, "watched, it stays put");
                Assert.AreEqual(50f, brain.Threat, 0.001f, "watched, its threat neither rises nor falls (D106)");
            }
            Assert.AreEqual(0, advance.Advances);
            Assert.Less(advance.TargetRow, 9, "it is still behind its target");

            shift.Cctv.ShowHome();
            float unseen = 0f;
            while (advance.Advances == 0) {
                yield return null;
                unseen += Time.deltaTime;
                Assert.Less(unseen, 5f, "it never moved unwatched");
            }
            Assert.GreaterOrEqual(unseen, 0.95f, "at least 1 s unobserved first");
            Assert.AreEqual(advance.TargetRow, advance.CurrentRow, "straight into its target row");
            Assert.AreEqual(1, brain.Passenger.Seat.Column, "the free seat nearest its column (its own, L1)");
            Assert.IsFalse(brain.IsObserved);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Starer_Ignored_Kills() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            float satDown = Time.time;
            MonsterBrain brain = Starer(shift, 9, 3);
            float died = -1f;
            DeathReport report = null;
            shift.Death.OnDeathStarted += r => {
                died = Time.time;
                report = r;
            };
            // Parked at the depot, eyes on the road: nobody ever looks at it
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => died >= 0f, 180f, "the Starer's kill");
            float seconds = died - satDown;
            Debug.Log($"[STARER] ignored: Die after {seconds:0.0} s (advanced {brain.GetComponent<StarerAdvance>().Advances} times)");
            Assert.AreEqual(DeathCause.MonsterKill, report.Cause);
            Assert.AreEqual("starer", report.SourceId);
            // Grace + 100 / the unwatched rate + the telegraph + the kill scare: about 26 s (D106)
            MonsterDefinition def = brain.Definition;
            float expected = def.graceSeconds + 100f / def.rules[def.rules.Length - 1].ratePerSecond
                + def.killTelegraphSeconds + def.killScare.Duration;
            Assert.GreaterOrEqual(seconds, expected - 2f, $"expected about {expected:0.0} s");
            Assert.LessOrEqual(seconds, expected + 3f, $"expected about {expected:0.0} s");
            Assert.AreEqual(KillOutcome.Killed, brain.Kill.LastOutcome);
            Time.timeScale = 1f;
            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.GameOver, 20f, "Game Over");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_Watched_NeverLethal() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 5, 1);
            StarerAdvance advance = brain.GetComponent<StarerAdvance>();
            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees R5");
            Time.timeScale = 3f;
            float highest = 0f;
            float watched = 0f;
            while (watched < 120f) {
                yield return null;
                watched += Time.deltaTime;
                highest = Mathf.Max(highest, brain.Threat);
            }
            Assert.Less(highest, 25f, "watched on CCTV for 120 s, it never leaves Dormant");
            Assert.Less(highest, 2f, "watched from the moment it sat down, it stays at 0 (D106)");
            Assert.AreEqual(ThreatStage.Dormant, brain.Stage);
            Assert.AreEqual(0, advance.Advances, "and never moved");
            Assert.IsFalse(brain.Kill.IsRunning);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_TelegraphEscape_ByWatching() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 7, 3);
            float threatAtEscape = -1f;
            brain.Kill.OnResolved += (b, outcome) => threatAtEscape = b.Threat;
            Time.timeScale = 2f;
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the grace period");

            brain.Meter.Core.SetValue(100f);
            yield return FlowTestUtil.WaitFor(() => brain.Kill.InTelegraph, 5f, "the telegraph");
            Assert.IsNull(brain.Passenger.Seat, "it stood up");
            float frontZ = shift.Cabin.SeatLocal(KillSequenceTests.Seat(shift, 1, 0)).z;
            Vector3 local = brain.Passenger.transform.localPosition;
            Assert.AreEqual(frontZ, local.z, 0.01f, "in the aisle beside R1");
            Assert.AreEqual(shift.Cabin.AisleAtDoorLocal.x, local.x, 0.01f);

            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees the aisle beside R1");
            yield return FlowTestUtil.WaitFor(() => !brain.Kill.IsRunning, 30f, "the escape");
            Assert.AreEqual(KillOutcome.Escaped, brain.Kill.LastOutcome);
            Assert.AreEqual(60f, threatAtEscape, 0.001f, "threat set to 60");
            Assert.IsNotNull(brain.Passenger.Seat, "it sat back down");
            Assert.AreEqual(StarerRules.EscapeRow, brain.Passenger.Seat.Row, "in R2");
            Assert.IsFalse(shift.Death.IsDying);
            Assert.AreEqual(ShiftState.Driving, shift.Director.State);
        }
    }
}
