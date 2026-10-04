using System;
using System.Collections;
using System.IO;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusDriver.Tests.PlayMode.Flow {
    // Shared helpers for tests that drive the real RunFlow. Every test reboots GameRoot on a
    // throwaway save root, so nothing ever lands in the real persistentDataPath.
    static class FlowTestUtil {
        public static string NewSaveRoot() {
            string root = Path.Combine(Path.GetTempPath(), "busdriver-playmode", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        public static void DeleteSaveRoot(string root) {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) {
                Directory.Delete(root, true);
            }
        }

        public static GameRoot Reboot(string saveRoot) {
            return GameRoot.RebootForTests(saveRoot);
        }

        // Real time, because tests may change timeScale
        public static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what) {
            float giveUp = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition()) {
                if (Time.realtimeSinceStartup > giveUp) {
                    Assert.Fail($"timed out after {timeoutSeconds} s waiting for {what}");
                }
                yield return null;
            }
        }

        public static IEnumerator WaitForMenu(GameServices game) {
            return WaitFor(() => SceneManager.GetActiveScene().name == SceneIds.Menu
                && game.Flow.State == RunFlowState.Menu && !game.Scenes.IsLoading, 60f, "the menu");
        }

        public static IEnumerator WaitForNight(GameServices game) {
            return WaitFor(() => game.Flow.State == RunFlowState.InNight && !game.Scenes.IsLoading, 90f, "the night");
        }

        // The night has begun and the intro card has released the bus (§2.1)
        public static IEnumerator WaitForDriving(GameServices game) {
            yield return WaitForNight(game);
            yield return WaitFor(() => {
                ShiftDirector director = Director();
                return director != null && director.State == ShiftState.Driving;
            }, 30f, "the Driving state");
            DoorDriver(UnityEngine.Object.FindAnyObjectByType<ShiftContext>());
        }

        // Riders stand on the step until the driver waves them aboard (§2.4), and a test has no
        // driver, so one is supplied for the rest of the night. A test about the decision itself
        // turns Accept off and answers the door on its own.
        public static TestDoorDriver DoorDriver(ShiftContext night) {
            if (night == null) {
                return null;
            }
            TestDoorDriver driver = night.GetComponent<TestDoorDriver>();
            if (driver == null) {
                driver = night.gameObject.AddComponent<TestDoorDriver>();
                driver.Cabin = night.Shift.Cabin;
            }
            return driver;
        }

        public static ShiftDirector Director() {
            ShiftContext night = UnityEngine.Object.FindAnyObjectByType<ShiftContext>();
            return night != null ? night.Director : null;
        }
    }

    // Stands in for the driver at the door: says yes to whoever is on the step, which is what
    // the driver does in the cases the rest of the tests are about
    sealed class TestDoorDriver : MonoBehaviour {
        internal BusCabin Cabin;
        internal bool Accept = true;

        void Update() {
            if (!Accept || Cabin == null) {
                return;
            }
            Passenger atDoor = Cabin.PassengerAtDoor;
            if (atDoor != null) {
                atDoor.AcceptAboard();
            }
        }
    }

    // Stops every death except Abandoned without expelling the monster, the way god mode does:
    // a kill sequence that runs out is spared (T-M4-07), so a long drive can ignore a monster
    sealed class SparingPreventer : IDeathPreventer {
        public int Count;
        public bool ExpelsMonster { get { return false; } }

        public bool TryPrevent(DeathReport report) {
            Count++;
            return true;
        }
    }
}
