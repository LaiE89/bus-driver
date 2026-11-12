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
    // plays its own jumpscare over the shoulder.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class GazerRush : MonoBehaviour, IMonsterPart {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        GazerRushConfig config;
        float looked;
        bool rushing;
        bool killing;
        bool seatHeld;
        bool motionHeld;
        float stride;
        Transform trackedCamera;
        bool shaking;
        const float ShakeMetres = 0.012f;
        const float ShakeHz = 14f;
        // On foot the camera sees most of the aisle. The shake only starts when the gaze is on the gazer.
        const float DirectLookDegrees = 12f;

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
                StopShake();
                return;
            }
            if (!brain.IsActive || shift.Director.State != ShiftState.Driving) {
                ReleaseHead();
                StopShake();
                return;
            }
            TrackCamera();
            TickShake();
            if (shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot) {
                if (!rushing) {
                    looked = 0f;
                }
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
                stride = 0f;
                HoldSeat();
                HoldMotion();
            }
        }

        void HoldSeat() {
            if (seatHeld || shift.Mode == null) {
                return;
            }
            shift.Mode.LockSeat();
            seatHeld = true;
        }

        void ReleaseSeat() {
            if (!seatHeld || shift == null || shift.Mode == null) {
                seatHeld = false;
                return;
            }
            shift.Mode.UnlockSeat();
            seatHeld = false;
        }

        void HoldMotion() {
            if (motionHeld || shift == null || shift.Cabin == null) {
                return;
            }
            shift.Cabin.HoldMotion();
            motionHeld = true;
        }

        void ReleaseMotion() {
            if (!motionHeld || shift == null || shift.Cabin == null) {
                motionHeld = false;
                return;
            }
            shift.Cabin.ReleaseMotion();
            motionHeld = false;
        }

        void OnDestroy() {
            StopShake();
            ReleaseSeat();
            ReleaseMotion();
        }

        // The cabin camera shakes it whenever that camera can see it. On foot it shakes only while
        // the player is looking straight at it. The seated driver view does not.
        void TickShake() {
            Transform view = passenger.View != null ? passenger.View.transform : null;
            if (view == null || !Watched()) {
                StopShake();
                return;
            }
            shaking = true;
            float t = Time.time * ShakeHz;
            view.localPosition = new Vector3(Mathf.Sin(t * 1.7f) * ShakeMetres, Mathf.Sin(t * 2.3f) * ShakeMetres * 0.65f, 0f);
        }

        void StopShake() {
            if (!shaking || passenger == null || passenger.View == null) {
                shaking = false;
                return;
            }
            passenger.View.transform.localPosition = Vector3.zero;
            shaking = false;
        }

        bool Watched() {
            if (shift.Attention == null) {
                return false;
            }
            ObserverKinds seen = shift.Attention.ObservedBy(passenger);
            if (shift.Cctv != null && shift.Cctv.IsViewingCCTV && (seen & ObserverKinds.Cctv) != 0) {
                return true;
            }
            return shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot
                && (seen & ObserverKinds.OnFoot) != 0 && LookingDirectly();
        }

        bool LookingDirectly() {
            Camera camera = shift.OnFootCamera;
            if (camera == null) {
                return false;
            }
            Vector3 from = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            return WithinGaze(from, forward, passenger.Head != null ? passenger.Head.position : passenger.transform.position)
                || WithinGaze(from, forward, passenger.transform.position);
        }

        static bool WithinGaze(Vector3 from, Vector3 forward, Vector3 point) {
            Vector3 to = point - from;
            if (to.sqrMagnitude < 1e-6f) {
                return true;
            }
            return Vector3.Angle(forward, to) <= DirectLookDegrees;
        }

        // While a cabin camera can see it, the head turns to that camera. Looking away lets it drop.
        void TrackCamera() {
            if (passenger.View == null) {
                return;
            }
            bool seen = shift.Cctv != null && shift.Cctv.IsViewingCCTV && shift.Attention != null
                && (shift.Attention.ObservedBy(passenger) & ObserverKinds.Cctv) != 0;
            Camera camera = seen ? shift.Cctv.ActiveCamera : null;
            if (camera == null) {
                ReleaseHead();
                return;
            }
            bool switched = trackedCamera != camera.transform;
            trackedCamera = camera.transform;
            passenger.View.SetLookAt(trackedCamera, 1f);
            passenger.View.SetTell(TellId.HeadTrack, 1f);
            if (switched) {
                passenger.View.PlayReaction(ReactionId.TurnToCamera);
            }
        }

        void ReleaseHead() {
            if (trackedCamera == null || passenger == null || passenger.View == null) {
                trackedCamera = null;
                return;
            }
            trackedCamera = null;
            passenger.View.SetLookAt(null, 0f);
            passenger.View.SetTell(TellId.HeadTrack, 0f);
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
            bool reached = AisleRush.Step(passenger, shift.Cabin, config.rushMetresPerSecond, config.reachMetres, ref stride);
            MonsterSounds.PlayTravel(shift, SoundIds.MonGazerSprint, passenger.transform, ref stride);
            if (reached) {
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
                ReleaseSeat();
                ReleaseMotion();
                return;
            }
            StartCoroutine(PlayKill());
        }

        IEnumerator PlayKill() {
            ReleaseHead();
            ScareDefinition source = brain != null && brain.Definition != null ? brain.Definition.killScare : null;
            ScareDefinition scare = MonsterSounds.WithKillSound(source, SoundIds.MonGazerKill);
            try {
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
                    ReleaseSeat();
                    ReleaseMotion();
                }
            } finally {
                if (scare != null && scare != source) {
                    Destroy(scare);
                }
            }
        }
    }
}
