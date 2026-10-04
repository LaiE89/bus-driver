using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Assemblies;

namespace BusDriver.Tests.EditMode {
    // Enforces the §4.1 hard rules on runtime code (Assets/Scripts): rule 5 (no Find*, tag lookups,
    // SendMessage, no static mutable state), rule 8 (no UnityEngine.Random), rule 12 (no direct
    // Debug.Log), plus no legacy UnityEngine.Input reads (§4.10). There is no allowlist (T-M1-20):
    // the only exceptions are Log itself and the two statics §4.1.5 names.
    public class ArchitectureRulesTests {
        public static readonly Dictionary<string, Regex> Rules = new Dictionary<string, Regex> {
            { "GameObjectFind", new Regex(@"\bGameObject\.Find\w*\s*\(") },
            { "FindObjectByType", new Regex(@"\bFind(?:Any|First)?Objects?(?:Of|By)Type\b") },
            { "SendMessage", new Regex(@"\b(?:SendMessage|SendMessageUpwards|BroadcastMessage)\s*\(") },
            { "UnityRandom", new Regex(@"(?<![\w.])(?:UnityEngine\.)?Random\.(?:Range|value|InitState|insideUnit\w*|onUnit\w*|rotation\w*|ColorHSV|state)\b") },
            { "DebugLog", new Regex(@"(?<![\w.])(?:UnityEngine\.)?Debug\.Log\w*\s*\(|(?<![\w.])print\s*\(") },
            { "LegacyInput", new Regex(@"(?<![\w.])(?:UnityEngine\.)?Input\.(?:Get\w+|mouse\w*|any\w*|touch\w*|inputString|acceleration|compositionString|imeCompositionMode)\b|\bEvent\.current\b") },
        };

        // Runtime files allowed to break a rule by design, not as legacy debt
        static readonly string[] Exempt = { "Core/Util/Log.cs|DebugLog" };

        // The allowed statics of §4.1.5: GameRoot's bootstrap field and Log's filter
        static readonly string[] AllowedStatics = {
            "BusDriver.Core.Util.Log.enabledMask",
            "BusDriver.Gameplay.Flow.GameRoot.bootstrapped",
        };

        static string ScriptsRoot {
            get { return Path.Combine(Application.dataPath, "Scripts"); }
        }

        // Comments and string literals can't break a rule, so they are blanked before matching
        public static string StripCommentsAndStrings(string code) {
            code = Regex.Replace(code, @"/\*.*?\*/", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
            code = Regex.Replace(code, @"//[^\n]*", "");
            code = Regex.Replace(code, @"@?""(?:\\.|[^""\\\n])*""", "\"\"");
            return code;
        }

        // Returns "<file>|<rule>" for each rule the source breaks
        public static List<string> Scan(string relativePath, string source) {
            List<string> found = new List<string>();
            string code = StripCommentsAndStrings(source);
            foreach (KeyValuePair<string, Regex> rule in Rules) {
                string key = relativePath + "|" + rule.Key;
                if (Array.IndexOf(Exempt, key) < 0 && rule.Value.IsMatch(code)) {
                    found.Add(key);
                }
            }
            return found;
        }

        static List<string> ScanAllScripts() {
            List<string> found = new List<string>();
            foreach (string file in Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)) {
                string relative = file.Substring(ScriptsRoot.Length + 1).Replace('\\', '/');
                found.AddRange(Scan(relative, File.ReadAllText(file)));
            }
            return found;
        }

        [Test]
        public void NoBannedApisInRuntimeCode() {
            List<string> offenders = new List<string>();
            offenders.AddRange(ScanAllScripts());
            Assert.IsEmpty(offenders, "Banned APIs in runtime code (ROADMAP §4.1): " + string.Join(", ", offenders));
        }

        [Test]
        public void ScannerFlagsANewGameObjectFind() {
            string source = "class X { void Awake() { var go = GameObject.Find(\"Bus\"); } }";
            CollectionAssert.Contains(Scan("New/X.cs", source), "New/X.cs|GameObjectFind");
        }

        [Test]
        public void ScannerIgnoresCommentsAndStrings() {
            string source = "// GameObject.Find(\"x\") is banned\nclass X { string s = \"Debug.Log(\"; }";
            Assert.IsEmpty(Scan("New/X.cs", source));
        }

        [Test]
        public void ScannerFlagsEveryRule() {
            string source = "class X { void F() { FindAnyObjectByType<X>(); SendMessage(\"a\"); Random.Range(0, 1); "
                + "Debug.Log(1); Input.GetKeyDown(KeyCode.A); } }";
            List<string> found = Scan("New/X.cs", source);
            foreach (string rule in new[] { "FindObjectByType", "SendMessage", "UnityRandom", "DebugLog", "LegacyInput" }) {
                CollectionAssert.Contains(found, "New/X.cs|" + rule);
            }
        }

        static List<string> MutableStatics() {
            List<string> found = new List<string>();
            foreach (Assembly assembly in CurrentAssemblies.GetLoadedAssemblies()) {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("BusDriver.") || name.Contains(".Tests") || name == "BusDriver.Editor") {
                    continue;
                }
                foreach (Type type in assembly.GetTypes()) {
                    // Compiler-generated closure caches are not program state
                    if (type.Name.StartsWith("<") || type.IsDefined(typeof(CompilerGeneratedAttribute), false)) {
                        continue;
                    }
                    FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    foreach (FieldInfo field in fields) {
                        if (field.IsInitOnly || field.IsLiteral) {
                            continue;
                        }
                        found.Add(type.FullName + "." + field.Name);
                    }
                }
            }
            return found;
        }

        [Test]
        public void NoStaticMutableStateExceptBootstrapAndLogFilter() {
            List<string> offenders = new List<string>();
            foreach (string field in MutableStatics()) {
                if (Array.IndexOf(AllowedStatics, field) < 0) {
                    offenders.Add(field);
                }
            }
            Assert.IsEmpty(offenders, "Static mutable fields in runtime code (ROADMAP §4.1.5): " + string.Join(", ", offenders));
        }

        // The two allowed statics must still exist, or the exception list has gone stale
        [Test]
        public void AllowedStaticsStillExist() {
            List<string> found = MutableStatics();
            foreach (string field in AllowedStatics) {
                CollectionAssert.Contains(found, field, "an allowed static no longer exists; update AllowedStatics");
            }
        }
    }
}
