using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Main-branch Weeping Angel behaviour on the MonsterBrain prefab: after a 20–40 s sit delay it
    // walks seat → aisle → toward the driver (or the player on foot) only while unwatched, and
    // kills at close range. The only addition is playing killScare before DeathDirector.Die.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class AngelStalk : MonoBehaviour, IMonsterPart {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        AngelStalkConfig config;

        bool canHunt;
        bool hunting;
        bool killing;
        float floorLocalY;
        float huntReadyAt;

        public bool IsHunting { get { return hunting; } }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition.Ability<AngelStalkConfig>() ?? new AngelStalkConfig();
            passenger.Seated += HandleSeated;
        }

        void OnDestroy() {
            if (passenger != null) {
                passenger.Seated -= HandleSeated;
            }
        }

        void HandleSeated(Passenger rider) {
            float low = Mathf.Min(config.minHuntDelay, config.maxHuntDelay);
            float high = Mathf.Max(config.minHuntDelay, config.maxHuntDelay);
            float delay = shift != null
                ? low + (float)shift.Rng.Get(RngStreams.Monster).NextDouble() * (high - low)
                : high;
            huntReadyAt = Time.time + delay;
            canHunt = true;
            BusCabin cabin = passenger.Cabin;
            floorLocalY = cabin != null ? cabin.AisleAtDoorLocal.y : passenger.transform.localPosition.y;
        }

        void Update() {
            if (shift == null || brain == null || killing) {
                return;
            }
            if (shift.Death != null && shift.Death.IsDying) {
                return;
            }
            if (shift.Director.State != ShiftState.Driving) {
                return;
            }
            if (passenger.WasKicked || passenger.State == PassengerState.Leaving || passenger.State == PassengerState.Gone) {
                hunting = false;
                canHunt = false;
                return;
            }
            if (!canHunt || Time.time < huntReadyAt || !passenger.IsAboard || passenger.Cabin == null) {
                return;
            }
            if (passenger.State != PassengerState.Seated) {
                return;
            }

            Camera watcher = ActiveWatcher();
            if (watcher == null || IsBeingWatched(watcher)) {
                return;
            }

            Move(Time.deltaTime);
        }

        // Prefer the head (what the player actually looks at). Feet can leave the frustum when the
        // player is close on foot even while staring at the body.
        bool IsBeingWatched(Camera watcher) {
            if (InViewport(watcher, passenger.Head != null ? passenger.Head.position : passenger.transform.position)) {
                return true;
            }
            if (passenger.Head != null && passenger.Head != passenger.transform) {
                return InViewport(watcher, passenger.transform.position);
            }
            return false;
        }

        static bool InViewport(Camera watcher, Vector3 world) {
            if (watcher == null || !watcher.enabled) {
                return false;
            }
            Vector3 viewport = watcher.WorldToViewportPoint(world);
            return viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        }

        Camera ActiveWatcher() {
            if (shift.Cctv != null && shift.Cctv.ActiveCamera != null) {
                return shift.Cctv.ActiveCamera;
            }
            return shift.DriverCamera;
        }

        void Move(float deltaTime) {
            if (!hunting) {
                BeginHunt();
            }

            Vector3 goal = NextWaypointLocal();
            Vector3 current = passenger.transform.localPosition;
            goal.y = floorLocalY;
            Vector3 flat = goal - current;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) {
                if (Mathf.Abs(current.y - floorLocalY) > 0.001f) {
                    current.y = floorLocalY;
                    passenger.transform.localPosition = current;
                }
                TryKillPlayer();
                return;
            }

            Face(flat);
            passenger.transform.localPosition = Vector3.MoveTowards(current, goal, config.walkSpeed * deltaTime);
            TryKillPlayer();
        }

        void TryKillPlayer() {
            if (!hunting || killing || shift == null || shift.Death == null || shift.Death.IsDying) {
                return;
            }
            Vector3 current = passenger.transform.localPosition;
            Vector3 target = HuntTargetLocal();
            Vector3 flat = target - current;
            flat.y = 0f;
            if (flat.magnitude <= config.killDistance) {
                StartCoroutine(KillPlayer());
            }
        }

        IEnumerator KillPlayer() {
            killing = true;
            canHunt = false;
            Log.Info(LogCat.Threat, "weeping_angel: proximity kill");
            ScareDefinition scare = brain.Definition != null ? brain.Definition.killScare : null;
            if (scare != null && shift.Scares != null) {
                ScareHandle handle;
                if (shift.Scares.Request(scare, ScareContext.For(brain), out handle)) {
                    while (!handle.IsNone && shift.ScarePlayer != null && shift.ScarePlayer.IsRunning(handle)) {
                        yield return null;
                    }
                }
            }
            if (shift.Death != null && !shift.Death.IsDying) {
                shift.Death.Die(DeathCause.MonsterKill, MonsterIds.WeepingAngel);
            }
        }

        void BeginHunt() {
            hunting = true;
            BusCabin cabin = passenger.Cabin;
            floorLocalY = cabin.AisleAtDoorLocal.y;
            Vector3 local = passenger.transform.localPosition;
            local.y = floorLocalY;
            passenger.StandAt(local, passenger.transform.localRotation);
            if (cabin.PassengerRoot != null && passenger.transform.parent != cabin.PassengerRoot) {
                passenger.transform.SetParent(cabin.PassengerRoot, true);
            }
            Log.Verbose(LogCat.Threat, "weeping_angel: hunt started");
        }

        // Waypoints: leave the row into the aisle, walk the aisle to the target Z, then step in.
        Vector3 NextWaypointLocal() {
            BusCabin cabin = passenger.Cabin;
            Vector3 aisle = cabin.AisleAtDoorLocal;
            Vector3 current = passenger.transform.localPosition;
            Vector3 target = HuntTargetLocal();

            Vector3 intoAisle = new Vector3(aisle.x, floorLocalY, current.z);
            if (Mathf.Abs(current.x - aisle.x) > config.aisleReach) {
                return intoAisle;
            }

            Vector3 alongAisle = new Vector3(aisle.x, floorLocalY, target.z);
            if (Mathf.Abs(current.z - target.z) > config.aisleReach) {
                return alongAisle;
            }

            Vector3 toTarget = new Vector3(target.x, floorLocalY, target.z);
            Vector3 flat = toTarget - current;
            flat.y = 0f;
            if (flat.magnitude <= config.stopDistance) {
                return new Vector3(current.x, floorLocalY, current.z);
            }
            return toTarget;
        }

        // Driving: path to the stand point behind the driver. On foot: chase the player body.
        Vector3 HuntTargetLocal() {
            BusCabin cabin = passenger.Cabin;
            if (shift != null && shift.Mode != null && shift.Mode.Mode == PlayerMode.Driving && cabin != null) {
                return cabin.StandPointLocal;
            }
            return PlayerLocalPosition();
        }

        Vector3 PlayerLocalPosition() {
            BusCabin cabin = passenger.Cabin;
            Vector3 world = shift != null && shift.Mode != null
                ? shift.Mode.PlayerPosition
                : passenger.transform.position;
            if (cabin.PassengerRoot != null) {
                return cabin.PassengerRoot.InverseTransformPoint(world);
            }
            return cabin.transform.InverseTransformPoint(world);
        }

        void Face(Vector3 localDirection) {
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude < 0.0001f) {
                return;
            }
            Quaternion look = Quaternion.LookRotation(localDirection);
            passenger.transform.localRotation = Quaternion.RotateTowards(
                passenger.transform.localRotation, look, 360f * Time.deltaTime);
        }

        // Tests: skip the sit delay and start hunting on the next unwatched frame
        internal void ForceHuntReady() {
            canHunt = true;
            huntReadyAt = 0f;
        }
    }
}
