using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace BusDriver.Tests.EditMode.Audio {
    // The mixer §4.12 asks for. Unity has no public API for creating mixer groups or snapshots, so
    // a person builds them (T-M1-10, [HUMAN]); until then this reports Inconclusive and names every
    // missing piece, instead of failing.
    public class AudioMixerValidatorTests {
        public const string MixerPath = "Assets/Audio/MainMixer.mixer";

        // Paths as FindMatchingGroups takes them
        static readonly string[] Groups = {
            "Master", "Master/Music", "Master/Ambience", "Master/SFX", "Master/SFX/Bus", "Master/SFX/Cabin",
            "Master/SFX/World", "Master/SFX/Scares", "Master/Voice", "Master/UI",
        };
        static readonly string[] Snapshots = { "Default", "Earplugs", "Tunnel", "Blackout" };
        static readonly string[] Parameters = { "MasterVolume", "MusicVolume", "AmbienceVolume", "SfxVolume", "VoiceVolume" };

        public static List<string> Missing(AudioMixer mixer) {
            List<string> missing = new List<string>();
            foreach (string path in Groups) {
                bool found = false;
                foreach (AudioMixerGroup group in mixer.FindMatchingGroups(path)) {
                    // FindMatchingGroups matches prefixes too; the group's own name must be the last segment
                    if (group.name == path.Substring(path.LastIndexOf('/') + 1)) {
                        found = true;
                    }
                }
                if (!found) {
                    missing.Add("group " + path);
                }
            }
            foreach (string snapshot in Snapshots) {
                if (mixer.FindSnapshot(snapshot) == null || mixer.FindSnapshot(snapshot).name != snapshot) {
                    missing.Add("snapshot " + snapshot);
                }
            }
            foreach (string parameter in Parameters) {
                float value;
                if (!mixer.GetFloat(parameter, out value)) {
                    missing.Add("exposed parameter " + parameter);
                }
            }
            return missing;
        }

        [Test]
        public void TheMixerHasTheRoadmapGroupsSnapshotsAndParameters() {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.IsNotNull(mixer, "no mixer at " + MixerPath);
            List<string> missing = Missing(mixer);
            if (missing.Count > 0) {
                Assert.Inconclusive("T-M1-10 [HUMAN] not done yet; the mixer lacks: " + string.Join(", ", missing));
            }
        }
    }
}
