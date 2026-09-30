using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode {
    // T-M1-21 (§4.2): the dependency direction is Core ← Gameplay ← UI ← Editor. Gameplay never
    // references UI; Core references none of our assemblies. Checked both in the .asmdef files and in
    // the compiled assemblies, so a reference sneaking in through either shows up.
    public class AsmdefRulesTests {
        static readonly string[] Ours = { "BusDriver.Core", "BusDriver.Gameplay", "BusDriver.UI", "BusDriver.Editor" };

        // name → the BusDriver assemblies its .asmdef references
        static Dictionary<string, List<string>> Asmdefs() {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            foreach (string file in Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories)) {
                AsmdefJson json = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(file));
                if (json.name == null || !json.name.StartsWith("BusDriver.")) {
                    continue;
                }
                Assert.IsFalse(result.ContainsKey(json.name), "two asmdefs are named " + json.name);
                result[json.name] = (json.references ?? new string[0]).Where(r => r.StartsWith("BusDriver.")).ToList();
            }
            return result;
        }

        [Serializable]
        class AsmdefJson {
            public string name;
            public string[] references;
        }

        [Test]
        public void TheRuntimeAssembliesAreCoreGameplayAndUI() {
            Dictionary<string, List<string>> asmdefs = Asmdefs();
            foreach (string name in Ours) {
                Assert.IsTrue(asmdefs.ContainsKey(name), name + ".asmdef is missing");
            }
            Assert.IsFalse(asmdefs.ContainsKey("BusDriver.Runtime"), "the transitional BusDriver.Runtime assembly is gone (T-M1-21)");
        }

        [Test]
        public void CoreReferencesNoneOfOurAssemblies() {
            CollectionAssert.IsEmpty(Asmdefs()["BusDriver.Core"]);
            AssertCompiledReferences("BusDriver.Core", new string[0]);
        }

        [Test]
        public void GameplayReferencesOnlyCore() {
            CollectionAssert.AreEquivalent(new[] { "BusDriver.Core" }, Asmdefs()["BusDriver.Gameplay"]);
            AssertCompiledReferences("BusDriver.Gameplay", new[] { "BusDriver.Core" });
        }

        [Test]
        public void UIReferencesCoreAndGameplayOnly() {
            CollectionAssert.AreEquivalent(new[] { "BusDriver.Core", "BusDriver.Gameplay" }, Asmdefs()["BusDriver.UI"]);
            AssertCompiledReferences("BusDriver.UI", new[] { "BusDriver.Core", "BusDriver.Gameplay" });
        }

        // The compiler drops unused references, so the compiled list may be shorter, never longer
        static void AssertCompiledReferences(string assemblyName, string[] allowed) {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            Assert.IsNotNull(assembly, assemblyName + " is not loaded");
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies()) {
                if (reference.Name.StartsWith("BusDriver.")) {
                    CollectionAssert.Contains(allowed, reference.Name, assemblyName + " must not reference " + reference.Name);
                }
            }
        }

        // Every runtime script sits in the folder of its namespace (Scripts/<Assembly>/<Area>/)
        [Test]
        public void EveryScriptLivesInItsNamespaceFolder() {
            string scripts = Path.Combine(Application.dataPath, "Scripts");
            List<string> misplaced = new List<string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)) {
                string text = File.ReadAllText(file);
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(text, @"^namespace\s+([\w.]+)", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (!match.Success) {
                    continue;
                }
                string[] parts = match.Groups[1].Value.Split('.');
                string expected = Path.Combine(scripts, parts[1], parts.Length > 2 ? parts[2] : "");
                string actual = Path.GetDirectoryName(file);
                if (!actual.StartsWith(expected)) {
                    misplaced.Add(file.Substring(scripts.Length + 1) + " (namespace " + match.Groups[1].Value + ")");
                }
            }
            Assert.IsEmpty(misplaced, "scripts outside their namespace folder: " + string.Join(", ", misplaced));
        }
    }
}
