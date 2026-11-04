using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

namespace BusDriver.Gameplay.Input {
    // Which kind of device the player last touched (T-M4b-02). Prompts and the cursor follow it.
    public enum DeviceFamily {
        KeyboardMouse,
        // DualSense (PS5) and DualShock 4 (PS4)
        PlayStation,
        Xbox,
        // Any other pad: shown with Xbox-style names, the Input System's own convention
        Gamepad,
    }

    // Prompt names for gamepad controls, per family. Plain text rather than glyph sprites so the
    // HUD font needs nothing new; art can replace them later (§4.13).
    public static class DeviceFamilies {
        public static DeviceFamily Of(InputDevice device) {
            if (device is DualShockGamepad) {
                return DeviceFamily.PlayStation;
            }
            if (device is XInputController) {
                return DeviceFamily.Xbox;
            }
            if (device is UnityEngine.InputSystem.Gamepad) {
                return DeviceFamily.Gamepad;
            }
            return DeviceFamily.KeyboardMouse;
        }

        public static bool IsGamepad(DeviceFamily family) {
            return family != DeviceFamily.KeyboardMouse;
        }

        // "<Gamepad>/buttonSouth" → "Cross" (PlayStation) or "A" (others). Null when the path isn't
        // a gamepad control this table knows.
        public static string GamepadControlName(string path, DeviceFamily family) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }
            int slash = path.IndexOf('/');
            if (slash < 0 || !path.StartsWith("<Gamepad>") && !path.StartsWith("<DualShockGamepad>") && !path.StartsWith("<XInputController>")) {
                return null;
            }
            string control = path.Substring(slash + 1);
            bool ps = family == DeviceFamily.PlayStation;
            switch (control) {
                case "buttonSouth": return ps ? "Cross" : "A";
                case "buttonEast": return ps ? "Circle" : "B";
                case "buttonWest": return ps ? "Square" : "X";
                case "buttonNorth": return ps ? "Triangle" : "Y";
                case "leftShoulder": return ps ? "L1" : "LB";
                case "rightShoulder": return ps ? "R1" : "RB";
                case "leftTrigger": return ps ? "L2" : "LT";
                case "rightTrigger": return ps ? "R2" : "RT";
                case "start": return ps ? "Options" : "Menu";
                case "select": return ps ? "Create" : "View";
                case "leftStickPress": return ps ? "L3" : "LS";
                case "rightStickPress": return ps ? "R3" : "RS";
                case "leftStick":
                case "leftStick/x":
                case "leftStick/y":
                    return "Left Stick";
                case "rightStick":
                case "rightStick/x":
                case "rightStick/y":
                    return "Right Stick";
                case "dpad": return "D-Pad";
                case "dpad/up": return "D-Pad Up";
                case "dpad/down": return "D-Pad Down";
                case "dpad/left": return "D-Pad Left";
                case "dpad/right": return "D-Pad Right";
                default: return null;
            }
        }
    }
}
