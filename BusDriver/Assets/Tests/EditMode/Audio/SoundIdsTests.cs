using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Audio {
    // SoundIds and ROADMAP Appendix A.3 list the same ids (§4.12)
    public class SoundIdsTests {
        static string RoadmapPath {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "ROADMAP.md")); }
        }

        // The backticked ids in the first column of the A.3 table
        static List<string> AppendixIds() {
            string text = File.ReadAllText(RoadmapPath);
            int start = text.IndexOf("### A.3 Audio");
            int end = text.IndexOf("### A.4", start);
            Assert.Greater(start, 0, "ROADMAP.md has no Appendix A.3");
            List<string> ids = new List<string>();
            foreach (string line in text.Substring(start, end - start).Split('\n')) {
                if (!line.StartsWith("| `")) {
                    continue;
                }
                string firstCell = line.Split('|')[1];
                foreach (Match m in Regex.Matches(firstCell, "`([a-z0-9_.]+)`")) {
                    ids.Add(m.Groups[1].Value);
                }
            }
            return ids;
        }

        [Test]
        public void SoundIdsMatchAppendixA3() {
            CollectionAssert.AreEquivalent(AppendixIds(), SoundIds.All);
        }

        [Test]
        public void SoundIdsAreValidAndUnique() {
            HashSet<string> seen = new HashSet<string>();
            foreach (string id in SoundIds.All) {
                Assert.IsTrue(Ids.IsValid(id), id);
                Assert.IsTrue(seen.Add(id), "duplicate " + id);
            }
        }
    }
}
