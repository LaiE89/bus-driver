using System;
using UnityEngine.InputSystem;

namespace BusDriver.Gameplay.Input {
    // Hand-written wrapper over Assets/Input/BusDriver.inputactions (§4.10). Every action is
    // resolved once, here, so a renamed action fails loudly at boot instead of reading as zero.
    // There's no generated wrapper because generating one needs an Editor click.
    public sealed class BusDriverActions {
        public const string DrivingMap = "Driving";
        public const string OnFootMap = "OnFoot";
        public const string GlobalMap = "Global";
        public const string UIMap = "UI";

        // Every action, as "Map/Action". The asset test checks this list against the asset.
        public static readonly string[] AllIds = {
            "Driving/Throttle", "Driving/Steer", "Driving/Handbrake", "Driving/Look", "Driving/CycleCamera",
            "Driving/Doors", "Driving/LeaveSeat", "Driving/Item1", "Driving/Item2", "Driving/Item3", "Driving/ResetBus",
            "OnFoot/Move", "OnFoot/Look", "OnFoot/Interact",
            "Global/Pause", "Global/DebugOverlay", "Global/ToggleArt",
            "UI/Navigate", "UI/Submit", "UI/Cancel", "UI/Point", "UI/Click", "UI/ScrollWheel",
        };

        public readonly InputActionAsset Asset;
        public readonly InputActionMap Driving;
        public readonly InputActionMap OnFoot;
        public readonly InputActionMap Global;
        public readonly InputActionMap UI;

        // Driving
        public readonly InputAction Throttle;
        public readonly InputAction Steer;
        public readonly InputAction Handbrake;
        public readonly InputAction DrivingLook;
        public readonly InputAction CycleCamera;
        public readonly InputAction Doors;
        public readonly InputAction LeaveSeat;
        public readonly InputAction Item1;
        public readonly InputAction Item2;
        public readonly InputAction Item3;
        public readonly InputAction ResetBus;
        // OnFoot
        public readonly InputAction Move;
        public readonly InputAction OnFootLook;
        public readonly InputAction Interact;
        // Global
        public readonly InputAction Pause;
        public readonly InputAction DebugOverlay;
        public readonly InputAction ToggleArt;
        // UI
        public readonly InputAction Navigate;
        public readonly InputAction Submit;
        public readonly InputAction Cancel;

        public BusDriverActions(InputActionAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            Asset = asset;
            Driving = asset.FindActionMap(DrivingMap, true);
            OnFoot = asset.FindActionMap(OnFootMap, true);
            Global = asset.FindActionMap(GlobalMap, true);
            UI = asset.FindActionMap(UIMap, true);

            Throttle = Resolve("Driving/Throttle");
            Steer = Resolve("Driving/Steer");
            Handbrake = Resolve("Driving/Handbrake");
            DrivingLook = Resolve("Driving/Look");
            CycleCamera = Resolve("Driving/CycleCamera");
            Doors = Resolve("Driving/Doors");
            LeaveSeat = Resolve("Driving/LeaveSeat");
            Item1 = Resolve("Driving/Item1");
            Item2 = Resolve("Driving/Item2");
            Item3 = Resolve("Driving/Item3");
            ResetBus = Resolve("Driving/ResetBus");
            Move = Resolve("OnFoot/Move");
            OnFootLook = Resolve("OnFoot/Look");
            Interact = Resolve("OnFoot/Interact");
            Pause = Resolve("Global/Pause");
            DebugOverlay = Resolve("Global/DebugOverlay");
            ToggleArt = Resolve("Global/ToggleArt");
            Navigate = Resolve("UI/Navigate");
            Submit = Resolve("UI/Submit");
            Cancel = Resolve("UI/Cancel");
            for (int i = 0; i < AllIds.Length; i++) {
                Resolve(AllIds[i]);
            }
        }

        // "Map/Action", or a bare action name when it's unique (the §2.23 hint placeholders use
        // bare names; "Look" is in two maps and always needs its map)
        public InputAction Find(string actionId) {
            return string.IsNullOrEmpty(actionId) ? null : Asset.FindAction(actionId, false);
        }

        InputAction Resolve(string id) {
            InputAction action = Asset.FindAction(id, false);
            if (action == null) {
                throw new InvalidOperationException($"input action '{id}' is missing from {Asset.name}");
            }
            return action;
        }
    }
}
