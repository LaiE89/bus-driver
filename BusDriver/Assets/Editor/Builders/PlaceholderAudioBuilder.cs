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
            float[] samples;
            if (definition.loop) {
                samples = Hum(hash, random);
            }else if (definition.group == AudioGroup.Scares) {
                samples = Burst(hash, random);
            }else if (definition.group == AudioGroup.Ui) {
                samples = Tick(hash);
            }else {
                samples = Blip(hash);
            }
            return Wav(samples);
        }

        // 2 s, a whole number of cycles so the loop point is seamless, with a little noise
        static float[] Hum(uint hash, System.Random random) {
            float frequency = 55f + (hash % 60) * 0.5f;
            float[] samples = new float[SampleRate * 2];
            for (int i = 0; i < samples.Length; i++) {
                float t = (float)i / SampleRate;
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t);
                samples[i] = 0.25f * tone + 0.05f * ((float)random.NextDouble() * 2f - 1f);
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
