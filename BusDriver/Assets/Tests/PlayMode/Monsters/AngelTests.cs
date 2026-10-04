using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // Main-branch Weeping Angel on MonsterBrain: hunt after a sit delay, freeze when watched,
    // proximity kill plays killScare then Die.
    public class AngelTests {
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

        static MonsterBrain Angel(ShiftServices shift, int row, int column) {
            return KillSequenceTests.SpawnSeated(shift, "weeping_angel", KillSequenceTests.Seat(shift, row, column), "look07");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Angel_FreezesWhenObserved() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Angel(shift, 9, 1);
            AngelStalk stalk = brain.GetComponent<AngelStalk>();
            Assert.IsNotNull(stalk, "the Weeping Angel prefab carries AngelStalk");
            Assert.IsNull(brain.Kill, "main-style angel has no kill-sequence telegraph");

            stalk.ForceHuntReady();
            shift.Cctv.ShowHome();
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => stalk.IsHunting, 10f, "the hunt to start");

            float walked = 0f;
            Vector3 beforeWalk = brain.Passenger.transform.localPosition;
            while (walked < 1.5f) {
                yield return null;
                walked += Time.deltaTime;
            }
            Assert.Greater(Vector3.Distance(beforeWalk, brain.Passenger.transform.localPosition), 0.05f, "unwatched, it moves");

            bool[] found = new bool[1];
            yield return KillSequenceTests.ObserveOnCctv(shift, brain, found);
            Assert.IsTrue(found[0], "a CCTV camera sees it in the aisle");
            Vector3 frozenAt = brain.Passenger.transform.localPosition;
            float held = 0f;
            while (held < 2f) {
                yield return null;
                held += Time.deltaTime;
                Assert.Less(Vector3.Distance(frozenAt, brain.Passenger.transform.localPosition), 0.01f, "observed, it freezes");
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Angel_ProximityKill_PlaysScareThenDies() {
            ShiftServices[] started = new ShiftServices[1];
            yield return Night(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterBrain brain = Angel(shift, 2, 1);
            AngelStalk stalk = brain.GetComponent<AngelStalk>();
            Assert.IsNotNull(brain.Definition.killScare, "killScare is assigned");

            DeathReport report = null;
            shift.Death.OnDeathStarted += r => report = r;
            stalk.ForceHuntReady();
            shift.Cctv.ShowHome();
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => report != null, 120f, "the angel's proximity kill");
            Assert.AreEqual(DeathCause.MonsterKill, report.Cause);
            Assert.AreEqual(MonsterIds.WeepingAngel, report.SourceId);
            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.GameOver, 20f, "Game Over");
        }
    }
}
