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
                ReleaseHead();
                return;
            }
            TrackCamera();
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
            ReleaseSeat();
            ReleaseMotion();
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
