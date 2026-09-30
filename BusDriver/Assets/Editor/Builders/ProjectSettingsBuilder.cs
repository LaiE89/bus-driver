using System.Collections.Generic;
using System.IO;
using BusDriver.Core.Util;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // BuildAll step 1 (§4.15): layers and the collision matrix (§4.16), the Containment tag,
    // Enter Play Mode Options off (D24), the Input System as the only input handler, the player
    // settings and the build scene list. Quality levels are T-M9-02's (§4.17).
    public static class ProjectSettingsBuilder {
        public const string CompanyName = "Bus Driver Team";
        public const string ProductName = "Bus Driver";
        // 0.<milestone>.<patch> until G3 (§4.20, D52); bump it when a milestone is closed
        public const string Version = "0.0.1";

        // The MVP scenes, listed until the generated ones exist (T-M1-16)
        public static readonly string[] LegacyBuildList = {
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/" + SceneIds.LegacyNight + ".unity",
        };

        const string TagManagerPath = "ProjectSettings/TagManager.asset";
        const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";
        const int InputSystemOnly = 1;

        [MenuItem("Tools/Bus Driver/Builders/Project Settings")]
        public static void Build() {
            WriteLayersAndTags();
            WriteCollisionMatrix();
            // Unity 6 reads the options enum, so the old "m_EnterPlayModeOptionsEnabled: 0" with
            // options 3 still meant no domain reload. Turning it off stores options None (reload
            // the domain and the scene) in Unity's own form (D62); only written when it's on.
            if (EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.None) {
                EditorSettings.enterPlayModeOptionsEnabled = false;
            }
            WritePlayerSettings();
            EditorBuildSettings.scenes = BuildScenes();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ collision

        // §4.16: Bus ↔ World; Player ↔ World, BusInterior; Zone ↔ Bus only; everything else in
        // 8–19 collides with nothing. Unassigned objects still sit on the built-in layers 0–7
        // (Default mostly), which keep colliding with the solid layers (D62).
        public static bool ShouldCollide(int a, int b) {
            bool aOurs = a >= Layers.First && a <= Layers.Last;
            bool bOurs = b >= Layers.First && b <= Layers.Last;
            if (!aOurs && !bOurs) {
                return true;
            }
            if (aOurs && bOurs) {
                return IsPair(a, b, Layers.Bus, Layers.World)
                    || IsPair(a, b, Layers.Player, Layers.World)
                    || IsPair(a, b, Layers.Player, Layers.BusInterior)
                    || IsPair(a, b, Layers.Zone, Layers.Bus);
            }
            int ours = aOurs ? a : b;
            int other = aOurs ? b : a;
            return other < Layers.First && IsSolid(ours);
        }

        static bool IsSolid(int layer) {
            return layer == Layers.Bus || layer == Layers.BusInterior || layer == Layers.World || layer == Layers.Player;
        }

        static bool IsPair(int a, int b, int x, int y) {
            return (a == x && b == y) || (a == y && b == x);
        }

        static void WriteCollisionMatrix() {
            SerializedObject dynamics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(DynamicsManagerPath)[0]);
            SerializedProperty matrix = dynamics.FindProperty("m_LayerCollisionMatrix");
            for (int a = 0; a < 32; a++) {
                uint row = matrix.GetArrayElementAtIndex(a).uintValue;
                for (int b = 0; b < 32; b++) {
                    // Only pairs involving our layers are ours to decide
                    bool ours = (a >= Layers.First && a <= Layers.Last) || (b >= Layers.First && b <= Layers.Last);
                    if (!ours) {
                        continue;
                    }
                    if (ShouldCollide(a, b)) {
                        row |= 1u << b;
                    }else {
                        row &= ~(1u << b);
                    }
                }
                matrix.GetArrayElementAtIndex(a).uintValue = row;
            }
            // The serialized object is the live physics settings, so this session sees it too
            dynamics.ApplyModifiedPropertiesWithoutUndo();
        }

        // --------------------------------------------------------- layers, tags

        static void WriteLayersAndTags() {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = Layers.First; i <= Layers.Last; i++) {
                layers.GetArrayElementAtIndex(i).stringValue = Layers.NameOf(i);
            }
            // A name of ours left over in another slot (the old PlayerHead at 8, say) goes
            for (int i = 8; i < layers.arraySize; i++) {
                if (i >= Layers.First && i <= Layers.Last) {
                    continue;
                }
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == "PlayerHead" || IsOurLayerName(layer.stringValue)) {
                    layer.stringValue = "";
                }
            }
            SerializedProperty tags = tagManager.FindProperty("tags");
            bool hasTag = false;
            for (int i = 0; i < tags.arraySize; i++) {
                hasTag |= tags.GetArrayElementAtIndex(i).stringValue == Tags.Containment;
            }
            if (!hasTag) {
                tags.arraySize++;
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = Tags.Containment;
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool IsOurLayerName(string name) {
            for (int i = Layers.First; i <= Layers.Last; i++) {
                if (Layers.NameOf(i) == name) {
                    return true;
                }
            }
            return false;
        }

        // --------------------------------------------------------------- player

        static void WritePlayerSettings() {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            Object[] playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerSettings.Length > 0) {
                SerializedObject so = new SerializedObject(playerSettings[0]);
                SerializedProperty handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != InputSystemOnly) {
                    handler.intValue = InputSystemOnly;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // SceneIds.BuildList once every generated scene exists, the MVP scenes until then
        public static EditorBuildSettingsScene[] BuildScenes() {
            string[] paths = LegacyBuildList;
            bool generated = true;
            foreach (string path in SceneIds.BuildList) {
                generated &= File.Exists(path);
            }
            if (generated) {
                paths = new List<string>(SceneIds.BuildList).ToArray();
            }
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++) {
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }
            return scenes;
        }
    }
}
