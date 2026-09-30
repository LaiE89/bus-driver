using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// In-game pause overlay: Resume / Options / Main Menu.
// UI is baked in the scene (or via Tools/Bus Driver/Bake Overlay Menus Into Scene).
public class PauseMenu : MonoBehaviour {
    [SerializeField] GameObject pauseRoot;
    [SerializeField] GameObject optionsRoot;
    [SerializeField] GameObject controlsRoot;
    [SerializeField] Button resumeButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button mainMenuButton;

    public bool IsOpen { get { return pauseRoot != null && pauseRoot.activeSelf; } }
    public bool OptionsOpen {
        get {
            return (optionsRoot != null && optionsRoot.activeSelf)
                || (controlsRoot != null && controlsRoot.activeSelf);
        }
    }

    void Awake() {
        WireButtons();
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
    }

    void WireButtons() {
        if (resumeButton != null) {
            ReplaceClick(resumeButton, Resume);
        }
        if (optionsButton != null) {
            ReplaceClick(optionsButton, OpenOptions);
        }
        if (mainMenuButton != null) {
            ReplaceClick(mainMenuButton, BackToMainMenu);
        }
        WireSubmenuButtons();
    }

    public void Show() {
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (pauseRoot != null) {
            pauseRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void Hide() {
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
    }

    public void Resume() {
        if (SceneController.Instance != null) {
            SceneController.Instance.SetPaused(false);
        }else {
            Hide();
            ingameMenus.pausedGame = false;
            Time.timeScale = 1f;
        }
        PlayUISound();
    }

    public void OpenOptions() {
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(true);
            OptionsMenu options = optionsRoot.GetComponentInChildren<OptionsMenu>(true);
            if (options != null) {
                options.InitializeSettings();
            }
        }
        PlayUISound();
    }

    public void OpenControls() {
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(true);
            ControlsMenu controls = controlsRoot.GetComponentInChildren<ControlsMenu>(true);
            if (controls != null) {
                controls.FixingText();
            }
        }
        PlayUISound();
    }

    public void CloseControls() {
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void CloseOptions() {
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (pauseRoot != null) {
            pauseRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void HandleEscapeFromSubmenu() {
        if (controlsRoot != null && controlsRoot.activeSelf) {
            CloseControls();
            return;
        }
        CloseOptions();
    }

    public void BackToMainMenu() {
        PlayUISound();
        Time.timeScale = 1f;
        ingameMenus.pausedGame = false;
        SceneManager.LoadScene("Menu");
    }

    public void OnOptionsBack() {
        CloseOptions();
    }

    void WireSubmenuButtons() {
        if (optionsRoot != null) {
            Button back = FindDirectChildButton(optionsRoot.transform, "Back Button");
            if (back != null) {
                ReplaceClick(back, CloseOptions);
            }
            Button controls = FindDirectChildButton(optionsRoot.transform, "Controls Button");
            if (controls != null) {
                ReplaceClick(controls, OpenControls);
            }
        }
        if (controlsRoot != null) {
            Button back = FindDirectChildButton(controlsRoot.transform, "Back Button");
            if (back != null) {
                ReplaceClick(back, CloseControls);
            }
        }
    }

    static Button FindDirectChildButton(Transform root, string childName) {
        Transform child = root.Find(childName);
        if (child != null) {
            return child.GetComponent<Button>();
        }
        foreach (Transform t in root) {
            if (t.name.IndexOf(childName, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                Button button = t.GetComponent<Button>();
                if (button != null) {
                    return button;
                }
            }
        }
        return null;
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
