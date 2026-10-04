using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.World;
using BusDriver.UI.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BusDriver.Tests.PlayMode.Flow {
    // The menu diorama v1 (T-M2-15, §2.22): the generated Menu scene is live, and its New Run button
    // still loads the night
    public class MenuDioramaTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static IEnumerator WaitRealSeconds(float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Menu_DioramaIsLive_AndNewRunLoadsTheNight() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.LoadMenu();
            yield return FlowTestUtil.WaitForMenu(game);
            yield return WaitRealSeconds(0.5f);

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "the menu has no main camera");
            CameraDrift drift = cam.GetComponent<CameraDrift>();
            Assert.IsNotNull(drift, "the menu camera has no CameraDrift");
            yield return WaitRealSeconds(1f);
            Assert.LessOrEqual(Vector3.Distance(drift.BasePosition, cam.transform.localPosition), 0.05f * Mathf.Sqrt(3f) + 1e-4f, "the drift stays within ±0.05 m");

            LightFlicker lamp = Object.FindAnyObjectByType<LightFlicker>();
            Assert.IsNotNull(lamp, "the stop has no lamp");
            Assert.AreEqual(FlickerMode.Subtle, lamp.Mode);
            LampBuzz buzz = Object.FindAnyObjectByType<LampBuzz>();
            Assert.IsTrue(buzz != null && buzz.IsPlaying, "the lamp buzz isn't playing");
            // No ambience bed until a real forest clip lands; the lamp buzz carries the scene
            Assert.IsNotNull(Object.FindAnyObjectByType<SceneAmbience>(), "the menu has no SceneAmbience");
            Assert.IsNotNull(GameObject.Find("Waiting Figure"), "no waiting figure");

            Button newRun = null;
            foreach (Button button in Object.FindObjectsByType<Button>()) {
                if (button.name == "New Run Button") {
                    newRun = button;
                }
            }
            Assert.IsNotNull(newRun, "no New Run button");
            newRun.onClick.Invoke();
            yield return FlowTestUtil.WaitForNight(game);
            Assert.AreEqual(Core.Util.SceneIds.Route01World, SceneManager.GetActiveScene().name);
        }
    }
}
