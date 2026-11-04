using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // After it sits, looking through the cabin camera long enough makes it sprint up the aisle and kill.
    // The driver view does not count. Looking away clears the timer. Once it runs, it keeps going
    // until the player stands up. While the player is on foot it holds still: no timer, no sprint,
    // no kill. It steps out to the aisle center, then sprints up the middle. Reaching the driver
    // plays the Starer's kill scare.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class GazerRush : MonoBehaviour, IMonsterPart {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        GazerRushConfig config;
        float looked;
        bool rushing;
        bool killing;

        public float LookedSeconds { get { return looked; } }
        public bool IsRushing { get { return rushing; } }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition.Ability<GazerRushConfig>() ?? new GazerRushConfig();
        }

        void Update() {
            if (shift == null || brain == null || passenger == null || killing) {
                return;
            }
            if (!brain.IsActive || shift.Director.State != ShiftState.Driving) {
                return;
            }
            if (shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot) {
                looked = 0f;
                rushing = false;
                return;
            }
            if (rushing) {
                TickRush();
                return;
            }
            if (passenger.State != PassengerState.Seated) {
                looked = 0f;
                return;
            }
            if (IsLooking()) {
                looked += Time.deltaTime;
            } else {
                looked = 0f;
            }
            if (config.lookSeconds > 0f && looked >= config.lookSeconds) {
                rushing = true;
                looked = 0f;
            }
        }

        bool IsLooking() {
            return shift.Cctv != null && shift.Cctv.IsViewingCCTV;
        }

        void TickRush() {
            if (shift.Mode == null || shift.Cabin == null || passenger.State != PassengerState.Seated) {
                return;
            }
            if (shift.Mode.Mode == PlayerMode.OnFoot) {
                return;
            }
            Vector3 world = shift.Cabin.StandPointWorld;
            Transform space = passenger.transform.parent != null ? passenger.transform.parent : shift.Cabin.transform;
            Vector3 target = space.InverseTransformPoint(world);
            Vector3 aisle = space.InverseTransformPoint(shift.Cabin.PassengerRoot.TransformPoint(shift.Cabin.AisleAtDoorLocal));
            Vector3 here = passenger.transform.localPosition;
            // Out of the seat sideways, then along the aisle, then the last step to the driver.
            bool inAisle = Mathf.Abs(here.x - aisle.x) <= 0.2f && Mathf.Abs(here.y - aisle.y) <= 0.25f;
            bool beside = inAisle && Mathf.Abs(here.z - target.z) <= 0.2f;
            Vector3 step = !inAisle
                ? new Vector3(aisle.x, aisle.y, here.z)
                : (beside ? target : new Vector3(aisle.x, aisle.y, target.z));
            passenger.RushStep(step, config.rushMetresPerSecond);
            if (Vector3.Distance(passenger.transform.position, world) <= config.reachMetres) {
                Kill();
            }
        }

        void Kill() {
            if (killing) {
                return;
            }
            killing = true;
            if (shift.Death == null) {
                killing = false;
                rushing = false;
                return;
            }
            if (shift.Death.PreventedBy(DeathCause.MonsterKill, MonsterIds.Gazer) != null) {
                killing = false;
                rushing = false;
                return;
            }
            StartCoroutine(PlayKill());
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
            if (shift == null || shift.Death == null || shift.Director.State != ShiftState.Driving) {
                yield break;
            }
            if (!shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Gazer)) {
                killing = false;
                rushing = false;
            }
        }
    }
}
