using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Bus {
    // Drives the engine loop (bus.engine_loop) from bus speed, and plays bus.handbrake once when a
    // handbrake stop reaches ~0 speed (the PR #5 cue, Appendix A.3).
    public class BusEngineSound : MonoBehaviour {
        [SerializeField] BusController bus;
        [SerializeField] float fullSpeedKmh = 50f;
        [SerializeField] float minPitch = 0.85f;
        [SerializeField] float maxPitch = 1.25f;
        [SerializeField] float pitchSmooth = 1.5f;

        [Header("Handbrake cue")]
        [Tooltip("Treat the bus as stopped at or below this speed")]
        [SerializeField] float stoppedKmh = 0.15f;

        IAudioService audio;
        SoundHandle engine;
        float pitch;
        bool brakePlayed;
        bool handbrakeUsedWhileMoving;

        public bool IsEngineLoopPlaying { get { return audio != null && audio.IsPlaying(engine); } }
        public float EnginePitch { get { return pitch; } }

        public void Init(ShiftServices shift) {
            audio = shift.Game.Audio;
        }

        void Awake() {
            if (bus == null) {
                bus = GetComponent<BusController>();
            }
            pitch = minPitch;
        }

        void Update() {
            if (audio == null || bus == null) {
                return;
            }

            float speed = bus.SpeedKmh;
            UpdateBrakeCue(speed);

            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeedKmh));
            pitch = Mathf.MoveTowards(pitch, Mathf.Lerp(minPitch, maxPitch, speed01), Time.deltaTime * pitchSmooth);

            bool running = !bus.IsParked && speed > 0.05f;
            if (running) {
                if (!audio.IsPlaying(engine)) {
                    engine = audio.Play(SoundIds.BusEngineLoop);
                }
                audio.SetPitch(engine, pitch);
            }else {
                StopEngine();
            }
        }

        void UpdateBrakeCue(float speedKmh) {
            if (bus.IsFrozen) {
                return;
            }

            if (speedKmh > stoppedKmh) {
                brakePlayed = false;
                if (bus.IsHandbrake) {
                    handbrakeUsedWhileMoving = true;
                }
                return;
            }

            // Speed is ~0: play once if the handbrake was used during this stop
            if (!brakePlayed && handbrakeUsedWhileMoving) {
                audio.Play(SoundIds.BusHandbrake);
                brakePlayed = true;
                handbrakeUsedWhileMoving = false;
            }
        }

        void StopEngine() {
            if (audio != null && !engine.IsNone) {
                audio.Stop(engine);
            }
            engine = SoundHandle.None;
        }

        void OnDisable() {
            StopEngine();
        }
    }
}
