using System;
using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The PR #5 game over (the Weeping Angel's kill), split out of SceneController (T-M1-15). It
    // lives on the night's scene root until DeathDirector and GameOverScreen replace it (T-M4-06).
    // Gameplay never references UI (§4.2): the Game Over menu listens to OnGameOver.
    public sealed class LegacyGameOver : MonoBehaviour {
        GameServices game;

        public bool IsGameOver { get; private set; }
        public event Action OnGameOver;

        public void Init(ShiftServices shift) {
            game = shift.Game;
        }

        // The game stops under the Game Over screen: not a pause, since it can't be resumed
        public void TriggerGameOver() {
            if (IsGameOver) {
                return;
            }
            IsGameOver = true;
            if (game != null) {
                game.Pause.TrySetPaused(false);
                game.Input.SetContext(InputContext.Screen);
            }
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (OnGameOver != null) {
                OnGameOver();
            }
        }
    }
}
