using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Economy;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using UnityEngine;

namespace BusDriver.Gameplay.Shift {
    // The shift states of §2.1. Explicit values, append-only.
    public enum ShiftState : int {
        None = 0, Depot = 1, Intro = 2, Driving = 3, Summary = 4, Dying = 5, GameOver = 6, RunWon = 7
    }

    // The night's state machine (§2.1, §4.6): Depot (nights 2–5) → Intro → Driving → Summary, or
    // Driving → Dying → GameOver; night 5's Summary leads to RunWon. It owns DriveLock.Scripted (the
    // bus only moves in Driving), decides when pausing is allowed (§4.11) and pushes the input
    // context for its states. DeathDirector moves it to Dying, then to GameOver once the death
    // presenter is done.
    public sealed class ShiftDirector : MonoBehaviour {
        public const int LastNight = 5;

        [Tooltip("How long the intro card holds before the bus is released, in seconds (§2.1) [TUNE]")]
        [SerializeField] float introSeconds = 3f;

        ShiftServices shift;
        GameServices game;
        BusController bus;
        PlayerModeController mode;
        RouteProgress progress;
        float stateTime;

        public ShiftState State { get; private set; } = ShiftState.None;
        public float IntroSeconds { get { return introSeconds; } }
        public int NightIndex { get; private set; }
        // Seconds spent in the current state (scaled time)
        public float StateTime { get { return stateTime; } }
        // The completed night, from the moment the Summary opens
        public NightResult Result { get; private set; }
        // Scaled seconds from the intro card to now, or to the Summary once the night is done: the
        // night's length (D43 aims night 1 at about 5 minutes; reported, never enforced)
        public float NightSeconds { get; private set; }
        public bool IsLastNight { get { return NightIndex >= LastNight; } }

        public event Action<ShiftState> OnStateChanged;
        // The doors are fully open at the end stop; RunFlow applies the result to the run (§4.4)
        public event Action<NightResult> OnNightCompleted;
        // The Summary's Continue on nights 1–4: RunFlow saves and loads the next night
        public event Action OnSummaryConfirmed;
        // The Summary's Continue on night 5: the run is won
        public event Action OnRunWon;

        // ShiftContext, before any other Init: the bus stays locked and pausing stays off until
        // Begin puts the night into its first state
        public void Init(ShiftServices services) {
            shift = services;
            game = services.Game;
            bus = services.Bus;
            mode = services.Mode;
            progress = services.Progress;
            NightIndex = services.Setup.NightIndex;
            bus.SetDriveLock(DriveLock.Scripted, true);
            game.Pause.CanPause = CanPause;
        }

        void OnDestroy() {
            if (progress != null) {
                progress.OnTerminus -= HandleTerminus;
            }
            // The next scene's root decides again
            if (game != null && game.Pause.CanPause == (Func<bool>)CanPause) {
                game.Pause.CanPause = null;
            }
        }

        // §4.5 step 16, the last Init step. The end stop is watched from here so the riders and the
        // ledger (earlier Init steps) have handled it before the night's result is taken.
        public void Begin() {
            if (State != ShiftState.None) {
                Log.Warn(LogCat.Flow, "ShiftDirector.Begin called twice; ignored");
                return;
            }
            if (progress != null) {
                progress.OnTerminus += HandleTerminus;
            }
            // The depot shop opens before nights 2–5 (D33)
            SetState(NightIndex > 1 ? ShiftState.Depot : ShiftState.Intro);
        }

        // The depot's Start shift (§2.18)
        public void ConfirmDepot() {
            if (State == ShiftState.Depot) {
                SetState(ShiftState.Intro);
            }
        }

        // The Summary's Continue (§2.21)
        public void ConfirmSummary() {
            if (State != ShiftState.Summary) {
                return;
            }
            if (IsLastNight) {
                SetState(ShiftState.RunWon);
                if (OnRunWon != null) {
                    OnRunWon();
                }
            }else if (OnSummaryConfirmed != null) {
                OnSummaryConfirmed();
            }
        }

        // DeathDirector (T-M4-06): the run is lost; the death presenter plays
        public void EnterDying() {
            if (State == ShiftState.Driving) {
                SetState(ShiftState.Dying);
            }
        }

        // DeathDirector (T-M4-06): the presenter is done
        public void EnterGameOver() {
            if (State == ShiftState.Dying || State == ShiftState.Driving) {
                SetState(ShiftState.GameOver);
            }
        }

