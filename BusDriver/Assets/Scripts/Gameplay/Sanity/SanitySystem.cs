using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // The night's sanity (§2.15, §4.6): value, drains, tiers. Hidden from the HUD; the F1 overlay
    // shows the number. Freezes outside Driving. Reaching 0 raises OnDepleted but does not kill
    // (DeathDirector ignores SanityZero).
    public sealed class SanitySystem : MonoBehaviour {
        static readonly object BaselineSource = new object();
        static readonly object TunnelSource = new object();
        static readonly object DoorsSource = new object();

        ShiftServices shift;
        SanityCore core;
        TunnelZone tunnel;
        SanityFx fx;
        bool depletedRaised;

        public float Value { get { return core != null ? core.Value : 0f; } }
        public SanityTier Tier { get { return core != null ? core.Tier : SanityTier.T0; } }
        public SanityCore Core { get { return core; } }

        public event Action<SanityTier> OnTierChanged;
        public event Action OnDepleted;

        // ShiftContext, step 7 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            float initial = services.Setup != null && services.Setup.Run != null
                ? services.Setup.Run.sanity : SanityCore.Max;
            core = new SanityCore(initial);
            depletedRaised = core.IsDepleted;
            SyncRun();

            if (services.Monsters != null) {
                services.Monsters.Sanity = () => Value;
            }
            if (services.Riders != null) {
                services.Riders.OnStatusChanged += HandleStatusChanged;
            }
            if (services.Scares != null) {
                services.Scares.OnScareStarted += HandleScareStarted;
            }
            tunnel = services.Route != null ? services.Route.GetComponentInChildren<TunnelZone>(true) : null;

            fx = GetComponent<SanityFx>();
            if (fx == null) {
                fx = gameObject.AddComponent<SanityFx>();
            }
            fx.Init(services, this);

            // Readout is registered first in ShiftContext.RegisterDebugSections; only cheats here
#if UNITY_EDITOR || BUSDRIVER_DEV
            services.Debug.AddCheat(new DebugCheat("Sanity", "Sanity 100", () => Set(100f, SanityReason.Cheat)));
            services.Debug.AddCheat(new DebugCheat("Sanity", "Sanity 60", () => Set(60f, SanityReason.Cheat)));
            services.Debug.AddCheat(new DebugCheat("Sanity", "Sanity 40", () => Set(40f, SanityReason.Cheat)));
            services.Debug.AddCheat(new DebugCheat("Sanity", "Sanity 20", () => Set(20f, SanityReason.Cheat)));
            services.Debug.AddCheat(new DebugCheat("Sanity", "Sanity 5", () => Set(5f, SanityReason.Cheat)));
#endif
            Log.Info(LogCat.Sanity, $"init at {Value:0.0} ({Tier})");
        }

        public void Apply(float delta, SanityReason reason) {
            if (core == null || delta == 0f) {
                return;
            }
            bool tierChanged = core.Apply(delta, out SanityTier previous);
            AfterChange(previous, tierChanged, reason, delta);
        }

        public void Set(float value, SanityReason reason = SanityReason.Cheat) {
            if (core == null) {
                return;
            }
            float before = core.Value;
            SanityTier previous = core.Tier;
            core.Set(value);
            AfterChange(previous, core.Tier != previous, reason, core.Value - before);
        }

        public void AddDrain(object source, float perSecond) {
            if (core != null) {
                core.SetDrain(source, perSecond);
            }
        }

        public void RemoveDrain(object source) {
            if (core != null) {
                core.RemoveDrain(source);
            }
        }

        void Update() {
            if (core == null || shift == null || shift.Director == null) {
                return;
            }
            if (shift.Director.State != ShiftState.Driving) {
                core.RemoveDrain(BaselineSource);
                core.RemoveDrain(TunnelSource);
                core.RemoveDrain(DoorsSource);
                return;
            }

            BalanceConfig balance = shift.Balance;
            float baseline = balance != null ? balance.sanityBaselinePerSecond : -0.05f;
            core.SetDrain(BaselineSource, baseline);

            bool inTunnel = tunnel != null && tunnel.IsInside;
            float tunnelRate = inTunnel && balance != null ? balance.tunnelSanityPerSecond : 0f;
            core.SetDrain(TunnelSource, tunnelRate);

            bool doorsRegen = shift.Doors != null && shift.Doors.IsFullyOpen
                && shift.Cabin != null && shift.Cabin.CurrentStop != null;
            float doorsRate = doorsRegen && balance != null ? balance.doorsOpenAtStopSanityPerSecond : 0f;
            core.SetDrain(DoorsSource, doorsRate);

            float before = core.Value;
            bool tierChanged = core.Tick(Time.deltaTime, out SanityTier previous);
            if (core.Value != before) {
                SyncRun();
            }
            if (tierChanged) {
                RaiseTier(previous);
            }
            CheckDepleted();
        }

        void HandleStatusChanged(RiderRecord record) {
            if (record == null || shift == null || shift.Balance == null) {
                return;
            }
            if (record.Status == RiderStatus.Kicked) {
                Apply(KickRules.SanityDelta(record.IsMonster, shift.Balance), SanityReason.Kick);
            }else if (record.Status == RiderStatus.Died) {
                Apply(shift.Balance.deathWitnessSanity, SanityReason.DeathWitness);
            }
        }

        void HandleScareStarted(ScareDefinition definition) {
            if (definition == null || shift == null) {
                return;
            }
            float cost = definition.SanityCost(shift.Balance);
            if (cost != 0f) {
                Apply(cost, SanityReason.Scare);
            }
        }

        void AfterChange(SanityTier previous, bool tierChanged, SanityReason reason, float delta) {
            SyncRun();
            Log.Verbose(LogCat.Sanity, $"{reason} {delta:+0.0;-0.0} → {Value:0.0} ({Tier})");
            if (tierChanged) {
                RaiseTier(previous);
            }
            CheckDepleted();
        }

        void RaiseTier(SanityTier previous) {
            Log.Info(LogCat.Sanity, $"tier {previous} → {Tier} at {Value:0.0}");
            if (OnTierChanged != null) {
                OnTierChanged(Tier);
            }
        }

        void CheckDepleted() {
            if (core == null) {
                return;
            }
            if (!core.IsDepleted) {
                depletedRaised = false;
                return;
            }
            if (depletedRaised) {
                return;
            }
            depletedRaised = true;
            Log.Info(LogCat.Sanity, "depleted (no death)");
            if (OnDepleted != null) {
                OnDepleted();
            }
        }

        void SyncRun() {
            if (shift != null && shift.Setup != null && shift.Setup.Run != null && core != null) {
                shift.Setup.Run.sanity = core.Value;
            }
        }

        void OnDestroy() {
            if (shift == null) {
                return;
            }
            if (shift.Riders != null) {
                shift.Riders.OnStatusChanged -= HandleStatusChanged;
            }
            if (shift.Scares != null) {
                shift.Scares.OnScareStarted -= HandleScareStarted;
            }
        }
    }
}
