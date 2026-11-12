using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Plays a monster's own sound, and retargets a shared kill scare's sting without editing the
    // asset the Starer still uses.
    static class MonsterSounds {
        public static SoundHandle Play(ShiftServices shift, string id, Transform at) {
            if (shift == null || shift.Game == null || shift.Game.Audio == null || string.IsNullOrEmpty(id)) {
                return SoundHandle.None;
            }
            if (at != null) {
                return shift.Game.Audio.PlayAt(id, at.position);
            }
            return shift.Game.Audio.Play(id);
        }

        // One play for each full metre the sprint has covered since the last step.
        public static void PlayTravel(ShiftServices shift, string id, Transform at, ref float travelled) {
            int steps = AisleRush.StepsOf(ref travelled);
            for (int i = 0; i < steps; i++) {
                Play(shift, id, at);
            }
        }

        // A runtime copy whose PlaySound steps use soundId. Null when there is nothing to copy.
        // The caller destroys the copy after the scare has finished reading it.
        public static ScareDefinition WithKillSound(ScareDefinition source, string soundId) {
            if (source == null || source.steps == null || string.IsNullOrEmpty(soundId)) {
                return source;
            }
            ScareDefinition copy = Object.Instantiate(source);
            ScareStep[] steps = new ScareStep[source.steps.Length];
            for (int i = 0; i < steps.Length; i++) {
                ScareStep step = source.steps[i];
                if (step.kind == ScareStepKind.PlaySound && !string.IsNullOrEmpty(step.soundId)) {
                    step.soundId = soundId;
                }
                steps[i] = step;
            }
            copy.steps = steps;
            return copy;
        }
    }
}
