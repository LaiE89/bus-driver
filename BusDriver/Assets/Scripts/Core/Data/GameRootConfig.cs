using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Core.Data {
    // The one Resources asset (§4.2, §4.8): everything GameRoot needs to build the services.
    // Created in T-M1-04 with the build label only; DataSeeder adopts it in T-M1-13 and later
    // tickets add the mixer, sound library, theme, balance, routes, nights and so on.
    public sealed class GameRootConfig : ScriptableObject {
        public const string ResourceName = "GameRootConfig";
        public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";

        [Tooltip("\"<bundleVersion> (<git short hash>)\", written by BuildScripts just before a player build. "
            + "Empty in the editor, where the label falls back to \"<version> (dev)\".")]
        public string buildLabel = "";

        [Tooltip("Assets/Audio/MainMixer.mixer")]
        public AudioMixer mixer;

        [Tooltip("Assets/Input/BusDriver.inputactions, also the project-wide actions. Typed loosely because "
            + "Core doesn't reference the Input System (§4.2); InputService casts it (D57).")]
        public ScriptableObject inputActions;

        [Tooltip("Every SoundDefinition (Data/Audio/SoundLibrary)")]
        public SoundLibrary soundLibrary;
        [Tooltip("Group, snapshot and parameter routing into the mixer (Data/Audio/AudioConfig)")]
        public AudioConfig audioConfig;

        [Tooltip("Data/UI/Theme.asset (a BusDriver.UI.Theme.UITheme; typed loosely because Core can't see UI, D61)")]
        public ScriptableObject uiTheme;
    }
}
