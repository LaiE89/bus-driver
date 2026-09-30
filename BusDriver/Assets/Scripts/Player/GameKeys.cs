using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Gameplay keys. Statics so the controls menu can rebind them and HUD/prompts stay in sync.
    public static class GameKeys {
        public const KeyCode defaultInteract = KeyCode.Mouse1;
        public const KeyCode defaultDoors = KeyCode.Q;
        public const KeyCode defaultLeaveSeat = KeyCode.E;
        public const KeyCode defaultHandbrake = KeyCode.LeftShift;

        public static KeyCode interact = defaultInteract;
        public static KeyCode doors = defaultDoors;
        public static KeyCode leaveSeat = defaultLeaveSeat;
        public static KeyCode handbrake = defaultHandbrake;

        public static string Label(KeyCode key) {
            switch (key) {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.LeftShift: return "L-SHIFT";
                case KeyCode.RightShift: return "R-SHIFT";
                case KeyCode.LeftControl: return "L-CTRL";
                case KeyCode.RightControl: return "R-CTRL";
                case KeyCode.LeftAlt: return "L-ALT";
                case KeyCode.RightAlt: return "R-ALT";
                case KeyCode.None: return "NONE";
                default: return key.ToString().ToUpper();
            }
        }

        public static void ResetDefaults() {
            interact = defaultInteract;
            doors = defaultDoors;
            leaveSeat = defaultLeaveSeat;
            handbrake = defaultHandbrake;
        }
    }
}
