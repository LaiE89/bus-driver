using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;

namespace BusDriver.UI.Screens {
    // Full-screen game over overlay: Retry / Main Menu.
    // UI is baked in the scene (or via Tools/Bus Driver/Bake Overlay Menus Into Scene).
    public class GameOverMenu : MonoBehaviour, IGameBindable {
        [SerializeField] GameObject gameOverRoot;
        [SerializeField] Button retryButton;
        [SerializeField] Button mainMenuButton;

        GameServices game;

        public bool IsOpen { get { return gameOverRoot != null && gameOverRoot.activeSelf; } }

        public void Bind(GameServices services) {
            game = services;
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

        // Reloading the scene resets pause and time scale (SceneLoader, PauseService)
        public void Retry() {
            PlayUISound();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void BackToMainMenu() {
            PlayUISound();
            if (game != null) {
                game.Flow.QuitToMenu();
            }else {
                SceneManager.LoadScene("Menu");
            }
        }

        static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action) {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        void PlayUISound() {
            if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
                SceneController.Instance.soundController.Play("UI Click");
            }
        }
    }
}
