using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // The CCTV canvas (§4.13, was half of DrivingHUD): camera label, blinking REC, timestamp and
    // scanlines, shown only while a feed is on screen. The timestamp is still the MVP's cosmetic
    // clock; T-M2-12 points it at ShiftClock.
    public sealed class CctvOverlayView : MonoBehaviour, IShiftBindable {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text camLabelText;
        [SerializeField] TMP_Text timestampText;
        [SerializeField] TMP_Text recText;
        [SerializeField] RawImage scanlines;
        // Seconds since midnight; the shift starts a little after 2 AM
        [SerializeField] float clockStart = 2 * 3600 + 13 * 60;

        CCTVSystem cctv;
        float clock;

        void Awake() {
            clock = clockStart;
            scanlines.texture = CreateScanlineTexture();
            panel.SetActive(false);
        }

        public void Bind(ShiftServices shift) {
            cctv = shift.Cctv;
            cctv.OnViewChanged += HandleViewChanged;
            HandleViewChanged(cctv.ActiveIndex);
        }

        void OnDestroy() {
            if (cctv != null) {
                cctv.OnViewChanged -= HandleViewChanged;
            }
        }

        void HandleViewChanged(int index) {
            panel.SetActive(index >= 0);
            camLabelText.text = cctv.ActiveLabel;
        }

        void Update() {
            if (cctv == null) {
                return;
            }
            clock += Time.deltaTime;
            if (!panel.activeSelf) {
                return;
            }
            int total = (int)clock % 86400;
            int hours = total / 3600;
            int hours12 = hours % 12 == 0 ? 12 : hours % 12;
            timestampText.text = $"{hours12:00}:{total / 60 % 60:00}:{total % 60:00} {(hours < 12 ? "AM" : "PM")}";
            recText.enabled = Time.unscaledTime % 1f < 0.5f;
            // One texture repeat per 4 screen pixels
            scanlines.uvRect = new Rect(0f, 0f, 1f, Screen.height / 4f);
        }

        static Texture2D CreateScanlineTexture() {
            Texture2D texture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 4; y++) {
                texture.SetPixel(0, y, new Color(0f, 0f, 0f, y == 0 ? 0.35f : 0f));
            }
            texture.Apply();
            return texture;
        }
    }
}
