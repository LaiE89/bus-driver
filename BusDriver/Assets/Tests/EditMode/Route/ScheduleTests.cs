using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Route {
    // The §3.2 timetable, to the second, and the §2.4 arrival ratings (T-M2-02)
    public class ScheduleTests {
        static readonly object[] Table = {
            new object[] { "farm_gate", "12:33:53 AM", 38.9 },
            new object[] { "gas_station", "12:42:53 AM", 128.9 },
            new object[] { "campground", "12:51:53 AM", 218.9 },
            new object[] { "church", "01:03:06 AM", 331.1 },
            new object[] { "clinic", "01:12:40 AM", 426.7 },
            new object[] { "trailhead", "01:20:33 AM", 505.6 },
            new object[] { "lodge", "01:26:53 AM", 568.9 },
        };

        [TestCaseSource(nameof(Table))]
        public void MatchesTheTimetable(string stopId, string clock, double realSeconds) {
            RouteDefinition route = RouteTestData.Route01;
            int index = route.IndexOfStop(stopId);
            Assert.GreaterOrEqual(index, 0, stopId);
            RouteStop stop = route.stops[index];
            Assert.AreEqual(clock, ClockText.Format(ScheduleMath.ScheduledGameSeconds(route, index), ClockFormat.Cctv));
            Assert.AreEqual(realSeconds, ScheduleMath.RealSecondsAfterStart(route.schedule, stop.distance, index), 0.05);
        }

        [Test]
        public void ScheduledTimesCoverEveryStopInOrder() {
            RouteDefinition route = RouteTestData.Route01;
            double[] times = ScheduleMath.ScheduledTimes(route);
            Assert.AreEqual(route.stops.Length, times.Length);
            for (int i = 1; i < times.Length; i++) {
                Assert.Greater(times[i], times[i - 1]);
            }
        }

        [Test]
        public void ArrivalRatingBoundaries() {
            RouteSchedule schedule = new RouteSchedule();
            const double scheduled = 3000.0;
            Assert.AreEqual(ArrivalRating.Early, ScheduleMath.Rate(schedule, scheduled - 600.0, scheduled));
            Assert.AreEqual(ArrivalRating.Early, ScheduleMath.Rate(schedule, scheduled - 60.0, scheduled));
            Assert.AreEqual(ArrivalRating.OnTime, ScheduleMath.Rate(schedule, scheduled - 59.9, scheduled));
            Assert.AreEqual(ArrivalRating.OnTime, ScheduleMath.Rate(schedule, scheduled, scheduled));
            Assert.AreEqual(ArrivalRating.OnTime, ScheduleMath.Rate(schedule, scheduled + 60.0, scheduled));
            Assert.AreEqual(ArrivalRating.Late, ScheduleMath.Rate(schedule, scheduled + 60.1, scheduled));
        }
    }
}
