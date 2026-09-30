using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Audio {
    // The looping ambience of a scene (§4.12): starts its loops once the scene is wired and stops
    // them with the scene. AudioService.StopSceneSounds catches anything left on a scene change.
    public sealed class SceneAmbience : MonoBehaviour, IGameBindable {
        [SerializeField] string[] loops = { SoundIds.AmbWind };

        IAudioService audio;
        SoundHandle[] handles = new SoundHandle[0];

        public void Bind(GameServices game) {
            audio = game.Audio;
        }

        void Start() {
            if (audio == null) {
                return;
            }
            handles = new SoundHandle[loops.Length];
            for (int i = 0; i < loops.Length; i++) {
                handles[i] = audio.Play(loops[i]);
            }
        }

        public bool IsPlaying(string id) {
            for (int i = 0; i < loops.Length && i < handles.Length; i++) {
                if (loops[i] == id) {
                    return audio != null && audio.IsPlaying(handles[i]);
                }
            }
            return false;
        }

        void OnDestroy() {
            if (audio == null) {
                return;
            }
            for (int i = 0; i < handles.Length; i++) {
                audio.Stop(handles[i]);
            }
        }
    }
}
