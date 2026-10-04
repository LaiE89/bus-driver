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
    // The kerb and aisle queues: riders walking the single-file paths never stand inside each
    // other, however many of them are getting on and off at once.
    public class QueueTests {
        const float TimeScale = 3f;
        const string StopId = "farm_gate";
        // Riders are about half a metre across and the queues leave a 0.9 m gap, so anything
        // closer than this means two of them are sharing a spot
        const float MinimumGap = 0.4f;

        string saveRoot;
        float worstGap;
        string worstPair;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
            worstGap = float.MaxValue;
            worstPair = "";
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NobodyOverlapsWhileGettingOnAndOff() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            DebugRiders debug = shift.DebugRiders;
            BusStop stop = night.Route.Stop(StopId);
            string endStop = shift.Progress.EndStop.StopId;

            // Two of them share a row, so they want the same spot in the aisle: the queue has to
            // hold one of them back a gap
            BusSeat[] row = OneRow(shift.Cabin);
            Assert.IsNotNull(debug.SpawnSeated(Rider("look10", StopId), row[0]), "could not seat the first of the pair");
            Assert.IsNotNull(debug.SpawnSeated(Rider("look11", StopId), row[1]), "could not seat the second of the pair");
            // Two more scattered elsewhere, and four waiting at the kerb
            for (int i = 2; i < 4; i++) {
                Assert.IsNotNull(debug.SpawnSeated(Rider("look1" + i, StopId)), "could not seat rider " + i);
            }
            for (int i = 0; i < 4; i++) {
                Assert.IsNotNull(debug.SpawnWaiting(stop, Rider("look2" + i, endStop)), "could not place waiting rider " + i);
            }

            Time.timeScale = TimeScale;
            yield return NightDrive.ArriveAt(night, StopId);
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open");
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsFullyOpen, 10f, "the doors opening");

            // Watch every frame until the stop has settled, then report the closest two ever got
            float deadline = Time.time + 180f;
            while (!NightDrive.StopSettled(shift, stop)) {
                Assert.Less(Time.time, deadline, "the stop never settled; closest pair so far: " + worstPair);
                SampleGaps(shift);
                yield return null;
            }
            SampleGaps(shift);
            Assert.Greater(worstGap, MinimumGap, "two riders stood inside each other: " + worstPair);
        }

        static RiderSpec Rider(string lookId, string destinationStopId) {
            return new RiderSpec { lookId = lookId, boardStopId = "depot", destinationStopId = destinationStopId };
        }

        // Two free seats level with each other, so they share one spot in the aisle
        static BusSeat[] OneRow(BusCabin cabin) {
            IReadOnlyList<BusSeat> seats = cabin.Seats;
            for (int i = 0; i < seats.Count; i++) {
                for (int j = i + 1; j < seats.Count; j++) {
                    if (seats[i] == null || seats[j] == null || !seats[i].IsFree || !seats[j].IsFree) {
                        continue;
                    }
                    if (Mathf.Abs(cabin.SeatLocal(seats[i]).z - cabin.SeatLocal(seats[j]).z) < 0.01f) {
                        return new[] { seats[i], seats[j] };
                    }
                }
            }
            Assert.Fail("the bus has no two free seats in one row");
            return null;
        }

        // Only riders on the single-file paths count: two in one row are supposed to sit close
        // together, and riders who have already stepped off are about to be destroyed anyway
        void SampleGaps(ShiftServices shift) {
            List<Passenger> walking = new List<Passenger>();
            foreach (RiderRecord record in shift.Riders.All) {
                Passenger passenger = record.Passenger;
                if (passenger == null) {
                    continue;
                }
                // A rider on the step is still standing in the aisle queue's first spot
                bool boarding = passenger.State == PassengerState.Boarding
                    || passenger.State == PassengerState.Greeting;
                bool leaving = passenger.State == PassengerState.Leaving && passenger.IsAboard;
                if (boarding || leaving) {
                    walking.Add(passenger);
                }
            }
            for (int i = 0; i < walking.Count; i++) {
                for (int j = i + 1; j < walking.Count; j++) {
                    // Both on the same path, so compare on the floor plane only
                    Vector3 offset = walking[i].transform.position - walking[j].transform.position;
                    offset.y = 0f;
                    float gap = offset.magnitude;
                    if (gap < worstGap) {
                        worstGap = gap;
                        worstPair = $"{shift.Riders.For(walking[i]).RiderId} and {shift.Riders.For(walking[j]).RiderId} "
                            + $"{gap:0.00} m apart, {walking[i].State} and {walking[j].State}";
                    }
                }
            }
        }
    }
}
