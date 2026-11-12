using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // It boards and sits like anyone else. Every interval, while the driver is in the seat and not
    // on a camera, it removes the nearest seated human, cancels their stop request, and takes their
    // seat and their look. The interval does not advance on foot or while a camera is up. Once it has
    // taken someone and the interval comes due with no human left aboard, it sprints the aisle and
    // kills over the shoulder. Kicking it off stops it.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class MimicHunt : MonoBehaviour, IMonsterPart {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        MimicCopyConfig config;
        float timer;
        int taken;
        bool rushing;
        bool killing;
        bool seatHeld;
        bool motionHeld;
        float stride;

        public int Taken { get { return taken; } }
        public bool IsRushing { get { return rushing; } }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition.Ability<MimicCopyConfig>() ?? new MimicCopyConfig();
        }

        void Update() {
            if (shift == null || brain == null || passenger == null || killing) {
                return;
            }
            if (!brain.IsActive || shift.Director == null || shift.Director.State != ShiftState.Driving) {
                return;
            }
            if (passenger.WasKicked) {
                return;
            }
            if (rushing) {
                if (OnFoot() || passenger.State != PassengerState.Seated) {
                    return;
                }
                bool reached = AisleRush.Step(passenger, shift.Cabin, RushSpeed(), Reach(), ref stride);
                MonsterSounds.PlayTravel(shift, SoundIds.MonMimicSprint, passenger.transform, ref stride);
                if (reached) {
                    KillDriver();
                }
                return;
            }
            if (passenger.State != PassengerState.Seated || Paused()) {
                return;
            }
            timer += Time.deltaTime;
            if (timer < Interval()) {
                return;
            }
            if (TryTakeHuman()) {
                timer = 0f;
                return;
            }
            if (HumanStillAboard()) {
                return;
            }
            if (taken >= 1 && !OnFoot()) {
                rushing = true;
                stride = 0f;
                HoldSeat();
                HoldMotion();
            }
            timer = 0f;
        }

        bool OnFoot() {
            return shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot;
        }

        bool Paused() {
            return OnFoot() || (shift.Cctv != null && shift.Cctv.IsViewingCCTV);
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

        bool TryTakeHuman() {
            if (shift.Riders == null) {
                return false;
            }
            List<RiderRecord> seated = shift.Riders.SeatedNormals();
            if (seated.Count == 0) {
                return false;
            }
            RiderRecord victim = NearestHuman(seated);
            Passenger body = victim != null ? victim.Passenger : null;
            if (body == null) {
                return false;
            }
            string lookId = body.LookId;
            if (string.IsNullOrEmpty(lookId)) {
                lookId = victim.Spec.lookId;
            }
            BusSeat seat = body.Seat;
            // At the body, not the head, so the take is heard in that seat.
            SoundHandle take = MonsterSounds.Play(shift, SoundIds.MonMimicTake, body.transform);
            if (shift.Game != null && shift.Game.Audio != null) {
                shift.Game.Audio.SetVolume(take, 1.6f);
            }
            body.VanishFromBus();
            TakeLook(lookId);
            TakeSeat(seat);
            taken++;
            return true;
        }

        RiderRecord NearestHuman(List<RiderRecord> seated) {
            RiderRecord nearest = null;
            float best = float.MaxValue;
            Vector3 from = passenger != null ? passenger.transform.position : Vector3.zero;
            for (int i = 0; i < seated.Count; i++) {
                Passenger body = seated[i].Passenger;
                if (body == null) {
                    continue;
                }
                float distance = (body.transform.position - from).sqrMagnitude;
                if (distance < best) {
                    best = distance;
                    nearest = seated[i];
                }
            }
            return nearest;
        }

        void TakeLook(string lookId) {
            if (shift.Views == null || passenger == null) {
                return;
            }
            shift.Views.Recreate(passenger, shift.Views.Look(lookId));
        }

        // The victim's seat is theirs now, even if it still lists them for this frame.
        void TakeSeat(BusSeat seat) {
            if (seat == null || passenger == null) {
                return;
            }
            if (seat.Occupant != null && seat.Occupant != passenger) {
                seat.Release(seat.Occupant);
            }
            passenger.MoveToSeat(seat);
        }

        bool HumanStillAboard() {
            if (shift.Riders == null) {
                return false;
            }
            IReadOnlyList<RiderRecord> aboard = shift.Riders.Aboard;
            for (int i = 0; i < aboard.Count; i++) {
                RiderRecord record = aboard[i];
                if (record.IsMonster || record.Passenger == null || record.Passenger == passenger) {
                    continue;
                }
                PassengerState state = record.Passenger.State;
                if (state != PassengerState.Leaving && state != PassengerState.Gone) {
                    return true;
                }
            }
            return false;
        }

        void KillDriver() {
            if (killing) {
                return;
            }
            killing = true;
            if (shift.Death == null || shift.Death.PreventedBy(DeathCause.MonsterKill, MonsterIds.Mimic) != null) {
                killing = false;
                rushing = false;
                ReleaseSeat();
                ReleaseMotion();
                return;
            }
            StartCoroutine(PlayKill());
        }

        IEnumerator PlayKill() {
            ScareDefinition source = ShoulderScare();
            ScareDefinition scare = MonsterSounds.WithKillSound(source, SoundIds.MonMimicKill);
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
                if (!shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Mimic)) {
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

        ScareDefinition ShoulderScare() {
            MonsterDefinition gazer = shift.Monsters != null ? shift.Monsters.Definition(MonsterIds.Gazer) : null;
            if (gazer != null && gazer.killScare != null) {
                return gazer.killScare;
            }
            return brain != null && brain.Definition != null ? brain.Definition.killScare : null;
        }

        float Interval() {
            return config != null && config.replaceIntervalSeconds > 0f ? config.replaceIntervalSeconds : 30f;
        }

        float RushSpeed() {
            return 8f;
        }

        float Reach() {
            return 0.9f;
        }
    }
}
