using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // ROADMAP §4.11: PauseService is the single owner of pause
    public class PauseTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        IEnumerator StartNight(GameServices game) {
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);
            // SceneController.Start puts the player in the seat
            yield return null;
            yield return null;
        }

        static IEnumerator WaitRealSeconds(float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTimeAndAudio() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartNight(game);
            Assert.AreEqual(InputContext.Driving, game.Input.EffectiveContext);

            BusController bus = Object.FindAnyObjectByType<BusController>();
            bus.GetComponent<BusInput>().ExternalControl = true;
            bus.SetInput(0f, 1f, false);
            yield return WaitRealSeconds(1.5f);
            Assert.Greater(bus.SpeedKmh, 1f, "the bus should be moving before the pause");

            Assert.IsTrue(game.Pause.TrySetPaused(true));
            Assert.IsTrue(game.Pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause, "the listener is paused");
            Assert.AreEqual(InputContext.Screen, game.Input.EffectiveContext);
            Assert.IsFalse(game.Input.Actions.Throttle.enabled, "driving input is off while paused");

            Rigidbody body = bus.GetComponent<Rigidbody>();
            Vector3 at = body.position;
            yield return WaitRealSeconds(0.5f);
            Assert.Less(Vector3.Distance(at, body.position), 0.0001f, "the bus moved while paused");

            Assert.IsTrue(game.Pause.TrySetPaused(false));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.AreEqual(InputContext.Driving, game.Input.EffectiveContext);
            bus.SetInput(0f, 0f, true);
        }

        [UnityTest]
        public IEnumerator Pause_RestoresTheStoredTimeScale() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartNight(game);
            // The fall slow-mo runs at 0.5 (§2.14)
            Time.timeScale = 0.5f;
            game.Pause.TrySetPaused(true);
            game.Pause.TrySetPaused(false);
            Assert.AreEqual(0.5f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Pause_NotAllowedInTheMenuOrAfterGameOver() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.QuitToMenu();
            yield return FlowTestUtil.WaitForMenu(game);
            Assert.IsFalse(game.Pause.TrySetPaused(true), "the menu can't be paused");
            Assert.AreEqual(1f, Time.timeScale);

            yield return StartNight(game);
            SceneController.Instance.TriggerGameOver();
            Assert.IsFalse(game.Pause.TrySetPaused(true), "Game Over can't be paused");
            Assert.AreEqual(InputContext.Screen, game.Input.EffectiveContext);
        }

        [UnityTest]
        public IEnumerator Pause_QuitToMenuLeavesTheGameUnpaused() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartNight(game);
            PauseScreen screen = Object.FindAnyObjectByType<PauseScreen>();
            screen.Back();
            Assert.IsTrue(game.Pause.IsPaused);
            Assert.IsTrue(screen.IsOpen, "the pause panel shows");

            screen.QuitToMenu();
            yield return FlowTestUtil.WaitForMenu(game);
            Assert.IsFalse(game.Pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.AreEqual(InputContext.Menu, game.Input.EffectiveContext);
        }

        [UnityTest]
        public IEnumerator Pause_BackResumesAndFocusLossIsOffInBatchMode() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartNight(game);
            PauseScreen screen = Object.FindAnyObjectByType<PauseScreen>();
            screen.Back();
            screen.Back();
            Assert.IsFalse(game.Pause.IsPaused);
            Assert.IsFalse(screen.IsOpen);
            if (Application.isBatchMode) {
                Assert.IsFalse(game.Pause.PauseOnFocusLoss);
                game.Pause.HandleFocus(false);
                Assert.IsFalse(game.Pause.IsPaused);
            }
        }
    }
}
