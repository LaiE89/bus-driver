using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // M4b controller support, driven by virtual devices: a pad drives the bus and turns the head,
    // the prompts follow the device in use, and losing the pad pauses the game
    public class GamepadTests {
        string saveRoot;
        Gamepad pad;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            if (pad != null && pad.added) {
                InputSystem.RemoveDevice(pad);
            }
            pad = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static IEnumerator WaitRealSeconds(float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }
        }

        IEnumerator StartDriving(GameServices game) {
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForDriving(game);
            yield return null;
            pad = InputSystem.AddDevice<Gamepad>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gamepad_DrivesBusAndSwitchesPrompts() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartDriving(game);
            game.Input.SetFamily(DeviceFamily.KeyboardMouse);
            Assert.AreEqual("E", game.Input.GetDisplayString("LeaveSeat"));

            // Full R2
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
            yield return null;
            yield return null;
            Assert.AreEqual(DeviceFamily.Gamepad, game.Input.ActiveFamily, "pulling a trigger makes the pad active");
            Assert.AreEqual("A", game.Input.GetDisplayString("LeaveSeat"), "the prompt switches to the pad");
            Assert.AreEqual(1f, game.Input.Actions.Throttle.ReadValue<float>(), 0.01f);
            BusController bus = Object.FindAnyObjectByType<BusController>();
            yield return WaitRealSeconds(3f);
            Assert.Greater(bus.SpeedKmh, 5f, "R2 should drive the bus");

            // Left stick steers, analogue
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f, leftStick = new Vector2(0.6f, 0f) });
            yield return null;
            yield return null;
            float steer = game.Input.Actions.Steer.ReadValue<float>();
            Assert.Greater(steer, 0.4f, "the left stick steers");
            Assert.Less(steer, 0.9f, "steering stays analogue");

            // Releasing the pad keeps it active: an idle pad never hands the prompts back. (A key
            // press does; the editor drops virtual keyboard events while the Game view is
            // unfocused, as in batch mode, so that half is a manual check.)
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
            Assert.AreEqual(DeviceFamily.Gamepad, game.Input.ActiveFamily);
            game.Input.SetFamily(DeviceFamily.KeyboardMouse);
            Assert.AreEqual("E", game.Input.GetDisplayString("LeaveSeat"));
        }

        [UnityTest]
        public IEnumerator Gamepad_RightStickTurnsTheHead() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            night.Shift.DriverLook.SetLook(0f, 0f);

            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(1f, 0f) });
            yield return WaitRealSeconds(0.5f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            // 180°/s at full deflection: about 90° in half a second. Read as mouse pixels, the
            // same deflection used to give about 4°.
            Assert.Greater(night.Shift.DriverLook.Yaw, 40f, "the right stick should turn the head at a usable rate");
        }

        [UnityTest]
        public IEnumerator Gamepad_DisconnectPauses() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartDriving(game);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.IsTrue(game.Input.IsGamepadActive);
            Assert.IsFalse(game.Pause.IsPaused);

            InputSystem.RemoveDevice(pad);
            yield return null;
            yield return null;
            Assert.IsTrue(game.Pause.IsPaused, "losing the pad mid-drive pauses the game");
            Assert.AreEqual(DeviceFamily.KeyboardMouse, game.Input.ActiveFamily, "the keyboard takes over");
            Assert.IsTrue(Cursor.visible, "the cursor is back for the pause screen");
            game.Pause.TrySetPaused(false);
        }
    }
}
