using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // The timetable and arrival ratings (§2.4). Times are game-seconds since midnight.
    public static class ScheduleMath {
        // scheduled(i) = shiftStart + gameRate × (distance_i / scheduleSpeed + dwellAllowance × i),
        // where i is the stop's 0-based index in route order
        public static double ScheduledGameSeconds(RouteSchedule schedule, float distance, int stopIndex) {
            return schedule.shiftStartGameSeconds
                + schedule.gameSecondsPerRealSecond * RealSecondsAfterStart(schedule, distance, stopIndex);
        }

        // The "real seconds after shift start" column of §3.2
        public static double RealSecondsAfterStart(RouteSchedule schedule, float distance, int stopIndex) {
            return (double)distance / schedule.scheduleSpeed + (double)schedule.dwellAllowanceSeconds * stopIndex;
        }

        public static double ScheduledGameSeconds(RouteDefinition route, int stopIndex) {
            return ScheduledGameSeconds(route.schedule, route.stops[stopIndex].distance, stopIndex);
        }

        public static double[] ScheduledTimes(RouteDefinition route) {
            double[] times = new double[route.stops.Length];
            for (int i = 0; i < times.Length; i++) {
                times[i] = ScheduledGameSeconds(route, i);
            }
            return times;
        }

        // Early: at least earlyThreshold before the scheduled time. Late: more than lateThreshold
        // after it. On time: anything in between. Only Early matters for money (D9).
        public static ArrivalRating Rate(RouteSchedule schedule, double arrivalGameSeconds, double scheduledGameSeconds) {
            double early = scheduledGameSeconds - arrivalGameSeconds;
            if (early >= schedule.earlyThresholdGameSeconds) {
                return ArrivalRating.Early;
            }
            if (arrivalGameSeconds - scheduledGameSeconds > schedule.lateThresholdGameSeconds) {
                return ArrivalRating.Late;
            }
            return ArrivalRating.OnTime;
        }
    }
}
