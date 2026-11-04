using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;

namespace BusDriver.Gameplay.Bus {
    // The engine as three crossfaded loops: bus.engine_idle_loop runs for the whole shift,
    // bus.engine_loop (driving) fades in with speed, and bus.engine_throttle_loop (full throttle)
    // fades in with the pedal. The driving and throttle loops are pitched by speed. Also plays
    // bus.handbrake once when a handbrake stop reaches ~0 speed (the PR #5 cue, Appendix A.3).
    public class BusEngineSound : MonoBehaviour {
        [SerializeField] BusController bus;
        [SerializeField] float fullSpeedKmh = 50f;
        [SerializeField] float minPitch = 0.85f;
        [SerializeField] float maxPitch = 1.25f;
        [SerializeField] float pitchSmooth = 1.5f;

        [Header("Layers")]
        [Tooltip("The driving loop reaches full volume at this speed")]
        [SerializeField] float driveFullKmh = 12f;
        [Tooltip("How fast the throttle layer follows the pedal, per second")]
        [SerializeField] float throttleFade = 3f;
        [Tooltip("How much the idle loop ducks under the other two")]
        [Range(0f, 1f)] [SerializeField] float idleDuck = 0.75f;

        [Header("Handbrake cue")]
        [Tooltip("Treat the bus as stopped at or below this speed")]
        [SerializeField] float stoppedKmh = 0.15f;

        IAudioService audio;
        ShiftDirector director;
        SoundHandle idle;
        SoundHandle engine;
        SoundHandle throttle;
        float pitch;
        float throttleMix;
        bool brakePlayed;
        bool handbrakeUsedWhileMoving;

        // The driving loop
        public bool IsEngineLoopPlaying { get { return audio != null && audio.IsPlaying(engine); } }
        public float EnginePitch { get { return pitch; } }

        public void Init(ShiftServices shift) {
            audio = shift.Game.Audio;
            director = shift.Director;
        }

        // The engine runs from the intro to the end of the drive: silent in the depot, on the
        // summary and from the moment a death starts (so the kill sting has the cabin to itself)
        bool EngineOn {
            get {
                if (director == null) {
                    return true;
                }
                return director.State == ShiftState.Intro || director.State == ShiftState.Driving;
            }
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

            if (!EngineOn) {
                StopAll();
                return;
            }

            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeedKmh));
            pitch = Mathf.MoveTowards(pitch, Mathf.Lerp(minPitch, maxPitch, speed01), Time.deltaTime * pitchSmooth);

            bool running = !bus.IsParked && speed > 0.05f;
            float drive = running ? Mathf.Clamp01(speed / Mathf.Max(0.01f, driveFullKmh)) : 0f;
            float pedal = bus.IsParked ? 0f : bus.Throttle;
            throttleMix = Mathf.MoveTowards(throttleMix, pedal, Time.deltaTime * throttleFade);

            Layer(ref idle, SoundIds.BusEngineIdleLoop, 1f - idleDuck * Mathf.Max(drive, throttleMix), 1f, true);
            Layer(ref engine, SoundIds.BusEngineLoop, drive * (1f - throttleMix), pitch, running);
            Layer(ref throttle, SoundIds.BusEngineThrottleLoop, throttleMix, pitch, throttleMix > 0.01f);
        }

        // Starts the loop while `on`, stops it otherwise, and sets its volume and pitch
        void Layer(ref SoundHandle handle, string id, float volume, float layerPitch, bool on) {
            if (!on) {
                StopLayer(ref handle);
                return;
            }
            if (!audio.IsPlaying(handle)) {
                handle = audio.Play(id);
            }
            audio.SetVolume(handle, volume);
            audio.SetPitch(handle, layerPitch);
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

        void StopLayer(ref SoundHandle handle) {
            if (audio != null && !handle.IsNone) {
                audio.Stop(handle);
            }
            handle = SoundHandle.None;
        }

        void OnDisable() {
            StopAll();
        }

        void StopAll() {
            StopLayer(ref idle);
            StopLayer(ref engine);
            StopLayer(ref throttle);
            throttleMix = 0f;
        }
    }
}
