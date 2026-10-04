using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Route;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Passengers {
    // Missed-stop consequences and the end-stop delivery (T-M3-04, §2.4) on night 1
    public class StopConsequenceTests {
        const float TimeScale = 3f;
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

        // Serve farm_gate (look01 → campground, look02 → church), then drive straight to the church:
        // gas_station and campground are missed. Waiting riders walk away; look01 is retargeted to
        // the next stop (church), gets off there, and takes the fare back.
        [UnityTest, Timeout(900000)]
        public IEnumerator MissedStop_RidersLostAndCarried() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            RouteProgress progress = shift.Progress;

            Time.timeScale = TimeScale;
            yield return NightDrive.ServeStop(night, "farm_gate");
            RiderRecord look01 = NightDrive.Rider(shift, "look01");
            RiderRecord look02 = NightDrive.Rider(shift, "look02");
            RiderRecord look03 = NightDrive.Rider(shift, "look03");
            RiderRecord look04 = NightDrive.Rider(shift, "look04");
            RiderRecord look05 = NightDrive.Rider(shift, "look05");
            Assert.AreEqual(RiderStatus.Aboard, look01.Status);
            Assert.AreEqual(RiderStatus.Aboard, look02.Status);

            shift.AutoPilot.StopAt("church");
            yield return FlowTestUtil.WaitFor(() => progress.Find("gas_station").State == StopState.Missed, 240f, "gas_station missed");
            Assert.AreEqual(RiderStatus.Lost, look03.Status, "a missed stop's riders walk away");
            Assert.AreEqual(RiderStatus.Lost, look04.Status);
            Assert.AreEqual(0, night.Route.Stop("gas_station").WaitingCount);
            Assert.AreEqual("campground", look01.DestinationStopId, "look01 is still going to campground");

            yield return FlowTestUtil.WaitFor(() => progress.Find("campground").State == StopState.Missed, 240f, "campground missed");
            Assert.AreEqual(RiderStatus.Lost, look05.Status);
            Assert.AreEqual("church", look01.DestinationStopId, "a rider whose stop was missed gets off at the next stop");
            Assert.IsTrue(look01.Retargeted);
            Assert.IsFalse(look02.Retargeted);
            // Walked off and despawned
            yield return FlowTestUtil.WaitFor(() => look03.Passenger == null && look04.Passenger == null, 60f, "the gas_station riders despawning");

            yield return NightDrive.ArriveAt(night, "church");
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open at the church");
            yield return FlowTestUtil.WaitFor(() => progress.TerminusReached, 20f, "the end stop served");
            Assert.AreEqual(RiderStatus.Delivered, look01.Status, "the retargeted rider is dropped at the next stop");
            Assert.AreEqual(RiderStatus.Delivered, look02.Status);
            Assert.AreEqual("church", look01.ExitStopId);
            Assert.AreEqual("church", look02.ExitStopId);
            Assert.AreEqual(0, shift.Ledger.Totals.Cents(LedgerKind.Refund),
                "the missed-stop refund waits until their farewell on the step");
            Assert.AreEqual(0, shift.Riders.Aboard.Count, "no non-monster rider is left aboard at the end stop");
            Assert.AreEqual(StopState.Served, progress.Find("church").State, "the end stop can't be missed");
            // Farewell (and refund) fire on the doorstep; then they finish walking off
            yield return FlowTestUtil.WaitFor(
                () => shift.Ledger.Totals.Cents(LedgerKind.Refund) == -shift.Balance.fareCents, 60f,
                "the missed-stop rider taking their fare back at farewell");
            yield return FlowTestUtil.WaitFor(() => shift.Cabin.Passengers.Count == 0, 60f, "the delivered riders getting off");
        }
    }
}
