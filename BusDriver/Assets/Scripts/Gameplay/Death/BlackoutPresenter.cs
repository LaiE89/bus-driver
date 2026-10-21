using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Scares;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // Legacy SanityZero presenter (§2.14): Whisperer kill scare or generic blackout. Kept wired
    // for scare assets / Game Over copy; DeathDirector ignores SanityZero so this never runs.
    public sealed class BlackoutPresenter : DeathPresenter {
        public const string WhispererKill = "scare.whisperer.kill";
        public const string GenericBlackout = "scare.blackout.generic";
        [Tooltip("Without either scare: fade to black over this long, seconds")]
        [SerializeField] float fallbackSeconds = 2f;

        ShiftServices shift;

        public override DeathCause Cause { get { return DeathCause.SanityZero; } }

        public override void Init(ShiftServices services) {
            shift = services;
        }

        public override IEnumerator Present(DeathReport report) {
            GameRootConfig config = shift.Game.Config;
            MonsterBrain whisperer = report.SourceId == DeathDirector.WhispererId && shift.Death != null
                ? shift.Death.ActiveMonster(DeathDirector.WhispererId) : null;
            ScareDefinition scare = whisperer != null ? config.Scare(WhispererKill) : null;
            if (scare == null) {
                scare = config.Scare(GenericBlackout);
            }
            if (scare != null && shift.ScarePlayer != null) {
                yield return shift.ScarePlayer.Play(scare, ScareContext.For(whisperer));
            }else {
                yield return shift.Fade.FadeTo(1f, fallbackSeconds);
            }
            shift.Fade.Set(1f);
        }
    }
}
