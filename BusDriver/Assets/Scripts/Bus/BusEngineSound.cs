using UnityEngine;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Screens;

namespace BusDriver.Gameplay.Bus {
    // Drives the looping "Bus Engine" clip from bus speed, and plays "Brake" once
    // when a handbrake stop reaches ~0 speed.
    public class BusEngineSound : MonoBehaviour {
        [SerializeField] BusController bus;
        [SerializeField] string soundName = "Bus Engine";
        [SerializeField] float fullSpeedKmh = 50f;
        [SerializeField] float minPitch = 0.85f;
        [SerializeField] float maxPitch = 1.25f;
        [SerializeField] float pitchSmooth = 1.5f;

        [Header("Handbrake cue")]
        [SerializeField] string brakeSound = "Brake";
        [Tooltip("Treat the bus as stopped at or below this speed")]
        [SerializeField] float stoppedKmh = 0.15f;

        SoundController sounds;
        AudioSource source;
        float baseVolume = 1f;
        bool wasPaused;
        bool brakePlayed;
        bool handbrakeUsedWhileMoving;

        void Start() {
            if (bus == null) {
                bus = GetComponent<BusController>();
            }
            if (SceneController.Instance != null) {
                sounds = SceneController.Instance.soundController;
            }
            if (sounds == null) {
                sounds = FindAnyObjectByType<SoundController>();
            }
            if (sounds == null) {
                Debug.LogWarning("BusEngineSound: no SoundController found");
                enabled = false;
                return;
            }
            source = sounds.GetSound(soundName);
            if (source == null) {
                enabled = false;
                return;
            }
            baseVolume = source.volume;
            source.loop = true;
            source.volume = 0f;
            source.pitch = minPitch;
            source.Stop();
        }

        void Update() {
            if (source == null || bus == null) {
                return;
            }

            if (ingameMenus.pausedGame) {
                wasPaused = true;
                return;
            }
            if (wasPaused) {
                wasPaused = false;
                if (!source.isPlaying) {
                    source.Play();
                }else {
                    source.UnPause();
                }
            }

            float speed = bus.SpeedKmh;
            UpdateBrakeCue(speed);

            bool running = !bus.IsParked && speed > 0.05f;
            source.volume = running ? baseVolume : 0f;

            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeedKmh));
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, speed01);
            source.pitch = Mathf.MoveTowards(source.pitch, targetPitch, Time.deltaTime * pitchSmooth);

            if (running) {
                if (!source.isPlaying) {
                    source.Play();
                }
            }else if (source.isPlaying) {
                source.Stop();
            }
        }

        void UpdateBrakeCue(float speedKmh) {
            if (sounds == null || string.IsNullOrEmpty(brakeSound) || bus.IsFrozen) {
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
                sounds.PlayOneShot(brakeSound);
                brakePlayed = true;
                handbrakeUsedWhileMoving = false;
            }
        }

        void OnDisable() {
            if (source != null && source.isPlaying) {
                source.Stop();
                source.volume = 0f;
            }
        }
    }
}
