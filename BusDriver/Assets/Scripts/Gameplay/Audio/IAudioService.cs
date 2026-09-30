using System;
using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Audio {
    // One playing instance. The default value is "no sound"; a handle stays usable until its
    // sound ends, is stopped, or is stolen by the voice limit, and AudioService.IsPlaying says so.
    public readonly struct SoundHandle : IEquatable<SoundHandle> {
        public static readonly SoundHandle None = default(SoundHandle);

        internal readonly int Id;

        internal SoundHandle(int id) {
            Id = id;
        }

        public bool IsNone { get { return Id == 0; } }

        public bool Equals(SoundHandle other) {
            return Id == other.Id;
        }

        public override bool Equals(object obj) {
            return obj is SoundHandle && Equals((SoundHandle)obj);
        }

        public override int GetHashCode() {
            return Id;
        }
    }

    // Playback by sound id (§4.6, §4.12). An interface so tests can pass a fake (§4.5).
    public interface IAudioService {
        SoundHandle Play(string id);
        SoundHandle PlayAt(string id, Vector3 position);
        // Follows the transform, and ends when it's destroyed
        SoundHandle PlayAttached(string id, Transform target);
        void Stop(SoundHandle handle);
        bool IsPlaying(SoundHandle handle);
        // Multiplies the definition's volume (0–1)
        void SetVolume(SoundHandle handle, float volume01);
        void SetPitch(SoundHandle handle, float pitch);
        // −1 left … 1 right (the hard-panned whisper, §2.11)
        void SetPan(SoundHandle handle, float pan);
        void SetSnapshot(AudioSnapshot snapshot, float fadeSeconds);
        void SetGroupVolume(AudioGroup group, float linear);
        void SetMasterVolume(float linear);
        // Everything except UI stops on every scene change (§4.3)
        void StopSceneSounds();
        // A definition with a caption started playing (CaptionView listens)
        event Action<string> OnCaption;
    }
}
