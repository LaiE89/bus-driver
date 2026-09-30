using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Route {
    // The stop records' rules (§2.4, T-M2-11): Served on the first fully open doors, Missed past the
    // margin, both final, and the end stop can never be Missed
    public class StopProgressTests {
        static StopProgress New(string endStopId = "lodge") {
            return new StopProgress(RouteTestData.Route01, endStopId);
        }

        [Test]
        public void StartsWithEveryStopPending() {
            StopProgress progress = New();
            Assert.AreEqual(RouteTestData.Route01.stops.Length, progress.Stops.Count);
            foreach (StopRecord record in progress.Stops) {
                Assert.AreEqual(StopState.Pending, record.State, record.StopId);
                Assert.IsTrue(double.IsNaN(record.ArrivalGameSeconds));
            }
            Assert.AreEqual("farm_gate", progress.Next.StopId);
            Assert.AreEqual("lodge", progress.EndStop.StopId);
        }

        [Test]
        public void ServedOnceWithTheRating() {
            StopProgress progress = New();
            StopRecord farm = progress.Find("farm_gate");
            // Scheduled 12:33:53; 90 game-s before it is Early (§2.4)
            Assert.IsTrue(progress.TryServe("farm_gate", farm.ScheduledGameSeconds - 90.0, out StopRecord served));
            Assert.AreSame(farm, served);
            Assert.AreEqual(StopState.Served, farm.State);
            Assert.AreEqual(ArrivalRating.Early, farm.Rating);
            Assert.AreEqual(farm.ScheduledGameSeconds - 90.0, farm.ArrivalGameSeconds, 1e-9);
            Assert.IsFalse(progress.TryServe("farm_gate", farm.ScheduledGameSeconds, out _), "a stop is served once");
            Assert.AreEqual("gas_station", progress.Next.StopId);
        }

        [Test]
        public void RatingsFollowTheSchedule() {
            StopProgress progress = New();
            StopRecord gas = progress.Find("gas_station");
            StopRecord camp = progress.Find("campground");
            progress.TryServe("gas_station", gas.ScheduledGameSeconds + 30.0, out _);
            progress.TryServe("campground", camp.ScheduledGameSeconds + 61.0, out _);
            Assert.AreEqual(ArrivalRating.OnTime, gas.Rating);
            Assert.AreEqual(ArrivalRating.Late, camp.Rating);
        }

        [Test]
        public void NoClockLeavesTheRatingNone() {
            StopProgress progress = New();
            Assert.IsTrue(progress.TryServe("farm_gate", double.NaN, out StopRecord record));
            Assert.AreEqual(ArrivalRating.None, record.Rating);
        }

        [Test]
        public void MissedPastTheMarginInRouteOrderAndFinal() {
            StopProgress progress = New();
            List<StopRecord> missed = new List<StopRecord>();
            // farm_gate is at 350 m, gas_station at 800 m, the margin is 30 m
            Assert.AreEqual(0, progress.MarkMissed(380f, missed), "exactly at the margin is not yet missed");
            Assert.AreEqual(2, progress.MarkMissed(830.5f, missed));
            CollectionAssert.AreEqual(new[] { "farm_gate", "gas_station" }, new[] { missed[0].StopId, missed[1].StopId });
            Assert.AreEqual(ArrivalRating.Missed, missed[0].Rating);
            Assert.AreEqual(0, progress.MarkMissed(900f, missed = new List<StopRecord>()), "already missed");
            Assert.IsFalse(progress.TryServe("gas_station", 3000.0, out _), "Missed is final, even after reversing to it");
            Assert.AreEqual("campground", progress.Next.StopId);
        }

        [Test]
        public void ServedStopsAreNeverMissed() {
            StopProgress progress = New();
            progress.TryServe("farm_gate", 2000.0, out _);
            List<StopRecord> missed = new List<StopRecord>();
            progress.MarkMissed(1000f, missed);
            Assert.AreEqual(1, missed.Count);
            Assert.AreEqual("gas_station", missed[0].StopId);
            Assert.AreEqual(StopState.Served, progress.Find("farm_gate").State);
        }

        [Test]
        public void TheEndStopIsNeverMissed() {
            StopProgress progress = New();
            List<StopRecord> missed = new List<StopRecord>();
            progress.MarkMissed(5000f, missed);
            Assert.AreEqual(RouteTestData.Route01.stops.Length - 1, missed.Count);
            Assert.AreEqual(StopState.Pending, progress.EndStop.State);
            Assert.AreEqual("lodge", progress.Next.StopId);
            Assert.IsTrue(progress.TryServe("lodge", 5000.0, out _));
            Assert.IsTrue(progress.EndStopServed);
            Assert.IsNull(progress.Next);
        }

        [Test]
        public void AnEarlierEndStopEndsTheNight() {
            // Night 1 ends at church (D43): the stops after it aren't part of the night
            StopProgress progress = New("church");
            Assert.AreEqual("church", progress.EndStop.StopId);
            Assert.IsFalse(progress.Find("clinic").InNight);
            List<StopRecord> missed = new List<StopRecord>();
            progress.MarkMissed(2900f, missed);
            Assert.AreEqual(3, missed.Count, "only the stops before church are missed");
            Assert.AreEqual(StopState.Pending, progress.Find("church").State);
            Assert.IsFalse(progress.TryServe("clinic", 5000.0, out _), "a stop after the end stop can't be served");
            Assert.IsTrue(progress.TryServe("church", 4000.0, out _));
            Assert.IsNull(progress.Next);
        }

        [Test]
        public void UnknownEndStopFallsBackToTheTerminus() {
            Assert.AreEqual("lodge", New("nowhere").EndStop.StopId);
        }
    }
}
