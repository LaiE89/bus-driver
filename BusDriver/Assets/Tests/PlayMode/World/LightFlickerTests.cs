using System.Collections;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.World {
    // T-M2-04: every mode stays within its bounds, a scripted flicker ends exactly at its duration,
    // and emissive views follow the lights
    public class LightFlickerTests {
        const float BaseIntensity = 10f;
        const float Frame = 1f / 60f;

        GameObject root;

        [TearDown]
        public void TearDown() {
            if (root != null) {
                Object.Destroy(root);
            }
        }

        LightFlicker Lamp(FlickerMode mode, out Light light, out EmissiveView view, int seed = 7) {
            root = new GameObject("Test Lamp");
            light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.intensity = BaseIntensity;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(root.transform, false);
            view = head.AddComponent<EmissiveView>();
            view.Configure(new[] { head.GetComponent<Renderer>() }, Color.white);
            LightFlicker flicker = root.AddComponent<LightFlicker>();
            flicker.Configure(mode, new[] { light }, new MonoBehaviour[] { view }, seed);
            // The tests step it by hand
            flicker.enabled = false;
            return flicker;
        }

        static float Emission(EmissiveView view) {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            view.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_EmissionColor").r;
        }

        [Test]
        public void Subtle_StaysWithinItsBand() {
            LightFlicker flicker = Lamp(FlickerMode.Subtle, out Light light, out EmissiveView view);
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < 60 * 60; i++) {
                flicker.Step(Frame);
                min = Mathf.Min(min, light.intensity);
                max = Mathf.Max(max, light.intensity);
            }
            Assert.GreaterOrEqual(min, BaseIntensity * (1f - 0.12f) - 1e-4f, "dipped below the subtle band");
            Assert.LessOrEqual(max, BaseIntensity + 1e-4f, "went above the base intensity");
            Assert.Greater(max - min, 0.01f, "a subtle flicker should move");
        }

        [Test]
        public void Unstable_StaysWithinBoundsAndDropsOut() {
            LightFlicker flicker = Lamp(FlickerMode.Unstable, out Light light, out EmissiveView view);
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < 60 * 60; i++) {
                flicker.Step(Frame);
                min = Mathf.Min(min, light.intensity);
                max = Mathf.Max(max, light.intensity);
            }
            Assert.GreaterOrEqual(min, 0f);
            Assert.LessOrEqual(max, BaseIntensity + 1e-4f);
            Assert.Less(min, BaseIntensity * 0.2f, "a minute of the unstable mode had no dropout");
        }

        [Test]
        public void SameSeed_SameSequence() {
            LightFlicker a = Lamp(FlickerMode.Unstable, out Light lightA, out _, 42);
            GameObject first = root;
            LightFlicker b = Lamp(FlickerMode.Unstable, out Light lightB, out _, 42);
            try {
                for (int i = 0; i < 600; i++) {
                    a.Step(Frame);
                    b.Step(Frame);
                    Assert.AreEqual(lightA.intensity, lightB.intensity, 1e-6f, "frame " + i);
                }
            }finally {
                Object.Destroy(first);
            }
        }

        [Test]
        public void Scripted_EndsExactlyAtItsDuration() {
            LightFlicker flicker = Lamp(FlickerMode.Scripted, out Light light, out EmissiveView view);
            bool finished = false;
            flicker.OnScriptedFinished += () => finished = true;
            flicker.Play(AnimationCurve.Constant(0f, 1f, 0.25f), 0.5f);
            for (int i = 0; i < 4; i++) {
                flicker.Step(0.1f);
                Assert.IsTrue(flicker.IsPlayingScripted, $"stopped early at {(i + 1) * 0.1f:0.0} s");
                Assert.AreEqual(BaseIntensity * 0.25f, light.intensity, 1e-4f);
            }
            flicker.Step(0.1f);
            Assert.IsFalse(flicker.IsPlayingScripted, "still playing at its duration");
            Assert.IsTrue(finished);
            Assert.AreEqual(BaseIntensity, light.intensity, 1e-4f, "the steady idle level didn't come back");
        }

        [Test]
        public void EmissiveViews_FollowTheLight() {
            LightFlicker flicker = Lamp(FlickerMode.Scripted, out Light light, out EmissiveView view);
            flicker.Play(AnimationCurve.Constant(0f, 1f, 0.4f), 1f);
            flicker.Step(0.1f);
            Assert.AreEqual(0.4f, view.Intensity, 1e-4f);
            Assert.AreEqual(0.4f, Emission(view), 1e-4f);
            flicker.Mode = FlickerMode.Subtle;
            flicker.StopScripted();
            for (int i = 0; i < 120; i++) {
                flicker.Step(Frame);
                Assert.AreEqual(light.intensity / BaseIntensity, view.Intensity, 1e-4f, "frame " + i);
            }
        }

        // The real Update path runs on scaled time
        [UnityTest]
        public IEnumerator Scripted_FinishesInPlay() {
            LightFlicker flicker = Lamp(FlickerMode.Scripted, out Light light, out EmissiveView view);
            flicker.enabled = true;
            flicker.Play(AnimationCurve.Constant(0f, 1f, 0f), 0.3f);
            yield return null;
            Assert.IsTrue(flicker.IsPlayingScripted);
            Assert.AreEqual(0f, light.intensity, 1e-4f);
            yield return new WaitForSeconds(0.45f);
            Assert.IsFalse(flicker.IsPlayingScripted);
            Assert.AreEqual(BaseIntensity, light.intensity, 1e-4f);
        }
    }
}
