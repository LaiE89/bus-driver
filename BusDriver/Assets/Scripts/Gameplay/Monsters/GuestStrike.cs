using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Letting it on does nothing by itself, and kicking it off does not kill. Talking to it while
    // it sits plays its kill scare in front of the driver view. Opening the doors at the night's
    // last stop, while it is already seated, makes it sprint the aisle and kill over the shoulder.
    // That sprint, like the others, holds every other rider still.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class GuestStrike : MonoBehaviour, IMonsterPart, IChatHandler {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        GuestStrikeConfig config;
        bool rushing;
        bool striking;
        bool seatHeld;
        bool motionHeld;
        bool listening;
        float stride;
        Transform tweakAnchor;
        float tweakUntil;
        float nextTweak = -1f;

        public bool IsRushing { get { return rushing; } }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition != null ? owner.Definition.Ability<GuestStrikeConfig>() : null;
            if (!listening && shift.Cabin != null) {
                shift.Cabin.BeforeStopExchange += OnStopExchange;
                listening = true;
            }
        }

        public bool OnChat(Passenger rider) {
            if (rider == null || rider.WasKicked || rider.State != PassengerState.Seated) {
                return false;
            }
            rushing = false;
            BeginKill(false);
            return true;
        }

        void Update() {
            if (shift == null || brain == null || passenger == null || striking) {
                return;
            }
            if (!brain.IsActive || shift.Director == null || shift.Director.State != ShiftState.Driving) {
                return;
            }
            if (passenger.WasKicked) {
                return;
            }
            if (rushing) {
                ClearTweak();
                if (OnFoot() || passenger.State != PassengerState.Seated) {
                    return;
                }
                bool reached = AisleRush.Step(passenger, shift.Cabin, Speed(), Reach(), ref stride);
                MonsterSounds.PlayTravel(shift, SoundIds.MonGuestSprint, passenger.transform, ref stride);
                if (reached) {
                    BeginKill(true);
                }
                return;
            }
            if (passenger.State == PassengerState.Seated && !OnFoot() && DoorsOpenAtEnd()) {
                BeginRush();
                return;
            }
            TickTweak();
        }

        // The driver opened the doors. At the last stop this starts before anyone else stands up.
        void OnStopExchange(BusStop stop) {
            if (rushing || striking || passenger == null || passenger.WasKicked || passenger.State != PassengerState.Seated || OnFoot()) {
                return;
            }
            if (shift == null || shift.Progress == null || shift.Progress.EndStop == null || stop == null) {
                return;
            }
            if (stop.StopId != shift.Progress.EndStop.StopId) {
                return;
            }
            BeginRush();
        }

        void BeginRush() {
            if (rushing || striking) {
                return;
            }
            ClearTweak();
            rushing = true;
            stride = 0f;
            HoldSeat();
            HoldMotion();
        }

        // A short jerk to one side, then back. The gap between jerks is several seconds.
        void TickTweak() {
            if (passenger.State != PassengerState.Seated || passenger.View == null) {
                return;
            }
            if (tweakUntil > 0f) {
                if (Time.time < tweakUntil) {
                    return;
                }
                ClearTweak();
            }
            if (nextTweak < 0f) {
                nextTweak = Time.time + 5f;
                return;
            }
            if (Time.time < nextTweak) {
                return;
            }
            if (tweakAnchor == null) {
                GameObject point = new GameObject("GuestTweak");
                tweakAnchor = point.transform;
                tweakAnchor.SetParent(passenger.transform, false);
            }
            System.Random rng = shift.Rng != null ? shift.Rng.Get(RngStreams.Monster) : null;
            float side = rng != null && rng.Next(2) == 0 ? -1f : 1f;
            float reach = 0.35f + (rng != null ? (float)rng.NextDouble() * 0.4f : 0.4f);
            tweakAnchor.localPosition = new Vector3(side * reach, 0.4f, 0.5f);
            passenger.View.SetLookAt(tweakAnchor, 1f);
            passenger.View.SetTell(TellId.HeadTrack, 1f);
            passenger.View.PlayReaction(ReactionId.TurnToCamera);
            tweakUntil = Time.time + 0.7f;
            nextTweak = Time.time + (rng != null ? 6f + (float)rng.NextDouble() * 8f : 10f);
            Transform at = passenger.Head != null ? passenger.Head : passenger.transform;
            MonsterSounds.Play(shift, SoundIds.MonGuestTweak, at);
        }

        void ClearTweak() {
            tweakUntil = 0f;
            if (passenger != null && passenger.View != null) {
                passenger.View.SetLookAt(null, 0f);
                passenger.View.SetTell(TellId.HeadTrack, 0f);
            }
        }

        bool OnFoot() {
            return shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot;
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
            if (listening && shift != null && shift.Cabin != null) {
                shift.Cabin.BeforeStopExchange -= OnStopExchange;
            }
            ReleaseSeat();
            ReleaseMotion();
        }

        bool AtLastStop() {
            if (shift.Progress == null || shift.Progress.EndStop == null || shift.Cabin == null || shift.Cabin.CurrentStop == null) {
                return false;
            }
            return shift.Cabin.CurrentStop.StopId == shift.Progress.EndStop.StopId;
        }

        bool DoorsOpenAtEnd() {
            return AtLastStop() && shift.Cabin.Doors != null && shift.Cabin.Doors.OpenedByDriver;
        }

        void BeginKill(bool shoulder) {
            if (striking) {
                return;
            }
            if (shift == null || shift.Death == null) {
                return;
            }
            if (shift.Death.PreventedBy(DeathCause.MonsterKill, MonsterIds.Guest) != null) {
                rushing = false;
                ReleaseSeat();
                ReleaseMotion();
                return;
            }
            striking = true;
            StartCoroutine(PlayKill(shoulder));
        }

        IEnumerator PlayKill(bool shoulder) {
            ClearTweak();
            ScareDefinition source = shoulder ? ShoulderScare() : FrontScare();
            ScareDefinition scare = MonsterSounds.WithKillSound(source, SoundIds.MonGuestKill);
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
                if (shift != null && shift.Death != null && shift.Director.State == ShiftState.Driving) {
                    shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Guest);
                }
            } finally {
                if (scare != null && scare != source) {
                    Destroy(scare);
                }
            }
        }

        ScareDefinition FrontScare() {
            return brain != null && brain.Definition != null ? brain.Definition.killScare : null;
        }

        ScareDefinition ShoulderScare() {
            MonsterDefinition gazer = shift.Monsters != null ? shift.Monsters.Definition(MonsterIds.Gazer) : null;
            if (gazer != null && gazer.killScare != null) {
                return gazer.killScare;
            }
            return FrontScare();
        }

        float Speed() {
            return config != null && config.rushMetresPerSecond > 0.01f ? config.rushMetresPerSecond : 8f;
        }

        float Reach() {
            return config != null && config.reachMetres > 0.01f ? config.reachMetres : 0.9f;
        }
    }
}
