using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // A seconds range for a random draw, min and max inclusive
    [Serializable]
    public struct FloatRange {
        public float min;
        public float max;

        public FloatRange(float min, float max) {
            this.min = min;
            this.max = max;
        }
    }

    // The game's balance numbers (§4.8, Data/Balance/Balance): money, attention, sanity, scares.
    // Seeded once from §2 and then the source of truth (§0.1); rules read it, nothing writes it
    // during play (§4.1 rule 2).
    public sealed class BalanceConfig : ScriptableObject {
        [Header("Money (§2.7), integer cents")]
        [Tooltip("Credited when a rider boards, monsters too (D5, D34)")]
        public int fareCents = 350;
        [Tooltip("An Early delivery's tip, as a percentage of the fare")]
        public int tipPercent = 50;
        [Tooltip("A kicked monster's bounty until its MonsterDefinition carries its own (T-M4-03, D90)")]
        public int defaultBountyCents = 500;

        [Header("Riders (§2.6)")]
        [Tooltip("The manifest generator keeps at most this many riders aboard at once")]
        public int maxAboard = 10;

        [Header("Attention (§2.8)")]
        [Tooltip("Degrees either side of straight ahead that still count as eyes on the road")]
        public float roadYawTolerance = 35f;
        [Tooltip("Observer ranges in metres")]
        public float cctvRange = 7f;
        public float driverRange = 5f;
        public float onFootRange = 8f;
        public float mirrorRange = 6f;

        [Header("Threat (§2.9)")]
        [Tooltip("The threat rate factor at sanity 0; 1.0 at sanity 60 and above")]
        public float sanityThreatFactorAt0 = 1.5f;

        [Header("Sanity (§2.15), per second or per event")]
        public float sanityBaselinePerSecond = -0.05f;
        public float tunnelSanityPerSecond = -0.5f;
        public float doorsOpenAtStopSanityPerSecond = 0.5f;
        public float deathWitnessSanity = -10f;
        public float innocentKickSanity = -8f;
        public float monsterKickSanity = 10f;
        public float monsterScareSanity = -5f;
        public float startleSanity = -2f;
        [Tooltip("Next night: min(100, max(end + bonus, floor))")]
        public float sanityCarryBonus = 30f;
        public float sanityCarryFloor = 60f;

        [Header("Hallucinations (§2.16)")]
        [Tooltip("Seconds between hallucinations for tiers T1..T4")]
        public FloatRange[] hallucinationIntervals = {
            new FloatRange(30f, 45f), new FloatRange(18f, 28f), new FloatRange(12f, 20f), new FloatRange(8f, 12f),
        };

        [Header("Scares (§2.17), seconds")]
        public float scareGlobalGap = 6f;
        public float monsterScareCooldown = 20f;
        public float startleCooldown = 10f;
        [Tooltip("How long a blocked Monster scare waits before it is dropped")]
        public float monsterScareQueueSeconds = 5f;
    }
}
