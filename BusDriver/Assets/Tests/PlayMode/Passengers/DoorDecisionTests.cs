using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Passengers {
    // The decision at the door (§2.4): a rider walks up the step, says where they are going and
    // waits there until the driver waves them on or turns them away. The fare only goes in the
    // box for the ones who are let aboard (§2.7).
    public class DoorDecisionTests {
        const float TimeScale = 3f;
        const string StopId = "farm_gate";

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

        [UnityTest, Timeout(600000)]
        public IEnumerator OnlyTheRidersTheDriverAcceptsGetAboardAndPay() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            // This test answers the door itself
            FlowTestUtil.DoorDriver(night).Accept = false;

            DebugRiders debug = shift.DebugRiders;
            BusStop stop = night.Route.Stop(StopId);
            string endStop = shift.Progress.EndStop.StopId;
            Assert.IsNotNull(debug.SpawnWaiting(stop, Rider("look20", endStop)), "could not place the first rider");
            Assert.IsNotNull(debug.SpawnWaiting(stop, Rider("look21", endStop)), "could not place the second rider");

            Time.timeScale = TimeScale;
            yield return NightDrive.ArriveAt(night, StopId);
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open");

            // The first one walks up the step and stops there
            yield return FlowTestUtil.WaitFor(() => shift.Cabin.PassengerAtDoor != null, 60f, "a rider reaching the step");
            Passenger first = shift.Cabin.PassengerAtDoor;
            RiderRecord firstRecord = shift.Riders.For(first);
            int beforeCents = shift.Ledger.Totals.NetCents;

            // Waiting on an answer: not aboard, nothing charged, and the doors stay open, so the
            // bus can't pull away from somebody standing in the stairwell
            for (int i = 0; i < 60; i++) {
                Assert.AreEqual(first, shift.Cabin.PassengerAtDoor, "the rider left the step on their own");
                Assert.AreEqual(RiderStatus.Waiting, firstRecord.Status, "a rider on the step counts as aboard");
                Assert.IsFalse(first.IsAboard, "a rider on the step counts as aboard");
                Assert.IsTrue(shift.Doors.IsOpenWanted, "a rider on the step doesn't hold the doors");
                Assert.AreEqual(beforeCents, shift.Ledger.Totals.NetCents, "an undecided rider paid a fare");
                yield return null;
            }

            // Turned away: off the step and into the night, with no fare and no review
            first.RefuseAtDoor();
            yield return FlowTestUtil.WaitFor(() => firstRecord.Status == RiderStatus.Refused, 30f, "the refused rider");
            Assert.AreEqual(beforeCents, shift.Ledger.Totals.NetCents, "a refused rider paid a fare");
            yield return FlowTestUtil.WaitFor(() => first == null || !first.IsAboard, 30f, "the refused rider leaving");

            // The next in line steps up, and accepting them does put a fare in the box
            yield return FlowTestUtil.WaitFor(() => shift.Cabin.PassengerAtDoor != null, 60f, "the next rider reaching the step");
            Passenger second = shift.Cabin.PassengerAtDoor;
            RiderRecord secondRecord = shift.Riders.For(second);
            Assert.AreNotEqual(firstRecord, secondRecord, "the refused rider came back");
            second.AcceptAboard();
            yield return FlowTestUtil.WaitFor(() => secondRecord.Status == RiderStatus.Aboard, 30f, "the accepted rider");
            Assert.Greater(shift.Ledger.Totals.NetCents, beforeCents, "an accepted rider paid no fare");
            Assert.AreEqual(RiderStatus.Refused, firstRecord.Status, "the refused rider's status changed later on");
        }

        static RiderSpec Rider(string lookId, string destinationStopId) {
            return new RiderSpec { lookId = lookId, boardStopId = StopId, destinationStopId = destinationStopId };
        }
    }
}
