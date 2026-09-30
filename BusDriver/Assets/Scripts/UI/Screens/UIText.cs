namespace BusDriver.UI.Screens {
    // Player-facing strings that aren't in a data asset (§4.13). English only; no localization.
    public static class UIText {
        public const string Paused = "PAUSED";
        public const string Resume = "RESUME";
        public const string Options = "OPTIONS";
        public const string Controls = "CONTROLS";
        public const string QuitToMenu = "QUIT TO MENU";
        public const string QuitGame = "QUIT GAME";
        public const string Back = "BACK";
        public const string Apply = "APPLY";
        public const string ResetAll = "RESET ALL";
        public const string Reset = "RESET";

        // Intro card (§2.21). The route name moves to RouteDefinition.displayName with T-M2-01.
        public const string IntroNight = "NIGHT {0}";
        public const string RouteName = "HOLLOW PINES LINE";

        // Confirmations (§2.21, §2.22)
        public const string LeaveEndsRun = "Leaving now ends your run.";
        public const string Leave = "LEAVE";
        public const string Stay = "STAY";
        public const string StartOver = "Start over? Your current run will be lost.";
        public const string Confirm = "YES";
        public const string Cancel = "NO";

        // The dash GPS (§4.13). Early/late are words as well as colours (§2.23).
        public const string GpsNext = "NEXT";
        public const string GpsEta = "ETA {0}";
        public const string GpsEtaUnknown = "ETA --:--";
        public const string GpsEarly = "EARLY";
        public const string GpsOnTime = "ON TIME";
        public const string GpsLate = "LATE";
        public const string GpsNoSignal = "NO SIGNAL";
        public const string GpsEndOfLine = "END OF LINE";

        // Rebinding (§4.10)
        public const string PressAKey = "Press a key or mouse button for {0}. Esc cancels.";
        public const string AllControlsReset = "All controls reset";
    }
}
