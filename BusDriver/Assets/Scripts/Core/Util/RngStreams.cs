using System;
using System.Collections.Generic;

namespace BusDriver.Core.Util {
    // One named System.Random per system, all derived from the run seed (§4.1.8), so drawing more
    // from one stream (an extra hallucination) never shifts another (the manifest).
    public sealed class RngStreams {
        public const string Manifest = "manifest";
        public const string Seating = "seating";
        public const string Hallucination = "hallucination";
        public const string Mimic = "mimic";
        public const string Scare = "scare";
        public const string Decoy = "decoy";
        public const string Dialogue = "dialogue";
        public const string Menu = "menu";
        // Monster behaviour that isn't the Mimic's copying (D69)
        public const string Monster = "monster";

        const uint FnvOffset = 2166136261;
        const uint FnvPrime = 16777619;

        readonly Dictionary<string, Random> streams = new Dictionary<string, Random>();

        public int Seed { get; }

        public RngStreams(int seed) {
            Seed = seed;
        }

        // The same stream instance on every call, so its sequence continues
        public Random Get(string name) {
            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentException("stream name is empty", nameof(name));
            }
            Random stream;
            if (!streams.TryGetValue(name, out stream)) {
                stream = new Random(StreamSeed(Seed, name));
                streams.Add(name, stream);
            }
            return stream;
        }

        // FNV-1a over the seed's 4 bytes then the name's UTF-16 code units, masked non-negative
        public static int StreamSeed(int seed, string name) {
            uint hash = FnvOffset;
            unchecked {
                for (int i = 0; i < 4; i++) {
                    hash = (hash ^ (byte)(seed >> (8 * i))) * FnvPrime;
                }
                for (int i = 0; i < name.Length; i++) {
                    char c = name[i];
                    hash = (hash ^ (byte)c) * FnvPrime;
                    hash = (hash ^ (byte)(c >> 8)) * FnvPrime;
                }
            }
            return (int)(hash & 0x7FFFFFFF);
        }

        // A fresh 32-bit run seed from the entropy behind Guid, since UnityEngine.Random is banned
        public static int NewSeed() {
            return BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
        }
    }
}
