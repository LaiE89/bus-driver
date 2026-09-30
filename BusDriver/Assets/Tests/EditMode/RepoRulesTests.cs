using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode {
    // The same two rules as tools/hooks/pre-commit (D41), so a clone without the hook is still
    // caught by verify.sh quick.
    public class RepoRulesTests {
        const long MaxFileBytes = 50L * 1024 * 1024;

        static readonly string[] SourceExtensions = {
            ".psd", ".blend", ".blend1", ".spp", ".kra", ".max", ".ma", ".mb", ".ztl"
        };

        [Test]
        public void NoAssetFileIsOver50MB() {
            List<string> offenders = new List<string>();
            foreach (string file in Directory.EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories)) {
                long size = new FileInfo(file).Length;
                if (size > MaxFileBytes) {
                    offenders.Add($"{Relative(file)} ({size / (1024 * 1024)} MB)");
                }
            }
            Assert.IsEmpty(offenders, "Files over 50 MB under Assets/ (D41): " + string.Join(", ", offenders));
        }

        [Test]
        public void NoSourceFormatFilesUnderAssets() {
            List<string> offenders = new List<string>();
            foreach (string file in Directory.EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories)) {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (System.Array.IndexOf(SourceExtensions, ext) >= 0) {
                    offenders.Add(Relative(file));
                }
            }
            Assert.IsEmpty(offenders, "Source-format files belong in the shared drive, not Assets/ (D41): " + string.Join(", ", offenders));
        }

        static string Relative(string file) {
            return "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
        }
    }
}
