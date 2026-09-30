using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Full-screen "quota done" overlay: Next Level (restarts for now) / Main Menu.
// UI is baked in the scene (or via Tools/Bus Driver/Bake Overlay Menus Into Scene).
public class QuotaFulfilledMenu : MonoBehaviour {
    [SerializeField] GameObject quotaRoot;
    [SerializeField] Button nextLevelButton;
    [SerializeField] Button mainMenuButton;

    public bool IsOpen { get { return quotaRoot != null && quotaRoot.activeSelf; } }

    void Awake() {
        WireButtons();
        if (quotaRoot != null) {
            quotaRoot.SetActive(false);
        }
    }

    void WireButtons() {
        if (nextLevelButton != null) {
            ReplaceClick(nextLevelButton, NextLevel);
        }
        if (mainMenuButton != null) {
            ReplaceClick(mainMenuButton, BackToMainMenu);
        }
    }

    public void Show() {
        if (quotaRoot != null) {
            quotaRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void Hide() {
        if (quotaRoot != null) {
            quotaRoot.SetActive(false);
        }
    }

    // Placeholder until real levels exist: reload the current route
    public void NextLevel() {
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