        // §4.11: pausing is allowed in Driving (which includes on foot), Depot and Dying
        public bool CanPause() {
            return State == ShiftState.Driving || State == ShiftState.Depot || State == ShiftState.Dying;
        }

        void Update() {
            if (State == ShiftState.None) {
                return;
            }
            stateTime += Time.deltaTime;
            if (State == ShiftState.Intro || State == ShiftState.Driving) {
                NightSeconds += Time.deltaTime;
            }
            if (State == ShiftState.Intro && stateTime >= introSeconds) {
                SetState(ShiftState.Driving);
            }else if (State == ShiftState.Depot) {
                // The depot shop arrives in T-M7-05; until then the depot is passed straight through
                ConfirmDepot();
            }
        }

        // The F1 "Win the night" cheat (T-M4-10): the Summary, as if the doors had just opened at
        // the end stop (riders still aboard aren't delivered)
        internal void WinNightForDebug() {
            HandleTerminus();
        }

        // §2.1: the doors are fully open at the night's end stop, so the night is won
        void HandleTerminus() {
            if (State != ShiftState.Driving) {
                return;
            }
            Result = BuildResult();
            Log.Info(LogCat.Flow, $"night {NightIndex} complete: {Money.FormatDelta(Result.NetCents)}, {Result.stats.ridersDelivered} delivered");
            Log.Info(LogCat.Flow, $"[NIGHT{NightIndex}] duration={NightSeconds:0.0}s");
            if (OnNightCompleted != null) {
                OnNightCompleted(Result);
            }
            SetState(ShiftState.Summary);
        }

        // Everything the Summary shows is here: the ledger totals, each stop's arrival and the counts
        NightResult BuildResult() {
            NightResult result = new NightResult { nightIndex = NightIndex };
            ShiftLedger ledger = shift.Ledger;
            if (ledger != null) {
                result.totals = ledger.Totals.Clone();
                result.walletBeforeCents = ledger.WalletBeforeCents;
            }else {
                result.walletBeforeCents = shift.Setup.Run.walletCents;
            }
            result.FillMoneyStats();
            // Sanity arrives in M5; until then the night ends where the run stands
            result.sanityEnd = shift.Setup.Run.sanity;
            if (progress != null) {
                IReadOnlyList<StopRecord> stops = progress.Stops;
                for (int i = 0; i < stops.Count; i++) {
                    StopRecord stop = stops[i];
                    if (!stop.InNight) {
                        continue;
                    }
                    result.arrivals.Add(new NightArrival {
                        stopId = stop.StopId,
                        displayName = stop.DisplayName,
                        scheduledGameSeconds = stop.ScheduledGameSeconds,
                        arrivalGameSeconds = stop.ArrivalGameSeconds,
                        rating = stop.Rating,
                    });
                }
            }
            if (shift.Riders != null) {
                IReadOnlyList<RiderRecord> riders = shift.Riders.All;
                for (int i = 0; i < riders.Count; i++) {
                    RiderRecord record = riders[i];
                    switch (record.Status) {
                        case RiderStatus.Delivered:
                            if (!record.IsMonster) {
                                result.stats.ridersDelivered++;
                            }
                            break;
                        case RiderStatus.Kicked:
                            if (record.IsMonster) {
                                result.stats.monstersKicked++;
                            }else {
                                result.stats.innocentsKicked++;
                            }
                            break;
                        case RiderStatus.Died:
                            result.stats.passengersLost++;
                            break;
                    }
                }
            }
            return result;
        }

        void SetState(ShiftState next) {
            if (State == next) {
                return;
            }
            ShiftState previous = State;
            State = next;
            stateTime = 0f;
            // The bus is only released while driving (§2.3)
            bus.SetDriveLock(DriveLock.Scripted, next != ShiftState.Driving);
            ApplyInputContext(next);
            Log.Info(LogCat.Flow, $"shift {previous} -> {next}");
            if (OnStateChanged != null) {
                OnStateChanged(next);
            }
        }

        // §4.10: screens take UI input; driving hands the context to the mode switch's body; the
        // dying presenter only listens for Pause; Game Over is a screen.
        void ApplyInputContext(ShiftState state) {
            switch (state) {
                case ShiftState.Driving:
                    game.Input.SetContext(mode != null && mode.Mode == PlayerMode.OnFoot ? InputContext.OnFoot : InputContext.Driving);
                    break;
                case ShiftState.Dying:
                    game.Input.SetContext(InputContext.Cinematic);
                    break;
                default:
                    game.Input.SetContext(InputContext.Screen);
                    break;
            }
        }
    }
}
