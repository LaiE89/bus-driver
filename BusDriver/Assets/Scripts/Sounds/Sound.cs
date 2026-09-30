using UnityEngine.Audio;
using UnityEngine;

namespace BusDriver.Gameplay.Audio {
    [System.Serializable]
    public class Sound{
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
        [Range(0.1f, 3f)] public float pitch;
        [Range(0f, 1f)] public float spatialBlend;
        public bool loop;
        public bool music;
        // UI sounds keep playing while the listener is paused (§4.11)
        public bool ignoreListenerPause;
        public AudioMixerGroup group;
        [HideInInspector] public AudioSource source;
    }
}
