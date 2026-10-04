using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Passengers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // Night 1 with the Starer, end to end (T-M4-11, §2.10, §2.13, §2.19): kicking it pays the
    // bounty, and the demo run stops once to throw it out and reaches the Summary
    public class Night1StarerTests {
        const int Seed = 20260930;

        string saveRoot;
        int errors;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
            errors = 0;
            Application.logMessageReceived += CountErrors;
        }

        [TearDown]
        public void TearDown() {
            Application.logMessageReceived -= CountErrors;
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        void CountErrors(string message, string stack, LogType type) {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
                errors++;
            }
        }

        static RiderRecord Starer(ShiftServices shift) {
            foreach (RiderRecord record in shift.Riders.All) {
                if (record.MonsterId == "starer") {
                    return record;
                }
            }
            Assert.Fail("night 1 has no Starer");
            return null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Starer_Kicked_BountyAndSanity() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftServices shift = Object.FindAnyObjectByType<ShiftContext>().Shift;
            yield return FlowTestUtil.WaitFor(() => shift.Bus.IsStopped, 10f, "the bus standing at the depot");

            BusSeat seat = null;
            foreach (BusSeat candidate in shift.Cabin.Seats) {
                if (candidate.Row == 4 && candidate.Column == 1) {
                    seat = candidate;
                }
            }
            Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec {
                lookId = "look09", boardStopId = "farm_gate", destinationStopId = "", monsterId = "starer",
            }, seat);
            RiderRecord record = shift.Riders.For(rider);
            Assert.IsTrue(record.IsMonster);
            int bountyBefore = shift.Ledger.Totals.Cents(LedgerKind.Bounty);

            yield return OnFootKick.WalkUpAndKick(shift, rider);
            Assert.AreEqual(RiderStatus.Kicked, record.Status, "a kick, at once");
            yield return OnFootKick.WaitGoneAndSitDown(shift, rider);

            // §2.7: the fare it never paid (it was placed in its seat) isn't refunded; the bounty is paid
            Assert.AreEqual(bountyBefore + 500, shift.Ledger.Totals.Cents(LedgerKind.Bounty), "+500 ¢ bounty");
            LedgerEntry last = shift.Ledger.Entries[shift.Ledger.Entries.Count - 1];
            Assert.AreEqual(LedgerKind.Bounty, last.Kind);
            Assert.AreEqual(record.RiderId, last.RiderId);
            Assert.AreEqual(0, shift.Ledger.Totals.Cents(LedgerKind.Refund), "a monster's fare is kept");
            // §2.13 sanity +10: SanitySystem arrives in M5 and applies KickRules to the Kicked status
            // this rider now has (D105)
            Assert.AreEqual(10f, KickRules.SanityDelta(record.IsMonster, shift.Balance));
            Assert.AreEqual(ShiftState.Driving, shift.Director.State);
            Assert.AreEqual(PlayerMode.Driving, shift.Mode.Mode);
            Assert.AreEqual(0, errors, "no errors");
        }

        // D43's demo: AutoPilot drives night 1, stops once (at gas_station, where the Starer boards,
        // D106) to throw it out on foot, and reaches the Summary. The duration is logged, not
        // asserted. It is kicked while it's still in its grace period: after that, 10 s unwatched is
        // Lethal, and walking down the aisle toward it keeps it in the on-foot camera's view.
        [UnityTest, Timeout(900000)]
        public IEnumerator Night1_DemoRun_WithKick() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            Time.timeScale = 3f;
            yield return NightDrive.ServeStop(night, "farm_gate");

            // gas_station: serve it, but stay stopped with the doors shut to deal with the Starer
            yield return NightDrive.ArriveAt(night, "gas_station");
            Assert.IsTrue(shift.Doors.TryOpen());
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsFullyOpen, 10f, "the doors opening");
            BusStop stop = night.Route.Stop("gas_station");
            yield return FlowTestUtil.WaitFor(() => NightDrive.StopSettled(shift, stop), 180f, "gas_station's riders settling");
            shift.Doors.TryClose();
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsClosed, 10f, "the doors closing");
            RiderRecord starer = Starer(shift);
            Assert.AreEqual(RiderStatus.Aboard, starer.Status, "the Starer boarded at gas_station");
            MonsterBrain brain = starer.Passenger.GetComponent<MonsterBrain>();
            Assert.Less(brain.Threat, 25f, "still Dormant");

            Time.timeScale = 1f;
            Passenger passenger = starer.Passenger;
            yield return OnFootKick.WalkUpAndKick(shift, passenger);
            yield return OnFootKick.WaitGoneAndSitDown(shift, passenger);
            Assert.AreEqual(RiderStatus.Kicked, starer.Status);
            Time.timeScale = 3f;
            shift.AutoPilot.Continue();

            yield return NightDrive.ServeStop(night, "campground");
            yield return NightDrive.ArriveAt(night, "church");
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open at the church");
            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.Summary, 90f, "the Summary after the bus empties");
            Time.timeScale = 1f;

            NightResult result = shift.Director.Result;
            Assert.AreEqual(1, result.stats.monstersKicked);
            Assert.AreEqual(0, result.stats.innocentsKicked);
            Assert.AreEqual(500, result.totals.Cents(LedgerKind.Bounty));
            Assert.AreEqual(5, result.stats.ridersDelivered, "every human rider delivered");
            Assert.IsFalse(shift.Death.IsDying);
            Assert.AreEqual(0, errors, "no errors");
            Debug.Log($"[NIGHT1] demo run with a kick: duration={shift.Director.NightSeconds:0.0}s (timeScale 3 while driving)");
        }
    }
}
