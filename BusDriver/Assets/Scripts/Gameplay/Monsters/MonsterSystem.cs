using System;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // The night's monsters (§4.6): every MonsterBrain registers here, the definitions and their
    // generated prefabs are looked up here, and here is the single kill-sequence slot (§2.14): a
    // monster that reaches Lethal takes it if it's free, and one that can't is held at 99.9 until
    // the slot is released. Every meter freezes while the slot is taken (§2.9 Timing).
    public sealed class MonsterSystem : MonoBehaviour {
        readonly List<MonsterBrain> active = new List<MonsterBrain>();
        ShiftServices shift;
        GameRootConfig config;

        public IReadOnlyList<MonsterBrain> Active { get { return active; } }
        // The monster whose kill sequence is running, or null
        public MonsterBrain KillOwner { get; private set; }
        public bool KillActive { get { return KillOwner != null; } }
        // Sanity for the threat multiplier (§2.9). The run's sanity until SanitySystem (T-M5-02)
        // points it at the live value.
        public Func<float> Sanity { get; set; }

        public event Action<MonsterBrain> OnRegistered;

        // ShiftContext, step 10 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            config = services.Game != null ? services.Game.Config : null;
            Sanity = () => services.Setup.Run.sanity;
            if (services.Economy != null) {
                services.Economy.BountyFor = BountyFor;
            }
            services.Debug.Register("Monsters", WriteDebug);
        }

        public MonsterDefinition Definition(string monsterId) {
            return config != null ? config.Monster(monsterId) : null;
        }

        // The monster's generated logic prefab (Passenger + MonsterBrain + ThreatMeter), or null
        public Passenger PrefabFor(string monsterId) {
            MonsterDefinition definition = Definition(monsterId);
            if (definition == null || definition.logicPrefab == null) {
                return null;
            }
            return definition.logicPrefab.GetComponent<Passenger>();
        }

        // §2.7: the monster's own bounty; the balance default for an unknown id
        public int BountyFor(string monsterId) {
            MonsterDefinition definition = Definition(monsterId);
            if (definition != null) {
                return definition.bountyCents;
            }
            return shift != null && shift.Balance != null ? shift.Balance.defaultBountyCents : 0;
        }

        public MonsterBrain Find(Passenger passenger) {
            for (int i = 0; i < active.Count; i++) {
                if (active[i].Passenger == passenger) {
                    return active[i];
                }
            }
            return null;
        }

        internal void Register(MonsterBrain brain) {
            if (brain == null || active.Contains(brain)) {
                return;
            }
            active.Add(brain);
            if (OnRegistered != null) {
                OnRegistered(brain);
            }
        }

        internal void Unregister(MonsterBrain brain) {
            active.Remove(brain);
            if (KillOwner == brain) {
                EndKill(brain);
            }
        }

        // The kill-sequence slot (§2.14): true when it was free or is already this monster's
        public bool TryBeginKill(MonsterBrain brain) {
            if (KillOwner != null && KillOwner != brain) {
                return false;
            }
            KillOwner = brain;
            return true;
        }

        // The sequence resolved (escape, kick, the Salt charm, death); held meters may climb again
        public void EndKill(MonsterBrain brain) {
            if (KillOwner != brain) {
                return;
            }
            KillOwner = null;
            for (int i = 0; i < active.Count; i++) {
                active[i].Meter.Core.HeldBelowLethal = false;
            }
        }

        // A brain's meter reached 100. A monster with no kill sequence of its own (the Whisperer,
        // whose escape is None) just stays Lethal; the others take the slot or wait at 99.9.
        internal void HandleLethal(MonsterBrain brain) {
            if (brain.Definition.escape.kind == EscapeKind.None) {
                return;
            }
            if (!TryBeginKill(brain)) {
                brain.Meter.Core.SetValue(ThreatMeterCore.HeldCeiling);
                brain.Meter.Core.HeldBelowLethal = true;
                Log.Info(LogCat.Threat, $"{brain.MonsterId} held at {ThreatMeterCore.HeldCeiling}: {KillOwner.MonsterId}'s kill sequence is running");
                return;
            }
            Log.Info(LogCat.Threat, $"{brain.MonsterId} reached Lethal");
            if (!brain.RaiseReachedLethal()) {
                // Nothing to play it: don't freeze every meter for good
                Log.Warn(LogCat.Threat, $"{brain.MonsterId} is Lethal but has no kill sequence");
                EndKill(brain);
            }
        }

        // F1 "Monsters" (§4.18): id, threat, stage, rate, who sees it, how long unseen, kill state
        void WriteDebug(StringBuilder text) {
            if (active.Count == 0) {
                text.Append("none aboard or waiting\n");
                return;
            }
            for (int i = 0; i < active.Count; i++) {
                MonsterBrain brain = active[i];
                ThreatMeterCore core = brain.Meter.Core;
                text.Append(brain.MonsterId).Append("  ")
                    .Append(core.Value.ToString("0.0")).Append(' ').Append(core.Stage)
                    .Append("  ").Append(brain.CurrentRate >= 0f ? "+" : "").Append(brain.CurrentRate.ToString("0.00")).Append("/s");
                if (!brain.IsActive) {
                    text.Append("  (not seated)\n");
                    continue;
                }
                text.Append("  seen ").Append(brain.ObservedBy)
                    .Append("  unseen ").Append(brain.TimeSinceObserved.ToString("0.0")).Append('s');
                if (core.InGrace) {
                    text.Append("  grace ").Append(core.GraceRemaining.ToString("0.0")).Append('s');
                }
                if (KillOwner == brain) {
                    text.Append("  KILL SEQUENCE");
                }else if (core.HeldBelowLethal) {
                    text.Append("  held");
                }
                if (core.Frozen) {
                    text.Append("  frozen");
                }
                text.Append('\n');
            }
        }
    }
}
