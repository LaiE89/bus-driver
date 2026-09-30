using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // What a monster's rules can ask about this frame (§2.9). MonsterBrain fills it from
    // PlayerAttention, already filtered by the monster's own observer kinds (§2.8).
    public struct ThreatContext {
        // Observed by any observer kind the monster counts
        public bool Observed;
        // Observed by the Cctv observer specifically
        public bool ObservedByCctv;
        public bool AttentionOnRoad;
        public bool PlayerOnFoot;
    }

    // The rule half of the threat framework (§2.9): the ordered rule list picks the rate, first
    // match wins, and the night and sanity multipliers scale positive rates only, so bad nights and
    // low sanity make monsters climb faster but never make watching them work better.
    public static class ThreatRules {
        // Sanity at or above this leaves threat rates alone (§2.9)
        public const float SanityFactorFullAt = 60f;

        public static bool Matches(ThreatCondition condition, in ThreatContext context) {
            switch (condition) {
                case ThreatCondition.Always: return true;
                case ThreatCondition.Observed: return context.Observed;
                case ThreatCondition.ObservedByCctv: return context.ObservedByCctv;
                case ThreatCondition.AttentionOnRoad: return context.AttentionOnRoad;
                case ThreatCondition.PlayerOnFoot: return context.PlayerOnFoot;
                default: return false;
            }
        }

        // Index of the first rule that holds, or -1 when none does
        public static int FirstMatch(IReadOnlyList<ThreatRule> rules, in ThreatContext context) {
            if (rules == null) {
                return -1;
            }
            for (int i = 0; i < rules.Count; i++) {
                if (Matches(rules[i].condition, context)) {
                    return i;
                }
            }
            return -1;
        }

        // The first matching rule's rate, unscaled; 0 when nothing matches
        public static float BaseRate(IReadOnlyList<ThreatRule> rules, in ThreatContext context) {
            int index = FirstMatch(rules, context);
            return index >= 0 ? rules[index].ratePerSecond : 0f;
        }

        // The rate the meter moves at this frame, per second
        public static float Rate(IReadOnlyList<ThreatRule> rules, in ThreatContext context, float nightMultiplier, float sanityFactor) {
            return ApplyMultipliers(BaseRate(rules, context), nightMultiplier, sanityFactor);
        }

        public static float ApplyMultipliers(float baseRate, float nightMultiplier, float sanityFactor) {
            return baseRate > 0f ? baseRate * nightMultiplier * sanityFactor : baseRate;
        }

        // 1.0 while sanity ≥ 60, rising linearly to factorAt0 at sanity 0 (BalanceConfig.sanityThreatFactorAt0)
        public static float SanityFactor(float sanity, float factorAt0) {
            if (sanity >= SanityFactorFullAt) {
                return 1f;
            }
            float t = sanity <= 0f ? 1f : 1f - sanity / SanityFactorFullAt;
            return 1f + (factorAt0 - 1f) * t;
        }
    }
}
