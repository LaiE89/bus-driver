using UnityEngine;

namespace BusDriver.Core.Util {
    // "<bundleVersion> (<git short hash>)", written into Resources/build_label.txt by
    // BuildScripts just before a player build (§4.20). Folded into GameRootConfig in T-M1-04.
    public static class BuildLabel {
        public const string ResourceName = "build_label";

        public static string Current {
            get {
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) {
                    return asset.text.Trim();
                }
                // Editor sessions and builds made outside BuildScripts have no label file
                return Application.version + " (dev)";
            }
        }
    }
}
