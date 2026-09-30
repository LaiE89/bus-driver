using System;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Scares {
    // The single gate for scares (§2.17, §4.6): every request goes through the ScareArbiter core,
    // and what it accepts (now, or later from its one-slot queue) is played by ScarePlayer. A Kill
    // interrupts whatever is playing. The arbiter's clock is the night's scaled time, so pausing
    // also holds a queued scare's wait. Each accepted scare raises OnScareStarted; SanitySystem
    // (T-M5-02) charges its ScareDefinition.SanityCost from there (§2.15).
    public sealed class ScareDirector : MonoBehaviour {
        sealed class Pending {
            public ScareDefinition Definition;
            public ScareContext Context;
        }

        ShiftServices shift;
        ScarePlayer player;
        ScareArbiter arbiter;
        double now;
        // The Startle-or-above scare the arbiter counts as running
        ScareHandle arbitrated;

        public ScareArbiter Arbiter { get { return arbiter; } }
        // Only a Kill may start while a kill-sequence telegraph runs (§2.14); KillSequence sets it
        public bool TelegraphActive {
            get { return arbiter != null && arbiter.TelegraphActive; }
            set {
                if (arbiter != null) {
                    arbiter.TelegraphActive = value;
                }
            }
        }
        public ScareDecision LastDecision { get; private set; }

        public event Action<ScareDefinition> OnScareStarted;
        // Every decision, accepted, queued or dropped (the F1 log, the tests)
        public event Action<ScareDecision> OnDecision;

        // ShiftContext, step 8 of the Init order (§4.5), after ScarePlayer
        public void Init(ShiftServices services) {
            shift = services;
            player = services.ScarePlayer;
            arbiter = new ScareArbiter(ScareArbiterSettings.From(services.Balance), () => now);
            services.Debug.Register("Scares", WriteDebug);
            if (DevBuild.IsEnabled()) {
                ScareDefinition[] scares = services.Game.Config.scares;
                for (int i = 0; i < scares.Length; i++) {
                    ScareDefinition scare = scares[i];
                    if (scare != null) {
                        services.Debug.AddCheat(new DebugCheat("Scares", scare.id, () => Request(scare, default(ScareContext))));
                    }
                }
            }
        }

        // True when the scare plays now or waits in the queue; false when it was dropped
        public bool Request(ScareDefinition definition, ScareContext context) {
            ScareHandle handle;
            return Request(definition, context, out handle);
        }

        // The same, with the running scare when it started at once (a kill sequence waits for its
        // kill scare to end before the death, §2.14); none when it was queued or dropped
        public bool Request(ScareDefinition definition, ScareContext context, out ScareHandle handle) {
            handle = default(ScareHandle);
            if (definition == null || arbiter == null) {
                return false;
            }
            SyncState();
            ScareDecision decision = arbiter.Request(definition.id, definition.tier, new Pending { Definition = definition, Context = context });
            Decided(decision);
            if (decision.Accepted) {
                handle = Play(definition, context);
            }
            return decision.Outcome != ScareOutcome.Dropped;
        }

        void Update() {
            if (arbiter == null) {
                return;
            }
            now += Time.deltaTime;
            SyncState();
            ScareDecision decision;
            object payload;
            if (arbiter.Update(out decision, out payload)) {
                Decided(decision);
                Pending pending = payload as Pending;
                if (decision.Accepted && pending != null) {
                    Play(pending.Definition, pending.Context);
                }
            }
            if (!arbitrated.IsNone && !player.IsRunning(arbitrated)) {
                arbitrated = default(ScareHandle);
                arbiter.NotifyFinished();
            }
        }

        void SyncState() {
            arbiter.Driving = shift.Director.State == ShiftState.Driving;
            arbiter.Paused = shift.Game.Pause.IsPaused;
        }

        ScareHandle Play(ScareDefinition definition, ScareContext context) {
            if (definition.tier == ScareTier.Kill) {
                // Kill preempts every running scare (§2.17)
                player.Interrupt();
            }
            ScareHandle handle = player.Begin(definition, context);
            if (definition.tier != ScareTier.Ambient) {
                arbitrated = handle;
            }
            Log.Info(LogCat.Scare, $"scare {definition.id} ({definition.tier})");
            if (OnScareStarted != null) {
                OnScareStarted(definition);
            }
            return handle;
        }

        void Decided(ScareDecision decision) {
            LastDecision = decision;
            if (!decision.Accepted) {
                Log.Verbose(LogCat.Scare, decision.ToString());
            }
            if (OnDecision != null) {
                OnDecision(decision);
            }
        }

        // F1 "Scares" (§4.18): what's playing and waiting, and the last 8 decisions
        void WriteDebug(StringBuilder text) {
            text.Append("playing ").Append(player.PlayingCount)
                .Append(arbiter.IsRunning ? "  running " + arbiter.RunningTier : "")
                .Append(arbiter.HasQueued ? "  queued " + arbiter.QueuedId : "")
                .Append(arbiter.TelegraphActive ? "  TELEGRAPH" : "").Append('\n');
            for (int i = 0; i < arbiter.LogCount; i++) {
                ScareDecision decision = arbiter.Recent(i);
                text.Append(decision.Time.ToString("0.0")).Append("s  ").Append(decision.ToString()).Append('\n');
            }
        }
    }
}
