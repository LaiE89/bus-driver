using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Audio {
    // The looping ambience of a scene (§4.12): starts its loops once the scene is wired and stops
    // them with the scene. AudioService.StopSceneSounds catches anything left on a scene change.
    // A second Bind (a test rebooting GameRoot under a live scene) restarts them on the new service.
    public sealed class SceneAmbience : MonoBehaviour, IGameBindable {
        [SerializeField] string[] loops = { SoundIds.AmbWind };

        IAudioService audio;
        IAudioService playingOn;
        bool started;
        SoundHandle[] handles = new SoundHandle[0];

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
            StopAll();
            playingOn = audio;
            handles = new SoundHandle[loops.Length];
            for (int i = 0; i < loops.Length; i++) {
                handles[i] = audio.Play(loops[i]);
            }
        }

        public bool IsPlaying(string id) {
            for (int i = 0; i < loops.Length && i < handles.Length; i++) {
                if (loops[i] == id) {
                    return playingOn != null && playingOn.IsPlaying(handles[i]);
                }
            }
            return false;
        }

        void StopAll() {
            if (playingOn == null) {
                return;
            }
            for (int i = 0; i < handles.Length; i++) {
                playingOn.Stop(handles[i]);
            }
        }

        void OnDestroy() {
            StopAll();
        }
    }
}
