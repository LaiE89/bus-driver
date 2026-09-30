using UnityEngine;
using UnityEngine.SceneManagement;
using BusDriver.Gameplay.Player;

namespace BusDriver.UI.Screens {
    public class ingameMenus : MonoBehaviour {
        [SerializeField] KeyCode pauseKey = KeyCode.Escape;
        public GameObject pauseMenuUI;
        public GameObject optionsMenuUI;
        public GameObject inGameUI;
        public static bool pausedGame = false;

        private void Start() {
            pauseMenuUI.SetActive(false);
            optionsMenuUI.SetActive(false);
            inGameUI.SetActive(true);
            pausedGame = false;
            Time.timeScale = 1;
        }

        private void Update() {
            if (Input.GetKeyDown(pauseKey) && !pausedGame) {
                if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
                    SceneController.Instance.soundController.PauseAll();
                }
                inGameUI.SetActive(false);
                Pause();
            }
        }

        public void Resume() {
            inGameUI.SetActive(true);
            pauseMenuUI.SetActive(false);
            Time.timeScale = 1;
            pausedGame = false;
            PlayUISound();
        }

        public void Pause() {
            PlayUISound();
            pauseMenuUI.SetActive(true);
            Time.timeScale = 0;
            pausedGame = true;
        }

        public void BackToMainMenu() {
            Time.timeScale = 1;
            SceneManager.LoadScene("Menu");
        }

        public void PlayUISound() {
            if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
                SceneController.Instance.soundController.Play("UI Click");
            }
        }
    }
}
