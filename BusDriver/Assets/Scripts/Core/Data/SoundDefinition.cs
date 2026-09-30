using UnityEngine;

namespace BusDriver.Core.Data {
    // One sound, by id (§4.8, §4.12). The audio engineers replace a placeholder by assigning clips
    // and unticking `placeholder`; PlaceholderAudioBuilder never touches a definition after that.
    [CreateAssetMenu(menuName = "Bus Driver/Sound Definition", fileName = "Sound")]
    public sealed class SoundDefinition : ScriptableObject {
        [Tooltip("lower_snake_case with dots, e.g. bus.engine_loop (SoundIds holds every id)")]
        public string id = "";
        [Tooltip("One is picked at random per play")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("± this much volume, uniform")]
        [Range(0f, 1f)] public float volumeJitter;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Tooltip("± this much pitch, uniform")]
        [Range(0f, 1f)] public float pitchJitter;
        public AudioGroup group = AudioGroup.SfxWorld;
        public SoundSpatial spatial = SoundSpatial.TwoD;
        [Min(0f)] public float minDistance = 1f;
        [Min(0f)] public float maxDistance = 30f;
        public bool loop;
        [Tooltip("At this many instances the oldest is stolen")]
        [Min(1)] public int maxVoices = 4;
        [Tooltip("AudioSource priority: 0 is the most important, 256 the least")]
        [Range(0, 256)] public int priority = 128;
        [Tooltip("A repeat within this many seconds is ignored")]
        [Min(0f)] public float cooldown;
        [Tooltip("Shown as a caption when Captions is on, e.g. \"[horn]\"")]
        public string caption = "";
        [Tooltip("True while the clip is a generated tone (PlaceholderAudioBuilder)")]
        public bool placeholder = true;
    }
}
