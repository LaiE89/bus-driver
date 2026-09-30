using System;
using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // The death pipeline (§2.14, §4.6). Die(cause, sourceId):
    // 1. ignored if already dying, or outside Driving (nothing can die in a Summary)
    // 2. the preventers get their chance (the Salt charm, god mode); Abandoned can't be prevented
    // 3. OnDeathStarted: RunFlow deletes run.json and records the loss at once (D21), so quitting
    //    mid-presenter can't save the run
    // 4. the shift goes to Dying: the clock, threat and every rule stop ticking, and input is
    //    Cinematic (only Pause live)
    // 5. the cause's presenter plays, then the shift goes to GameOver and OnPresented fires.
    //    Abandoned has no presenter and no Game Over: its caller goes straight to the menu.
    public sealed class DeathDirector : MonoBehaviour {
        [Tooltip("One per cause; a cause without one fades to black")]
        [SerializeField] DeathPresenter[] presenters = new DeathPresenter[0];
        [Tooltip("A cause without a presenter fades to black over this long, seconds")]
        [SerializeField] float fallbackFadeSeconds = 1f;

        readonly List<IDeathPreventer> preventers = new List<IDeathPreventer>();
        ShiftServices shift;

        public bool IsDying { get; private set; }
        public bool IsPresented { get; private set; }
        // The death, from the moment it starts
        public DeathReport Report { get; private set; }

        public event Action<DeathReport> OnDeathStarted;
        public event Action<DeathReport> OnPresented;

        // ShiftContext, step 9 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            for (int i = 0; i < presenters.Length; i++) {
                if (presenters[i] != null) {
                    presenters[i].Init(services);
                }
            }
        }

        public void AddPreventer(IDeathPreventer preventer) {
            if (preventer != null && !preventers.Contains(preventer)) {
                preventers.Add(preventer);
            }
        }

        public void RemovePreventer(IDeathPreventer preventer) {
            preventers.Remove(preventer);
        }

        // KillSequence asks before the kill scare plays (§2.14 step 4): true when a preventer
        // (the Salt charm) takes this death instead
        public bool TryPrevent(DeathCause cause, string sourceId) {
            return cause != DeathCause.Abandoned && Prevented(BuildReport(cause, sourceId));
        }

        // True when the death started
        public bool Die(DeathCause cause, string sourceId = "") {
            if (IsDying || cause == DeathCause.None || shift == null) {
                return false;
            }
            if (shift.Director.State != ShiftState.Driving) {
                Log.Warn(LogCat.Death, $"Die({cause}) ignored in {shift.Director.State}");
                return false;
            }
            DeathReport report = BuildReport(cause, sourceId);
            if (cause != DeathCause.Abandoned && Prevented(report)) {
                Log.Info(LogCat.Death, $"death prevented: {report}");
                return false;
            }
            IsDying = true;
            Report = report;
            Log.Info(LogCat.Death, $"death: {report} at d={report.DistanceAlong:0} m");
            if (OnDeathStarted != null) {
                OnDeathStarted(report);
            }
            shift.Director.EnterDying();
            StartCoroutine(Present(report));
            return true;
        }

        bool Prevented(DeathReport report) {
            for (int i = 0; i < preventers.Count; i++) {
                if (preventers[i].TryPrevent(report)) {
                    return true;
                }
            }
            return false;
        }

        // A SanityZero with a Whisperer aboard is the Whisperer's (its presenter and its Game Over
        // hint, §2.14, §2.21)
        DeathReport BuildReport(DeathCause cause, string sourceId) {
            DeathReport report = new DeathReport {
                Cause = cause,
                SourceId = sourceId ?? "",
                NightIndex = shift.Setup.NightIndex,
                GameSeconds = shift.Clock != null ? shift.Clock.NowGameSeconds : 0.0,
                DistanceAlong = shift.Tracker != null ? shift.Tracker.DistanceAlong : 0f,
            };
            if (cause == DeathCause.SanityZero && report.SourceId.Length == 0) {
                MonsterBrain whisperer = ActiveMonster(WhispererId);
                if (whisperer != null) {
                    report.SourceId = WhispererId;
                }
            }
            return report;
        }

        public const string WhispererId = "whisperer";

        // An aboard, seated monster of that type, or null
        public MonsterBrain ActiveMonster(string monsterId) {
            if (shift.Monsters == null) {
                return null;
            }
            IReadOnlyList<MonsterBrain> monsters = shift.Monsters.Active;
            for (int i = 0; i < monsters.Count; i++) {
                if (monsters[i] != null && monsters[i].IsActive && monsters[i].MonsterId == monsterId) {
                    return monsters[i];
                }
            }
            return null;
        }

        IEnumerator Present(DeathReport report) {
            if (report.Cause != DeathCause.Abandoned) {
                DeathPresenter presenter = PresenterFor(report.Cause);
                if (presenter != null) {
                    yield return presenter.Present(report);
                }else {
                    yield return shift.Fade.FadeTo(1f, fallbackFadeSeconds);
                }
            }
            IsPresented = true;
            if (report.Cause != DeathCause.Abandoned) {
                shift.Director.EnterGameOver();
            }
            if (OnPresented != null) {
                OnPresented(report);
            }
        }

        DeathPresenter PresenterFor(DeathCause cause) {
            for (int i = 0; i < presenters.Length; i++) {
                if (presenters[i] != null && presenters[i].Cause == cause) {
                    return presenters[i];
                }
            }
            return null;
        }
    }
}
