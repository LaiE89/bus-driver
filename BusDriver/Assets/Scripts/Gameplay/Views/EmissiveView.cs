using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The stock IEmissiveView (§4.14): scales the emission of a set of renderers through a
    // MaterialPropertyBlock, so the shared material (and batching) is never touched.
    public sealed class EmissiveView : MonoBehaviour, IEmissiveView {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] renderers = new Renderer[0];
        [Tooltip("The full-intensity emission. Black means: read it from the first renderer's material")]
        [ColorUsage(false, true)]
        [SerializeField] Color emission = Color.black;

        MaterialPropertyBlock block;

        public float Intensity { get; private set; } = 1f;
        public Color Emission { get { return emission; } }

        public void SetEmission(float intensity01) {
            Intensity = Mathf.Clamp01(intensity01);
            if (block == null) {
                block = new MaterialPropertyBlock();
                ResolveEmission();
            }
            block.SetColor(EmissionColorId, emission * Intensity);
            for (int i = 0; i < renderers.Length; i++) {
                if (renderers[i] != null) {
                    renderers[i].SetPropertyBlock(block);
                }
            }
        }

        // For code that builds a view at runtime (tests); builders set the serialized fields
        internal void Configure(Renderer[] targets, Color fullEmission) {
            renderers = targets;
            emission = fullEmission;
            block = null;
        }

        void ResolveEmission() {
            if (emission.maxColorComponent > 0f) {
                return;
            }
            for (int i = 0; i < renderers.Length; i++) {
                if (renderers[i] != null && renderers[i].sharedMaterial != null
                    && renderers[i].sharedMaterial.HasProperty(EmissionColorId)) {
                    emission = renderers[i].sharedMaterial.GetColor(EmissionColorId);
                    return;
                }
            }
        }
    }
}
