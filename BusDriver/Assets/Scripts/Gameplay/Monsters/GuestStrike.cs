using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Letting this rider through the door plays its kill scare, then kills the driver. The face
    // is the Starer's, held in the center of the driver view. Turning it away does not.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class GuestStrike : MonoBehaviour, IMonsterPart, IAcceptHandler {
        ShiftServices shift;
        MonsterBrain brain;
        bool striking;

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
        }

        public bool OnAccept(Passenger passenger) {
            if (striking) {
                return true;
            }
            if (shift == null || shift.Death == null) {
                return false;
            }
            GuestStrikeConfig config = brain != null && brain.Definition != null ? brain.Definition.Ability<GuestStrikeConfig>() : null;
            if (config != null && !config.killOnAccept) {
                return false;
            }
            if (shift.Death.PreventedBy(DeathCause.MonsterKill, MonsterIds.Guest) != null) {
                return false;
            }
            striking = true;
            StartCoroutine(PlayKill());
            return true;
        }

        IEnumerator PlayKill() {
            ScareDefinition scare = brain != null && brain.Definition != null ? brain.Definition.killScare : null;
            if (scare != null && shift.Scares != null && shift.ScarePlayer != null) {
                ScareHandle handle;
                if (shift.Scares.Request(scare, ScareContext.For(brain), out handle)) {
                    while (!handle.IsNone && shift.ScarePlayer.IsRunning(handle)) {
                        if (shift.Director.State != ShiftState.Driving) {
                            yield break;
                        }
                        yield return null;
                    }
                }
            }
            if (shift != null && shift.Death != null && shift.Director.State == ShiftState.Driving) {
                shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Guest);
            }
        }
    }
}
