using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // MonsterKill (§2.14): the monster's kill scare has already played and blacked out the screen
    // (KillSequence plays it before Die), so this only holds black for a moment
    public sealed class MonsterKillPresenter : DeathPresenter {
        [Tooltip("Seconds of black before the Game Over screen [TUNE]")]
        [SerializeField] float holdSeconds = 0.5f;

        ShiftServices shift;

        public override DeathCause Cause { get { return DeathCause.MonsterKill; } }

        public override void Init(ShiftServices services) {
            shift = services;
        }

        public override IEnumerator Present(DeathReport report) {
            shift.Fade.Set(1f);
            yield return new WaitForSeconds(holdSeconds);
        }
    }
}
