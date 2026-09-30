using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Views/Environment (§4.8, T-M2-06): an entry for every EnvironmentKinds key.
    // The greybox views are generated after the seeder runs, so EnvironmentPrefabBuilder fills
    // (and keeps refreshing) every greyboxView; artView is left to the artists (D76).
    public static class EnvironmentSeed {
        public const string RelativePath = "Views/Environment.asset";
        public const string AssetPath = DataSeeder.DataRoot + "/" + RelativePath;

        public static void Fill(EnvironmentViewSet set) {
            set.entries.Clear();
            foreach (string kind in EnvironmentKinds.All()) {
                set.entries.Add(new EnvironmentViewEntry {
                    kind = kind,
                    greyboxView = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabBuilder.ViewPath(kind)),
                });
            }
        }

        // Adoption only appends missing kinds and fills an empty config reference
        public static void Adopt(string root, GameRootConfig config) {
            EnvironmentViewSet set = AssetDatabase.LoadAssetAtPath<EnvironmentViewSet>(root + "/" + RelativePath);
            if (set == null) {
                return;
            }
            bool changed = false;
            foreach (string kind in EnvironmentKinds.All()) {
                if (set.Find(kind) == null) {
                    set.entries.Add(new EnvironmentViewEntry { kind = kind });
                    changed = true;
                }
            }
            if (changed) {
                EditorUtility.SetDirty(set);
            }
            if (config.environment == null) {
                config.environment = set;
            }
        }
    }
}
