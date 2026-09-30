using System;
using System.Collections.Generic;

namespace BusDriver.Core.Util {
    // Scene names and the build list (§4.3). Scenes are loaded by name; the build list is what
    // ProjectSettingsBuilder and BuildScripts put in the player (T-M1-13).
    public static class SceneIds {
        public const string Menu = "Menu";
        public const string NightSystems = "Night_Systems";
        public const string Route01World = "Route01_World";
        public const string Route01Dressing = "Route01_Dressing";

        public const string GeneratedFolder = "Assets/Generated/Scenes";
        public const string DressingPath = "Assets/Scenes/" + Route01Dressing + ".unity";

        public static readonly IReadOnlyList<string> BuildList = Array.AsReadOnly(new[] {
            GeneratedFolder + "/" + Menu + ".unity",
            GeneratedFolder + "/" + NightSystems + ".unity",
            GeneratedFolder + "/" + Route01World + ".unity",
        });
    }
}
