using UnityEngine;
using UnityEngine.UI;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;

namespace BusDriver.UI.Screens {
    // The pause screen (§2.21, was PauseMenu + ingameMenus): Resume / Options / Quit to Menu /
    // Quit Game. It shows whatever PauseService says; Esc (Global/Pause) or UI Cancel pauses, backs
    // out of Options/Controls, or resumes. The panels are baked into the legacy scene by the
    // builders; Screens.prefab and ScreenRouter take over in T-M1-16.
    public class PauseScreen : MonoBehaviour, IGameBindable {
        [SerializeField] GameObject pauseRoot;
        [SerializeField] GameObject optionsRoot;
        [SerializeField] GameObject controlsRoot;
        [SerializeField] Button resumeButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button quitToMenuButton;
        [SerializeField] Button quitGameButton;

        GameServices game;

        public bool IsOpen { get { return pauseRoot != null && pauseRoot.activeSelf; } }
        public bool OptionsOpen {
            get {
                return (optionsRoot != null && optionsRoot.activeSelf)
                    || (controlsRoot != null && controlsRoot.activeSelf);
            }
        }

        void Awake() {
            WireButtons();
            SetActive(pauseRoot, false);
            SetActive(optionsRoot, false);
            SetActive(controlsRoot, false);
        }

        // From the scene root, before Start
        public void Bind(GameServices services) {
            game = services;
            game.Pause.OnPauseChanged += HandlePauseChanged;
        }

        void OnDestroy() {
            if (game != null) {
                game.Pause.OnPauseChanged -= HandlePauseChanged;
            }
        }

        // Pause and Cancel are both Esc on the keyboard: one press, one step back
        void Update() {
            if (game == null) {
                return;
            }
            if (game.Input.Actions.Pause.WasPerformedThisFrame() || game.Input.Actions.Cancel.WasPerformedThisFrame()) {
                Back();
            }
        }

        public void Back() {
            if (!game.Pause.IsPaused) {
                game.Pause.TrySetPaused(true);
            }else if (OptionsOpen) {
                HandleEscapeFromSubmenu();
            }else {
                Resume();
            }
        }

        void WireButtons() {
            if (resumeButton != null) {
                ReplaceClick(resumeButton, Resume);
            }
            if (optionsButton != null) {
                ReplaceClick(optionsButton, OpenOptions);
            }
            if (quitToMenuButton != null) {
                ReplaceClick(quitToMenuButton, QuitToMenu);
            }
            if (quitGameButton != null) {
                ReplaceClick(quitGameButton, QuitGame);
            }
            WireSubmenuButtons();
        }

        void HandlePauseChanged(bool paused) {
            if (paused) {
                Show();
            }else {
                Hide();
            }
        }

        public void Show() {
            SetActive(optionsRoot, false);
            SetActive(controlsRoot, false);
            SetActive(pauseRoot, true);
            PlayUISound();
        }

        public void Hide() {
            SetActive(pauseRoot, false);
            SetActive(optionsRoot, false);
            SetActive(controlsRoot, false);
        }

        public void Resume() {
            if (game != null) {
                game.Pause.TrySetPaused(false);
            }
            PlayUISound();
        }

        public void OpenOptions() {
            SetActive(pauseRoot, false);
            SetActive(controlsRoot, false);
            if (optionsRoot != null) {
                optionsRoot.SetActive(true);
                OptionsScreen options = optionsRoot.GetComponentInChildren<OptionsScreen>(true);
                if (options != null) {
                    options.InitializeSettings();
                }
            }
            PlayUISound();
        }

        public void OpenControls() {
            SetActive(optionsRoot, false);
            if (controlsRoot != null) {
                controlsRoot.SetActive(true);
                ControlsScreen controls = controlsRoot.GetComponentInChildren<ControlsScreen>(true);
                if (controls != null) {
                    controls.FixingText();
                }
            }
            PlayUISound();
        }

        public void CloseControls() {
            SetActive(controlsRoot, false);
            SetActive(optionsRoot, true);
            PlayUISound();
        }

        public void CloseOptions() {
            SetActive(optionsRoot, false);
            SetActive(controlsRoot, false);
            SetActive(pauseRoot, true);
            PlayUISound();
        }

        public void HandleEscapeFromSubmenu() {
            if (controlsRoot != null && controlsRoot.activeSelf) {
                CloseControls();
                return;
            }
            CloseOptions();
        }

        // A plain quit for now; leaving a night as a death (D21) arrives in T-M7-08
        public void QuitToMenu() {
            PlayUISound();
            if (game == null) {
                Log.Error(LogCat.Flow, "PauseScreen was never bound to the game services");
                return;
            }
            game.Flow.QuitToMenu();
        }

        public void QuitGame() {
            PlayUISound();
            Log.Info(LogCat.Flow, "quit requested");
            Application.Quit();
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

        static void SetActive(GameObject target, bool active) {
            if (target != null) {
                target.SetActive(active);
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
}
