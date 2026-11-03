using System;
using System.IO;
using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // BuildAll step 5 (§4.12): a short synthesized WAV in Generated/Audio/<id>.wav for every
    // SoundDefinition that still has `placeholder` ticked, assigned as its only clip. A loop gets
    // a hum, a scare a noise burst, UI a tick, anything else a blip; the pitch comes from the id,
    // so every build writes the same bytes. Once an audio engineer assigns real clips and unticks
    // `placeholder`, this never touches the definition again.
    public static class PlaceholderAudioBuilder {
        public const string Folder = BuilderUtil.GeneratedRoot + "/Audio";
        const int SampleRate = 22050;

        [MenuItem("Tools/Bus Driver/Builders/Placeholder Audio")]
        public static void Build() {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            if (config == null || config.soundLibrary == null) {
                throw new InvalidOperationException("GameRootConfig has no SoundLibrary; run DataSeeder first");
            }
            BuilderUtil.EnsureFolder(Folder);
            foreach (SoundDefinition definition in config.soundLibrary.sounds) {
                if (definition == null || !definition.placeholder) {
                    continue;
                }
                string path = Folder + "/" + definition.id + ".wav";
                WriteIfChanged(path, Synthesize(definition));
                AssetDatabase.ImportAsset(path);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (definition.clips == null || definition.clips.Length != 1 || definition.clips[0] != clip) {
                    definition.clips = new[] { clip };
                    EditorUtility.SetDirty(definition);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Identical bytes are never rewritten, so a rebuild leaves git clean
        static void WriteIfChanged(string path, byte[] wav) {
            if (File.Exists(path)) {
                byte[] existing = File.ReadAllBytes(path);
                if (existing.Length == wav.Length && ((ReadOnlySpan<byte>)existing).SequenceEqual(wav)) {
                    return;
                }
            }
            File.WriteAllBytes(path, wav);
        }

        // ------------------------------------------------------------- synthesis

        static byte[] Synthesize(SoundDefinition definition) {
            uint hash = Fnv1a(definition.id);
            System.Random random = new System.Random((int)hash);
            float[] samples = ById(definition.id, hash, random);
            if (samples == null) {
                if (definition.loop) {
                    samples = Hum(hash);
                }else if (definition.group == AudioGroup.Scares || definition.group == AudioGroup.Voice) {
                    samples = Burst(hash, random);
                }else if (definition.group == AudioGroup.Ui) {
                    samples = Tick(hash);
                }else {
                    samples = Blip(hash);
                }
            }
            return Wav(samples);
        }

        // Horror-leaning placeholders for sanity / Whisperer (still replaced when real clips land)
        static float[] ById(string id, uint hash, System.Random random) {
            switch (id) {
                case "mon.whisper_loop":
                case "mon.whisper_feed_loop":
                case "hal.whisper_driver":
                case "mon.whisper.driver":
                    return WhisperMurmur(hash, random, id.Contains("feed") ? 3f : 2.5f);
                case "hal.window_knock":
                    return WindowKnock(hash, random);
                case "hal.footsteps_behind":
                    return FootstepsBehind(hash, random);
                case "hal.static_burst":
                    return StaticBurst(hash, random);
                case "hal.door_chime":
                    return WrongChime(hash);
                case "scare.startle_sting":
                    return ScareSting(hash, random);
                default:
                    return null;
            }
        }

        // Layered breathy noise + low formants: reads as murmuring, not a sine beep
        static float[] WhisperMurmur(uint hash, System.Random random, float seconds) {
            int count = Mathf.Max(1, (int)(SampleRate * seconds));
            float[] samples = new float[count];
            float f1 = 90f + (hash % 40);
            float f2 = 180f + ((hash >> 5) % 70);
            float f3 = 320f + ((hash >> 11) % 90);
            for (int i = 0; i < count; i++) {
                float t = (float)i / SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                // Soft band-ish shaping via summing delayed noise taps
                float breath = noise * 0.55f
                    + Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.12f
                    + Mathf.Sin(2f * Mathf.PI * f2 * t + 0.7f) * 0.08f
                    + Mathf.Sin(2f * Mathf.PI * f3 * t + 1.3f) * 0.05f;
                float flutter = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (2.3f + (hash % 5) * 0.15f) * t);
                float consonant = Mathf.Sin(2f * Mathf.PI * (12f + (hash % 7)) * t) > 0.75f ? 0.35f : 0f;
                samples[i] = 0.55f * flutter * (breath + consonant * noise);
            }
            // Seamless loop: fade edges into each other
            int fade = Mathf.Min(SampleRate / 10, count / 4);
            for (int i = 0; i < fade; i++) {
                float w = (float)i / fade;
                samples[i] *= w;
                samples[count - 1 - i] *= w;
            }
            return samples;
        }

        static float[] WindowKnock(uint hash, System.Random random) {
            float[] samples = new float[(int)(SampleRate * 0.7f)];
            float[] hits = { 0.02f, 0.14f, 0.22f };
            for (int h = 0; h < hits.Length; h++) {
                float start = hits[h];
                float pitch = 140f + (hash % 40) + h * 18f;
                for (int i = 0; i < samples.Length; i++) {
                    float t = (float)i / SampleRate - start;
                    if (t < 0f || t > 0.12f) {
                        continue;
                    }
                    float thump = Mathf.Exp(-28f * t) * Mathf.Sin(2f * Mathf.PI * pitch * t);
                    float click = Mathf.Exp(-90f * t) * ((float)random.NextDouble() * 2f - 1f);
                    samples[i] += 0.7f * thump + 0.35f * click;
                }
            }
            return samples;
        }

        static float[] FootstepsBehind(uint hash, System.Random random) {
            float[] samples = new float[(int)(SampleRate * 1.4f)];
            float[] steps = { 0.05f, 0.45f, 0.82f, 1.15f };
            for (int s = 0; s < steps.Length; s++) {
                float start = steps[s];
                float pitch = 55f + (hash % 25) + (s % 2) * 8f;
                for (int i = 0; i < samples.Length; i++) {
                    float t = (float)i / SampleRate - start;
                    if (t < 0f || t > 0.18f) {
                        continue;
                    }
                    float body = Mathf.Exp(-18f * t) * Mathf.Sin(2f * Mathf.PI * pitch * t);
                    float grit = Mathf.Exp(-40f * t) * ((float)random.NextDouble() * 2f - 1f) * 0.45f;
                    samples[i] += 0.65f * body + grit;
                }
            }
            return samples;
        }

        static float[] StaticBurst(uint hash, System.Random random) {
            float[] samples = new float[(int)(SampleRate * 0.55f)];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f));
                float noise = (float)random.NextDouble() * 2f - 1f;
                float grit = ((hash + (uint)i * 2654435761u) % 1000) / 1000f * 2f - 1f;
                samples[i] = 0.7f * env * (0.7f * noise + 0.3f * grit);
            }
            return samples;
        }

        static float[] WrongChime(uint hash) {
            float[] samples = new float[(int)(SampleRate * 0.9f)];
            float a = 410f + (hash % 30);
            float b = 520f + ((hash >> 4) % 40);
            float c = 670f + ((hash >> 8) % 50);
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-2.2f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * a * t)
                    + 0.7f * Mathf.Sin(2f * Mathf.PI * b * t)
                    + 0.45f * Mathf.Sin(2f * Mathf.PI * c * t);
                samples[i] = 0.35f * env * tone;
            }
            return samples;
        }

        static float[] ScareSting(uint hash, System.Random random) {
            float[] samples = new float[(int)(SampleRate * 0.85f)];
            float low = 45f + (hash % 20);
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float boom = Mathf.Exp(-5f * t) * Mathf.Sin(2f * Mathf.PI * low * t);
                float scream = Mathf.Exp(-3f * t) * Mathf.Sin(2f * Mathf.PI * (700f + 400f * t) * t);
                float noise = Mathf.Exp(-8f * t) * ((float)random.NextDouble() * 2f - 1f);
                samples[i] = 0.55f * boom + 0.4f * scream + 0.35f * noise;
            }
            return samples;
        }

        // 2 s, a whole number of cycles so the loop point is seamless. Tonal only: a noise layer
        // reads as radio static once it loops under a quiet scene, and it clicks at the loop point
        static float[] Hum(uint hash) {
            float frequency = 55f + (hash % 60) * 0.5f;
            float[] samples = new float[SampleRate * 2];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                // One cycle of a 0.5 Hz tremolo fits the loop, so it breathes and still joins up
                float tremolo = 0.85f + 0.15f * Mathf.Sin(Mathf.PI * t);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t);
                samples[i] = 0.25f * tremolo * tone;
            }
            return samples;
        }

        // 0.6 s of decaying noise over a low thump
        static float[] Burst(uint hash, System.Random random) {
            float frequency = 70f + (hash % 50);
            float[] samples = new float[(int)(SampleRate * 0.6f)];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float decay = Mathf.Exp(-6f * t);
                float noise = (float)random.NextDouble() * 2f - 1f;
                samples[i] = decay * (0.5f * noise + 0.4f * Mathf.Sin(2f * Mathf.PI * frequency * t));
            }
            return samples;
        }

        static float[] Tick(uint hash) {
            float frequency = 1500f + (hash % 1000);
            float[] samples = new float[(int)(SampleRate * 0.06f)];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                samples[i] = 0.5f * Mathf.Exp(-60f * t) * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }
            return samples;
        }

        // 0.25 s tone with a short fade in and out
        static float[] Blip(uint hash) {
            float frequency = 300f + (hash % 600);
            float[] samples = new float[(int)(SampleRate * 0.25f)];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Min(1f, Mathf.Min(t / 0.01f, (0.25f - t) / 0.05f));
                samples[i] = 0.4f * envelope * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }
            return samples;
        }

        // 16-bit mono PCM
        static byte[] Wav(float[] samples) {
            int dataBytes = samples.Length * 2;
            using (MemoryStream stream = new MemoryStream(44 + dataBytes))
            using (BinaryWriter writer = new BinaryWriter(stream)) {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++) {
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        static uint Fnv1a(string text) {
            uint hash = 2166136261;
            for (int i = 0; i < text.Length; i++) {
                hash ^= text[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
