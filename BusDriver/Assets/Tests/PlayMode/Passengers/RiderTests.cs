using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Passengers {
    // Riders, destinations and the alight-then-board order (T-M3-02, §2.4, §2.6)
    public class RiderTests {
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
        public IEnumerator Stop_AlightsThenBoards() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            DebugRiders debug = shift.DebugRiders;
            BusCabin cabin = shift.Cabin;
            BusStop stop = night.Route.Stop(StopId);
            string endStop = shift.Progress.EndStop.StopId;

            // Two riding to this stop, two waiting at it
            for (int i = 0; i < 2; i++) {
                Assert.IsNotNull(debug.SpawnSeated(new RiderSpec { lookId = "look0" + (7 + i), boardStopId = "depot", destinationStopId = StopId }));
                Assert.IsNotNull(debug.SpawnWaiting(stop, new RiderSpec { lookId = "look0" + (1 + i), boardStopId = StopId, destinationStopId = endStop }));
            }
            Assert.AreEqual(2, shift.Riders.Aboard.Count);
            List<string> events = new List<string>();
            cabin.OnPassengerLeft += p => events.Add("left " + shift.Riders.For(p).Spec.lookId);
            cabin.OnPassengerBoarded += p => events.Add("boarded " + shift.Riders.For(p).Spec.lookId);

            Time.timeScale = TimeScale;
            shift.AutoPilot.StopAt(StopId);
            yield return FlowTestUtil.WaitFor(() => shift.AutoPilot.Arrived && shift.Bus.IsStopped, 240f, "AutoPilot arriving at " + StopId);
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open");
            int waitingAtStart = stop.WaitingCount;
            yield return FlowTestUtil.WaitFor(() => events.Count >= 2 + waitingAtStart, 120f, "everyone alighting and boarding");

            Assert.GreaterOrEqual(waitingAtStart, 2);
            Assert.IsTrue(events[0].StartsWith("left") && events[1].StartsWith("left"),
                "both riders for this stop must be off before anyone boards: " + string.Join(", ", events));
            for (int i = 2; i < events.Count; i++) {
                StringAssert.StartsWith("boarded", events[i]);
            }
            int delivered = 0;
            foreach (RiderRecord record in shift.Riders.All) {
                if (record.Status == RiderStatus.Delivered) {
                    delivered++;
                    Assert.AreEqual(StopId, record.ExitStopId);
                }
            }
            Assert.AreEqual(2, delivered, "the two riders for this stop are Delivered");
        }

        // Seats come from the seating stream, in the preferred zone while it has room (§2.6)
        [UnityTest, Timeout(300000)]
        public IEnumerator Seats_FollowZonePreference() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForNight(game);
            BusCabin cabin = Object.FindAnyObjectByType<ShiftContext>().Shift.Cabin;
            // The rear zone is rows R7–R9: 12 seats
            for (int i = 0; i < 12; i++) {
                BusSeat seat = cabin.FindFreeSeat(SeatZone.Rear, SeatZone.Mid);
                Assert.IsNotNull(seat);
                Assert.AreEqual(SeatZone.Rear, seat.Zone, "the rear zone still had room");
                seat.Reserve(new GameObject("Seat Holder").AddComponent<Passenger>());
            }
            // Rear is full: the fallback zone next
            Assert.AreEqual(SeatZone.Mid, cabin.FindFreeSeat(SeatZone.Rear, SeatZone.Mid).Zone);
            Assert.AreEqual(SeatZone.Front, BusSeat.ZoneOfRow(3));
            Assert.AreEqual(SeatZone.Mid, BusSeat.ZoneOfRow(4));
            Assert.AreEqual(SeatZone.Rear, BusSeat.ZoneOfRow(7));
        }
    }
}
