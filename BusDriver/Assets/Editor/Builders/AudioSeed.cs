using System.Collections.Generic;
using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Audio (§4.8, §4.12, Appendix A.3): one SoundDefinition per sound id, the
    // SoundLibrary that lists them, and the AudioConfig routing.
    public static class AudioSeed {
        public const string LibraryRelativePath = "Audio/SoundLibrary.asset";
        public const string ConfigRelativePath = "Audio/AudioConfig.asset";
        public const string SoundsRelativeFolder = "Audio/Sounds";
        const string ClipsFolder = "Assets/Audio/Clips/";
        const float PlaceholderVolume = 0.35f;

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

        // ------------------------------------------------------------------ sounds

        struct SoundSpec {
            public string Id;
            public AudioGroup Group;
            public bool ThreeD;
            public bool Loop;
            public string Caption;
            // An existing clip under Audio/Clips; empty means a generated placeholder
            public string Clip;
            public float Volume;
            public float Pitch;
        }

        static SoundSpec S(string id, AudioGroup group, bool threeD, bool loop, string caption = "") {
            return new SoundSpec { Id = id, Group = group, ThreeD = threeD, Loop = loop, Caption = caption, Clip = "", Volume = PlaceholderVolume, Pitch = 1f };
        }

        // An entry of the deleted Sound Controller prefab, with its clip, volume and pitch as they were
        static SoundSpec Clip(SoundSpec spec, string clip, float volume, float pitch = 1f) {
            spec.Clip = clip;
            spec.Volume = volume;
            spec.Pitch = pitch;
            return spec;
        }

        // Appendix A.3, row by row. Sound Controller.prefab held seven entries: Bus Engine, Brake,
        // Card Tap, Camera, Wind Ambience, UI Click and Dialogue.
        static readonly SoundSpec[] Sounds = {
            Clip(S(SoundIds.BusEngineLoop, AudioGroup.SfxBus, false, true), "Bus/amb_driving_lp_01.ogg", 0.05f),
            S(SoundIds.BusAccel, AudioGroup.SfxBus, false, false),
            S(SoundIds.BusDecel, AudioGroup.SfxBus, false, false),
            Clip(S(SoundIds.BusHandbrake, AudioGroup.SfxBus, false, false), "Bus/sfx_airbrake.wav", 0.1f),
            S(SoundIds.BusDoorOpen, AudioGroup.SfxBus, true, false),
            S(SoundIds.BusDoorClose, AudioGroup.SfxBus, true, false),
            Clip(S(SoundIds.BusFareTap, AudioGroup.SfxCabin, true, false), "Bus/sfx_cardtap_nl_01.wav", 1f),
            // 2D on purpose: a stop bell has to cut through the cabin wherever the bus is
            S(SoundIds.BusStopRequest, AudioGroup.SfxCabin, false, false, "[stop requested]"),
            S(SoundIds.BusRumbleStrip, AudioGroup.SfxBus, false, true),
            S(SoundIds.BusCrashMinor, AudioGroup.SfxBus, false, false),
            S(SoundIds.BusCrashMajor, AudioGroup.SfxBus, false, false),
            S(SoundIds.BusHorn, AudioGroup.SfxBus, false, false, "[horn]"),
            Clip(S(SoundIds.CctvSwitch, AudioGroup.SfxCabin, false, false), "Bus/sfx_cameraswitch_nl_01.wav", 1f),
            S(SoundIds.CctvStaticLoop, AudioGroup.SfxCabin, false, true),
            Clip(S(SoundIds.AmbWind, AudioGroup.Ambience, false, true), "Ambience/Wind Ambience.ogg", 0.5f),
            S(SoundIds.AmbForestNight, AudioGroup.Ambience, false, true),
            S(SoundIds.AmbTunnel, AudioGroup.Ambience, false, true),
            S(SoundIds.AmbCabinHum, AudioGroup.Ambience, false, true),
            S(SoundIds.AmbLampBuzz, AudioGroup.Ambience, true, true),
            S(SoundIds.PlayerFootstepBus, AudioGroup.SfxCabin, true, false),
            S(SoundIds.PlayerFootstepGravel, AudioGroup.SfxWorld, true, false),
            S(SoundIds.PaxFootstep, AudioGroup.SfxCabin, true, false),
            S(SoundIds.PaxMutterLoop, AudioGroup.Voice, true, true, "[muttering]"),
            S(SoundIds.PaxDeath, AudioGroup.SfxCabin, true, false),
            S(SoundIds.PaxKickOut, AudioGroup.SfxCabin, true, false),
            S(SoundIds.MonTelegraphRumble, AudioGroup.Scares, false, false),
            S(SoundIds.MonExpel, AudioGroup.Scares, true, false),
            S(SoundIds.MonStarerSting, AudioGroup.Scares, false, false),
            S(SoundIds.MonStarerKill, AudioGroup.Scares, false, false),
            S(SoundIds.MonWhisperLoop, AudioGroup.Voice, true, true, "[whispering]"),
            S(SoundIds.MonWhisperFeedLoop, AudioGroup.Voice, false, true),
            S(SoundIds.MonWhisperDriver, AudioGroup.Voice, false, false, "[whisper] driver…"),
            S(SoundIds.MonWhispererKill, AudioGroup.Scares, false, false),
            S(SoundIds.MonMimicSting, AudioGroup.Scares, false, false),
            S(SoundIds.MonMimicKill, AudioGroup.Scares, false, false),
            S(SoundIds.MonMimicReveal, AudioGroup.Scares, true, false),
            S(SoundIds.MonAngelScrape, AudioGroup.SfxCabin, true, true, "[stone scraping]"),
            S(SoundIds.MonAngelSting, AudioGroup.Scares, false, false),
            S(SoundIds.MonAngelKill, AudioGroup.Scares, false, false),
            S(SoundIds.ScareStartleSting, AudioGroup.Scares, false, false),
            S(SoundIds.ScareLightsOut, AudioGroup.Scares, false, false),
            S(SoundIds.HalFootstepsBehind, AudioGroup.SfxCabin, true, false, "[footsteps behind you]"),
            S(SoundIds.HalDoorChime, AudioGroup.SfxCabin, true, false, "[door chime]"),
            S(SoundIds.HalWindowKnock, AudioGroup.Scares, true, false, "[knock on the window]"),
            S(SoundIds.HalStaticBurst, AudioGroup.SfxCabin, false, false),
            S(SoundIds.HalWhisperDriver, AudioGroup.Voice, false, false, "[whisper] driver…"),
            S(SoundIds.SanHeartbeatLoop, AudioGroup.Scares, false, true),
            S(SoundIds.DeathFallWind, AudioGroup.Scares, false, true),
            S(SoundIds.DeathFallImpact, AudioGroup.Scares, false, false),
            S(SoundIds.DeathBlackout, AudioGroup.Scares, false, false),
            S(SoundIds.ItemCoffee, AudioGroup.SfxCabin, false, false),
            S(SoundIds.ItemEarplugs, AudioGroup.SfxCabin, false, false),
            Clip(S(SoundIds.UiClick, AudioGroup.Ui, false, false), "Others/UI Click.wav", 1f, 1.4f),
            S(SoundIds.UiHover, AudioGroup.Ui, false, false),
            S(SoundIds.UiPurchase, AudioGroup.Ui, false, false),
            S(SoundIds.UiError, AudioGroup.Ui, false, false),
            S(SoundIds.UiMoneyUp, AudioGroup.Ui, false, false),
            S(SoundIds.UiMoneyDown, AudioGroup.Ui, false, false),
            S(SoundIds.UiNightCard, AudioGroup.Ui, false, false),
            Clip(S(SoundIds.UiTypeTick, AudioGroup.Ui, false, false), "Others/Dialogue.wav", 0.1f, 0.5f),
            S(SoundIds.MenuDistantEngine, AudioGroup.Ambience, true, false),
            S(SoundIds.MenuBusArrive, AudioGroup.SfxWorld, true, false),
            S(SoundIds.MusSummarySting, AudioGroup.Music, false, false),
            S(SoundIds.MusGameoverSting, AudioGroup.Music, false, false),
        };

        public static string SoundRelativePath(string id) {
            return SoundsRelativeFolder + "/" + id + ".asset";
        }

        public static IEnumerable<Seed> SoundSeeds() {
            foreach (SoundSpec spec in Sounds) {
                SoundSpec captured = spec;
                yield return Seed.Of<SoundDefinition>(SoundRelativePath(spec.Id), definition => FillSound(definition, captured));
            }
        }

        static void FillSound(SoundDefinition definition, SoundSpec spec) {
            definition.id = spec.Id;
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(spec.Clip)) {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipsFolder + spec.Clip);
                if (clip == null) {
                    Debug.LogError($"AudioSeed: no clip at {ClipsFolder}{spec.Clip} for {spec.Id}");
                }
            }
            definition.clips = clip != null ? new[] { clip } : new AudioClip[0];
            // PlaceholderAudioBuilder fills in a generated clip while this stays on
            definition.placeholder = clip == null;
            definition.volume = spec.Volume;
            definition.volumeJitter = 0f;
            definition.pitch = spec.Pitch;
            definition.pitchJitter = 0f;
            definition.group = spec.Group;
            definition.spatial = spec.ThreeD ? SoundSpatial.ThreeD : SoundSpatial.TwoD;
            definition.minDistance = 1f;
            definition.maxDistance = 25f;
            definition.loop = spec.Loop;
            definition.maxVoices = spec.Loop ? 1 : 4;
            definition.priority = spec.Group == AudioGroup.Scares || spec.Group == AudioGroup.Voice ? 64 : 128;
            definition.cooldown = 0f;
            definition.caption = spec.Caption;
        }

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

        // Additive only: a seeded definition missing from the library is appended, and an empty
        // mixer-group slot is filled once the mixer has that group
        public static void Adopt(string root, GameRootConfig gameConfig) {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(root + "/" + LibraryRelativePath);
            if (library != null) {
                bool added = false;
                foreach (SoundSpec spec in Sounds) {
                    SoundDefinition definition = AssetDatabase.LoadAssetAtPath<SoundDefinition>(root + "/" + SoundRelativePath(spec.Id));
                    if (definition != null && !library.sounds.Contains(definition)) {
                        library.sounds.Add(definition);
                        added = true;
                    }
                }
                if (added) {
                    library.Index();
                    EditorUtility.SetDirty(library);
                }
            }

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
