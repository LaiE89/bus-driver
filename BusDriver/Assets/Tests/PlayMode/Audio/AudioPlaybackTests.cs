using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Audio {
    // Every sound id of Appendix A.3 plays through the real AudioService and content (T-M1-11)
    public class AudioPlaybackTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest]
        public IEnumerator Audio_EveryIdPlays() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            AudioService audio = (AudioService)game.Audio;
            yield return null;
            foreach (string id in SoundIds.All) {
                SoundHandle handle = audio.Play(id);
                Assert.IsFalse(handle.IsNone, id + " did not play");
                AudioSource source = audio.SourceOf(handle);
                Assert.IsNotNull(source, id + " has no source");
                Assert.IsNotNull(source.clip, id + " has no clip");
                Assert.IsTrue(source.isPlaying, id + "'s source is not playing");
                audio.Stop(handle);
            }
        }
    }
}
