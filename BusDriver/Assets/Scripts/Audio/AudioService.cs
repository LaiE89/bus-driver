using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using UnityEngine;
using UnityEngine.Audio;

namespace BusDriver.Gameplay.Audio {
    // Pooled playback by id (§4.12): 32 AudioSources under GameRoot, per-definition voice limits
    // (the oldest instance is stolen) and cooldowns, attached sources that follow a transform,
    // group routing, exposed-parameter volumes and snapshots. GameRoot ticks it every LateUpdate.
    public sealed class AudioService : IAudioService {
        public const int PoolSize = 32;
        public const float DefaultSnapshotFade = 0.3f;

        sealed class Voice {
            public AudioSource Source;
            public int HandleId;
            public SoundDefinition Definition;
            public float StartedAt;
            // float.PositiveInfinity for loops
            public float EndsAt;
            public Transform Attached;
            public bool IsAttached;
            public float VolumeScale = 1f;
            public float BaseVolume;

            public bool Busy { get { return HandleId != 0; } }
        }

        readonly Voice[] voices = new Voice[PoolSize];
        readonly SoundLibrary library;
        readonly AudioConfig config;
        readonly AudioMixer mixer;
        readonly AudioMixerGroup masterGroup;
        // Seconds; scaled time by default, so a paused game's voices don't time out early
        readonly Func<float> clock;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        // Clip and jitter variety only; not gameplay randomness (§4.1 rule 8 covers gameplay)
        readonly System.Random random = new System.Random(Environment.TickCount);
        readonly HashSet<string> warned = new HashSet<string>();
        int nextHandle = 1;

        public event Action<string> OnCaption;

        public AudioService(Transform parent, SoundLibrary library, AudioConfig config, AudioMixer mixer, Func<float> clock) {
            this.library = library;
            this.config = config;
            this.mixer = mixer;
            this.clock = clock;
            if (mixer != null) {
                AudioMixerGroup[] masters = mixer.FindMatchingGroups("Master");
                masterGroup = masters.Length > 0 ? masters[0] : null;
            }
            GameObject pool = new GameObject("Audio Pool");
            pool.transform.SetParent(parent, false);
            for (int i = 0; i < PoolSize; i++) {
                GameObject host = new GameObject("Voice " + i);
                host.transform.SetParent(pool.transform, false);
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                voices[i] = new Voice { Source = source };
            }
        }

        // ------------------------------------------------------------------ playing

        public SoundHandle Play(string id) {
            return Start(id, null, false, Vector3.zero, false);
        }

        public SoundHandle PlayAt(string id, Vector3 position) {
            return Start(id, null, false, position, true);
        }

        public SoundHandle PlayAttached(string id, Transform target) {
            if (target == null) {
                return SoundHandle.None;
            }
            return Start(id, target, true, target.position, true);
        }

