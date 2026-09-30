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
                lookId = "look06", boardStopId = "gas_station", destinationStopId = "church", monsterId = "starer",
            }, seat);
            Assert.IsNotNull(rider);
            MonsterBrain brain = rider.GetComponent<MonsterBrain>();
            Assert.IsNotNull(brain, "a monster rider spawns from its generated prefab");
            Assert.AreSame(starer, brain.Definition);
            Assert.Contains(brain, (System.Collections.ICollection)shift.Monsters.Active);
            Assert.IsTrue(brain.IsActive, "seated and aboard");
            Assert.IsTrue(brain.Meter.Core.InGrace, "grace starts when it sits down");

            // The rates come from the definition: Observed first, then Always (§2.10, D106)
            Assert.AreEqual(ThreatCondition.Observed, starer.rules[0].condition);
            float watchedRate = starer.rules[0].ratePerSecond;
            float unwatchedRate = starer.rules[1].ratePerSecond;
            Assert.AreEqual(0f, watchedRate, "watching only freezes the Starer (D106)");
            Assert.Greater(unwatchedRate, 0f);

            // Grace: nothing moves for the first 10 s (§2.9)
            Time.timeScale = 3f;
            shift.Cctv.ShowHome();
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0f, brain.Threat, "the meter holds during grace");

            // Watched on CAM 2 first. Unwatched, it would climb fast enough to advance out of CAM 2's
            // frame (T-M4-08), and then the camera couldn't find it again
            ShowCamera(shift.Cctv, 1);
            yield return FlowTestUtil.WaitFor(() => brain.IsObserved, 5f, "CAM 2 to see the Starer");
            yield return FlowTestUtil.WaitFor(() => !brain.Meter.Core.InGrace, 30f, "the end of the grace period");
            yield return null;
            Assert.AreEqual(ObserverKinds.Cctv, brain.ObservedBy);
            float[] result = new float[2];
            yield return Measure(brain, 2f, result);
            Assert.AreEqual(0f, result[0], 0.001f, $"watched from 0: frozen over {result[1]:0.00} s");
            Assert.AreEqual(0f, brain.CurrentRate);

            // Watched at 50 it doesn't fall either: the meter only stops rising
            brain.Meter.Core.SetValue(50f);
            yield return Measure(brain, 2f, result);
            Assert.AreEqual(0f, result[0], 0.001f, $"watched at 50: frozen over {result[1]:0.00} s");
            Assert.AreEqual(ThreatStage.Aggressive, brain.Stage, "still Aggressive at 50");

            // Unwatched: it climbs at the definition's rate
            shift.Cctv.ShowHome();
            yield return FlowTestUtil.WaitFor(() => !brain.IsObserved, 5f, "the home view to lose the Starer");
            yield return Measure(brain, 3f, result);
            float expected = unwatchedRate * result[1];
            Assert.AreEqual(expected, result[0], expected * Tolerance, $"unwatched: +{unwatchedRate}/s over {result[1]:0.00} s");
            Assert.Greater(brain.CurrentRate, 0f);

            // The bounty comes from the definition (§2.7)
            Assert.AreEqual(starer.bountyCents, shift.Economy.BountyFor("starer"));
            Assert.IsTrue(brain.AllowKick(rider), "no greybox monster refuses a kick (§2.13)");
        }
    }
}
