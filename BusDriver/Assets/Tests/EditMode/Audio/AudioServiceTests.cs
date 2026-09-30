using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Audio {
    // ROADMAP §4.12, on a fake clock so voice lifetimes and cooldowns are exact
    public class AudioServiceTests {
        GameObject host;
        SoundLibrary library;
        AudioService audio;
        float now;
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp() {
            now = 100f;
            host = new GameObject("AudioServiceTests");
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            created.Add(library);
            audio = new AudioService(host.transform, library, null, null, () => now);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(host);
            foreach (Object o in created) {
                Object.DestroyImmediate(o);
            }
            created.Clear();
        }

        SoundDefinition Define(string id, float seconds = 1f, bool loop = false, int maxVoices = 4, float cooldown = 0f,
            AudioGroup group = AudioGroup.SfxWorld, string caption = "", int priority = 128) {
            AudioClip clip = AudioClip.Create(id, Mathf.Max(1, (int)(seconds * 1000f)), 1, 1000, false);
            SoundDefinition definition = ScriptableObject.CreateInstance<SoundDefinition>();
            definition.id = id;
            definition.clips = new[] { clip };
            definition.loop = loop;
            definition.maxVoices = maxVoices;
            definition.cooldown = cooldown;
            definition.group = group;
            definition.caption = caption;
            definition.priority = priority;
            library.sounds.Add(definition);
            created.Add(clip);
            created.Add(definition);
            return definition;
        }

        [Test]
        public void TheVoiceLimitStealsTheOldestInstance() {
            Define("test.limited", maxVoices: 2, seconds: 10f);
            SoundHandle first = audio.Play("test.limited");
            now += 0.1f;
            SoundHandle second = audio.Play("test.limited");
            now += 0.1f;
            SoundHandle third = audio.Play("test.limited");

            Assert.IsFalse(audio.IsPlaying(first), "the oldest instance is stolen");
            Assert.IsTrue(audio.IsPlaying(second));
            Assert.IsTrue(audio.IsPlaying(third));
            Assert.AreEqual(2, audio.ActiveVoiceCount);
        }

        [Test]
        public void ARepeatWithinTheCooldownIsIgnored() {
            Define("test.cooldown", cooldown: 1f);
            Assert.IsFalse(audio.Play("test.cooldown").IsNone);
            now += 0.5f;
            Assert.IsTrue(audio.Play("test.cooldown").IsNone, "within the cooldown");
            now += 0.6f;
            Assert.IsFalse(audio.Play("test.cooldown").IsNone, "after the cooldown");
        }

        [Test]
        public void AHandleIsInvalidAfterStop() {
            Define("test.loop", loop: true);
            SoundHandle handle = audio.Play("test.loop");
            Assert.IsTrue(audio.IsPlaying(handle));
            audio.Stop(handle);
            Assert.IsFalse(audio.IsPlaying(handle));
            // Stopping again, or touching a dead handle, is harmless
            audio.Stop(handle);
            audio.SetVolume(handle, 0.5f);
            Assert.AreEqual(0, audio.ActiveVoiceCount);
        }

        [Test]
        public void AOneShotEndsWithItsClipAndALoopDoesNot() {
            Define("test.short", seconds: 0.5f);
            Define("test.loop", loop: true, seconds: 0.5f);
            SoundHandle shot = audio.Play("test.short");
            SoundHandle loop = audio.Play("test.loop");
            now += 0.6f;
            audio.Tick();
            Assert.IsFalse(audio.IsPlaying(shot));
            Assert.IsTrue(audio.IsPlaying(loop));
        }

        [Test]
        public void AnAttachedSoundEndsWhenItsTransformIsDestroyed() {
            Define("test.attached", loop: true);
            GameObject target = new GameObject("target");
            target.transform.position = new Vector3(1f, 2f, 3f);
            SoundHandle handle = audio.PlayAttached("test.attached", target.transform);
            target.transform.position = new Vector3(4f, 5f, 6f);
            audio.Tick();
            Assert.IsTrue(audio.IsPlaying(handle));
            Object.DestroyImmediate(target);
            audio.Tick();
            Assert.IsFalse(audio.IsPlaying(handle));
        }

        [Test]
        public void SceneChangesStopEverythingButUi() {
            Define("test.world", loop: true);
            Define("test.ui", loop: true, group: AudioGroup.Ui);
            SoundHandle world = audio.Play("test.world");
            SoundHandle ui = audio.Play("test.ui");
            audio.StopSceneSounds();
            Assert.IsFalse(audio.IsPlaying(world));
            Assert.IsTrue(audio.IsPlaying(ui));
        }

        [Test]
        public void ACaptionIsRaisedWhenACaptionedSoundPlays() {
            Define("test.horn", caption: "[horn]");
            Define("test.silent");
            List<string> captions = new List<string>();
            audio.OnCaption += captions.Add;
            audio.Play("test.horn");
            audio.Play("test.silent");
            CollectionAssert.AreEqual(new[] { "[horn]" }, captions);
        }

        [Test]
        public void AFullPoolStealsTheLeastImportantVoice() {
            Define("test.important", loop: true, maxVoices: 64, priority: 0);
            Define("test.filler", loop: true, maxVoices: 64, priority: 200);
            SoundHandle important = audio.Play("test.important");
            SoundHandle firstFiller = SoundHandle.None;
            for (int i = 1; i < AudioService.PoolSize; i++) {
                SoundHandle h = audio.Play("test.filler");
                if (i == 1) {
                    firstFiller = h;
                }
            }
            Assert.AreEqual(AudioService.PoolSize, audio.ActiveVoiceCount);
            SoundHandle extra = audio.Play("test.important");
            Assert.IsTrue(audio.IsPlaying(extra));
            Assert.IsTrue(audio.IsPlaying(important), "a more important voice survives");
            Assert.IsFalse(audio.IsPlaying(firstFiller), "the oldest least-important voice goes");
        }

        [Test]
        public void AnUnknownIdPlaysNothing() {
            Assert.IsTrue(audio.Play("test.missing").IsNone);
        }
    }
}
