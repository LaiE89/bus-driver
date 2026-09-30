using System;
using BusDriver.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // The one entry point for generated content (§4.15), in batch mode too:
    //   Unity -batchmode -projectPath BusDriver -executeMethod BusDriver.Editor.Builders.BuildAll.Run -quit
    // Runs the 11 steps in order and stops at the first failure. A step whose builder doesn't
    // exist yet logs "skipped" with the ticket that adds it. Any failure exits with code 1 in
    // batch mode.
    public static class BuildAll {
        struct Step {
            public string Name;
            public Action Run;
            // Set while the step's builder doesn't exist yet
            public string SkippedUntil;
        }

        static Step[] Steps {
            get {
                return new[] {
                    new Step { Name = "ProjectSettingsBuilder", Run = ProjectSettingsBuilder.Build },
                    new Step { Name = "MaterialLibraryBuilder", Run = MaterialLibraryBuilder.Build },
                    new Step { Name = "MeshBuilder", SkippedUntil = "T-M2-03 / T-M2-06" },
                    new Step { Name = "DataSeeder", Run = () => DataSeeder.SeedMissing() },
                    new Step { Name = "PlaceholderAudioBuilder", Run = PlaceholderAudioBuilder.Build },
                    new Step { Name = "IconBuilder", SkippedUntil = "M7 (item and journal icons)" },
                    new Step { Name = "PrefabBuilder", SkippedUntil = "T-M1-14" },
                    new Step { Name = "RouteBuilder", SkippedUntil = "T-M1-16" },
                    new Step { Name = "NightSystemsBuilder", SkippedUntil = "T-M1-16" },
                    new Step { Name = "MenuBuilder", SkippedUntil = "T-M1-16" },
                    new Step { Name = "ContentValidator", Run = Validate },
                };
            }
        }

        [MenuItem("Tools/Bus Driver/Build All")]
        public static void Run() {
            bool ok = RunSteps();
            if (Application.isBatchMode && !ok) {
                EditorApplication.Exit(1);
            }
        }

        public static bool RunSteps() {
            Step[] steps = Steps;
            for (int i = 0; i < steps.Length; i++) {
                string label = $"[BUILD-ALL] {i + 1}/{steps.Length} {steps[i].Name}";
                if (steps[i].Run == null) {
                    Debug.Log($"{label}: skipped (arrives in {steps[i].SkippedUntil})");
                    continue;
                }
                try {
                    steps[i].Run();
                    Debug.Log(label + ": ok");
                }catch (Exception e) {
                    Debug.LogException(e);
                    Debug.LogError(label + ": FAILED: " + e.Message);
                    return false;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BUILD-ALL] done");
            return true;
        }

        static void Validate() {
            if (!ContentValidator.ValidateAndLog()) {
                throw new InvalidOperationException("content validation failed; see the [VALIDATE] lines");
            }
        }
    }
}