        SoundHandle Start(string id, Transform attach, bool isAttached, Vector3 position, bool positioned) {
            SoundDefinition definition;
            if (library == null || !library.TryGet(id, out definition)) {
                WarnOnce(id, $"no sound definition for '{id}'");
                return SoundHandle.None;
            }
            if (definition.clips == null || definition.clips.Length == 0) {
                WarnOnce(id, $"sound '{id}' has no clips");
                return SoundHandle.None;
            }
            float now = clock();
            float last;
            if (definition.cooldown > 0f && lastPlayed.TryGetValue(id, out last) && now - last < definition.cooldown) {
                return SoundHandle.None;
            }
            // The definition's own voice limit: the oldest instance makes room
            if (CountVoices(definition) >= Mathf.Max(1, definition.maxVoices)) {
                Release(OldestOf(definition));
            }
            Voice voice = FreeVoice();
            if (voice == null) {
                voice = LeastImportant();
                Release(voice);
            }
            lastPlayed[id] = now;

            AudioClip clip = definition.clips[definition.clips.Length == 1 ? 0 : random.Next(definition.clips.Length)];
            float pitch = Mathf.Max(0.05f, definition.pitch + Jitter(definition.pitchJitter));
            AudioSource source = voice.Source;
            source.clip = clip;
            source.loop = definition.loop;
            source.pitch = pitch;
            source.panStereo = 0f;
            source.priority = definition.priority;
            source.spatialBlend = definition.spatial == SoundSpatial.ThreeD ? 1f : 0f;
            source.minDistance = definition.minDistance;
            source.maxDistance = Mathf.Max(definition.minDistance, definition.maxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.outputAudioMixerGroup = MixerGroupFor(definition.group);
            // UI keeps playing under the pause screen (§4.11)
            source.ignoreListenerPause = definition.group == AudioGroup.Ui;
            source.transform.position = positioned ? position : Vector3.zero;

            voice.HandleId = nextHandle++;
            voice.Definition = definition;
            voice.StartedAt = now;
            voice.EndsAt = definition.loop ? float.PositiveInfinity : now + clip.length / pitch;
            voice.Attached = attach;
            voice.IsAttached = isAttached;
            voice.VolumeScale = 1f;
            voice.BaseVolume = Mathf.Clamp01(definition.volume + Jitter(definition.volumeJitter));
            source.volume = voice.BaseVolume;
            source.Play();

            if (!string.IsNullOrEmpty(definition.caption) && OnCaption != null) {
                OnCaption(definition.caption);
            }
            return new SoundHandle(voice.HandleId);
        }

        float Jitter(float amount) {
            return amount <= 0f ? 0f : ((float)random.NextDouble() * 2f - 1f) * amount;
        }

        public void Stop(SoundHandle handle) {
            Voice voice = Find(handle);
            if (voice != null) {
                Release(voice);
            }
        }

        public bool IsPlaying(SoundHandle handle) {
            return Find(handle) != null;
        }

        public void SetVolume(SoundHandle handle, float volume01) {
            Voice voice = Find(handle);
            if (voice != null) {
                voice.VolumeScale = Mathf.Clamp01(volume01);
                voice.Source.volume = voice.BaseVolume * voice.VolumeScale;
            }
        }

        public void SetPitch(SoundHandle handle, float pitch) {
            Voice voice = Find(handle);
            if (voice != null) {
                voice.Source.pitch = Mathf.Max(0.05f, pitch);
            }
        }

        public void SetPan(SoundHandle handle, float pan) {
            Voice voice = Find(handle);
            if (voice != null) {
                voice.Source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            }
        }

        public void StopSceneSounds() {
            for (int i = 0; i < voices.Length; i++) {
                if (voices[i].Busy && voices[i].Definition.group != AudioGroup.Ui) {
                    Release(voices[i]);
                }
            }
        }

        // Frees finished voices and moves attached ones. No allocations (§4.1 rule 14).
        public void Tick() {
            float now = clock();
            for (int i = 0; i < voices.Length; i++) {
                Voice voice = voices[i];
                if (!voice.Busy) {
                    continue;
                }
                if (now >= voice.EndsAt) {
                    Release(voice);
                    continue;
                }
                if (voice.IsAttached) {
                    if (voice.Attached == null) {
                        Release(voice);
                    }else {
                        voice.Source.transform.position = voice.Attached.position;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ mixer

        public void SetSnapshot(AudioSnapshot snapshot, float fadeSeconds) {
            if (mixer == null) {
                return;
            }
            string name = config != null ? config.SnapshotName(snapshot) : snapshot.ToString();
            AudioMixerSnapshot found = mixer.FindSnapshot(name);
            if (found == null) {
                // Until the [HUMAN] mixer snapshots exist (T-M1-10)
                WarnOnce("snapshot:" + name, $"the mixer has no snapshot '{name}'");
                return;
            }
            found.TransitionTo(Mathf.Max(0f, fadeSeconds));
        }

        public void SetGroupVolume(AudioGroup group, float linear) {
            AudioConfig.GroupRoute route;
            if (config == null || !config.TryGetGroup(group, out route) || string.IsNullOrEmpty(route.volumeParameter)) {
                WarnOnce("volume:" + group, $"no exposed volume parameter for group {group}");
                return;
            }
            SetParameter(route.volumeParameter, linear);
        }

        public void SetMasterVolume(float linear) {
            if (config != null && !string.IsNullOrEmpty(config.masterVolumeParameter)) {
                SetParameter(config.masterVolumeParameter, linear);
            }
        }

        void SetParameter(string parameter, float linear) {
            if (mixer == null) {
                return;
            }
            if (!mixer.SetFloat(parameter, LinearToDecibels(linear))) {
                WarnOnce("param:" + parameter, $"the mixer doesn't expose '{parameter}'");
            }
        }

        public static float LinearToDecibels(float linear) {
            return 20f * Mathf.Log10(Mathf.Max(linear, 0.0001f));
        }

        AudioMixerGroup MixerGroupFor(AudioGroup group) {
            AudioConfig.GroupRoute route;
            if (config != null && config.TryGetGroup(group, out route) && route.mixerGroup != null) {
                return route.mixerGroup;
            }
            return masterGroup;
        }

        // ------------------------------------------------------------------ voices

        Voice Find(SoundHandle handle) {
            if (handle.IsNone) {
                return null;
            }
            for (int i = 0; i < voices.Length; i++) {
                if (voices[i].HandleId == handle.Id) {
                    return voices[i];
                }
            }
            return null;
        }

        int CountVoices(SoundDefinition definition) {
            int count = 0;
            for (int i = 0; i < voices.Length; i++) {
                if (voices[i].Busy && voices[i].Definition == definition) {
                    count++;
                }
            }
            return count;
        }

        Voice OldestOf(SoundDefinition definition) {
            Voice oldest = null;
            for (int i = 0; i < voices.Length; i++) {
                Voice voice = voices[i];
                if (voice.Busy && voice.Definition == definition && (oldest == null || voice.HandleId < oldest.HandleId)) {
                    oldest = voice;
                }
            }
            return oldest;
        }

        Voice FreeVoice() {
            for (int i = 0; i < voices.Length; i++) {
                if (!voices[i].Busy) {
                    return voices[i];
                }
            }
            return null;
        }

        // The pool is full: the least important voice (highest priority number), oldest first
        Voice LeastImportant() {
            Voice pick = voices[0];
            for (int i = 1; i < voices.Length; i++) {
                Voice voice = voices[i];
                int a = voice.Definition.priority;
                int b = pick.Definition.priority;
                if (a > b || (a == b && voice.HandleId < pick.HandleId)) {
                    pick = voice;
                }
            }
            return pick;
        }

        static void Release(Voice voice) {
            if (voice == null || !voice.Busy) {
                return;
            }
            if (voice.Source != null) {
                voice.Source.Stop();
                voice.Source.clip = null;
            }
            voice.HandleId = 0;
            voice.Definition = null;
            voice.Attached = null;
            voice.IsAttached = false;
        }

        void WarnOnce(string key, string message) {
            if (warned.Add(key)) {
                Log.Warn(LogCat.Audio, message);
            }
        }

        // Tests
        internal int ActiveVoiceCount {
            get {
                int count = 0;
                for (int i = 0; i < voices.Length; i++) {
                    if (voices[i].Busy) {
                        count++;
                    }
                }
                return count;
            }
        }
    }
}
