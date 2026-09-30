using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // What a kick does to sanity (§2.13, §2.15): +10 for a monster, −8 for an innocent. The money
    // side is EconomyRules' (§2.7). SanitySystem (T-M5-02) applies this when a rider's status turns
    // Kicked; until it exists, this is the one place the numbers are read.
    public static class KickRules {
        public static float SanityDelta(bool monster, BalanceConfig balance) {
            if (balance == null) {
                return 0f;
            }
            return monster ? balance.monsterKickSanity : balance.innocentKickSanity;
        }
    }
}
