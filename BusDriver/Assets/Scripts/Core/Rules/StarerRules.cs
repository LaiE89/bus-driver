using System;

namespace BusDriver.Core.Rules {
    // The Starer's numbers (§2.10) as pure functions, so they are unit-tested
    public static class StarerRules {
        public const int FrontRow = 1;
        // The row it escapes back into (§2.10 Kill sequence)
        public const int EscapeRow = 2;

        // targetRow = boardRow − round((boardRow − 1) × threat / 100): R1 at Lethal. Halves round
        // up, not to even, so the row changes at the same threat on every platform.
        public static int TargetRow(int boardRow, float threat) {
            if (boardRow <= FrontRow) {
                return FrontRow;
            }
            float t = Math.Max(0f, Math.Min(100f, threat));
            int advance = (int)Math.Floor((boardRow - 1) * t / 100f + 0.5f);
            return Math.Max(FrontRow, boardRow - advance);
        }

        // It only moves forward, and only when it sits behind its target (a larger row number)
        public static bool ShouldAdvance(int currentRow, int targetRow, float secondsUnobserved, float unobservedBeforeAdvance) {
            return currentRow > targetRow && secondsUnobserved >= unobservedBeforeAdvance;
        }

        // Stillness = stage / 3 (§2.10); stage is ThreatStage as 0..3
        public static float Stillness(int stage) {
            return Math.Max(0, Math.Min(3, stage)) / 3f;
        }
    }
}
