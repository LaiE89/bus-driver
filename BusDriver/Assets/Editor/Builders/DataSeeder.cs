using System;
using System.Collections.Generic;
using System.IO;
using BusDriver.Core.Data;
using BusDriver.UI.Theme;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BusDriver.Editor.Builders {
    // One design-data asset the seeder can create: where it lives under the data root, and how to
    // fill a fresh instance with the ROADMAP's first-pass numbers.
    public sealed class Seed {
        public readonly string RelativePath;
        public readonly Type Type;
        readonly Action<ScriptableObject> fill;

        public Seed(string relativePath, Type type, Action<ScriptableObject> fill) {
            RelativePath = relativePath;
            Type = type;
            this.fill = fill;
        }

        public static Seed Of<T>(string relativePath, Action<T> fill) where T : ScriptableObject {
            return new Seed(relativePath, typeof(T), asset => fill((T)asset));
        }

        public void Fill(ScriptableObject asset) {
            fill(asset);
        }
    }

    // BuildAll step 4 (§4.15): creates every MISSING asset in Data/ from the seed data. It never
    // overwrites (§0.1: once seeded, the asset is the source of truth). Adoption afterwards only
    // fills empty references and appends missing list entries: it never replaces or removes one.
    // "Reseed Data (overwrite)" is separate, asks first, and is never run by the agent unless a
    // ticket says so (§0.3).
    public static class DataSeeder {
        public const string DataRoot = "Assets/Data";
        public const string MixerPath = "Assets/Audio/MainMixer.mixer";
        public const string InputActionsPath = "Assets/Input/BusDriver.inputactions";

        // Every seed, by path under the data root. Later tickets append theirs here.
        public static IEnumerable<Seed> All() {
            yield return Seed.Of<UITheme>(UIThemeSeed.RelativePath, UIThemeSeed.Fill);
            foreach (Seed sound in AudioSeed.SoundSeeds()) {
                yield return sound;
            }
            yield return Seed.Of<SoundLibrary>(AudioSeed.LibraryRelativePath, AudioSeed.FillLibrary);
            yield return Seed.Of<AudioConfig>(AudioSeed.ConfigRelativePath, AudioSeed.FillConfig);
            yield return Seed.Of<RouteDefinition>(RouteSeed.RelativePath, RouteSeed.Fill);
            yield return Seed.Of<VolumeProfile>(LightingSeed.PostRelativePath, LightingSeed.FillPost);
            yield return Seed.Of<NightLightingPreset>(LightingSeed.PresetRelativePath, LightingSeed.FillPreset);
            yield return Seed.Of<EnvironmentViewSet>(EnvironmentSeed.RelativePath, EnvironmentSeed.Fill);
        }

        [MenuItem("Tools/Bus Driver/Builders/Data Seeder (create missing)")]
        public static void SeedMissingMenu() {
            SeedMissing();
        }

        // Returns the paths it created
        public static List<string> SeedMissing(string root = DataRoot, bool adopt = true) {
            List<string> created = new List<string>();
            foreach (Seed seed in All()) {
                string path = root + "/" + seed.RelativePath;
                if (AssetDatabase.LoadAssetAtPath(path, seed.Type) != null) {
                    continue;
                }
                if (File.Exists(path)) {
                    // Something else sits at the path; overwriting it is the one thing we never do
                    Debug.LogError($"DataSeeder: {path} exists but isn't a {seed.Type.Name}; leaving it alone");
                    continue;
                }
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
                ScriptableObject asset = ScriptableObject.CreateInstance(seed.Type);
                seed.Fill(asset);
                AssetDatabase.CreateAsset(asset, path);
                created.Add(path);
                Debug.Log("[SEED] created " + path);
            }
            if (adopt) {
                Adopt(root);
            }
            AssetDatabase.SaveAssets();
            return created;
        }

        [MenuItem("Tools/Bus Driver/Reseed Data (overwrite)")]
        static void ReseedMenu() {
            bool confirmed = EditorUtility.DisplayDialog("Reseed Data (overwrite)",
                "This overwrites every seeded asset under " + DataRoot + " with the ROADMAP's first-pass values. "
                + "Every tuning edit made since seeding is lost. Continue?", "Overwrite", "Cancel");
            if (confirmed) {
                Reseed(DataRoot);
            }
        }

        // Refills existing assets in place, so their GUIDs and every reference survive
        internal static void Reseed(string root) {
            foreach (Seed seed in All()) {
                string path = root + "/" + seed.RelativePath;
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath(path, seed.Type) as ScriptableObject;
                if (asset == null) {
                    continue;
                }
                seed.Fill(asset);
                EditorUtility.SetDirty(asset);
                Debug.Log("[SEED] reseeded " + path);
            }
            SeedMissing(root);
        }

        // --------------------------------------------------------------- adoption

        static void Adopt(string root) {
            GameRootConfig config = EnsureGameRootConfig();
            AudioSeed.Adopt(root, config);
            if (config.mixer == null) {
                config.mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            if (config.inputActions == null) {
                config.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            }
            if (config.soundLibrary == null) {
                config.soundLibrary = AssetDatabase.LoadAssetAtPath<SoundLibrary>(root + "/" + AudioSeed.LibraryRelativePath);
            }
            if (config.audioConfig == null) {
                config.audioConfig = AssetDatabase.LoadAssetAtPath<AudioConfig>(root + "/" + AudioSeed.ConfigRelativePath);
            }
            if (config.uiTheme == null) {
                config.uiTheme = AssetDatabase.LoadAssetAtPath<UITheme>(root + "/" + UIThemeSeed.RelativePath);
            }
            AdoptRoute(config, AssetDatabase.LoadAssetAtPath<RouteDefinition>(root + "/" + RouteSeed.RelativePath));
            LightingSeed.Adopt(root);
            EnvironmentSeed.Adopt(root, config);
            EditorUtility.SetDirty(config);
        }

        // Appends the route if the config doesn't list it yet; never removes or reorders one
        static void AdoptRoute(GameRootConfig config, RouteDefinition route) {
            if (route == null) {
                return;
            }
            if (config.routes == null) {
                config.routes = new RouteDefinition[0];
            }
            if (Array.IndexOf(config.routes, route) >= 0) {
                return;
            }
            RouteDefinition[] routes = new RouteDefinition[config.routes.Length + 1];
            Array.Copy(config.routes, routes, config.routes.Length);
            routes[routes.Length - 1] = route;
            config.routes = routes;
        }

        // Created in T-M1-04; this only rebuilds it if someone deleted it
        public static GameRootConfig EnsureGameRootConfig() {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            if (config == null) {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(GameRootConfig.AssetPath));
                config = ScriptableObject.CreateInstance<GameRootConfig>();
                AssetDatabase.CreateAsset(config, GameRootConfig.AssetPath);
                Debug.Log("[SEED] created " + GameRootConfig.AssetPath);
            }
            return config;
        }
    }
}
