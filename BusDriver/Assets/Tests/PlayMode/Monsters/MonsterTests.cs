using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Monsters {
    // The monster framework (T-M4-03, §2.9): a monster rider is its generated prefab with a brain
    // and a meter, bound to the night, whose threat follows its definition's rules
    public class MonsterTests {
        const int Seed = 20260930;
        const float Tolerance = 0.05f;

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

        static IEnumerator StartNight(string saveRoot, ShiftServices[] result) {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            result[0] = Object.FindAnyObjectByType<ShiftContext>().Shift;
        }

        // A free seat CAM 2 frames within its range
        static BusSeat SeatCam2Sees(ShiftServices shift) {
            CctvCamera cam2 = shift.Cctv.Cameras[1];
            Transform root = shift.Cabin.PassengerRoot;
            foreach (BusSeat seat in shift.Cabin.Seats) {
                Vector3 head = root.TransformPoint(shift.Cabin.SeatLocal(seat)) + root.up * Passenger.SeatedHeadHeight;
                if (seat.IsFree && PlayerAttention.Sees(cam2.Camera, head, cam2.ObserveRange)) {
                    return seat;
                }
            }
            return null;
        }

        static void ShowCamera(CCTVSystem cctv, int index) {
            cctv.ShowHome();
            while (cctv.ActiveIndex != index) {
                cctv.Cycle();
            }
        }

        // The meter's change over `seconds` of scaled time, and the time it actually covered. The
        // brain ticks in Update with the same Time.deltaTime the loop adds up.
        static IEnumerator Measure(MonsterBrain brain, float seconds, float[] result) {
            float start = brain.Threat;
            float elapsed = 0f;
            while (elapsed < seconds) {
                yield return null;
                elapsed += Time.deltaTime;
            }
            result[0] = brain.Threat - start;
            result[1] = elapsed;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Monster_ThreatFollowsRules() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            MonsterDefinition starer = shift.Monsters.Definition("starer");
            Assert.IsNotNull(starer, "no Starer definition in GameRootConfig");
            Assert.AreEqual(1f, shift.Night.threatRateMultiplier, "night 1's threat multiplier");

            BusSeat seat = SeatCam2Sees(shift);
            Assert.IsNotNull(seat, "no seat in CAM 2's frame");
            Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec {
                lookId = "look06", boardStopId = "campground", destinationStopId = "church", monsterId = "starer",
            }, seat);
            Assert.IsNotNull(rider);
            MonsterBrain brain = rider.GetComponent<MonsterBrain>();
            Assert.IsNotNull(brain, "a monster rider spawns from its generated prefab");
            Assert.AreSame(starer, brain.Definition);
            Assert.Contains(brain, (System.Collections.ICollection)shift.Monsters.Active);
            Assert.IsTrue(brain.IsActive, "seated and aboard");
            Assert.IsTrue(brain.Meter.Core.InGrace, "grace starts when it sits down");

            // Grace: nothing moves for the first 10 s, then it rises unwatched (§2.9, §2.10)
            Time.timeScale = 3f;
            shift.Cctv.ShowHome();
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0f, brain.Threat, "the meter holds during grace");
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the end of the grace period");
            yield return null;

            Assert.IsFalse(brain.IsObserved, "the home view doesn't see a rear seat");
            float[] result = new float[2];
            yield return Measure(brain, 3f, result);
            float expected = 1.5f * result[1];
            Assert.AreEqual(expected, result[0], expected * Tolerance, $"unwatched: +1.5/s over {result[1]:0.00} s");
            Assert.Greater(brain.CurrentRate, 0f);

            // Watched on CAM 2: −5/s, from a value high enough to measure. On camera first: at 50,
            // unwatched for a second, the Starer would advance out of CAM 2's frame (T-M4-08).
            ShowCamera(shift.Cctv, 1);
            yield return FlowTestUtil.WaitFor(() => brain.IsObserved, 5f, "CAM 2 to see the Starer");
            brain.Meter.Core.SetValue(50f);
            Assert.AreEqual(ObserverKinds.Cctv, brain.ObservedBy);
            yield return Measure(brain, 3f, result);
            expected = -5f * result[1];
            Assert.AreEqual(expected, result[0], -expected * Tolerance, $"watched: −5/s over {result[1]:0.00} s");
            Assert.AreEqual(ThreatStage.Unsettled, brain.Stage, "50 − 15 is Unsettled");

            // The bounty comes from the definition (§2.7)
            Assert.AreEqual(starer.bountyCents, shift.Economy.BountyFor("starer"));
            Assert.IsTrue(brain.AllowKick(rider), "no greybox monster refuses a kick (§2.13)");
        }
    }
}
