using UnityEngine;

// Drives the looping "Bus Engine" clip on SoundController from bus speed.
public class BusEngineSound : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] string soundName = "Bus Engine";
    [SerializeField] float audibleFromKmh = 0.5f;
    [SerializeField] float fullSpeedKmh = 50f;
    [SerializeField] float idleVolume = 0.12f;
    [SerializeField] float maxVolumeScale = 1f;
    [SerializeField] float minPitch = 0.75f;
    [SerializeField] float maxPitch = 1.35f;

    AudioSource source;
    float baseVolume = 1f;
    bool wasPaused;

    void Start() {
        if (bus == null) {
            bus = GetComponent<BusController>();
        }
        SoundController sounds = null;
        if (PlayerModeController.Instance != null) {
            sounds = PlayerModeController.Instance.soundController;
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
        if (!source.isPlaying) {
            source.Play();
        }
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
        float speed01 = Mathf.Clamp01(Mathf.InverseLerp(audibleFromKmh, fullSpeedKmh, speed));
        bool moving = speed >= audibleFromKmh && !bus.IsParked;

        float targetVol = moving
            ? Mathf.Lerp(idleVolume, baseVolume * maxVolumeScale, speed01)
            : 0f;
        float targetPitch = moving
            ? Mathf.Lerp(minPitch, maxPitch, speed01)
            : minPitch;

        source.volume = Mathf.MoveTowards(source.volume, targetVol, Time.deltaTime * 2.5f);
        source.pitch = Mathf.MoveTowards(source.pitch, targetPitch, Time.deltaTime * 1.5f);

        if (moving && !source.isPlaying) {
            source.Play();
        }
    }

    void OnDisable() {
        if (source != null && source.isPlaying) {
            source.Stop();
            source.volume = 0f;
        }
    }
}
