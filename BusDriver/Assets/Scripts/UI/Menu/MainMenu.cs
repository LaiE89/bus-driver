using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.UI.Screens;

namespace BusDriver.UI.Menu {
    // The generated menu's buttons (MenuBuilder v0, T-M1-16): New Run, Options, Controls, Quit, and
    // the loading bar while the night loads. Options and Controls are the Screens.prefab screens,
    // pushed on its router. The full menu (Continue, Journal, Credits, the diorama) is M8's.
    public sealed class MainMenu : MonoBehaviour, IGameBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] OptionsScreen options;
        [SerializeField] ControlsScreen controls;
        [SerializeField] Button newRunButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button controlsButton;
        [SerializeField] Button quitButton;

        GameServices game;

        // From MenuContext.Initialize, before Start
        public void Bind(GameServices services) {
            game = services;
        }

        void Awake() {
            newRunButton.onClick.AddListener(PlayGame);
            optionsButton.onClick.AddListener(OpenOptions);
            controlsButton.onClick.AddListener(OpenControls);
            quitButton.onClick.AddListener(QuitGame);
        }

        // Controller-ready rule 4 (§4.10): something always has focus
        void Start() {
            if (EventSystem.current != null) {
                EventSystem.current.SetSelectedGameObject(newRunButton.gameObject);
            }
        }

        public void PlayGame() {
            PlayUISound();
            if (game == null) {
                Log.Error(LogCat.Flow, "MainMenu was never bound to the game services; the Menu scene needs its MenuContext");
                return;
            }
            // GameRoot's loading overlay takes it from here: this scene is destroyed partway
            // through the load, so the bar can't live on this canvas
            game.Flow.NewRun();
        }

        public void OpenOptions() {
            PlayUISound();
            router.Push(options);
        }

        public void OpenControls() {
            PlayUISound();
            router.Push(controls);
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
