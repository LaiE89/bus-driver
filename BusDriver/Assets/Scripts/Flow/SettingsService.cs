using System;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Player;
using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Gameplay.Flow {
    // Owns SettingsData (§2.23, §4.6): loads settings.json at boot, applies it, and saves it when
    // the player presses Apply or leaves the options screen (§4.9).
    public sealed class SettingsService {
        // The only exposed mixer parameter until the [HUMAN] mixer groups exist (T-M1-10)
        public const string MasterVolumeParameter = "volume";

        readonly ISaveStore saves;
        readonly AudioMixer mixer;

        public SettingsData Current { get; private set; }

        // After Apply, so listeners see the applied values
        public event Action OnChanged;

        public SettingsService(ISaveStore saves, AudioMixer mixer) {
            this.saves = saves;
            this.mixer = mixer;
            Current = saves.LoadOrNew<SettingsData>(SaveSlot.Settings);
            Sanitize(Current);
        }

        public void Apply() {
            ApplyDisplay();
            ApplyQualityAndFrameRate();
            ApplyScene();
            ApplyAudio();
            LegacyKeyBindings.Restore(Current.legacyKeyBindings);
            if (OnChanged != null) {
                OnChanged();
            }
        }

        public void Save() {
            LegacyKeyBindings.Capture(Current.legacyKeyBindings);
            saves.Save(SaveSlot.Settings, Current);
            Log.Info(LogCat.Save, "settings saved");
        }

        // Everything back to §2.23, except that the first-launch warning stays acknowledged
        public void RestoreDefaults() {
            bool acknowledged = Current.warningAcknowledged;
            Current = new SettingsData { warningAcknowledged = acknowledged };
            LegacyKeyBindings.ResetDefaults();
            Apply();
        }

        // RenderSettings belong to the active scene, so every scene load needs the brightness
        // again (LightingPresetApplier takes this over in T-M2-05)
        public void ApplyScene() {
            float b = Current.brightness;
            RenderSettings.ambientLight = new Color(b, b, b, 1f);
        }

        // Mixers ignore SetFloat before their first update, so GameRoot calls this again in Start
        public void ApplyAudio() {
            if (mixer != null) {
                mixer.SetFloat(MasterVolumeParameter, LinearToDecibels(Current.masterVolume));
            }
        }

        public static float LinearToDecibels(float linear) {
            return 20f * Mathf.Log10(Mathf.Max(linear, 0.0001f));
        }

        // targetFpsIndex: 30, 60, 120, unlimited, VSync (the existing options list)
        public static void FrameRateFor(int index, out int targetFrameRate, out int vSyncCount) {
            vSyncCount = 0;
            switch (index) {
                case 0: targetFrameRate = 30; break;
                case 1: targetFrameRate = 60; break;
                case 2: targetFrameRate = 120; break;
                case 4: targetFrameRate = -1; vSyncCount = 1; break;
                default: targetFrameRate = -1; break;
            }
        }

        void ApplyDisplay() {
            // Resolution changes mean nothing without a window
            if (Application.isBatchMode || Application.isEditor) {
                return;
            }
            FullScreenMode mode = (FullScreenMode)(int)Current.fullscreenMode;
            if (Current.resolutionWidth > 0 && Current.resolutionHeight > 0) {
                RefreshRate rate = Screen.currentResolution.refreshRateRatio;
                if (Current.refreshRate > 0) {
                    rate = new RefreshRate { numerator = (uint)Current.refreshRate, denominator = 1 };
                }
                Screen.SetResolution(Current.resolutionWidth, Current.resolutionHeight, mode, rate);
            }else {
                Screen.fullScreenMode = mode;
            }
        }

        void ApplyQualityAndFrameRate() {
            int level = Mathf.Clamp(Current.qualityLevel, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != level) {
                QualitySettings.SetQualityLevel(level, false);
            }
            int frameRate;
            int vSync;
            FrameRateFor(Current.targetFpsIndex, out frameRate, out vSync);
            QualitySettings.vSyncCount = vSync;
            Application.targetFrameRate = frameRate;
        }

        static void Sanitize(SettingsData data) {
            data.mouseSensitivity = Mathf.Clamp(data.mouseSensitivity, SettingsData.MinMouseSensitivity, SettingsData.MaxMouseSensitivity);
            data.masterVolume = Mathf.Clamp01(data.masterVolume);
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.ambienceVolume = Mathf.Clamp01(data.ambienceVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
            data.voiceVolume = Mathf.Clamp01(data.voiceVolume);
            data.brightness = Mathf.Clamp(data.brightness, 0f, 1f);
            if (data.bindingOverridesJson == null) {
                data.bindingOverridesJson = "";
            }
            if (data.legacyKeyBindings == null) {
                data.legacyKeyBindings = new System.Collections.Generic.Dictionary<string, string>();
            }
        }
    }
}
