using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BusDriver.Core.Save {
    // One step that reshapes a slot's data from FromVersion to FromVersion + 1, as a JObject
    // transform, before it's deserialized into the current model (§4.9).
    public interface ISaveMigration {
        SaveSlot Slot { get; }
        int FromVersion { get; }
        void Migrate(JObject data);
    }

    public sealed class SaveMigrationException : Exception {
        public SaveMigrationException(string message) : base(message) {
        }
    }

    // The current version of each slot, and the steps that bring older files up to it.
    // From G1 on, every save-model change bumps a version here and adds a step and a fixture set.
    public sealed class SaveMigrations {
        public const int InitialVersion = 1;

        readonly Dictionary<SaveSlot, int> current = new Dictionary<SaveSlot, int>();
        readonly List<ISaveMigration> steps = new List<ISaveMigration>();

        // The game's registry. Every slot is still at v1, so there are no steps yet.
        public static SaveMigrations CreateDefault() {
            return new SaveMigrations();
        }

        public int Current(SaveSlot slot) {
            int version;
            return current.TryGetValue(slot, out version) ? version : InitialVersion;
        }

        public SaveMigrations SetCurrent(SaveSlot slot, int version) {
            current[slot] = version;
            return this;
        }

        public SaveMigrations Register(ISaveMigration step) {
            if (Find(step.Slot, step.FromVersion) != null) {
                throw new ArgumentException($"a {step.Slot} migration from v{step.FromVersion} is already registered");
            }
            steps.Add(step);
            return this;
        }

        public bool CanMigrate(SaveSlot slot, int fromVersion) {
            for (int v = fromVersion; v < Current(slot); v++) {
                if (Find(slot, v) == null) {
                    return false;
                }
            }
            return true;
        }

        // Runs every step from fromVersion up to the current version, in order
        public void Migrate(SaveSlot slot, JObject data, int fromVersion) {
            for (int v = fromVersion; v < Current(slot); v++) {
                ISaveMigration step = Find(slot, v);
                if (step == null) {
                    throw new SaveMigrationException($"no {slot} migration from v{v} to v{v + 1}");
                }
                step.Migrate(data);
            }
        }

        ISaveMigration Find(SaveSlot slot, int fromVersion) {
            for (int i = 0; i < steps.Count; i++) {
                if (steps[i].Slot == slot && steps[i].FromVersion == fromVersion) {
                    return steps[i];
                }
            }
            return null;
        }
    }
}
