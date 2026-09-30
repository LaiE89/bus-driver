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

        [Tooltip("Every route (Data/Routes). Route 1 is the only one (§1.5)")]
        public RouteDefinition[] routes = new RouteDefinition[0];

        [Tooltip("Environment kind → view (Data/Views/Environment)")]
        public EnvironmentViewSet environment;

        [Tooltip("Nights 1–5 (Data/Nights), index 0 = night 1")]
        public NightDefinition[] nights = new NightDefinition[0];

        [Tooltip("Every passenger look (Data/Looks), in id order")]
        public PassengerLookDefinition[] looks = new PassengerLookDefinition[0];

        public RouteDefinition Route(string routeId) {
            for (int i = 0; i < routes.Length; i++) {
                if (routes[i] != null && routes[i].id == routeId) {
                    return routes[i];
                }
            }
            return null;
        }

        // Night n (1-based), or null
        public NightDefinition Night(int nightIndex) {
            for (int i = 0; i < nights.Length; i++) {
                if (nights[i] != null && nights[i].nightIndex == nightIndex) {
                    return nights[i];
                }
            }
            return null;
        }

        public PassengerLookDefinition Look(string lookId) {
            for (int i = 0; i < looks.Length; i++) {
                if (looks[i] != null && looks[i].id == lookId) {
                    return looks[i];
                }
            }
            return null;
        }
    }
}
