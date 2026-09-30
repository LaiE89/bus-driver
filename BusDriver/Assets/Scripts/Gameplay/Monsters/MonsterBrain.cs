using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // What makes a rider a monster (§2.9, §4.1 rule 4): it sits beside the Passenger and a
    // ThreatMeter on the monster's prefab variant. Every frame of Driving, once it has sat down, it
    // asks PlayerAttention what the player sees, runs its definition's rules and ticks the meter.
    // The meter holds still while any kill sequence runs (§2.9 Timing). Reaching Lethal hands the
    // monster to MonsterSystem, which starts at most one kill sequence at a time (§2.14).
    [RequireComponent(typeof(Passenger), typeof(ThreatMeter))]
    public sealed class MonsterBrain : MonoBehaviour, IKickHandler {
        [SerializeField] MonsterDefinition definition;

        Passenger passenger;
        ThreatMeter meter;
        ShiftServices shift;
        MonsterSystem system;
        PlayerAttention attention;
        IMonsterPart[] parts;
        bool seatedOnce;

        public MonsterDefinition Definition { get { return definition; } }
        public string MonsterId { get { return definition != null ? definition.id : ""; } }
        public Passenger Passenger { get { return passenger; } }
        public ThreatMeter Meter { get { return meter; } }
        public float Threat { get { return meter.Value; } }
        public ThreatStage Stage { get { return meter.Stage; } }
        // The rate the meter moved at in the last Driving frame, per second
        public float CurrentRate { get; private set; }
        // Aboard and has sat down: the only time its rules run
        public bool IsActive {
            get {
                return seatedOnce && passenger != null && passenger.IsAboard
                    && passenger.State != PassengerState.Leaving && passenger.State != PassengerState.Gone;
            }
        }
        // The observer kinds that see it right now, among those it counts (§2.8)
        public ObserverKinds ObservedBy {
            get { return attention != null && definition != null ? attention.ObservedBy(passenger) & definition.observerKinds : ObserverKinds.None; }
        }
        public bool IsObserved { get { return ObservedBy != ObserverKinds.None; } }
        public float TimeSinceObserved {
            get { return attention != null && definition != null ? attention.TimeSinceObserved(passenger, definition.observerKinds) : 0f; }
        }
        // Its kill sequence (§2.14); null for a monster without one (the Whisperer)
        public KillSequence Kill { get; private set; }
        // Its kill sequence is running (the telegraph, or the kill scare after it)
        public bool InKillSequence { get { return Kill != null && Kill.IsRunning; } }

        // It reached Lethal and the kill-sequence slot is its own. KillSequence (T-M4-07) takes it
        // from here; MonsterSystem frees the slot again when nothing does.
        public event Action<MonsterBrain> OnReachedLethal;

        void Awake() {
            passenger = GetComponent<Passenger>();
            meter = GetComponent<ThreatMeter>();
            Kill = GetComponent<KillSequence>();
            parts = GetComponents<IMonsterPart>();
        }

        // ManifestSpawner, right after the rider is created (§4.5 step 14). A definition passed in
        // replaces the prefab's own (tests); null keeps it.
        public void Bind(ShiftServices services, MonsterDefinition monster = null) {
            if (monster != null) {
                definition = monster;
            }
            shift = services;
            system = services.Monsters;
            attention = services.Attention;
            passenger.SetSeatPreference(definition.seatZonePreference, definition.seatZoneFallback);
            passenger.Seated += HandleSeated;
            meter.Init(this);
            meter.OnStageChanged += HandleStageChanged;
            system.Register(this);
            for (int i = 0; i < parts.Length; i++) {
                parts[i].Bind(services, this);
            }
        }

        void OnDestroy() {
            if (passenger != null) {
                passenger.Seated -= HandleSeated;
            }
            if (meter != null) {
                meter.OnStageChanged -= HandleStageChanged;
            }
            if (system != null) {
                system.Unregister(this);
            }
        }

        // No greybox monster refuses a kick (§2.13); the hook exists for future content
        public bool AllowKick(Passenger rider) {
            return true;
        }

        // §2.9 Grace: the meter only starts after the monster first sits down
        void HandleSeated(Passenger rider) {
            if (seatedOnce) {
                return;
            }
            seatedOnce = true;
            meter.Core.StartGrace(definition.graceSeconds);
        }

        void Update() {
            if (shift == null || !IsActive) {
                return;
            }
            meter.Core.Frozen = system.KillActive;
            if (shift.Director.State != ShiftState.Driving) {
                return;
            }
            ThreatContext context = BuildContext();
            CurrentRate = ThreatRules.Rate(definition.rules, context, NightMultiplier, SanityFactor);
            meter.Core.Tick(Time.deltaTime, CurrentRate);
        }

        ThreatContext BuildContext() {
            ObserverKinds seen = attention != null ? attention.ObservedBy(passenger) : ObserverKinds.None;
            return new ThreatContext {
                Observed = (seen & definition.observerKinds) != 0,
                ObservedByCctv = (seen & ObserverKinds.Cctv) != 0,
                AttentionOnRoad = attention != null && attention.AttentionOnRoad,
                PlayerOnFoot = attention != null && attention.Mode == AttentionMode.OnFoot,
            };
        }

        float NightMultiplier {
            get { return shift.Night != null ? shift.Night.threatRateMultiplier : 1f; }
        }

        float SanityFactor {
            get { return ThreatRules.SanityFactor(system.Sanity(), shift.Balance.sanityThreatFactorAt0); }
        }

        void HandleStageChanged(MonsterBrain brain, ThreatStage from, ThreatStage to) {
            if (to == ThreatStage.Lethal) {
                system.HandleLethal(this);
            }
        }

        // MonsterSystem: true when a kill sequence took it
        internal bool RaiseReachedLethal() {
            if (OnReachedLethal == null) {
                return false;
            }
            OnReachedLethal(this);
            return true;
        }
    }
}
