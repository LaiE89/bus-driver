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

        // Summary (§2.21). Ratings are words plus a shape, never colour alone (§2.23).
        public const string SummaryTitle = "NIGHT {0} COMPLETE";
        public const string Continue = "CONTINUE";
        public const string LedgerFares = "FARES  {0} × {1}";
        public const string LedgerTips = "TIPS";
        public const string LedgerRefunds = "REFUNDS";
        public const string LedgerLost = "PASSENGERS LOST";
        public const string LedgerBounties = "BOUNTIES";
        public const string LedgerTotal = "NIGHT TOTAL";
        public const string SummaryWallet = "WALLET  {0}  →  {1}";
        public const string ArrivalsStop = "STOP";
        public const string ArrivalsScheduled = "SCHEDULED";
        public const string ArrivalsActual = "ARRIVED";
        public const string ArrivalsRating = "RATING";
        public const string ArrivalNone = "—";
        public const string RatingEarly = "▲ EARLY";
        public const string RatingOnTime = "● ON TIME";
        public const string RatingLate = "▼ LATE";
        public const string RatingMissed = "○ MISSED";
        public const string SummaryCounts = "MONSTERS KICKED {0}    INNOCENTS KICKED {1}    RIDERS DELIVERED {2}";

        // Game Over (§2.21). The monster's name comes from its MonsterDefinition.displayName.
        public const string GameOverTitle = "GAME OVER";
        public const string GameOverMonster = "{0} GOT YOU";
        public const string SomethingName = "Something";
        public const string GameOverSanity = "YOUR MIND WENT DARK";
        public const string GameOverFall = "YOU WENT OVER THE EDGE";
        public const string GameOverAbandoned = "YOU LEFT YOUR SHIFT";
        public const string HintFall = "Keep your eyes on the road at Dead Man's Bend.";
        public const string HintSanity = "The dark gets in. Coffee helps.";
        public const string GameOverStats = "NIGHTS SURVIVED  {0}\nFARES COLLECTED  {1}\nMONSTERS KICKED  {2}\nINNOCENTS KICKED  {3}";
        public const string NewRun = "NEW RUN";
        public const string MainMenu = "MAIN MENU";

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
