using System.Collections.Generic;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.Bus {
    // The cabin light group (§2.14, §2.17): the point lights over the aisle and the view's emissive
    // strips. Scares, kill-sequence telegraphs and (later) the Mimic's replace ask for a flicker or
    // a blackout for a while; each request has an owner, so ending one never cancels another's.
    // Any "off" wins over any flicker. Runs on scaled time, so pausing holds the lights.
    public sealed class CabinLights : MonoBehaviour {
        [SerializeField] Light[] lights = new Light[0];
        [SerializeField] BusViewBase view;
        [Tooltip("Flicker pattern speed, Hz [TUNE]")]
        [SerializeField] float flickerFrequency = 11f;
        [Tooltip("Level while a flicker is in a dark beat [TUNE]")]
        [SerializeField] float flickerLow = 0.08f;

        readonly Dictionary<object, float> flickers = new Dictionary<object, float>();
        readonly Dictionary<object, float> offs = new Dictionary<object, float>();
        readonly List<object> expired = new List<object>();
        float[] baseIntensity;
        float time;

        // 0 dark .. 1 normal
        public float Level { get; private set; } = 1f;
        public bool IsFlickering { get { return flickers.Count > 0; } }
        public bool IsOff { get { return offs.Count > 0; } }

        void Awake() {
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) {
                baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            }
        }

        // Flicker for a while; float.PositiveInfinity until Stop(owner)
        public void Flicker(object owner, float seconds) {
            flickers[owner] = time + seconds;
            Apply();
        }

        public void Off(object owner, float seconds) {
            offs[owner] = time + seconds;
            Apply();
        }

        // Ends everything this owner asked for
        public void Stop(object owner) {
            flickers.Remove(owner);
            offs.Remove(owner);
            Apply();
        }

        void Update() {
            time += Time.deltaTime;
            Expire(flickers);
            Expire(offs);
            Apply();
        }

        void Expire(Dictionary<object, float> requests) {
            if (requests.Count == 0) {
                return;
            }
            expired.Clear();
            foreach (KeyValuePair<object, float> request in requests) {
                if (time >= request.Value) {
                    expired.Add(request.Key);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                requests.Remove(expired[i]);
            }
        }

        void Apply() {
            float level = 1f;
            if (offs.Count > 0) {
                level = 0f;
            }else if (flickers.Count > 0) {
                // Hard blinks, not a smooth dim: a failing fluorescent tube
                float noise = Mathf.PerlinNoise(time * flickerFrequency, 0.37f);
                level = noise > 0.45f ? 1f : flickerLow;
            }
            if (Mathf.Approximately(level, Level) && baseIntensity != null) {
                return;
            }
            Level = level;
            if (baseIntensity == null) {
                return;
            }
            for (int i = 0; i < lights.Length; i++) {
                if (lights[i] != null) {
                    lights[i].intensity = baseIntensity[i] * level;
                }
            }
            if (view != null) {
                view.SetInteriorLights(level);
            }
        }
    }
}
