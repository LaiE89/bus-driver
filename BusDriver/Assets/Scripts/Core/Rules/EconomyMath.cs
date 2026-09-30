using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // The money numbers of §2.7, in integer cents
    public static class EconomyMath {
        // tipPercent of the fare, rounded to the nearest cent, halves up (350 at 50 % → 175)
        public static int Tip(int fareCents, int tipPercent) {
            if (fareCents <= 0 || tipPercent <= 0) {
                return 0;
            }
            return (int)(((long)fareCents * tipPercent + 50) / 100);
        }

        // §2.7: a rider delivered to their own destination at a stop reached Early earns the tip. A
        // rider carried on from a missed stop never does, and neither does a monster.
        public static bool EarnsTip(bool isMonster, bool retargeted, string destinationStopId, string exitStopId, ArrivalRating rating) {
            return !isMonster && !retargeted && rating == ArrivalRating.Early
                && !string.IsNullOrEmpty(exitStopId) && exitStopId == destinationStopId;
        }

        // §2.15: next night's sanity, min(100, max(end + bonus, floor))
        public static float CarriedSanity(float end, float bonus, float floor) {
            float carried = end + bonus;
            if (carried < floor) {
                carried = floor;
            }
            return carried > 100f ? 100f : carried;
        }
    }
}
