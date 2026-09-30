using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // One monster's threat meter (§2.9): 0–100 with the stages Dormant [0, 25), Unsettled [25, 50),
    // Aggressive [50, 100) and Lethal at 100. The ThreatMeter component ticks it only in Driving
    // with the rate ThreatRules picked; everything else about when it may move lives here:
    // - grace: it doesn't move for the first seconds after the monster sits down
    // - frozen: it doesn't move at all while any kill sequence runs, its own included
    // - held: while another monster's kill sequence runs it can't pass 99.9, so only one kill
    //   sequence ever runs at a time (§2.14)
    public sealed class ThreatMeterCore {
        public const float Max = 100f;
        public const float UnsettledAt = 25f;
        public const float AggressiveAt = 50f;
        // Where a held meter stops (§2.14)
        public const float HeldCeiling = 99.9f;

        public float Value { get; private set; }
        public ThreatStage Stage { get; private set; }
        public float GraceRemaining { get; private set; }
        public bool InGrace { get { return GraceRemaining > 0f; } }
        public bool Frozen { get; set; }
        // Caps rises at HeldCeiling while set; an already Lethal meter stays Lethal
        public bool HeldBelowLethal { get; set; }
        public bool IsLethal { get { return Stage == ThreatStage.Lethal; } }

        // (from, to), once per change however many stages a single move crosses
        public event Action<ThreatStage, ThreatStage> OnStageChanged;

        public ThreatMeterCore(float initial = 0f) {
            Value = Clamp(initial);
            Stage = StageOf(Value);
        }

        public static ThreatStage StageOf(float value) {
            if (value >= Max) {
                return ThreatStage.Lethal;
            }
            if (value >= AggressiveAt) {
                return ThreatStage.Aggressive;
            }
            if (value >= UnsettledAt) {
                return ThreatStage.Unsettled;
            }
            return ThreatStage.Dormant;
        }

        // The monster just sat down (§2.9 Grace)
        public void StartGrace(float seconds) {
            GraceRemaining = seconds > 0f ? seconds : 0f;
        }

        // One frame at the given rate. Grace is used up first, and only the rest of the frame
        // moves the meter, so a grace period ends on the exact second.
        public void Tick(float deltaSeconds, float ratePerSecond) {
            if (Frozen || deltaSeconds <= 0f) {
                return;
            }
            if (GraceRemaining > 0f) {
                if (deltaSeconds <= GraceRemaining) {
                    GraceRemaining -= deltaSeconds;
                    return;
                }
                deltaSeconds -= GraceRemaining;
                GraceRemaining = 0f;
            }
            Add(ratePerSecond * deltaSeconds);
        }

        // A rule change outside the frame rate: an escape's reset, the Mimic's replace (§2.10–§2.12b).
        // The hold only caps rises, so a reset downward always lands.
        public void Add(float delta) {
            if (delta == 0f) {
                return;
            }
            float next = Value + delta;
            if (delta > 0f && HeldBelowLethal && Stage != ThreatStage.Lethal && next > HeldCeiling) {
                next = Math.Max(Value, HeldCeiling);
            }
            SetValueInternal(next);
        }

        // Straight to a value (escapes reset to 60, the Mimic's replace to 20, debug cheats). Not
        // capped by the hold: callers that set a value mean it.
        public void SetValue(float value) {
            SetValueInternal(value);
        }

        void SetValueInternal(float value) {
            Value = Clamp(value);
            ThreatStage next = StageOf(Value);
            if (next == Stage) {
                return;
            }
            ThreatStage previous = Stage;
            Stage = next;
            if (OnStageChanged != null) {
                OnStageChanged(previous, next);
            }
        }

        static float Clamp(float value) {
            if (value < 0f) {
                return 0f;
            }
            return value > Max ? Max : value;
        }
    }
}
