using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Scares;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // SanityZero (§2.14): scare.whisperer.kill when a Whisperer is aboard (T-M5-06 seeds it),
    // otherwise scare.blackout.generic, a 2 s fade to black. The heartbeat that stops is
    // SanityFx's (T-M5-03), which ends it when the death starts.
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
