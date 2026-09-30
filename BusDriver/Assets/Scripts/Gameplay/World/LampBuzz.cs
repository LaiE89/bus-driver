using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // A lamp's electrical buzz (§2.22 amb.lamp_buzz): a 3D loop on the lamp whose volume follows the
    // lamp's flicker, so the hum drops out with the light. A second Bind (a test rebooting GameRoot
    // under a live scene) restarts it on the new service.
    public sealed class LampBuzz : MonoBehaviour, IGameBindable {
        [SerializeField] LightFlicker flicker;

        IAudioService audio;
        IAudioService playingOn;
        bool started;
        SoundHandle handle;

        public bool IsPlaying { get { return playingOn != null && playingOn.IsPlaying(handle); } }

        public void Bind(GameServices game) {
            audio = game.Audio;
            if (started) {
                Play();
            }
        }

        void Start() {
            started = true;
            Play();
        }

        void Play() {
            if (audio == null || audio == playingOn) {
                return;
            }
            if (playingOn != null) {
                playingOn.Stop(handle);
            }
            playingOn = audio;
            handle = audio.PlayAttached(SoundIds.AmbLampBuzz, transform);
        }

        void Update() {
            if (playingOn != null && flicker != null) {
                playingOn.SetVolume(handle, Mathf.Clamp01(flicker.Multiplier));
            }
        }

        void OnDestroy() {
            if (playingOn != null) {
                playingOn.Stop(handle);
            }
        }
    }
}
