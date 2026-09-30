using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Save {
    // settings.json data, v1 (§4.9). Field initialisers are the §2.23 defaults.
    public sealed class SettingsData {
        public const float DefaultVolume = 0.8f;
        public const float DefaultMouseSensitivity = 60f;
        public const float MinMouseSensitivity = 10f;
        public const float MaxMouseSensitivity = 200f;
        public const int QualityLow = 0;
        public const int QualityMedium = 1;
        public const int QualityHigh = 2;
        // 30, 60, 120, unlimited, VSync (the existing options menu's list)
        public const int TargetFpsUnlimited = 3;
        public const float DefaultBrightness = 0.1f;

        // Audio, linear 0–1
        public float masterVolume = DefaultVolume;
        public float musicVolume = DefaultVolume;
        public float ambienceVolume = DefaultVolume;
        public float sfxVolume = DefaultVolume;
        public float voiceVolume = DefaultVolume;

        // Controls
        public float mouseSensitivity = DefaultMouseSensitivity;
        public bool invertY;
        // InputActionAsset.SaveBindingOverridesAsJson (T-M1-06); empty = the asset's defaults
        public string bindingOverridesJson = "";

        // Graphics. A zero resolution or refresh rate means the display's current one.
        public int qualityLevel = QualityMedium;
        public int resolutionWidth;
        public int resolutionHeight;
        public int refreshRate;
        public WindowMode fullscreenMode = WindowMode.FullScreenWindow;
        public int targetFpsIndex = TargetFpsUnlimited;
        public float brightness = DefaultBrightness;

        // Gameplay
        public ScareIntensity scareIntensity = ScareIntensity.Full;
        public bool hintsEnabled = true;
        public bool captionsEnabled = true;

        // First launch (D37)
        public bool warningAcknowledged;

        // TEMPORARY (D56): the MVP's KeyCode rebinds by action ("cycleCamera" → "Space") until
        // bindingOverridesJson takes over in T-M1-07, which deletes this field. Old files that
        // still carry it load fine, because unknown members are ignored.
        public Dictionary<string, string> legacyKeyBindings = new Dictionary<string, string>();
    }
}
