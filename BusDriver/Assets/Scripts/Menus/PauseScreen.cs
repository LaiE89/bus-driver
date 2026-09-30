using UnityEngine;
using UnityEngine.UI;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;

namespace BusDriver.UI.Screens {
    // The pause screen (§2.21): Resume, Options, Controls, Quit to Menu, Quit Game. ScreenRouter
    // pushes it when PauseService pauses; popping it (Resume, or Esc/B) empties the stack, which
    // resumes. It lives in Screens.prefab (T-M1-16).
    public sealed class PauseScreen : ScreenView, IGameBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] OptionsScreen options;
        [SerializeField] ControlsScreen controls;
        [SerializeField] Button resumeButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button controlsButton;
        [SerializeField] Button quitToMenuButton;
        [SerializeField] Button quitGameButton;

        GameServices game;

        protected override void Awake() {
            base.Awake();
            resumeButton.onClick.AddListener(Resume);
            optionsButton.onClick.AddListener(OpenOptions);
            controlsButton.onClick.AddListener(OpenControls);
            quitToMenuButton.onClick.AddListener(QuitToMenu);
            quitGameButton.onClick.AddListener(QuitGame);
        }

        public void Bind(GameServices services) {
            game = services;
        }

        public override void OnOpened() {
            PlayUISound();
        }

        public void Resume() {
            PlayUISound();
            if (router.Top == this) {
                router.Pop();
            }
        }

        public void OpenOptions() {
            PlayUISound();
            router.Push(options);
        }

        public void OpenControls() {
            PlayUISound();
            router.Push(controls);
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

        void PlayUISound() {
            if (game != null) {
                game.Audio.Play(SoundIds.UiClick);
            }
        }
    }
}
