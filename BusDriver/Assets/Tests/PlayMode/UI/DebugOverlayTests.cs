using System.Collections;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Debug;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.UI {
    // T-M1-18: F1 toggles the overlay in the Editor; the first sections and the cheat registry
    public class DebugOverlayTests {
        string saveRoot;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior savedBackground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
            // A batch-mode player never has focus, and the project's settings drop device input
            // without it. The values go back in TearDown, so the asset is unchanged.
            savedBackground = InputSystem.settings.backgroundBehavior;
            savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void TearDown() {
            if (keyboard != null) {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest]
        public IEnumerator F1_TogglesTheOverlayWithItsSections() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForDriving(game);
            DebugOverlay overlay = Object.FindAnyObjectByType<DebugOverlay>();
            Assert.IsNotNull(overlay, "the night has no debug overlay in the Editor");
            Assert.IsFalse(overlay.IsOpen, "the overlay starts closed");

            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Press(Key.F1);
            Assert.IsTrue(overlay.IsOpen, "F1 should open the overlay");
            StringAssert.Contains("Run", overlay.Text);
            StringAssert.Contains("seed " + game.Flow.Run.seed, overlay.Text);
            StringAssert.Contains("Clock", overlay.Text);
            // RouteTracker's section (T-M2-09): the bus starts at the depot, before farm_gate
            StringAssert.Contains("next farm_gate", overlay.Text);
            StringAssert.Contains("mode Road", overlay.Text);

            yield return Press(Key.F1);
            Assert.IsFalse(overlay.IsOpen, "F1 again should close it");
        }

        [UnityTest]
        public IEnumerator Cheats_BecomeButtonsThatRun() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForDriving(game);
            DebugRegistry registry = Object.FindAnyObjectByType<ShiftContext>().Shift.Debug;
            DebugOverlay overlay = Object.FindAnyObjectByType<DebugOverlay>();
            int before = overlay.CheatButtonCount;
            int runs = 0;
            registry.AddCheat(new DebugCheat("Test", "Count", () => runs++));
            Assert.AreEqual(before + 1, overlay.CheatButtonCount);
            overlay.SetOpen(true);
            UnityEngine.UI.Button button = GameObject.Find("Cheat Count").GetComponent<UnityEngine.UI.Button>();
            button.onClick.Invoke();
            Assert.AreEqual(1, runs);
        }

        IEnumerator Press(Key key) {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
