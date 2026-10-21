using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Pure sanity math (§2.15): value, clamping, tiers, named drains per second, one-off deltas.
    // SanitySystem owns the live instance; nothing else mutates sanity.
    public sealed class SanityCore {
        public const float Max = 100f;
        // Tier boundaries: T0 ≥75, T1 ≥50, T2 ≥25, T3 ≥10, else T4 (§2.15)
        public const float Tier1Below = 75f;
        public const float Tier2Below = 50f;
        public const float Tier3Below = 25f;
        public const float Tier4Below = 10f;

        readonly Dictionary<object, float> drains = new Dictionary<object, float>();

        public float Value { get; private set; }
        public SanityTier Tier { get; private set; }
        public bool IsDepleted { get { return Value <= 0f; } }
        public IReadOnlyDictionary<object, float> Drains { get { return drains; } }

        public SanityCore(float initial) {
            Value = Clamp(initial);
            Tier = TierOf(Value);
        }

        public static SanityTier TierOf(float value) {
            if (value >= Tier1Below) {
                return SanityTier.T0;
            }
            if (value >= Tier2Below) {
                return SanityTier.T1;
            }
            if (value >= Tier3Below) {
                return SanityTier.T2;
            }
            if (value >= Tier4Below) {
                return SanityTier.T3;
            }
            return SanityTier.T4;
        }

        public static float Clamp(float value) {
            if (value < 0f) {
                return 0f;
            }
            if (value > Max) {
                return Max;
            }
            return value;
        }

        // Applies a one-off delta. Returns true when the tier changed.
        public bool Apply(float delta, out SanityTier previousTier) {
            previousTier = Tier;
            if (delta == 0f) {
                return false;
            }
            Value = Clamp(Value + delta);
            Tier = TierOf(Value);
            return Tier != previousTier;
        }

        public void Set(float value) {
            Value = Clamp(value);
            Tier = TierOf(Value);
        }

        // Signed per-second rate for this source (negative drains). 0 removes it.
        public void SetDrain(object source, float perSecond) {
            if (source == null) {
                return;
            }
            if (perSecond == 0f) {
                drains.Remove(source);
            }else {
                drains[source] = perSecond;
            }
        }

        public void RemoveDrain(object source) {
            if (source != null) {
                drains.Remove(source);
            }
        }

        public float DrainSum() {
            float sum = 0f;
            foreach (KeyValuePair<object, float> entry in drains) {
                sum += entry.Value;
            }
            return sum;
        }

        // Applies stacked drains for dt seconds. Returns true when the tier changed.
        public bool Tick(float dt, out SanityTier previousTier) {
            previousTier = Tier;
            if (dt <= 0f || drains.Count == 0) {
                return false;
            }
            return Apply(DrainSum() * dt, out previousTier);
        }
    }
}
