using System;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Input;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The single owner of pause (§4.11). Pausing stops scaled time (so rules, physics and
    // WaitForSeconds stop), pauses the listener (UI sources set ignoreListenerPause) and shows the
    // Screen input context whatever the game wanted. Whether pausing is allowed is the current
    // scene root's call, through CanPause.
    public sealed class PauseService {
        readonly InputService input;
        // Restored on unpause: the fall slow-mo runs at 0.5 (§2.14)
        float storedTimeScale = 1f;

        public bool IsPaused { get; private set; }
        // Set by the scene root; null (the menu, loads, scene changes) means pausing isn't allowed
        public Func<bool> CanPause { get; set; }
        // Focus loss pauses whenever pausing is allowed, except in batch mode (tests, smoke)
        public bool PauseOnFocusLoss { get; set; } = !Application.isBatchMode;

        public event Action<bool> OnPauseChanged;

        public PauseService(InputService input) {
            this.input = input;
        }

        public bool IsPauseAllowed {
            get { return CanPause != null && CanPause(); }
        }

        // False when pausing isn't allowed right now. Unpausing is always allowed.
        public bool TrySetPaused(bool paused) {
            if (paused == IsPaused) {
                return true;
            }
            if (paused && !IsPauseAllowed) {
                return false;
            }
            IsPaused = paused;
            if (paused) {
                storedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                input.SetOverride(InputContext.Screen);
            }else {
                Time.timeScale = storedTimeScale;
                AudioListener.pause = false;
                input.ClearOverride();
            }
            Log.Info(LogCat.Flow, paused ? "paused" : "resumed");
            if (OnPauseChanged != null) {
                OnPauseChanged(paused);
            }
            return true;
        }

        public void HandleFocus(bool hasFocus) {
            if (!hasFocus && PauseOnFocusLoss && !IsPaused) {
                TrySetPaused(true);
            }
        }

        // A single-mode scene load always starts unpaused at normal speed (quitting from the pause
        // screen, Retry); the new scene's root decides again whether pausing is allowed
        public void ResetForSceneChange() {
            if (IsPaused) {
                IsPaused = false;
                input.ClearOverride();
                if (OnPauseChanged != null) {
                    OnPauseChanged(false);
                }
            }
            storedTimeScale = 1f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            CanPause = null;
        }
    }
}
