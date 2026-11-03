using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // The Whisperer's ability (§2.11): while aboard and seated, drains sanity by stage and plays
    // the four-voices whisper bed. Volume starts near silent at Dormant and eases up as threat
    // climbs through Unsettled → Aggressive → Lethal.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class WhispererDrain : MonoBehaviour, IMonsterPart {
        const float FeedQuietScale = 0.55f;

        ShiftServices shift;
        MonsterBrain brain;
        WhispererDrainConfig config;
        bool draining;
        float volume;
        SoundHandle whisperLoop;
        SoundHandle feedLoop;

        public float CurrentDrainPerSecond { get; private set; }
        public float CurrentWhisperVolume { get { return volume; } }
        public bool WhisperLoopPlaying { get; private set; }
        public bool FeedLoopPlaying { get; private set; }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            config = owner.Definition.Ability<WhispererDrainConfig>() ?? new WhispererDrainConfig();
            volume = StageVolume(ThreatStage.Dormant);
        }

        void OnDestroy() {
            StopAll();
        }

        void Update() {
            if (shift == null || brain == null) {
                return;
            }
            bool active = brain.IsActive
                && shift.Director != null
                && shift.Director.State == ShiftState.Driving;
            if (!active) {
                StopAll();
                return;
            }

            if (shift.Sanity != null) {
                float rate = DrainFor(brain.Stage);
                CurrentDrainPerSecond = rate;
                shift.Sanity.AddDrain(this, -rate);
                draining = rate > 0f;
            }
            UpdateAudio();
        }

        void UpdateAudio() {
            IAudioService audio = shift.Game != null ? shift.Game.Audio : null;
            if (audio == null) {
                WhisperLoopPlaying = false;
                FeedLoopPlaying = false;
                return;
            }

            float target = StageVolume(brain.Stage);
            float lerp = config != null ? Mathf.Max(0.01f, config.whisperVolumeLerpPerSecond) : 0.35f;
            volume = Mathf.MoveTowards(volume, target, lerp * Time.deltaTime);

            if (!audio.IsPlaying(whisperLoop)) {
                whisperLoop = audio.Play(SoundIds.MonWhisperLoop);
            }
            if (!whisperLoop.IsNone) {
                audio.SetVolume(whisperLoop, volume);
            }
            WhisperLoopPlaying = audio.IsPlaying(whisperLoop);

            float feedVolume = brain.IsObserved ? volume : volume * FeedQuietScale;
            if (!audio.IsPlaying(feedLoop)) {
                feedLoop = audio.Play(SoundIds.MonWhisperFeedLoop);
            }
            if (!feedLoop.IsNone) {
                audio.SetVolume(feedLoop, feedVolume);
            }
            FeedLoopPlaying = audio.IsPlaying(feedLoop);
        }

        float DrainFor(ThreatStage stage) {
            return StageValue(config != null ? config.drainPerSecondByStage : null, stage, 0.4f);
        }

        float StageVolume(ThreatStage stage) {
            return StageValue(config != null ? config.whisperVolumeByStage : null, stage, 0.02f);
        }

        static float StageValue(float[] values, ThreatStage stage, float fallback) {
            if (values == null || values.Length == 0) {
                return fallback;
            }
            int index = (int)stage;
            if (index < 0) {
                return 0f;
            }
            if (index >= values.Length) {
                index = values.Length - 1;
            }
            return Mathf.Max(0f, values[index]);
        }

        void StopAll() {
            if (draining && shift != null && shift.Sanity != null) {
                shift.Sanity.RemoveDrain(this);
            }
            draining = false;
            CurrentDrainPerSecond = 0f;
            volume = config != null ? StageVolume(ThreatStage.Dormant) : 0.02f;
            IAudioService audio = shift != null && shift.Game != null ? shift.Game.Audio : null;
            if (audio != null) {
                if (audio.IsPlaying(whisperLoop)) {
                    audio.Stop(whisperLoop);
                }
                if (audio.IsPlaying(feedLoop)) {
                    audio.Stop(feedLoop);
                }
            }
            whisperLoop = SoundHandle.None;
            feedLoop = SoundHandle.None;
            WhisperLoopPlaying = false;
            FeedLoopPlaying = false;
        }
    }
}
