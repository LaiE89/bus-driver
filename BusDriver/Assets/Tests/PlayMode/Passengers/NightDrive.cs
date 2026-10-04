using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;

namespace BusDriver.Tests.PlayMode.Passengers {
    // Drives a real night with AutoPilot, stop by stop, for the rider and economy tests (M3)
    static class NightDrive {
        // AutoPilot to the stop, stopped with the door in its zone
        public static IEnumerator ArriveAt(ShiftContext night, string stopId) {
            AutoPilot pilot = night.Shift.AutoPilot;
            BusController bus = night.Bus;
            pilot.StopAt(stopId);
            yield return FlowTestUtil.WaitFor(() => pilot.Arrived && bus.IsStopped, 300f, "AutoPilot arriving at " + stopId);
        }

        // Arrive, open the doors, wait until everyone for this stop is off and everyone waiting is
        // seated, then close the doors and drive on
        public static IEnumerator ServeStop(ShiftContext night, string stopId) {
            ShiftServices shift = night.Shift;
            yield return ArriveAt(night, stopId);
            Assert.IsTrue(shift.Doors.TryOpen(), "the doors wouldn't open at " + stopId);
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsFullyOpen, 10f, "the doors opening at " + stopId);
            BusStop stop = night.Route.Stop(stopId);
            yield return FlowTestUtil.WaitFor(() => StopSettled(shift, stop), 180f, "everyone alighting and boarding at " + stopId);
            shift.Doors.TryClose();
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsClosed, 10f, "the doors closing at " + stopId);
            shift.AutoPilot.Continue();
        }

        // Nobody left to get off here, nobody left waiting, nobody still walking to a seat
        public static bool StopSettled(ShiftServices shift, BusStop stop) {
            if (stop.WaitingCount > 0) {
                return false;
            }
            foreach (RiderRecord record in shift.Riders.All) {
                Passenger passenger = record.Passenger;
                if (passenger == null) {
                    continue;
                }
                if (record.Status == RiderStatus.Waiting && (passenger.State == PassengerState.Boarding
                        || passenger.State == PassengerState.Greeting)) {
                    return false;
                }
                if (record.Status == RiderStatus.Aboard) {
                    if (passenger.State != PassengerState.Seated) {
                        return false;
                    }
                    if (!record.IsMonster && record.DestinationStopId == stop.StopId) {
                        return false;
                    }
                }
                if (record.Status == RiderStatus.Delivered && passenger.IsAboard) {
                    return false;
                }
            }
            return true;
        }

        // The rider in this look (looks are unique within a night's manifest, D34)
        public static RiderRecord Rider(ShiftServices shift, string lookId) {
            foreach (RiderRecord record in shift.Riders.All) {
                if (record.Spec.lookId == lookId) {
                    return record;
                }
            }
            Assert.Fail("no rider in " + lookId);
            return null;
        }
    }
}
