using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Assemblies;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using Debug = UnityEngine.Debug;

namespace BusDriver.Editor.Build {
    // Standalone builds (§4.20). Output goes to <repo>/Builds/<platform>/<version>/, and the label
    // "<bundleVersion> (<git short hash>)" is written into GameRootConfig.buildLabel for the build.
    // Batch mode:  -executeMethod BusDriver.Editor.Build.BuildScripts.BuildCurrent [-dev]
    public static class BuildScripts {
        public const string DevDefine = "BUSDRIVER_DEV";
        public const string ProductFileName = "BusDriver";

        public static string RepoRoot {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")); }
        }

        public static string OutputDirectory(string platform) {
            return Path.Combine(RepoRoot, "Builds", platform, PlayerSettings.bundleVersion);
        }

        public static string MacAppPath {
            get { return Path.Combine(OutputDirectory("mac"), ProductFileName + ".app"); }
        }

        public static string WindowsExePath {
            get { return Path.Combine(OutputDirectory("windows"), ProductFileName + ".exe"); }
        }

        [MenuItem("Tools/Bus Driver/Build/macOS (development)")]
        static void MenuMacDev() {
            BuildMac(true);
        }

        [MenuItem("Tools/Bus Driver/Build/macOS (release)")]
        static void MenuMacRelease() {
            BuildMac(false);
        }

        [MenuItem("Tools/Bus Driver/Build/Windows (development)")]
        static void MenuWindowsDev() {
            BuildWindows(true);
        }

        [MenuItem("Tools/Bus Driver/Build/Windows (release)")]
        static void MenuWindowsRelease() {
            BuildWindows(false);
        }

        public static bool BuildWindows(bool dev) {
            return Build(BuildTarget.StandaloneWindows64, WindowsExePath, dev);
        }

        public static bool BuildMac(bool dev) {
            return Build(BuildTarget.StandaloneOSX, MacAppPath, dev);
        }

        // For -executeMethod: builds for the editor's own OS and exits with 0/1 in batch mode
        public static void BuildCurrent() {
            bool dev = Array.IndexOf(Environment.GetCommandLineArgs(), "-dev") >= 0;
            bool ok = Application.platform == RuntimePlatform.OSXEditor ? BuildMac(dev) : BuildWindows(dev);
            ExitIfBatch(ok);
        }

        public static void BuildWindowsBatch() {
            ExitIfBatch(BuildWindows(Array.IndexOf(Environment.GetCommandLineArgs(), "-dev") >= 0));
        }

        public static void BuildMacBatch() {
            ExitIfBatch(BuildMac(Array.IndexOf(Environment.GetCommandLineArgs(), "-dev") >= 0));
        }

        static void ExitIfBatch(bool ok) {
            if (Application.isBatchMode) {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        // Development builds get the Development flag and BUSDRIVER_DEV, which keeps the F1 overlay and
        // the cheats (§4.18); release builds get neither (BuildScriptsTests)
        public static BuildPlayerOptions OptionsFor(BuildTarget target, string outputPath, string[] scenes, bool dev) {
            return new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = dev ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = dev ? new[] { DevDefine } : new string[0],
            };
        }

        static bool Build(BuildTarget target, string outputPath, bool dev) {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target)) {
                // Windows from a Mac needs the Unity Hub module installed by a person (T-M0-08)
                Debug.LogError($"[BUILD] FAIL {target}: the build support module is not installed. "
                    + "Add it in Unity Hub (Windows/Mac Build Support (Mono), T-M0-08).");
                return false;
            }
            // The generated scenes, in SceneIds order (§4.20)
            List<string> scenes = new List<string>(SceneIds.BuildList);
            foreach (string scene in scenes) {
                if (!File.Exists(scene)) {
                    Debug.LogError($"[BUILD] FAIL: {scene} is missing; run Build All first");
                    return false;
                }
            }
            string label = $"{PlayerSettings.bundleVersion} ({GitShortHash()})";
            string previousLabel = SetConfigLabel(label);

