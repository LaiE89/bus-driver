using System;
using System.Collections.Generic;
using BusDriver.UI.Screens;
using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Moves the MVP's rebindable KeyCode statics (GameKeys, ControlsMenu.switchCameraKey) in and
    // out of SettingsData.legacyKeyBindings, so rebinds persist in settings.json (D56). Deleted
    // with those statics when the Input System migration lands (T-M1-07).
    public static class LegacyKeyBindings {
        public const string CycleCamera = "cycleCamera";
        public const string Handbrake = "handbrake";
        public const string Doors = "doors";
        public const string LeaveSeat = "leaveSeat";
        public const string Interact = "interact";

        public static void Capture(Dictionary<string, string> into) {
            into[CycleCamera] = ControlsMenu.switchCameraKey.ToString();
            into[Handbrake] = GameKeys.handbrake.ToString();
            into[Doors] = GameKeys.doors.ToString();
            into[LeaveSeat] = GameKeys.leaveSeat.ToString();
            into[Interact] = GameKeys.interact.ToString();
        }

        // Missing or unreadable entries fall back to the D48 defaults
        public static void Restore(Dictionary<string, string> from) {
            ControlsMenu.switchCameraKey = Read(from, CycleCamera, ControlsMenu.defaultSwitchCameraKey);
            GameKeys.handbrake = Read(from, Handbrake, GameKeys.defaultHandbrake);
            GameKeys.doors = Read(from, Doors, GameKeys.defaultDoors);
            GameKeys.leaveSeat = Read(from, LeaveSeat, GameKeys.defaultLeaveSeat);
            GameKeys.interact = Read(from, Interact, GameKeys.defaultInteract);
        }

        public static void ResetDefaults() {
            ControlsMenu.switchCameraKey = ControlsMenu.defaultSwitchCameraKey;
            GameKeys.ResetDefaults();
        }

        static KeyCode Read(Dictionary<string, string> from, string id, KeyCode fallback) {
            string name;
            KeyCode key;
            if (from != null && from.TryGetValue(id, out name) && Enum.TryParse(name, out key) && key != KeyCode.None) {
                return key;
            }
            return fallback;
        }
    }
}
