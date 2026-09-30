using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Views;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Passengers {
    // Decoy behaviours (T-M3-08, §2.6): each DecoyKind shows on its rider's view within 20 s
    public class DecoyTests {
        const float TimeScale = 2f;
        const float Window = 20f;

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

        struct Pose {
            public bool Active;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        static List<Pose> Snapshot(PassengerViewBase view) {
            List<Pose> poses = new List<Pose>();
            foreach (Transform node in view.GetComponentsInChildren<Transform>(true)) {
                poses.Add(new Pose { Active = node.gameObject.activeSelf, Position = node.localPosition, Rotation = node.localRotation, Scale = node.localScale });
            }
            return poses;
        }

        static bool Differs(List<Pose> a, List<Pose> b) {
            if (a.Count != b.Count) {
                return true;
            }
            for (int i = 0; i < a.Count; i++) {
                if (a[i].Active != b[i].Active || (a[i].Position - b[i].Position).sqrMagnitude > 1e-8f
                    || Quaternion.Angle(a[i].Rotation, b[i].Rotation) > 0.05f || (a[i].Scale - b[i].Scale).sqrMagnitude > 1e-8f) {
                    return true;
                }
            }
            return false;
        }

        static void SetDriver(Passenger rider, bool on) {
            DecoyDriver driver = rider.GetComponent<DecoyDriver>();
            if (driver != null) {
                driver.enabled = on;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Decoys_ShowTheirTell() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;

            DecoyKind[] kinds = { DecoyKind.None, DecoyKind.NodOff, DecoyKind.PhoneGlow, DecoyKind.Mutter, DecoyKind.FacingBackwards, DecoyKind.HoodUp };
            List<Passenger> riders = new List<Passenger>();
            List<List<Pose>> baselines = new List<List<Pose>>();
            for (int i = 0; i < kinds.Length; i++) {
                Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec {
                    lookId = "look" + (7 + i).ToString("00"), boardStopId = "farm_gate", destinationStopId = "church", decoy = kinds[i],
                });
                Assert.IsNotNull(rider, "no seat for the " + kinds[i] + " rider");
                Assert.AreEqual(kinds[i] != DecoyKind.None, rider.GetComponent<DecoyDriver>() != null, kinds[i] + " rider's decoy driver");
                // A still baseline: only the decoy's tell may move anything
                rider.View.SetTell(TellId.Stillness, 1f);
                SetDriver(rider, false);
                riders.Add(rider);
            }
            // Baselines once every view has settled into its seated pose, before any decoy acts
            yield return null;
            yield return null;
            for (int i = 0; i < riders.Count; i++) {
                baselines.Add(Snapshot(riders[i].View));
                SetDriver(riders[i], true);
            }

            Time.timeScale = TimeScale;
            float[] shownAt = new float[kinds.Length];
            for (int i = 0; i < shownAt.Length; i++) {
                shownAt[i] = -1f;
            }
            float start = Time.time;
            while (Time.time - start < Window + 1f) {
                yield return null;
                for (int i = 1; i < riders.Count; i++) {
                    if (shownAt[i] < 0f && Differs(baselines[i], Snapshot(riders[i].View))) {
                        shownAt[i] = Time.time - start;
                    }
                }
                if (shownAt[0] < 0f && Differs(baselines[0], Snapshot(riders[0].View))) {
                    shownAt[0] = Time.time - start;
                }
            }
            Assert.Less(shownAt[0], 0f, "the plain rider's view changed on its own; the test can't tell decoys apart");
            for (int i = 1; i < kinds.Length; i++) {
                Assert.GreaterOrEqual(shownAt[i], 0f, kinds[i] + " never showed on its view");
                // One frame of ramp past the latest first nod at 20 s
                Assert.LessOrEqual(shownAt[i], Window + 0.25f, kinds[i] + " took too long to show");
            }
        }
    }
}
