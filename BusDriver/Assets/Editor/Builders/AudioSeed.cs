using System.Collections.Generic;
using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Audio (§4.8, §4.12): the SoundLibrary and the AudioConfig routing.
    public static class AudioSeed {
        public const string LibraryRelativePath = "Audio/SoundLibrary.asset";
        public const string ConfigRelativePath = "Audio/AudioConfig.asset";

        // AudioGroup → the mixer group's path (the §4.12 tree) and the exposed volume parameter
        // that controls it. UI has no parameter of its own; Master covers it.
        struct GroupSpec {
            public AudioGroup Group;
            public string MixerPath;
            public string VolumeParameter;
        }

        static readonly GroupSpec[] Groups = {
            new GroupSpec { Group = AudioGroup.Music, MixerPath = "Master/Music", VolumeParameter = "MusicVolume" },
            new GroupSpec { Group = AudioGroup.Ambience, MixerPath = "Master/Ambience", VolumeParameter = "AmbienceVolume" },
            new GroupSpec { Group = AudioGroup.SfxBus, MixerPath = "Master/SFX/Bus", VolumeParameter = "SfxVolume" },
            new GroupSpec { Group = AudioGroup.SfxCabin, MixerPath = "Master/SFX/Cabin", VolumeParameter = "SfxVolume" },
            new GroupSpec { Group = AudioGroup.SfxWorld, MixerPath = "Master/SFX/World", VolumeParameter = "SfxVolume" },
            new GroupSpec { Group = AudioGroup.Voice, MixerPath = "Master/Voice", VolumeParameter = "VoiceVolume" },
            new GroupSpec { Group = AudioGroup.Scares, MixerPath = "Master/SFX/Scares", VolumeParameter = "SfxVolume" },
            new GroupSpec { Group = AudioGroup.Ui, MixerPath = "Master/UI", VolumeParameter = "" },
        };

        public static void FillLibrary(SoundLibrary library) {
            library.sounds = new List<SoundDefinition>();
        }

        // Mixer groups stay empty here: they don't exist until a person builds them (T-M1-10), and
        // adoption fills each one in as soon as it does
        public static void FillConfig(AudioConfig config) {
            config.masterVolumeParameter = "MasterVolume";
            config.groups = new AudioConfig.GroupRoute[Groups.Length];
            for (int i = 0; i < Groups.Length; i++) {
                config.groups[i] = new AudioConfig.GroupRoute {
                    group = Groups[i].Group,
                    mixerGroup = null,
                    volumeParameter = Groups[i].VolumeParameter,
                };
            }
            config.snapshots = new[] {
                new AudioConfig.SnapshotRoute { snapshot = AudioSnapshot.Default, snapshotName = "Default" },
                new AudioConfig.SnapshotRoute { snapshot = AudioSnapshot.Earplugs, snapshotName = "Earplugs" },
                new AudioConfig.SnapshotRoute { snapshot = AudioSnapshot.Tunnel, snapshotName = "Tunnel" },
                new AudioConfig.SnapshotRoute { snapshot = AudioSnapshot.Blackout, snapshotName = "Blackout" },
            };
        }

        // Additive only: an empty mixer-group slot is filled once the mixer has that group
        public static void Adopt(string root, GameRootConfig gameConfig) {
            AudioConfig config = AssetDatabase.LoadAssetAtPath<AudioConfig>(root + "/" + ConfigRelativePath);
            AudioMixer mixer = gameConfig.mixer != null ? gameConfig.mixer : AssetDatabase.LoadAssetAtPath<AudioMixer>(DataSeeder.MixerPath);
            if (config == null || mixer == null) {
                return;
            }
            bool changed = false;
            for (int i = 0; i < config.groups.Length; i++) {
                if (config.groups[i].mixerGroup != null) {
                    continue;
                }
                AudioMixerGroup group = FindGroup(mixer, PathFor(config.groups[i].group));
                if (group != null) {
                    config.groups[i].mixerGroup = group;
                    changed = true;
                }
            }
            if (changed) {
                EditorUtility.SetDirty(config);
            }
        }

        static string PathFor(AudioGroup group) {
            foreach (GroupSpec spec in Groups) {
                if (spec.Group == group) {
                    return spec.MixerPath;
                }
            }
            return "";
        }

        // FindMatchingGroups matches prefixes too; the group's own name must be the last segment
        static AudioMixerGroup FindGroup(AudioMixer mixer, string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }
            string last = path.Substring(path.LastIndexOf('/') + 1);
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(path)) {
                if (group.name == last) {
                    return group;
                }
            }
            return null;
        }
    }
}
