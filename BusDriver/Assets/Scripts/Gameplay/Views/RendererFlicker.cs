using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The Mimic's flicker for every passenger view, greybox or art (§4.14, D39): ViewFactory adds it
    // to each view, so artists never implement it. It is the only writer of the view's
    // Renderer.enabled: views hide parts by switching GameObjects, never renderers.
    //   MimicFlicker: every so often the renderers blink off for 0.05–0.1 s; the gap between blinks
    //   is lerp(8 s, 2 s, intensity) (§2.12).
    //   Fast blink: the MimicReveal's strobe (12 Hz) while it's on.
    // Scaled time, so it freezes with the pause and the rules.
    public sealed class RendererFlicker : MonoBehaviour {
        const float SlowestGap = 8f;
        const float FastestGap = 2f;
        const float MinBlink = 0.05f;
        const float MaxBlink = 0.1f;
        const float FastBlinkHz = 12f;

        [Tooltip("Seeds the blink lengths; ViewFactory hands each view its own")]
        [SerializeField] int seed;

        Renderer[] renderers = new Renderer[0];
        System.Random rng;
        bool visible = true;
        float flicker;
        bool fastBlink;
        float nextBlinkAt;
        float blinkOffUntil;
        bool appliedOn = true;
        bool initialized;

        public float Flicker { get { return flicker; } }
        public bool FastBlink { get { return fastBlink; } }
        public bool Visible { get { return visible; } }
        // True while the renderers are switched off by a blink or the strobe
        public bool IsBlinkedOff { get; private set; }

        public void Init(int flickerSeed) {
            seed = flickerSeed;
            rng = new System.Random(seed);
            Refresh();
        }

        void Awake() {
            if (rng == null) {
                rng = new System.Random(seed);
            }
            Refresh();
        }

        // Re-reads the view's renderers (a view that adds parts after Awake calls this)
        public void Refresh() {
            renderers = GetComponentsInChildren<Renderer>(true);
            initialized = true;
            Apply(true);
        }

        public void SetVisible(bool on) {
            visible = on;
            Apply(true);
        }

        public void SetFlicker(float intensity01) {
            float next = Mathf.Clamp01(intensity01);
            if (next > 0f && flicker <= 0f) {
                // The first blink comes one full gap after the flicker starts
                nextBlinkAt = Time.time + Gap(next);
            }
            flicker = next;
        }

        public void SetFastBlink(bool on) {
            fastBlink = on;
        }

        float Gap(float intensity) {
            return Mathf.Lerp(SlowestGap, FastestGap, intensity);
        }

        void Update() {
            if (!initialized) {
                return;
            }
            float now = Time.time;
            bool off = false;
            if (flicker > 0f) {
                if (now >= nextBlinkAt) {
                    float length = MinBlink + (float)rng.NextDouble() * (MaxBlink - MinBlink);
                    blinkOffUntil = now + length;
                    nextBlinkAt = blinkOffUntil + Gap(flicker);
                }
                off = now < blinkOffUntil;
            }
            if (fastBlink && Mathf.FloorToInt(now * FastBlinkHz) % 2 == 1) {
                off = true;
            }
            IsBlinkedOff = off;
            Apply(false);
        }

        void Apply(bool force) {
            bool on = visible && !IsBlinkedOff;
            if (!force && on == appliedOn) {
                return;
            }
            appliedOn = on;
            for (int i = 0; i < renderers.Length; i++) {
                if (renderers[i] != null) {
                    renderers[i].enabled = on;
                }
            }
        }
    }
}
