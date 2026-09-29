using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Full-screen game over overlay: Retry / Main Menu.
// UI is baked in the scene (or via Tools/Bus Driver/Bake Overlay Menus Into Scene).
public class GameOverMenu : MonoBehaviour {
    [SerializeField] GameObject gameOverRoot;
    [SerializeField] Button retryButton;
    [SerializeField] Button mainMenuButton;

    public bool IsOpen { get { return gameOverRoot != null && gameOverRoot.activeSelf; } }

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

    public void Retry() {
        PlayUISound();
        Time.timeScale = 1f;
        ingameMenus.pausedGame = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BackToMainMenu() {
        PlayUISound();
        Time.timeScale = 1f;
        ingameMenus.pausedGame = false;
        SceneManager.LoadScene("Menu");
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
