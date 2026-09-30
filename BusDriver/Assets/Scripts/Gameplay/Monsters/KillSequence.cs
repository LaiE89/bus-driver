using System;
using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // How a kill sequence ended (§2.14)
    public enum KillOutcome : int {
        // The escape condition held long enough (D31): threat back to the reset value
        Escaped = 0,
        // Kicked out during the telegraph
        Kicked = 1,
        // A preventer that doesn't expel (god mode) let it off like an escape
        Spared = 2,
        // The Salt charm: the monster is gone, fare kept, no bounty
        Expelled = 3,
        // The kill scare played and DeathDirector.Die(MonsterKill) was called
        Killed = 4,
        // The night stopped driving under it (another death, the Summary)
        Aborted = 5,
    }

    // A monster's kill sequence (§2.14, D31): what happens when its meter reaches Lethal and
    // MonsterSystem gives it the single slot.
    // 1. Telegraph, killTelegraphSeconds long: the cabin lights flicker and the rumble plays
    //    (scare.telegraph.generic, straight through ScarePlayer), the monster shows TelegraphStand,
    //    and ScareDirector suppresses every scare but a Kill.
    // 2. The player escapes by the definition's escape condition, held without a break (watched
    //    for the Starer, unwatched for the Mimic), or by kicking it. Either ends the sequence; the
    //    condition also sets the threat back to escape.resetThreat.
    // 3. Otherwise a preventer gets its chance (the Salt charm expels the monster; god mode lets
    //    it off), and failing that the kill scare plays and DeathDirector.Die(MonsterKill) follows.
    // The slot is released whatever happens, so a monster held at 99.9 can go next. Scaled time:
    // pausing holds the telegraph.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class KillSequence : MonoBehaviour, IMonsterPart {
        public const string TelegraphScareId = "scare.telegraph.generic";

        [Tooltip("The Salt charm's flash when it expels a monster: seconds [TUNE]")]
        [SerializeField] float expelFlashSeconds = 0.3f;
        [Tooltip("The flash's light intensity [TUNE]")]
        [SerializeField] float expelFlashIntensity = 8f;

        enum Phase { Idle, Telegraph, Kill }

        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        Phase phase;
        ScareHandle telegraph;

        public bool IsRunning { get { return phase != Phase.Idle; } }
        public bool InTelegraph { get { return phase == Phase.Telegraph; } }
        // Seconds into the telegraph, scaled time
        public float TelegraphElapsed { get; private set; }
        // How long the escape condition has held without a break
        public float EscapeProgress { get; private set; }
        public KillOutcome LastOutcome { get; private set; }

        public event Action<MonsterBrain> OnTelegraphStarted;
        // The telegraph ended in an escape: the condition held, or the monster was kicked
        public event Action<MonsterBrain> OnTelegraphEscaped;
        // The telegraph ran its full length; the kill (or a preventer) follows
        public event Action<MonsterBrain> OnTelegraphCompleted;
        // Every ending, once, with how it ended
        public event Action<MonsterBrain, KillOutcome> OnResolved;

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            brain.OnReachedLethal += HandleReachedLethal;
        }

        void OnDestroy() {
            if (brain != null) {
                brain.OnReachedLethal -= HandleReachedLethal;
            }
            if (phase == Phase.Telegraph && shift != null) {
                StopTelegraphEffects();
            }
        }

        void HandleReachedLethal(MonsterBrain owner) {
            if (IsRunning) {
                return;
            }
            StartCoroutine(Run());
        }

        IEnumerator Run() {
            phase = Phase.Telegraph;
            TelegraphElapsed = 0f;
            EscapeProgress = 0f;
            MonsterDefinition definition = brain.Definition;
            MonsterEscape escape = definition.escape;
            shift.Scares.TelegraphActive = true;
            ScareDefinition telegraphScare = shift.Game.Config.Scare(TelegraphScareId);
            telegraph = telegraphScare != null ? shift.ScarePlayer.Begin(telegraphScare, ScareContext.For(brain)) : default(ScareHandle);
            SetTell(1f);
            Log.Info(LogCat.Threat, $"{brain.MonsterId}: kill telegraph ({definition.killTelegraphSeconds:0.0} s, escape {escape.kind} {escape.seconds:0.0} s)");
            if (OnTelegraphStarted != null) {
                OnTelegraphStarted(brain);
            }

            while (TelegraphElapsed < definition.killTelegraphSeconds) {
                yield return null;
                if (WasKicked) {
                    EndTelegraph(false);
                    Resolve(KillOutcome.Kicked);
                    yield break;
                }
                if (shift.Director.State != ShiftState.Driving) {
                    EndTelegraph(false);
                    Resolve(KillOutcome.Aborted);
                    yield break;
                }
                float dt = Time.deltaTime;
                TelegraphElapsed += dt;
                EscapeProgress = EscapeHolds(escape.kind) ? EscapeProgress + dt : 0f;
                if (escape.kind != EscapeKind.None && EscapeProgress >= escape.seconds) {
                    EndTelegraph(false);
                    brain.Meter.Core.SetValue(escape.resetThreat);
                    Resolve(KillOutcome.Escaped);
                    yield break;
                }
            }
            EndTelegraph(true);

            IDeathPreventer preventer = shift.Death != null ? shift.Death.PreventedBy(DeathCause.MonsterKill, brain.MonsterId) : null;
            if (preventer != null) {
                if (preventer.ExpelsMonster) {
                    Expel();
                    yield break;
                }
                brain.Meter.Core.SetValue(escape.resetThreat);
                Resolve(KillOutcome.Spared);
                yield break;
            }

            phase = Phase.Kill;
            ScareHandle kill;
            if (definition.killScare != null && shift.Scares.Request(definition.killScare, ScareContext.For(brain), out kill)) {
                while (!kill.IsNone && shift.ScarePlayer.IsRunning(kill)) {
                    yield return null;
                }
            }
            if (shift.Death != null) {
                shift.Death.Die(DeathCause.MonsterKill, brain.MonsterId);
            }
            Resolve(KillOutcome.Killed);
        }

        bool WasKicked {
            get { return passenger == null || passenger.WasKicked || passenger.State == PassengerState.Leaving || passenger.State == PassengerState.Gone; }
        }

        bool EscapeHolds(EscapeKind kind) {
            switch (kind) {
                case EscapeKind.ObserveFor: return brain.IsObserved;
                case EscapeKind.UnobservedFor: return !brain.IsObserved;
                default: return false;
            }
        }

        // The telegraph's lights, rumble, tell and scare suppression end together
        void EndTelegraph(bool completed) {
            StopTelegraphEffects();
            if (completed) {
                if (OnTelegraphCompleted != null) {
                    OnTelegraphCompleted(brain);
                }
            }
        }

        void StopTelegraphEffects() {
            if (!telegraph.IsNone) {
                shift.ScarePlayer.Interrupt(telegraph);
                telegraph = default(ScareHandle);
            }
            shift.Scares.TelegraphActive = false;
            SetTell(0f);
        }

        void SetTell(float intensity) {
            if (passenger != null && passenger.View != null) {
                passenger.View.SetTell(TellId.TelegraphStand, intensity);
            }
        }

        // §2.14 step 4: the Salt charm. A flash and mon.expel where it stood, and it is gone: the
        // fare is kept and there's no bounty (§2.7).
        void Expel() {
            Vector3 at = passenger.Head != null ? passenger.Head.position : passenger.transform.position;
            GameObject flash = new GameObject("Expel Flash");
            flash.transform.SetParent(passenger.transform.parent, false);
            flash.transform.position = at;
            Light light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = expelFlashIntensity;
            light.color = new Color(1f, 0.95f, 0.85f);
            Destroy(flash, expelFlashSeconds);
            shift.Game.Audio.PlayAt(SoundIds.MonExpel, at);
            Log.Info(LogCat.Threat, $"{brain.MonsterId} expelled by a preventer");
            shift.Riders.MarkExpelled(passenger);
            Resolve(KillOutcome.Expelled);
            Destroy(passenger.gameObject);
        }

        void Resolve(KillOutcome outcome) {
            phase = Phase.Idle;
            LastOutcome = outcome;
            Log.Info(LogCat.Threat, $"{brain.MonsterId}: kill sequence {outcome}");
            if ((outcome == KillOutcome.Escaped || outcome == KillOutcome.Kicked) && OnTelegraphEscaped != null) {
                OnTelegraphEscaped(brain);
            }
            if (OnResolved != null) {
                OnResolved(brain, outcome);
            }
            shift.Monsters.EndKill(brain);
        }
    }
}
