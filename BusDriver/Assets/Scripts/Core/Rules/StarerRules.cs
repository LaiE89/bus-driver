using System;

namespace BusDriver.Core.Rules {
    // The Starer's advance numbers: silent one-row hops while unwatched, then a kill hop from the
    // near-front row (no aisle telegraph).
    public static class StarerRules {
        public const int FrontRow = 1;
        // Sitting here (or closer), the next hop kills instead of moving
        public const int DefaultKillFromRow = 2;

        // One seat row closer to the driver, or FrontRow when already there
        public static int NextRow(int currentRow) {
            return Math.Max(FrontRow, currentRow - 1);
        }

        // True once the player has looked away long enough for another hop
        public static bool ShouldHop(float secondsUnobserved, float advanceIntervalSeconds) {
            return secondsUnobserved >= advanceIntervalSeconds && advanceIntervalSeconds > 0f;
        }

        // At or ahead of the kill row: the next hop is a jumpscare kill (while driving)
        public static bool IsKillHop(int currentRow, int killFromRow) {
            int from = killFromRow < FrontRow ? FrontRow : killFromRow;
            return currentRow <= from;
        }

        // Stillness = stage / 3; stage is ThreatStage as 0..3
        public static float Stillness(int stage) {
            return Math.Max(0, Math.Min(3, stage)) / 3f;
        }
    }
}
