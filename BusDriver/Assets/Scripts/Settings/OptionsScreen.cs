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
    // The options screen (was OptionsMenu). It edits SettingsService.Current, applies each change
    // at once (§2.23) and saves on Apply or when the screen is left (§4.9). The UI's UnityEvents
    // call the public setters by name, so their names stay as the prefab has them.
    public class OptionsScreen : MonoBehaviour, IGameBindable {
        [SerializeField] Slider sensSlider;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Slider brightnessSlider;
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullScreenToggle;
        [SerializeField] TMP_Dropdown targetFPSDropdown;

        Resolution[] resolutions = new Resolution[0];
        SoundController soundController;
        SettingsService settings;
        // Filling the controls must not write back into the settings
        bool refreshing;
        bool dirty;

        public void Bind(GameServices game) {
            settings = game.Settings;
        }

        private void Awake() {
            if (SceneController.Instance != null) {
                soundController = SceneController.Instance.soundController;
            }else {
                soundController = MainMenu.soundController;
            }
        }

        private void Start() {
            InitializeSettings();
        }

        void OnDisable() {
            if (dirty && settings != null) {
                settings.Save();
                dirty = false;
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
            if (soundController != null) {
                soundController.Play("UI Click");
            }
        }

        void ApplyAll() {
            settings.Apply();
            dirty = true;
        }
    }
}
