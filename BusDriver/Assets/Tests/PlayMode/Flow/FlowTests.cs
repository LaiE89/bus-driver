using System.Collections;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    public class FlowTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest]
        public IEnumerator Boot_CreatesSingleGameRoot() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;

            game.Flow.QuitToMenu();
            yield return FlowTestUtil.WaitForMenu(game);
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);
            yield return null;
            game.Flow.QuitToMenu();
            yield return FlowTestUtil.WaitForMenu(game);

            Assert.AreEqual(1, Object.FindObjectsByType<GameRoot>().Length, "GameRoot count after Menu → Night → Menu");
            Assert.AreEqual(1, CountDontDestroyOnLoadRoots(), "GameRoot must be the only DontDestroyOnLoad object");
        }

        [UnityTest]
        public IEnumerator Flow_NewRun_LoadsNight() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;

            game.Flow.NewRun();
            Assert.AreEqual(RunFlowState.LoadingNight, game.Flow.State);
            yield return FlowTestUtil.WaitForNight(game);

            Assert.AreEqual(SceneIds.LegacyNight, SceneManager.GetActiveScene().name);
            LegacyNightRoot night = Object.FindAnyObjectByType<LegacyNightRoot>();
            Assert.IsNotNull(night, "the night scene has no LegacyNightRoot");
            Assert.AreSame(game, night.Game, "the night root was not initialized with the running services");
            Assert.IsNotNull(night.Setup, "Begin was not called");
            Assert.AreEqual(1, night.Setup.NightIndex);
            Assert.IsFalse(night.Setup.IsDebugRun);
            Assert.IsFalse(game.Flow.IsDebugRun);

            SaveService disk = new SaveService(saveRoot, SaveMigrations.CreateDefault(), "test");
            RunState run;
            Assert.IsTrue(disk.TryLoad(SaveSlot.Run, out run), "New Run did not save run.json");
            Assert.AreEqual(game.Flow.Run.seed, run.seed);
            Assert.AreEqual(1, run.nightIndex);
            Assert.IsFalse(run.nightInProgress);
            MetaProgress meta;
            Assert.IsTrue(disk.TryLoad(SaveSlot.Meta, out meta), "New Run did not record the run start");
            Assert.AreEqual(1, meta.runsStarted);
        }

        // Objects of ours in the DontDestroyOnLoad scene (the test runner keeps its own there too)
        static int CountDontDestroyOnLoadRoots() {
            GameRoot root = Object.FindAnyObjectByType<GameRoot>();
            if (root == null) {
                return 0;
            }
            int ours = 0;
            foreach (GameObject go in root.gameObject.scene.GetRootGameObjects()) {
                foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (behaviour != null && behaviour.GetType().Assembly.GetName().Name.StartsWith("BusDriver.")) {
                        ours++;
                        break;
                    }
                }
            }
            return ours;
        }
    }
}
