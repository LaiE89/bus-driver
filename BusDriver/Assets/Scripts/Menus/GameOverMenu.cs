using UnityEngine;
using UnityEngine.UI;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;

namespace BusDriver.UI.Screens {
    // The PR #5 game over overlay: New Run / Main Menu. It shows when LegacyGameOver fires; both
    // go away with GameOverScreen and DeathDirector (T-M4-06).
    public class GameOverMenu : MonoBehaviour, IShiftBindable {
        [SerializeField] GameObject gameOverRoot;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainMenuButton;

        GameServices game;
        LegacyGameOver gameOver;

        public bool IsOpen { get { return gameOverRoot != null && gameOverRoot.activeSelf; } }

        public void Bind(ShiftServices shift) {
            game = shift.Game;
            gameOver = shift.GameOver;
            if (gameOver != null) {
                gameOver.OnGameOver += Show;
            }
        }

        void OnDestroy() {
            if (gameOver != null) {
                gameOver.OnGameOver -= Show;
            }
        }

        void Awake() {
            WireButtons();
            if (gameOverRoot != null) {
                gameOverRoot.SetActive(false);
            }
        }

        void WireButtons() {
            if (retryButton != null) {
                ReplaceClick(retryButton, Retry);
            }
            if (mainMenuButton != null) {
                ReplaceClick(mainMenuButton, BackToMainMenu);
            }
        }

        public void Show() {
            if (gameOverRoot != null) {
                gameOverRoot.SetActive(true);
            }
            PlayUISound();
        }

        public void Hide() {
            if (gameOverRoot != null) {
                gameOverRoot.SetActive(false);
            }
        }

        // A fresh run, as the Game Over screen's New Run will be (§2.21). The scene load resets
        // pause and time scale (SceneLoader, PauseService).
        public void Retry() {
            PlayUISound();
            if (game != null) {
                game.Flow.NewRun();
            }
        }

        public void BackToMainMenu() {
            PlayUISound();
            if (game != null) {
                game.Flow.QuitToMenu();
            }
        }

        static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action) {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        void PlayUISound() {
            if (game != null) {
                game.Audio.Play(SoundIds.UiClick);
            }
        }
    }
}
