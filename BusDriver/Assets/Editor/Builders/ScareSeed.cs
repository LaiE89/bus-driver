using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using UnityEditor;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Scares (§2.10, §2.12b, §2.14, §2.17, §4.8, T-M4-05, T-M6-05): the Starer's
    // lens and kill scares, the Weeping Angel's closer/kill scares, the generic blackout the
    // SanityZero presenter plays when no Whisperer is aboard, and the generic kill-sequence
    // telegraph KillSequence plays directly (T-M4-07).
    public static class ScareSeed {
        public const string Folder = "Scares";
        public const string StarerLens = "scare.starer.lens";
        public const string StarerKill = "scare.starer.kill";
        public const string AngelCloser = "scare.angel.closer";
        public const string AngelKill = "scare.angel.kill";
        public const string BlackoutGeneric = "scare.blackout.generic";
        public const string TelegraphGeneric = "scare.telegraph.generic";

        public static string RelativePath(string scareId) {
            return Folder + "/" + scareId + ".asset";
        }

        public static IEnumerable<Seed> Seeds() {
            yield return Seed.Of<ScareDefinition>(RelativePath(StarerLens), FillStarerLens);
            yield return Seed.Of<ScareDefinition>(RelativePath(StarerKill), FillStarerKill);
            yield return Seed.Of<ScareDefinition>(RelativePath(AngelCloser), FillAngelCloser);
            yield return Seed.Of<ScareDefinition>(RelativePath(AngelKill), FillAngelKill);
            yield return Seed.Of<ScareDefinition>(RelativePath(BlackoutGeneric), FillBlackout);
            yield return Seed.Of<ScareDefinition>(RelativePath(TelegraphGeneric), FillTelegraph);
        }

        static ScareStep Step(float at, ScareStepKind kind, float duration = 0f, string soundId = "", float intensity = 0f, string anchor = "", string param = "") {
            return new ScareStep { at = at, kind = kind, duration = duration, soundId = soundId, intensity = intensity, anchor = anchor, param = param };
        }

        // §2.10: the next CCTV cycle cuts to the Starer's camera, its face filling the lens, with a sting
        static void FillStarerLens(ScareDefinition scare) {
            scare.id = StarerLens;
            scare.tier = ScareTier.Monster;
            scare.steps = new[] {
                Step(0f, ScareStepKind.CutToCctv),
                Step(0f, ScareStepKind.ShowScareHead, 1.4f, anchor: ScareAnchors.CctvLens),
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.MonStarerSting),
                Step(0f, ScareStepKind.CctvStatic, 0.25f, intensity: 0.35f),
            };
        }

        // §2.10: it forces the driver view, its face at DriverShoulder, a sting, then blackout
        static void FillStarerKill(ScareDefinition scare) {
            scare.id = StarerKill;
            scare.tier = ScareTier.Kill;
            scare.steps = new[] {
                Step(0f, ScareStepKind.LockInput, 2.2f),
                Step(0f, ScareStepKind.ForceHomeView),
                Step(0f, ScareStepKind.ShowScareHead, 1.6f, anchor: ScareAnchors.DriverShoulder, param: "lookAt"),
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.MonStarerKill),
                Step(0f, ScareStepKind.CameraShake, 0.6f, intensity: 0.5f),
                Step(0f, ScareStepKind.FlickerCabinLights, 1.2f),
                Step(1.2f, ScareStepKind.Blackout, 0.4f),
                Step(1.6f, ScareStepKind.Wait, 0.6f),
            };
        }

        // §2.12b: the first observation while it's standing — a sting and a short cabin-light stutter
        static void FillAngelCloser(ScareDefinition scare) {
            scare.id = AngelCloser;
            scare.tier = ScareTier.Monster;
            scare.steps = new[] {
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.MonAngelSting),
                Step(0f, ScareStepKind.FlickerCabinLights, 0.3f),
            };
        }

        // §2.12b: lights cut, force driver view, face at DriverShoulder, sting, blackout
        static void FillAngelKill(ScareDefinition scare) {
            scare.id = AngelKill;
            scare.tier = ScareTier.Kill;
            scare.steps = new[] {
                Step(0f, ScareStepKind.LockInput, 2.2f),
                Step(0f, ScareStepKind.ForceHomeView),
                Step(0f, ScareStepKind.CabinLightsOff, 0.6f),
                Step(0f, ScareStepKind.ShowScareHead, 1.6f, anchor: ScareAnchors.DriverShoulder, param: "lookAt"),
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.MonAngelKill),
                Step(0f, ScareStepKind.CameraShake, 0.6f, intensity: 0.5f),
                Step(1.2f, ScareStepKind.Blackout, 0.4f),
                Step(1.6f, ScareStepKind.Wait, 0.6f),
            };
        }

        // §2.14 SanityZero without a Whisperer aboard: the heartbeat stops, fade to black over 2 s
        static void FillBlackout(ScareDefinition scare) {
            scare.id = BlackoutGeneric;
            scare.tier = ScareTier.Kill;
            scare.steps = new[] {
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.DeathBlackout),
                Step(0f, ScareStepKind.Blackout, 2f),
            };
        }

        // §2.14 step 2: the cabin lights flicker and the rumble plays for the 4 s telegraph. Ambient,
        // because it isn't a jump scare; KillSequence plays it through ScarePlayer, not the arbiter.
        static void FillTelegraph(ScareDefinition scare) {
            scare.id = TelegraphGeneric;
            scare.tier = ScareTier.Ambient;
            scare.steps = new[] {
                Step(0f, ScareStepKind.FlickerCabinLights, 4f),
                Step(0f, ScareStepKind.PlaySound, 4f, soundId: SoundIds.MonTelegraphRumble),
            };
        }

        // Lists every seeded scare in the config and gives monsters their scares where they have
        // none; never replaces or removes a reference
        public static void Adopt(string root, GameRootConfig config) {
            List<ScareDefinition> scares = new List<ScareDefinition>(config.scares ?? new ScareDefinition[0]);
            scares.RemoveAll(s => s == null);
            foreach (string id in new[] { StarerLens, StarerKill, AngelCloser, AngelKill, BlackoutGeneric, TelegraphGeneric }) {
                ScareDefinition scare = AssetDatabase.LoadAssetAtPath<ScareDefinition>(root + "/" + RelativePath(id));
                if (scare != null && !scares.Contains(scare)) {
                    scares.Add(scare);
                }
            }
            config.scares = scares.ToArray();
            AdoptMonsterScares(root, NightSeed.Starer, StarerLens, StarerKill);
            AdoptMonsterScares(root, NightSeed.WeepingAngel, AngelCloser, AngelKill);
        }

        static void AdoptMonsterScares(string root, string monsterId, string monsterScareId, string killScareId) {
            MonsterDefinition monster = MonsterSeed.Load(root, monsterId);
            if (monster == null) {
                return;
            }
            bool changed = false;
            if (monster.monsterScare == null) {
                monster.monsterScare = AssetDatabase.LoadAssetAtPath<ScareDefinition>(root + "/" + RelativePath(monsterScareId));
                changed = true;
            }
            if (monster.killScare == null) {
                monster.killScare = AssetDatabase.LoadAssetAtPath<ScareDefinition>(root + "/" + RelativePath(killScareId));
                changed = true;
            }
            if (changed) {
                EditorUtility.SetDirty(monster);
            }
        }
    }
}
