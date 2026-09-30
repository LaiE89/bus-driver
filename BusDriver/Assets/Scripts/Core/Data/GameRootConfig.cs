using UnityEngine;

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
    }
}
