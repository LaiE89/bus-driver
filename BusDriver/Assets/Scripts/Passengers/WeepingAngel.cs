using UnityEngine;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;

namespace BusDriver.Gameplay.Monsters {
    // Moves only when unwatched, and only after it has sat down once.
    // Path is seat → aisle → along aisle → toward the player, so it never cuts through benches.
    public class WeepingAngel : Monster {
        [SerializeField] float moveSpeed = 0.55f;
        [SerializeField] float aisleReach = 0.08f;
        [SerializeField] float stopDistance = 0.35f;
        [SerializeField] float killDistance = 0.6f;
        [SerializeField] float minHuntDelay = 20f;
        [SerializeField] float maxHuntDelay = 40f;

        bool canHunt;
        bool hunting;
        float floorLocalY;
        float huntReadyAt;

        protected override void OnSeated() {
            float delay = Random.Range(Mathf.Min(minHuntDelay, maxHuntDelay), Mathf.Max(minHuntDelay, maxHuntDelay));
            huntReadyAt = Time.time + delay;
            canHunt = true;
            floorLocalY = Cabin != null ? Cabin.AisleAtDoorLocal.y : transform.localPosition.y;
        }

        protected override void OnLeaving(bool kicked) {
            hunting = false;
            canHunt = false;
        }

        protected override void Tick(float deltaTime) {
            if (SceneController.Instance != null && SceneController.Instance.IsGameOver) {
                return;
            }
            if (!canHunt || Time.time < huntReadyAt || !IsAboard || Cabin == null) {
                return;
            }
            if (State != PassengerState.Seated) {
                return;
            }

            Camera watcher = Cabin.ViewCamera;
            if (watcher == null || IsBeingWatched(watcher)) {
                return;
            }

            Move(deltaTime);
        }

        // Prefer the head (what the player actually looks at). Feet can leave the frustum
        // when the player is close on foot even while staring at the body.
        bool IsBeingWatched(Camera watcher) {
            if (IsSeenBy(watcher)) {
                return true;
            }
            if (head != null && head != transform) {
                Vector3 viewport = watcher.WorldToViewportPoint(head.position);
                if (viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f) {
                    return true;
                }
            }
            return false;
        }

        void Move(float deltaTime) {
            if (!hunting) {
                BeginHunt();
            }

            Vector3 goal = NextWaypointLocal();
            Vector3 current = transform.localPosition;
            goal.y = floorLocalY;
            Vector3 flat = goal - current;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) {
                // Keep stuck to the floor even when idle at a waypoint
                if (Mathf.Abs(current.y - floorLocalY) > 0.001f) {
                    current.y = floorLocalY;
                    transform.localPosition = current;
                }
                TryKillPlayer();
                return;
            }

            Face(flat);
            transform.localPosition = Vector3.MoveTowards(current, goal, moveSpeed * deltaTime);
            TryKillPlayer();
        }

        void TryKillPlayer() {
            if (!hunting || SceneController.Instance == null || SceneController.Instance.IsGameOver) {
                return;
            }
            Vector3 current = transform.localPosition;
            Vector3 target = HuntTargetLocal();
            Vector3 flat = target - current;
            flat.y = 0f;
            if (flat.magnitude <= killDistance) {
                SceneController.Instance.TriggerGameOver();
            }
        }

        void BeginHunt() {
            hunting = true;
            VacateSeat();
            if (Cabin.PassengerRoot != null) {
                transform.SetParent(Cabin.PassengerRoot, true);
            }
            floorLocalY = Cabin.AisleAtDoorLocal.y;
            Vector3 local = transform.localPosition;
            local.y = floorLocalY;
            transform.localPosition = local;
        }

        // Waypoints: leave the row into the aisle, walk the aisle to the target Z, then step in.
        Vector3 NextWaypointLocal() {
            Vector3 aisle = Cabin.AisleAtDoorLocal;
            Vector3 current = transform.localPosition;
            Vector3 target = HuntTargetLocal();

            Vector3 intoAisle = new Vector3(aisle.x, floorLocalY, current.z);
            if (Mathf.Abs(current.x - aisle.x) > aisleReach) {
                return intoAisle;
            }

            Vector3 alongAisle = new Vector3(aisle.x, floorLocalY, target.z);
            if (Mathf.Abs(current.z - target.z) > aisleReach) {
                return alongAisle;
            }

            Vector3 toTarget = new Vector3(target.x, floorLocalY, target.z);
            Vector3 flat = toTarget - current;
            flat.y = 0f;
            if (flat.magnitude <= stopDistance) {
                return new Vector3(current.x, floorLocalY, current.z);
            }
            return toTarget;
        }

        // Seated: path to the stand point behind the driver so the angel can finish the
        // approach outside the windshield view. On foot: chase the player body.
        Vector3 HuntTargetLocal() {
            if (SceneController.Instance != null
                && SceneController.Instance.Mode == PlayerMode.Driving
                && Cabin != null) {
                return Cabin.StandPointLocal;
            }
            return PlayerLocalPosition();
        }

        Vector3 PlayerLocalPosition() {
            Vector3 world = SceneController.Instance != null
                ? SceneController.Instance.PlayerPosition
                : transform.position;
            if (Cabin.PassengerRoot != null) {
                return Cabin.PassengerRoot.InverseTransformPoint(world);
            }
            return Cabin.transform.InverseTransformPoint(world);
        }

        void Face(Vector3 localDirection) {
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude < 0.0001f) {
                return;
            }
            Quaternion look = Quaternion.LookRotation(localDirection);
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, look, 360f * Time.deltaTime);
        }
    }
}
