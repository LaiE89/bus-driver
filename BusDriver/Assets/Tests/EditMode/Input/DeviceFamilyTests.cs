using System.IO;
using BusDriver.Gameplay.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

namespace BusDriver.Tests.EditMode.Input {
    // M4b: pads are recognised by family, and prompts name their buttons the way the pad prints them
    public class DeviceFamilyTests {
        const string AssetPath = "Assets/Input/BusDriver.inputactions";

        InputActionAsset copy;

        [SetUp]
        public void SetUp() {
            copy = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
        }

        [TearDown]
        public void TearDown() {
            copy.Disable();
            UnityEngine.Object.DestroyImmediate(copy);
        }

        [Test]
        public void DevicesMapToTheirFamily() {
            InputDevice dualSense = InputSystem.AddDevice<DualSenseGamepadHID>();
            InputDevice xbox = InputSystem.AddDevice<XInputController>();
            InputDevice generic = InputSystem.AddDevice<Gamepad>();
            try {
                Assert.AreEqual(DeviceFamily.PlayStation, DeviceFamilies.Of(dualSense));
                Assert.AreEqual(DeviceFamily.Xbox, DeviceFamilies.Of(xbox));
                Assert.AreEqual(DeviceFamily.Gamepad, DeviceFamilies.Of(generic));
                Assert.AreEqual(DeviceFamily.KeyboardMouse, DeviceFamilies.Of(null));
            }finally {
                InputSystem.RemoveDevice(dualSense);
                InputSystem.RemoveDevice(xbox);
                InputSystem.RemoveDevice(generic);
            }
        }

        [Test]
        public void PlayStationPromptsUsePlayStationNames() {
            InputService input = new InputService(copy, null);
            Assert.AreEqual("Cross", input.GetDisplayString(input.Actions.LeaveSeat, DeviceFamily.PlayStation));
            Assert.AreEqual("Square", input.GetDisplayString(input.Actions.Doors, DeviceFamily.PlayStation));
            Assert.AreEqual("Triangle", input.GetDisplayString(input.Actions.CycleCamera, DeviceFamily.PlayStation));
            Assert.AreEqual("Circle", input.GetDisplayString(input.Actions.Handbrake, DeviceFamily.PlayStation));
            Assert.AreEqual("R1", input.GetDisplayString(input.Actions.AcceptRider, DeviceFamily.PlayStation));
            Assert.AreEqual("L1", input.GetDisplayString(input.Actions.RefuseRider, DeviceFamily.PlayStation));
            Assert.AreEqual("L2/R2", input.GetDisplayString(input.Actions.Throttle, DeviceFamily.PlayStation));
            Assert.AreEqual("Options", input.GetDisplayString(input.Actions.Pause, DeviceFamily.PlayStation));
            Assert.AreEqual("Left Stick", input.GetDisplayString(input.Actions.Steer, DeviceFamily.PlayStation));
        }

        [Test]
        public void XboxAndKeyboardPrompts() {
            InputService input = new InputService(copy, null);
            Assert.AreEqual("A", input.GetDisplayString(input.Actions.LeaveSeat, DeviceFamily.Xbox));
            Assert.AreEqual("LT/RT", input.GetDisplayString(input.Actions.Throttle, DeviceFamily.Xbox));
            Assert.AreEqual("A", input.GetDisplayString(input.Actions.Interact, DeviceFamily.Gamepad));
            Assert.AreEqual("E", input.GetDisplayString(input.Actions.LeaveSeat, DeviceFamily.KeyboardMouse));
            // Nothing pressed yet: keyboard prompts
            Assert.AreEqual(DeviceFamily.KeyboardMouse, input.ActiveFamily);
            Assert.AreEqual("E", input.GetDisplayString("LeaveSeat"));
        }

        [Test]
        public void ActionsWithoutAPadBindingFallBackToTheKeyboard() {
            InputService input = new InputService(copy, null);
            Assert.AreEqual("F1", input.GetDisplayString(input.Actions.DebugOverlay, DeviceFamily.PlayStation));
        }

        [Test]
        public void UnknownPathsHaveNoName() {
            Assert.IsNull(DeviceFamilies.GamepadControlName("<Keyboard>/e", DeviceFamily.PlayStation));
            Assert.IsNull(DeviceFamilies.GamepadControlName("<Gamepad>/touchpad", DeviceFamily.PlayStation));
            Assert.AreEqual("D-Pad Left", DeviceFamilies.GamepadControlName("<Gamepad>/dpad/left", DeviceFamily.Xbox));
        }
    }
}
