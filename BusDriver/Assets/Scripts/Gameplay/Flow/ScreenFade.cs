using System.Collections;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The night's fade to black, as a value (0 clear, 1 black). Gameplay drives it: the KillPlane
    // respawn now, the death presenters later. The HUD's ScreenFadeView draws it, so gameplay never
    // references UI (§4.2). Fades run on scaled time: pausing holds them.
    public sealed class ScreenFade {
        public float Alpha { get; private set; }
        // A line shown over the black (the fall's "YOU WENT OVER THE EDGE"); empty for none
        public string Caption { get; set; } = "";

        public void Set(float alpha) {
            Alpha = Mathf.Clamp01(alpha);
        }

        // Run on the MonoBehaviour whose lifetime the fade shares (§4.1 rule 13)
        public IEnumerator FadeTo(float target, float seconds) {
            float from = Alpha;
            target = Mathf.Clamp01(target);
            for (float t = 0f; t < seconds; t += Time.deltaTime) {
                Alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            Alpha = target;
        }
    }
}
