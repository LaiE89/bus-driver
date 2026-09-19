using UnityEngine;

// Drives the looping "Bus Engine" clip on SoundController from bus speed.
// Volume stays flat (set on the Sound Controller entry); only pitch follows speed.
public class BusEngineSound : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] string soundName = "Bus Engine";
    [SerializeField] float fullSpeedKmh = 50f;
    [SerializeField] float minPitch = 0.85f;
    [SerializeField] float maxPitch = 1.25f;
    [SerializeField] float pitchSmooth = 1.5f;

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

        bool running = !bus.IsParked && bus.SpeedKmh > 0.05f;
        source.volume = running ? baseVolume : 0f;

        float speed01 = Mathf.Clamp01(bus.SpeedKmh / Mathf.Max(0.01f, fullSpeedKmh));
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

    void OnDisable() {
        if (source != null && source.isPlaying) {
            source.Stop();
            source.volume = 0f;
        }
    }
}
