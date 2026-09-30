using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // One stop's record for the night (§2.4): Pending until it is Served (doors fully open in its
    // zone) or Missed (passed by more than the margin). Both are final.
    public sealed class StopRecord {
        public readonly string StopId;
        public readonly string DisplayName;
        public readonly int Index;
        public readonly float Distance;
        public readonly double ScheduledGameSeconds;

        public StopState State { get; internal set; }
        // Game-seconds since midnight when the doors were fully open; NaN until Served
        public double ArrivalGameSeconds { get; internal set; } = double.NaN;
        // Missed for a missed stop, None while Pending
        public ArrivalRating Rating { get; internal set; }
        // The night's terminus (§2.4). It can't be Missed.
        public bool IsEndStop { get; internal set; }
        // Stops past the night's end stop aren't part of this night (night 1 ends at church, D43)
        public bool InNight { get; internal set; }

        internal StopRecord(RouteStop stop, int index, double scheduled) {
            StopId = stop.stopId;
            DisplayName = stop.displayName;
            Index = index;
            Distance = stop.distance;
            ScheduledGameSeconds = scheduled;
        }
    }

    // The stop records of one night (§2.4, §4.6 RouteProgress's core). Plain C# so the rules are
    // EditMode-tested; RouteProgress feeds it the doors and the tracker's distance.
    public sealed class StopProgress {
        readonly RouteDefinition route;
        readonly StopRecord[] records;

        public IReadOnlyList<StopRecord> Stops { get { return records; } }
        public StopRecord EndStop { get; private set; }
        public bool EndStopServed { get { return EndStop != null && EndStop.State == StopState.Served; } }

        // The first Pending stop of the night, in route order, or null once none is left
        public StopRecord Next {
            get {
                for (int i = 0; i < records.Length; i++) {
                    if (records[i].InNight && records[i].State == StopState.Pending) {
                        return records[i];
                    }
                }
                return null;
            }
        }

        public StopProgress(RouteDefinition route, string endStopId) {
            this.route = route;
            records = new StopRecord[route.stops.Length];
            for (int i = 0; i < records.Length; i++) {
                records[i] = new StopRecord(route.stops[i], i, ScheduleMath.ScheduledGameSeconds(route, i));
            }
            SetEndStop(endStopId);
        }

        // NightDefinition.endStopId (T-M3-03); an unknown id falls back to the route's terminus
        public void SetEndStop(string endStopId) {
            int end = route.IndexOfStop(endStopId);
            if (end < 0) {
                end = route.IndexOfStop(route.terminusStopId);
            }
            if (end < 0) {
                end = records.Length - 1;
            }
            for (int i = 0; i < records.Length; i++) {
                records[i].IsEndStop = i == end;
                records[i].InNight = i <= end;
            }
            EndStop = end >= 0 ? records[end] : null;
        }

        public StopRecord Find(string stopId) {
            for (int i = 0; i < records.Length; i++) {
                if (records[i].StopId == stopId) {
                    return records[i];
                }
            }
            return null;
        }

        // The doors are fully open at this stop for the first time. False for an unknown stop, one
        // outside the night, or one already Served or Missed (Missed is final, even after reversing).
        // A NaN arrival (no clock) leaves the rating None.
        public bool TryServe(string stopId, double arrivalGameSeconds, out StopRecord record) {
            record = Find(stopId);
            if (record == null || !record.InNight || record.State != StopState.Pending) {
                return false;
            }
            record.State = StopState.Served;
            record.ArrivalGameSeconds = arrivalGameSeconds;
            record.Rating = double.IsNaN(arrivalGameSeconds)
                ? ArrivalRating.None
                : ScheduleMath.Rate(route.schedule, arrivalGameSeconds, record.ScheduledGameSeconds);
            return true;
        }

        // Every Pending stop the bus is more than missedStopMargin past becomes Missed, in route
        // order, and is appended to newlyMissed. The end stop never does. Returns how many.
        public int MarkMissed(float distanceAlong, List<StopRecord> newlyMissed) {
            int count = 0;
            for (int i = 0; i < records.Length; i++) {
                StopRecord record = records[i];
                if (!record.InNight || record.IsEndStop || record.State != StopState.Pending) {
                    continue;
                }
                if (distanceAlong > record.Distance + route.schedule.missedStopMargin) {
                    record.State = StopState.Missed;
                    record.Rating = ArrivalRating.Missed;
                    newlyMissed.Add(record);
                    count++;
                }
            }
            return count;
        }
    }
}
