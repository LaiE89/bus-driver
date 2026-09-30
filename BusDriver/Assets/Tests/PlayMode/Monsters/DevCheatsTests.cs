using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // The F1 monster and death cheats (T-M4-10, §4.18), run the way the overlay runs them
    public class DevCheatsTests {
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

        static void Run(ShiftServices shift, string label) {
            foreach (DebugCheat cheat in shift.Debug.Cheats) {
                if (cheat.Label == label) {
                    cheat.Run();
                    return;
                }
            }
            Assert.Fail("no cheat '" + label + "'");
        }

        // The one monster riding (night 1's own Starer is still waiting at gas_station)
        static MonsterBrain Only(ShiftServices shift) {
            MonsterBrain riding = null;
            foreach (MonsterBrain brain in shift.Monsters.Active) {
                if (brain.IsActive) {
                    Assert.IsNull(riding, "one monster riding");
                    riding = brain;
                }
            }
            Assert.IsNotNull(riding, "a monster riding");
            return riding;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Cheats_SpawnThreatGodModeLethalWin() {
            ShiftServices[] started = new ShiftServices[1];
            yield return KillSequenceTests.StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            shift.Cctv.ShowHome();

            Run(shift, "Spawn The Starer seated");
            MonsterBrain starer = Only(shift);
            Assert.AreEqual("starer", starer.MonsterId);
            Assert.IsTrue(starer.IsActive, "seated and aboard");
            Assert.AreEqual(SeatZone.Rear, starer.Passenger.Seat.Zone, "at the back");

            Run(shift, "Threat 50 (all)");
            Assert.AreEqual(50f, starer.Threat, 0.001f);

            Run(shift, "God mode on/off");
            Assert.IsFalse(shift.Death.Die(DeathCause.SanityZero), "god mode ignores a death");
            Assert.IsFalse(shift.Death.IsDying);

            Time.timeScale = 3f;
            Run(shift, "Force Lethal");
            yield return null;
            Assert.IsTrue(starer.Kill.IsRunning, "Lethal starts the kill sequence");
            yield return FlowTestUtil.WaitFor(() => !starer.Kill.IsRunning, 30f, "the kill sequence");
            Assert.AreEqual(KillOutcome.Spared, starer.Kill.LastOutcome, "god mode lets it off");
            Assert.IsFalse(shift.Death.IsDying);
            Assert.AreEqual(ShiftState.Driving, shift.Director.State);

            Run(shift, "God mode on/off");
            Time.timeScale = 1f;
            Run(shift, "Win the night");
            Assert.AreEqual(ShiftState.Summary, shift.Director.State, "straight to the Summary");
            Assert.IsNotNull(shift.Director.Result);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Cheats_KillMeByCause() {
            ShiftServices[] started = new ShiftServices[1];
            yield return KillSequenceTests.StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            Run(shift, "Kill me: fall");
            Assert.IsTrue(shift.Death.IsDying);
            Assert.AreEqual(DeathCause.Fall, shift.Death.Report.Cause);
            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.GameOver, 30f, "Game Over");
        }
    }
}
