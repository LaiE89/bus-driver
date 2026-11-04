using BusDriver.Core.Data;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // After it sits: looking through the cabin camera counts as looking at it, and so does staring
    // at it while on foot. Either one, held long enough, makes it sprint to the driver and kill.
    // The driver view does not count. Looking away clears the timer. Once it runs, it does not stop.
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
            if (shift.Cctv != null && shift.Cctv.IsViewingCCTV) {
                return true;
            }
            if (shift.Attention == null || shift.Attention.Mode != AttentionMode.OnFoot) {
                return false;
            }
            return (shift.Attention.ObservedBy(passenger) & ObserverKinds.OnFoot) != 0;
        }

        void TickRush() {
            if (shift.Mode == null || shift.Cabin == null || passenger.State != PassengerState.Seated) {
                return;
            }
            bool onFoot = shift.Mode.Mode == PlayerMode.OnFoot;
            Vector3 world = onFoot ? shift.Mode.PlayerPosition : shift.Cabin.StandPointWorld;
            Transform space = passenger.transform.parent != null ? passenger.transform.parent : shift.Cabin.transform;
            passenger.RushStep(space.InverseTransformPoint(world), config.rushMetresPerSecond);
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
                return;
            }
            if (!shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Gazer)) {
                killing = false;
                rushing = false;
            }
        }
    }
}
