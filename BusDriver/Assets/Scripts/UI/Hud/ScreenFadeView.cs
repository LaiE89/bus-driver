using BusDriver.Gameplay.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // Draws ScreenFade: a black full-screen image over the HUD and under the screens, so the pause
    // menu stays readable during a fade, and its caption fading in with it
    public sealed class ScreenFadeView : MonoBehaviour, IShiftBindable {
        [SerializeField] Image image;
        [SerializeField] TMP_Text caption;

        ScreenFade fade;

        public void Bind(ShiftServices shift) {
            fade = shift.Fade;
            Apply();
        }

        void LateUpdate() {
            Apply();
        }

        void Apply() {
            if (image == null) {
                return;
            }
            float alpha = fade != null ? fade.Alpha : 0f;
            image.enabled = alpha > 0.001f;
            Color color = image.color;
            color.a = alpha;
            image.color = color;
            if (caption != null) {
                string text = fade != null ? fade.Caption : "";
                bool show = image.enabled && !string.IsNullOrEmpty(text);
                caption.enabled = show;
                if (show) {
                    if (caption.text != text) {
                        caption.text = text;
                    }
                    caption.alpha = alpha;
                }
            }
        }
    }
}
