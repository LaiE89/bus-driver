using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Menu;

namespace BusDriver.UI.Screens {
    // The options screen (was OptionsMenu), a ScreenView in Screens.prefab (T-M1-16). It edits
    // SettingsService.Current, applies each change at once (§2.23) and saves on Apply or when the
    // screen is left (§4.9). Its controls are generated; the listeners are added here.
    public class OptionsScreen : ScreenView, IGameBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] ControlsScreen controls;
        [SerializeField] Slider sensSlider;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Slider brightnessSlider;
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullScreenToggle;
        [SerializeField] TMP_Dropdown targetFPSDropdown;
        [SerializeField] Button applyButton;
        [SerializeField] Button controlsButton;
        [SerializeField] Button backButton;

        // SettingsService.FrameRateFor's order
        static readonly string[] TargetFpsLabels = { "30", "60", "120", "Unlimited", "VSync" };

        Resolution[] resolutions = new Resolution[0];
        IAudioService audio;
        SettingsService settings;
        // Filling the controls must not write back into the settings
        bool refreshing;
        bool dirty;

        public void Bind(GameServices game) {
            settings = game.Settings;
            audio = game.Audio;
        }

        protected override void Awake() {
            base.Awake();
            sensSlider.onValueChanged.AddListener(AdjustSensitivity);
            qualityDropdown.onValueChanged.AddListener(ChangeQuality);
            volumeSlider.onValueChanged.AddListener(SetVolume);
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
            fullScreenToggle.onValueChanged.AddListener(SetFullscreen);
            targetFPSDropdown.onValueChanged.AddListener(SetTargetFPS);
            applyButton.onClick.AddListener(SaveSettings);
            controlsButton.onClick.AddListener(OpenControls);
            backButton.onClick.AddListener(Back);
        }

        public override void OnOpened() {
            PlayUISound();
            InitializeSettings();
        }

        // Leaving the screen saves (§4.9)
        public override void OnClosed() {
            if (dirty && settings != null) {
                settings.Save();
                dirty = false;
            }
        }

        public void OpenControls() {
            PlayUISound();
            router.Push(controls);
        }

        public void Back() {
            PlayUISound();
            if (router.Top == this) {
                router.Pop();
            }
        }

        public void InitializeSettings() {
            if (settings == null) {
                Log.Warn(LogCat.Flow, "OptionsScreen was never bound to the settings; its scene root didn't bind it");
                return;
            }
            SettingsData current = settings.Current;
            refreshing = true;

            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            List<string> options = new List<string>();
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            targetFPSDropdown.ClearOptions();
            targetFPSDropdown.AddOptions(new List<string>(TargetFpsLabels));
            int selected = 0;
            int wantWidth = current.resolutionWidth > 0 ? current.resolutionWidth : Screen.currentResolution.width;
            int wantHeight = current.resolutionHeight > 0 ? current.resolutionHeight : Screen.currentResolution.height;
            for (int i = 0; i < resolutions.Length; i++) {
                options.Add(resolutions[i].width + " x " + resolutions[i].height + " "
                    + Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value) + "Hz");
                if (resolutions[i].width == wantWidth && resolutions[i].height == wantHeight) {
                    selected = i;
                }
            }
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(selected);
            resolutionDropdown.RefreshShownValue();

            sensSlider.minValue = SettingsData.MinMouseSensitivity;
            sensSlider.maxValue = SettingsData.MaxMouseSensitivity;
            sensSlider.SetValueWithoutNotify(current.mouseSensitivity);
            qualityDropdown.SetValueWithoutNotify(current.qualityLevel);
            volumeSlider.SetValueWithoutNotify(current.masterVolume);
            brightnessSlider.SetValueWithoutNotify(current.brightness);
            fullScreenToggle.SetIsOnWithoutNotify(current.fullscreenMode != WindowMode.Windowed);
            targetFPSDropdown.SetValueWithoutNotify(current.targetFpsIndex);
            refreshing = false;
        }

        bool CanEdit() {
            return settings != null && !refreshing;
        }

        public void SetVolume(float newVolume) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.masterVolume = Mathf.Clamp01(newVolume);
            settings.ApplyAudio();
            dirty = true;
        }

        public void SetResolution(int newResolutionIndex) {
            if (!CanEdit() || newResolutionIndex < 0 || newResolutionIndex >= resolutions.Length) {
                return;
            }
            Resolution resolution = resolutions[newResolutionIndex];
            settings.Current.resolutionWidth = resolution.width;
            settings.Current.resolutionHeight = resolution.height;
            settings.Current.refreshRate = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
            ApplyAll();
        }

        public void SetFullscreen(bool newFullscreen) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.fullscreenMode = newFullscreen ? WindowMode.FullScreenWindow : WindowMode.Windowed;
            ApplyAll();
        }

        public void AdjustSensitivity(float newSens) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.mouseSensitivity = Mathf.Clamp(newSens, SettingsData.MinMouseSensitivity, SettingsData.MaxMouseSensitivity);
            dirty = true;
        }

        public void SetBrightness(float newBrightness) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.brightness = Mathf.Clamp01(newBrightness);
            settings.ApplyScene();
            dirty = true;
        }

        public void ChangeQuality(int newIndex) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.qualityLevel = newIndex;
            ApplyAll();
        }

        public void SetTargetFPS(int newIndex) {
            if (!CanEdit()) {
                return;
            }
            settings.Current.targetFpsIndex = newIndex;
            ApplyAll();
        }

        // The Apply button
        public void SaveSettings() {
            PlayUISound();
            if (settings != null) {
                settings.Save();
                dirty = false;
            }
        }

        public void PlayUISound() {
            if (audio != null) {
                audio.Play(SoundIds.UiClick);
            }
        }

        void ApplyAll() {
            settings.Apply();
            dirty = true;
        }
    }
}
