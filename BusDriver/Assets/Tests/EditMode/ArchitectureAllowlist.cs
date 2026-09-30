namespace BusDriver.Tests.EditMode {
    // Violations of ROADMAP §4.1 that exist today, in legacy code (T-M0-06). This list may only
    // SHRINK: ArchitectureRulesTests fails on any violation not listed here, and on any entry that
    // no longer matches a violation (delete it). T-M1-20 deletes the whole list.
    static class ArchitectureAllowlist {
        // "<path under Assets/Scripts>|<rule>"
        public static readonly string[] Patterns = {
            "SceneController.cs|GameObjectFind",
            "SceneController.cs|FindObjectByType",
            "SceneController.cs|UnityRandom",
            "SceneController.cs|LegacyInput",
            "Settings/ControlsMenu.cs|LegacyInput",
            "CCTV/CCTVSystem.cs|LegacyInput",
            "Bus/BusCabin.cs|UnityRandom",
            "Bus/BusEngineSound.cs|FindObjectByType",
            "Bus/BusInput.cs|LegacyInput",
            "Sounds/SoundController.cs|FindObjectByType",
            "Passengers/WeepingAngel.cs|UnityRandom",
            "Menus/MainMenu.cs|GameObjectFind",
            "Menus/PauseMenu.cs|FindObjectByType",
            "Menus/ingameMenus.cs|LegacyInput",
            "Player/DriverLook.cs|LegacyInput",
            "Player/OnFootController.cs|LegacyInput",
            "Player/PlayerInteractor.cs|FindObjectByType",
            "Player/PlayerInteractor.cs|LegacyInput",
        };

        // "<type full name>.<field>" for static mutable fields (auto-property backing fields show
        // as <Name>k__BackingField)
        public static readonly string[] Statics = {
            "BusDriver.Gameplay.Player.SceneController.<Instance>k__BackingField",
            "BusDriver.UI.Screens.ControlsMenu.switchCameraKey",
            "BusDriver.Gameplay.Player.GameKeys.interact",
            "BusDriver.Gameplay.Player.GameKeys.doors",
            "BusDriver.Gameplay.Player.GameKeys.leaveSeat",
            "BusDriver.Gameplay.Player.GameKeys.handbrake",
            "BusDriver.UI.Menu.MainMenu.soundController",
            "BusDriver.UI.Screens.ingameMenus.pausedGame",
        };
    }
}
