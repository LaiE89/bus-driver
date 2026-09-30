using System;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // Append-only: the value is serialized on every lamp prefab
    public enum FlickerMode : int {
        // Stop lamps: a slow, shallow waver
        Subtle = 0,
        // The tunnel: a faster waver plus short dropouts
        Unstable = 1,
        // Steady until a scare or menu event plays a curve
        Scripted = 2,
    }

    // Drives a group of lights and every IEmissiveView in the same group (§4.14). A group is the
    // set of references on one LightFlicker, so a lamp's bulb and its glowing head always agree.
    // It runs on scaled time: scripted flickers belong to scares, which stop with the pause (§4.11),
    // and a paused frame should look frozen rather than keep flickering behind the menu.
    // Randomness comes from a System.Random seeded from `seed` (the builder gives each lamp its
    // own), or from SetRandom, never from UnityEngine.Random (§4.1.8).
    public sealed class LightFlicker : MonoBehaviour {
        [SerializeField] FlickerMode mode = FlickerMode.Subtle;
        [SerializeField] Light[] lights = new Light[0];
        [Tooltip("Components implementing IEmissiveView; they follow the lights' multiplier")]
        [SerializeField] MonoBehaviour[] emissiveViews = new MonoBehaviour[0];
        [SerializeField] int seed = 1;

        [Header("Subtle")]
        [Tooltip("The multiplier stays within [1 − depth, 1]")]
        [SerializeField] float subtleDepth = 0.12f;
        [SerializeField] float subtleSpeed = 1.3f;

        [Header("Unstable")]
        [SerializeField] float unstableDepth = 0.35f;
        [SerializeField] float unstableSpeed = 6f;
        [SerializeField] float dropoutsPerSecond = 0.5f;
        [SerializeField] float dropoutMinSeconds = 0.04f;
        [SerializeField] float dropoutMaxSeconds = 0.18f;
        [Tooltip("During a dropout the multiplier sits between 0 and this")]
        [SerializeField] float dropoutLevel = 0.15f;

        float[] baseIntensities;
        IEmissiveView[] emissives;
        System.Random random;
        bool ready;
        float noiseTime;
        float noiseOffset;
        float dropoutLeft;
        float dropoutValue;

        AnimationCurve scriptedCurve;
        float scriptedDuration;
        float scriptedElapsed;

        public float Multiplier { get; private set; } = 1f;
        public bool IsPlayingScripted { get; private set; }
        public int LightCount { get { return lights.Length; } }

        public FlickerMode Mode {
            get { return mode; }
            set { mode = value; }
        }

        // After a scripted curve reaches its duration
        public event Action OnScriptedFinished;

        public void SetRandom(System.Random stream) {
            random = stream;
            noiseOffset = (float)(random.NextDouble() * 1000.0);
        }

        // multiplier(t) = curve(elapsed / duration), clamped to 0..1; the idle mode resumes exactly
        // at `duration`. Playing again restarts.
        public void Play(AnimationCurve curve, float duration) {
            scriptedCurve = curve;
            scriptedDuration = Mathf.Max(0.0001f, duration);
            scriptedElapsed = 0f;
            IsPlayingScripted = true;
        }

        public void StopScripted() {
            IsPlayingScripted = false;
        }

        // For code that builds a lamp at runtime (tests); builders set the serialized fields
        internal void Configure(FlickerMode flickerMode, Light[] targets, MonoBehaviour[] views, int randomSeed) {
            mode = flickerMode;
            lights = targets;
            emissiveViews = views;
            seed = randomSeed;
            ready = false;
            random = null;
        }

        void Update() {
            Step(Time.deltaTime);
        }

        // One frame of dt scaled seconds; the tests drive it directly
        internal void Step(float dt) {
            EnsureReady();
            Multiplier = Evaluate(dt);
            for (int i = 0; i < lights.Length; i++) {
                if (lights[i] != null) {
                    lights[i].intensity = baseIntensities[i] * Multiplier;
                }
            }
            for (int i = 0; i < emissives.Length; i++) {
                if (emissives[i] != null) {
                    emissives[i].SetEmission(Multiplier);
                }
            }
        }

        float Evaluate(float dt) {
            noiseTime += dt;
            if (IsPlayingScripted) {
                scriptedElapsed += dt;
                if (scriptedElapsed < scriptedDuration) {
                    float value = scriptedCurve != null ? scriptedCurve.Evaluate(scriptedElapsed / scriptedDuration) : 1f;
                    return Mathf.Clamp01(value);
                }
                IsPlayingScripted = false;
                if (OnScriptedFinished != null) {
                    OnScriptedFinished();
                }
            }
            switch (mode) {
                case FlickerMode.Subtle:
                    return 1f - Mathf.Clamp01(subtleDepth) * Noise(subtleSpeed);
                case FlickerMode.Unstable:
                    return Unstable(dt);
                default:
                    return 1f;
            }
        }

        float Unstable(float dt) {
            if (dropoutLeft > 0f) {
                dropoutLeft -= dt;
                return dropoutValue;
            }
            // A Poisson-ish start: the chance of a dropout this frame is rate × dt
            if (random.NextDouble() < dropoutsPerSecond * dt) {
                dropoutLeft = Mathf.Lerp(dropoutMinSeconds, dropoutMaxSeconds, (float)random.NextDouble());
                dropoutValue = Mathf.Clamp01(dropoutLevel) * (float)random.NextDouble();
                return dropoutValue;
            }
            return 1f - Mathf.Clamp01(unstableDepth) * Noise(unstableSpeed);
        }

        // 0..1
        float Noise(float speed) {
            return Mathf.Clamp01(Mathf.PerlinNoise(noiseOffset + noiseTime * speed, noiseOffset * 0.37f));
        }

        // Lazy, because the group can reach other objects (the view's renderers): §4.1.6 keeps
        // Awake to this object's own components
        void EnsureReady() {
            if (ready) {
                return;
            }
            ready = true;
            baseIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) {
                baseIntensities[i] = lights[i] != null ? lights[i].intensity : 0f;
            }
            emissives = new IEmissiveView[emissiveViews.Length];
            for (int i = 0; i < emissiveViews.Length; i++) {
                emissives[i] = emissiveViews[i] as IEmissiveView;
            }
            if (random == null) {
                SetRandom(new System.Random(seed));
            }
        }
    }
}
