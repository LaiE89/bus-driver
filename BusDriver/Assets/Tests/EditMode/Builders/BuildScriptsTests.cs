using System;
using System.IO;
using BusDriver.Editor.Build;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // T-M1-18: the F1 overlay and the cheats exist only where BUSDRIVER_DEV (or the Editor) is
    // defined, so a release build must never get the define
    public class BuildScriptsTests {
        static readonly string[] Scenes = { "Assets/Generated/Scenes/Menu.unity" };

        [Test]
        public void ReleaseBuild_HasNoDevDefineOrDevelopmentFlag() {
            BuildPlayerOptions options = BuildScripts.OptionsFor(BuildTarget.StandaloneOSX, "out", Scenes, false);
            Assert.AreEqual(BuildOptions.None, options.options & BuildOptions.Development);
            Assert.IsFalse(Array.IndexOf(options.extraScriptingDefines ?? new string[0], BuildScripts.DevDefine) >= 0,
                "a release build must not define " + BuildScripts.DevDefine);
        }

        [Test]
        public void DevelopmentBuild_DefinesBusDriverDev() {
            BuildPlayerOptions options = BuildScripts.OptionsFor(BuildTarget.StandaloneOSX, "out", Scenes, true);
            Assert.AreEqual(BuildOptions.Development, options.options & BuildOptions.Development);
            CollectionAssert.Contains(options.extraScriptingDefines, BuildScripts.DevDefine);
        }

        // The gate itself: DevBuild.Enabled is compiled from exactly these symbols, and the overlay
        // removes itself when it's false
        [Test]
        public void DevBuildGate_UsesTheDevDefine() {
            string registry = Source("DebugRegistry.cs");
            StringAssert.Contains("#if UNITY_EDITOR || " + BuildScripts.DevDefine, registry);
            string overlay = Source("DebugOverlay.cs");
            StringAssert.Contains("if (!DevBuild.IsEnabled())", overlay);
        }

        // T-M4-10: the monster and death cheats don't compile into a release build at all
        [Test]
        public void DevCheats_OnlyCompileWithTheDevDefine() {
            string cheats = Source("DevCheats.cs").Trim();
            StringAssert.StartsWith("#if UNITY_EDITOR || " + BuildScripts.DevDefine, cheats);
            StringAssert.EndsWith("#endif", cheats);
            string context = Source("ShiftContext.cs");
            int call = context.IndexOf("DevCheats.Register", StringComparison.Ordinal);
            Assert.Greater(call, 0, "ShiftContext registers the cheats");
            int gate = context.LastIndexOf("#if UNITY_EDITOR || " + BuildScripts.DevDefine, call, StringComparison.Ordinal);
            int end = context.LastIndexOf("#endif", call, StringComparison.Ordinal);
            Assert.Greater(gate, end, "the call sits inside the dev gate");
        }

        static string Source(string fileName) {
            string[] found = Directory.GetFiles("Assets/Scripts", fileName, SearchOption.AllDirectories);
            Assert.AreEqual(1, found.Length, "expected exactly one " + fileName);
            return File.ReadAllText(found[0]);
        }

        [Test]
        public void NightSystems_HasTheOverlay() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIPrefabBuilder.DebugOverlayPath);
            Assert.IsNotNull(prefab, "run Build All: " + UIPrefabBuilder.DebugOverlayPath + " is missing");
            Assert.IsNotNull(prefab.GetComponent<BusDriver.UI.Debug.DebugOverlay>());
        }
    }
}
