using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Dash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Passengers {
    // The night manifest (T-M3-03, §2.19) and the night's early end (D43)
    public class ManifestTests {
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

        // §2.19's night 1 table
        static readonly string[][] Night1 = {
            new[] { "farm_gate", "look01", "campground", "" },
            new[] { "farm_gate", "look02", "church", "" },
            new[] { "gas_station", "look03", "church", "" },
            new[] { "gas_station", "look04", "campground", "" },
            new[] { "campground", "look05", "church", "" },
            new[] { "gas_station", "look06", "", "starer" },
            new[] { "campground", "look07", "", "weeping_angel" },
        };

        [UnityTest, Timeout(300000)]
        public IEnumerator Manifest_Night1Spawns() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            IReadOnlyList<RiderRecord> riders = night.Shift.Riders.All;
            Assert.AreEqual(7, riders.Count, "night 1 has seven riders");
            for (int i = 0; i < Night1.Length; i++) {
                RiderRecord record = riders[i];
                string[] row = Night1[i];
                Assert.AreEqual(row[0], record.Spec.boardStopId, "rider " + i + " boarding stop");
                Assert.AreEqual(row[1], record.Spec.lookId, "rider " + i + " look");
                Assert.AreEqual(row[2], record.DestinationStopId, "rider " + i + " destination");
                Assert.AreEqual(row[3], record.MonsterId, "rider " + i + " monster");
                Assert.AreEqual(RiderStatus.Waiting, record.Status);
                Passenger passenger = record.Passenger;
                Assert.IsNotNull(passenger, "rider " + i + " wasn't spawned");
                Assert.AreEqual(PassengerState.Waiting, passenger.State);
                Assert.AreEqual(row[1], passenger.LookId, "rider " + i + " wears the wrong look");
                Assert.IsNotNull(passenger.View, "rider " + i + " has no view");
                BusStop stop = night.Route.Stop(row[0]);
                Assert.IsTrue(passenger.transform.IsChildOf(stop.transform), "rider " + i + " isn't waiting at " + row[0]);
                Assert.Less(Vector3.Distance(passenger.transform.position, stop.transform.position), 12f);
            }
            Assert.AreEqual(DecoyKind.NodOff, riders[2].Spec.decoy, "look03 is the NodOff decoy");
            Assert.AreEqual(2, night.Route.Stop("farm_gate").WaitingCount);
            Assert.AreEqual(3, night.Route.Stop("gas_station").WaitingCount, "the decoy, look04 and the Starer (D106)");
            Assert.AreEqual(2, night.Route.Stop("campground").WaitingCount, "look05 and the Weeping Angel");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Manifest_NoMonstersFlag_DropsTheStarer() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            Assert.AreEqual(5, night.Shift.Riders.All.Count);
            foreach (RiderRecord record in night.Shift.Riders.All) {
                Assert.IsFalse(record.IsMonster, record.Spec + " is a monster");
            }
        }

        // D43: night 1 ends at the church and the road is closed 60 m past it; night 2 runs to the lodge
        [UnityTest, Timeout(400000)]
        public IEnumerator NightEndBarrier_Night1Only() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            Assert.AreEqual("church", night.Shift.Progress.EndStop.StopId);
            NightEndBarrier barrier = ActiveBarrier(night);
            Assert.IsNotNull(barrier, "night 1 has no active ROAD CLOSED barrier");
            RouteProjection at = night.Shift.Tracker.Path.Project(barrier.transform.position);
            Assert.AreEqual(1960f, at.Distance, 0.5f, "the barrier stands 60 m past the church");
            Assert.Less(Mathf.Abs(at.Lateral), 0.5f, "the barrier sits across the road's centre");
            Assert.IsNotNull(barrier.GetComponentInChildren<Collider>(), "the barrier has no collider");
            RouteMapView gps = Object.FindAnyObjectByType<RouteMapView>();
            Assert.AreEqual(1900f / 5f, gps.RouteLine.DimAfter, 0.01f, "the GPS doesn't grey out the route past the church");

            game.Flow.NewDebugRun(Seed, 2);
            yield return FlowTestUtil.WaitFor(() => game.Flow.State == RunFlowState.LoadingNight, 10f, "night 2 loading");
            yield return FlowTestUtil.WaitForNight(game);
            night = Object.FindAnyObjectByType<ShiftContext>();
            Assert.AreEqual(2, night.Setup.NightIndex);
            Assert.AreEqual("lodge", night.Shift.Progress.EndStop.StopId);
            Assert.IsNull(ActiveBarrier(night), "night 2 runs to the lodge: no barrier");
            Assert.Greater(Object.FindAnyObjectByType<RouteMapView>().RouteLine.DimAfter, 1000f);
            // Until T-M7-01 night 2 reuses night 1's list; its monster rides to the lodge
            Assert.AreEqual(6, night.Shift.Riders.All.Count);
            Assert.AreEqual("lodge", night.Shift.Riders.All[5].DestinationStopId);
        }

        static NightEndBarrier ActiveBarrier(ShiftContext night) {
            foreach (NightEndBarrier barrier in night.Route.NightEndBarriers) {
                if (barrier != null && barrier.gameObject.activeInHierarchy) {
                    return barrier;
                }
            }
            return null;
        }
    }
}
