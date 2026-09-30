using UnityEngine.UI;
using TMPro;
using UnityEngine;
using BusDriver.Core.Util;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.UI.Screens;

namespace BusDriver.UI.Menu {
    public class MainMenu : MonoBehaviour, IGameBindable {
        [SerializeField] public OptionsScreen options;
        [SerializeField] public GameObject loadingScreen;
        [SerializeField] public Slider slider;
        [SerializeField] public TextMeshProUGUI progressText;

        GameServices game;
        bool loading;

        // From MenuContext.Initialize, before Start
        public void Bind(GameServices services) {
            game = services;
        }

        private void Start() {
            options.InitializeSettings();
        }

        public void PlayGame() {
            PlayUISound();
            if (game == null) {
                Log.Error(LogCat.Flow, "MainMenu was never bound to the game services; the Menu scene needs its MenuContext");
                return;
            }
            loading = true;
            loadingScreen.SetActive(true);
            slider.value = 0f;
            game.Flow.NewRun();
        }

        // The load itself runs on GameRoot, so this object can be destroyed by it at any time
        void Update() {
            if (!loading || game == null) {
                return;
            }
            slider.value = game.Scenes.Progress;
            progressText.SetText($"{(slider.value * 100).ToString("N2")}%");
        }

        public void QuitGame() {
            Log.Info(LogCat.Flow, "quit requested");
            Application.Quit();
        }

        public void PlayUISound() {
            if (game != null) {
                game.Audio.Play(SoundIds.UiClick);
            }
        }
    }
}
