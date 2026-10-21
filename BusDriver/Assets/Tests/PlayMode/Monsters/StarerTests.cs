using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // The Starer: silent one-row hops while unwatched; kill hop from the near-front row while
    // driving; never kills on foot
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
            return KillSequenceTests.SpawnSeated(shift, "starer", KillSequenceTests.Seat(shift, row, column), "look06",
                requireKillSequence: false);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_AdvancesOnlyUnobserved() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 9, 1);
            StarerAdvance advance = brain.GetComponent<StarerAdvance>();
            Assert.IsNotNull(advance, "the Starer's prefab carries its ability");
            Assert.IsNull(brain.Kill, "no aisle kill sequence");
            Assert.AreEqual(9, advance.BoardRow, "the row it sat down in");
            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees R9");
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the grace period");
            float watched = 0f;
            while (watched < 4f) {
                yield return null;
                watched += Time.deltaTime;
                Assert.AreEqual(9, advance.CurrentRow, "watched, it stays put");
                Assert.AreEqual(0, advance.Advances);
            }

            shift.Cctv.ShowHome();
            float unseen = 0f;
            while (advance.Advances == 0) {
                yield return null;
                unseen += Time.deltaTime;
                Assert.Less(unseen, 8f, "it never hopped unwatched");
            }
            Assert.GreaterOrEqual(unseen, 2.5f, "at least one advance interval unobserved");
            Assert.AreEqual(8, advance.CurrentRow, "one silent row closer");
            Assert.AreEqual(1, brain.Passenger.Seat.Column, "nearest free seat in that row");
            Assert.IsFalse(brain.IsObserved);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Starer_Ignored_KillsFromNearFront() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 2, 3);
            StarerAdvance advance = brain.GetComponent<StarerAdvance>();
            float died = -1f;
            DeathReport report = null;
            shift.Death.OnDeathStarted += r => {
                died = Time.time;
                report = r;
            };
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the grace period");
            float afterGrace = Time.time;
            yield return FlowTestUtil.WaitFor(() => died >= 0f, 60f, "the Starer's kill hop");
            Assert.AreEqual(DeathCause.MonsterKill, report.Cause);
            Assert.AreEqual("starer", report.SourceId);
            Assert.AreEqual(2, advance.CurrentRow, "it never stood in the aisle; still seated");
            Assert.IsNotNull(brain.Passenger.Seat);
            Assert.GreaterOrEqual(died - afterGrace, 2.5f, "one advance interval after grace");
            Time.timeScale = 1f;
            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.GameOver, 20f, "Game Over");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_Watched_NeverAdvances() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 5, 1);
            StarerAdvance advance = brain.GetComponent<StarerAdvance>();
            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees R5");
            Time.timeScale = 3f;
            float watched = 0f;
            while (watched < 40f) {
                yield return null;
                watched += Time.deltaTime;
            }
            Assert.AreEqual(0, advance.Advances, "watched the whole time, never hopped");
            Assert.AreEqual(5, advance.CurrentRow);
            Assert.IsFalse(shift.Death.IsDying);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_OnFoot_NeverKills() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Starer(shift, 2, 1);
            Assert.IsTrue(shift.Bus.IsStopped);
            Assert.IsTrue(shift.Mode.TryLeaveSeat(), "couldn't leave the seat");
            Assert.AreEqual(PlayerMode.OnFoot, shift.Mode.Mode);
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the grace period");
            float waited = 0f;
            while (waited < 20f) {
                yield return null;
                waited += Time.deltaTime;
                Assert.IsFalse(shift.Death.IsDying, "on foot, the Starer must not kill");
            }
            Assert.AreEqual(2, brain.GetComponent<StarerAdvance>().CurrentRow);
        }
    }
}
