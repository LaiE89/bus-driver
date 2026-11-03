using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // The kill sequence (T-M4-07, §2.14, D31): one at a time, a telegraph the player can escape,
    // and the threat set back when they do
    public class KillSequenceTests {
        const int Seed = 20260930;

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

        internal static IEnumerator StartNight(string saveRoot, ShiftServices[] result) {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            result[0] = Object.FindAnyObjectByType<ShiftContext>().Shift;
        }

        internal static MonsterBrain SpawnSeated(ShiftServices shift, string monsterId, BusSeat seat, string lookId,
            bool requireKillSequence = true) {
            Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec {
                lookId = lookId, boardStopId = "campground", destinationStopId = "", monsterId = monsterId,
            }, seat);
            Assert.IsNotNull(rider, "no rider spawned in " + (seat != null ? seat.name : "a free seat"));
            MonsterBrain brain = rider.GetComponent<MonsterBrain>();
            Assert.IsNotNull(brain);
            if (requireKillSequence) {
                Assert.IsNotNull(brain.Kill, monsterId + " has a KillSequence on its prefab");
            }
            return brain;
        }

        internal static BusSeat Seat(ShiftServices shift, int row, int column) {
            foreach (BusSeat seat in shift.Cabin.Seats) {
                if (seat.Row == row && seat.Column == column) {
                    return seat;
                }
            }
            return null;
        }

        // Cycles the CCTV until a camera observes the monster; false if none does
        internal static IEnumerator ObserveOnCctv(ShiftServices shift, MonsterBrain brain, bool[] found) {
            CCTVSystem cctv = shift.Cctv;
            cctv.ShowHome();
            for (int i = 0; i < cctv.Cameras.Count; i++) {
                cctv.Cycle();
                yield return null;
                yield return null;
                if (brain.IsObserved) {
                    found[0] = true;
                    yield break;
                }
            }
            cctv.ShowHome();
            found[0] = false;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator KillSequence_OnlyOneAtATime() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            SparingPreventer spare = new SparingPreventer();
            shift.Death.AddPreventer(spare);
            shift.Cctv.ShowHome();

            // Mimic still uses a KillSequence; the Starer kills via seat hops instead
            MonsterBrain first = SpawnSeated(shift, "mimic", Seat(shift, 8, 0), "look07");
            MonsterBrain second = SpawnSeated(shift, "mimic", Seat(shift, 9, 3), "look08");
            Time.timeScale = 3f;
            yield return FlowTestUtil.WaitFor(() => !first.Meter.Core.InGrace && !second.Meter.Core.InGrace, 30f, "the grace periods");

            first.Meter.Core.SetValue(100f);
            yield return null;
            Assert.AreSame(first, shift.Monsters.KillOwner, "the first to reach Lethal takes the slot");
            Assert.IsTrue(first.Kill.InTelegraph);
            Assert.IsTrue(shift.Scares.TelegraphActive, "the telegraph suppresses scares");

            second.Meter.Core.SetValue(100f);
            yield return null;
            Assert.AreSame(first, shift.Monsters.KillOwner, "the slot stays with the first");
            Assert.IsFalse(second.Kill.IsRunning, "a second kill sequence never starts while one runs");
            Assert.AreEqual(ThreatMeterCore.HeldCeiling, second.Threat, 0.001f, "the second holds at 99.9");
            Assert.IsTrue(second.Meter.Core.HeldBelowLethal);
            Assert.IsTrue(first.Meter.Core.Frozen && second.Meter.Core.Frozen, "every meter freezes during a kill sequence");

            // The first runs its 4 s telegraph and is spared; the second then climbs the last 0.1
            yield return FlowTestUtil.WaitFor(() => !first.Kill.IsRunning, 30f, "the first kill sequence to end");
            Assert.AreEqual(KillOutcome.Spared, first.Kill.LastOutcome);
            Assert.AreEqual(1, spare.Count);
            yield return FlowTestUtil.WaitFor(() => second.Kill.IsRunning, 10f, "the held monster's own sequence");
            Assert.AreSame(second, shift.Monsters.KillOwner);
            Assert.IsFalse(first.Kill.IsRunning, "only one sequence at a time");
            yield return FlowTestUtil.WaitFor(() => !second.Kill.IsRunning, 30f, "the second kill sequence to end");
            Assert.AreEqual(KillOutcome.Spared, second.Kill.LastOutcome);
            Assert.IsNull(shift.Monsters.KillOwner, "the slot is free again");
            Assert.IsFalse(shift.Scares.TelegraphActive);
            Assert.AreEqual(ShiftState.Driving, shift.Director.State, "nobody died");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator KillSequence_EscapeResetsThreat() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            shift.Cctv.ShowHome();
            // Mimic escapes by staying unobserved (§2.12)
            MonsterBrain mimic = SpawnSeated(shift, "mimic", Seat(shift, 5, 1), "look07");
            float threatAtEscape = -1f;
            float telegraphAtEscape = -1f;
            mimic.Kill.OnResolved += (brain, outcome) => {
                threatAtEscape = brain.Threat;
                telegraphAtEscape = mimic.Kill.TelegraphElapsed;
            };
            Time.timeScale = 2f;
            yield return FlowTestUtil.WaitFor(() => !mimic.Meter.Core.InGrace, 30f, "the grace period");

            mimic.Meter.Core.SetValue(100f);
            yield return FlowTestUtil.WaitFor(() => mimic.Kill.InTelegraph, 5f, "the telegraph");
            Assert.IsFalse(mimic.IsObserved, "home view: Mimic escapes by looking away");

            yield return FlowTestUtil.WaitFor(() => !mimic.Kill.IsRunning, 30f, "the escape");
            Assert.AreEqual(KillOutcome.Escaped, mimic.Kill.LastOutcome, "unwatched for 2 s, it backs down");
            Assert.AreEqual(mimic.Definition.escape.resetThreat, threatAtEscape, 0.001f, "the threat goes back to 60");
            Assert.GreaterOrEqual(telegraphAtEscape, mimic.Definition.escape.seconds, "it took the escape seconds");
            Assert.Less(telegraphAtEscape, mimic.Definition.killTelegraphSeconds, "within the telegraph");
            Assert.IsNull(shift.Monsters.KillOwner);
            Assert.IsFalse(shift.Scares.TelegraphActive);
            Assert.IsFalse(shift.Death.IsDying);
            Assert.AreEqual(ShiftState.Driving, shift.Director.State);
            Assert.AreEqual(RiderStatus.Aboard, shift.Riders.For(mimic.Passenger).Status, "still aboard");
        }
    }
}