            NamedBuildTarget named = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(named, ManagedStrippingLevel.Low);
            if (target == BuildTarget.StandaloneOSX) {
                SetMacArchitecture(MacArchitecture());
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildPlayerOptions options = OptionsFor(target, outputPath, scenes.ToArray(), dev);
            BuildReport report;
            try {
                report = BuildPipeline.BuildPlayer(options);
            }finally {
                SetConfigLabel(previousLabel);
            }
            BuildSummary summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            string line = $"{target} {(dev ? "development" : "release")} '{label}' -> {outputPath}: {summary.result}, "
                + $"{summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0} s";
            if (ok) {
                Debug.Log("[BUILD] OK " + line);
            }else {
                Debug.LogError("[BUILD] FAIL " + line);
            }
            return ok;
        }

        // UserBuildSettings lives in the macOS build support module, so an editor without that module
        // installed has no assembly to compile against (CS0234). Reflection keeps this file building
        // on a Windows-only install; the Build target check above already stops the build itself.
        static void SetMacArchitecture(OSArchitecture architecture) {
            Type settings = FindEditorType("UnityEditor.OSXStandalone.UserBuildSettings");
            PropertyInfo property = settings == null
                ? null
                : settings.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            if (property == null) {
                Debug.LogWarning("[BUILD] macOS build support is not installed, so the architecture is "
                    + "left at its default. Add Mac Build Support (Mono) in Unity Hub (T-M0-08).");
                return;
            }
            property.SetValue(null, Enum.ToObject(property.PropertyType, (int)architecture));
        }

        static Type FindEditorType(string fullName) {
            foreach (Assembly assembly in CurrentAssemblies.GetLoadedAssemblies()) {
                Type type = assembly.GetType(fullName, false);
                if (type != null) {
                    return type;
                }
            }
            return null;
        }

        // Universal (Intel + Apple silicon) per §4.20. Burst merges its two slices with the editor's
        // llvm-lipo, which 6000.6.0f1 installs without the execute bit; until a person runs
        // chmod +x on it, fall back to Apple silicon only (D52).
        static OSArchitecture MacArchitecture() {
            string lipo = Path.Combine(EditorApplication.applicationContentsPath, "Resources/Burst/Client/bcl/hostmac/llvm-lipo");
            if (!File.Exists(lipo) || IsExecutable(lipo)) {
                return OSArchitecture.x64ARM64;
            }
            Debug.LogWarning("[BUILD] llvm-lipo is not executable, so the macOS build is Apple silicon only (D52). "
                + "For a Universal build run: chmod +x \"" + lipo + "\"");
            return OSArchitecture.ARM64;
        }

        static bool IsExecutable(string path) {
            try {
                using (Process test = Process.Start(new ProcessStartInfo("/bin/test", "-x \"" + path + "\"") {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })) {
                    test.WaitForExit(5000);
                    return test.ExitCode == 0;
                }
            }catch (Exception) {
                return true;
            }
        }

        // The label only has to be in the player; the asset goes back to its committed value
        // afterwards, so a build never leaves the working tree dirty. Returns the previous value.
        static string SetConfigLabel(string label) {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            if (config == null) {
                Debug.LogError("[BUILD] " + GameRootConfig.AssetPath + " is missing; the build shows the dev label");
                return "";
            }
            string previous = config.buildLabel;
            config.buildLabel = label;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            return previous;
        }

        static string GitShortHash() {
            try {
                ProcessStartInfo info = new ProcessStartInfo("git", "rev-parse --short HEAD") {
                    WorkingDirectory = RepoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (Process git = Process.Start(info)) {
                    string hash = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit(5000);
                    return git.ExitCode == 0 && hash.Length > 0 ? hash : "nogit";
                }
            }catch (Exception e) {
                Debug.LogWarning("[BUILD] git hash unavailable: " + e.Message);
                return "nogit";
            }
        }
    }
}
