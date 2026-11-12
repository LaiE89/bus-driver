using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // What a kick does to sanity: +30 for a monster, −15 for a human. The money
    // side is EconomyRules. SanitySystem applies this when a rider's status turns Kicked.
    public static class KickRules {
        public static float SanityDelta(bool monster, BalanceConfig balance) {
            if (balance == null) {
                return 0f;
            }
            return monster ? balance.monsterKickSanity : balance.innocentKickSanity;
        }
    }
}
