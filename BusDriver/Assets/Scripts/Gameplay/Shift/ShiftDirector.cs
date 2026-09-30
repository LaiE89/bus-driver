using System;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using UnityEngine;

namespace BusDriver.Gameplay.Shift {
    // The shift states of §2.1. Explicit values, append-only.
    public enum ShiftState : int {
        None = 0, Depot = 1, Intro = 2, Driving = 3, Summary = 4, Dying = 5, GameOver = 6, RunWon = 7
    }

    // The night's state machine (§2.1, §4.6). It owns DriveLock.Scripted: the bus only moves in
    // Driving. It also decides when pausing is allowed (§4.11) and pushes the input context for
    // its states. This is the T-M1-17 skeleton, Intro → Driving; the other states arrive with
    // T-M3-06 (Summary, Depot placeholder, Dying, GameOver, RunWon). The legacy Game Over overlay
    // (until T-M4-06) moves it to GameOver.
    public sealed class ShiftDirector : MonoBehaviour {
        [Tooltip("How long the intro card holds before the bus is released, in seconds (§2.1) [TUNE]")]
        [SerializeField] float introSeconds = 3f;

        GameServices game;
        BusController bus;
        PlayerModeController mode;
        LegacyGameOver gameOver;
        float stateTime;

        public ShiftState State { get; private set; } = ShiftState.None;
        public float IntroSeconds { get { return introSeconds; } }
        public int NightIndex { get; private set; }
        // Seconds spent in the current state (scaled time)
        public float StateTime { get { return stateTime; } }

        public event Action<ShiftState> OnStateChanged;

        // ShiftContext, before any other Init: the bus stays locked and pausing stays off until
        // Begin puts the night into its first state
        public void Init(ShiftServices shift) {
            game = shift.Game;
            bus = shift.Bus;
            mode = shift.Mode;
            gameOver = shift.GameOver;
            NightIndex = shift.Setup.NightIndex;
            bus.SetDriveLock(DriveLock.Scripted, true);
            game.Pause.CanPause = CanPause;
            if (gameOver != null) {
                gameOver.OnGameOver += HandleGameOver;
            }
        }

        void OnDestroy() {
            if (gameOver != null) {
                gameOver.OnGameOver -= HandleGameOver;
            }
            // The next scene's root decides again
            if (game != null && game.Pause.CanPause == (Func<bool>)CanPause) {
                game.Pause.CanPause = null;
            }
        }

        // §4.5 step 16, the last Init step
        public void Begin() {
            if (State != ShiftState.None) {
                Log.Warn(LogCat.Flow, "ShiftDirector.Begin called twice; ignored");
                return;
            }
            // The depot (nights 2–5, D33) comes before the intro from T-M7-05
            SetState(ShiftState.Intro);
        }

        // §4.11: pausing is allowed in Driving (which includes on foot), Depot and Dying
        public bool CanPause() {
            if (gameOver != null && gameOver.IsGameOver) {
                return false;
            }
            return State == ShiftState.Driving || State == ShiftState.Depot || State == ShiftState.Dying;
        }

        void Update() {
            if (State == ShiftState.None) {
                return;
            }
            stateTime += Time.deltaTime;
            if (State == ShiftState.Intro && stateTime >= introSeconds) {
                SetState(ShiftState.Driving);
            }
        }

        void HandleGameOver() {
            SetState(ShiftState.GameOver);
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

        // §4.10: screens take UI input; driving hands the context to the mode switch's body. The
        // legacy Game Over overlay sets its own (Screen) context.
        void ApplyInputContext(ShiftState state) {
            switch (state) {
                case ShiftState.Driving:
                    game.Input.SetContext(mode != null && mode.Mode == PlayerMode.OnFoot ? InputContext.OnFoot : InputContext.Driving);
                    break;
                case ShiftState.Dying:
                    game.Input.SetContext(InputContext.Cinematic);
                    break;
                case ShiftState.GameOver:
                    break;
                default:
                    game.Input.SetContext(InputContext.Screen);
                    break;
            }
        }
    }
}
