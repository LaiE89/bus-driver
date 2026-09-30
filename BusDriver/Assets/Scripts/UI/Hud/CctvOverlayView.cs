using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // The CCTV canvas (§4.13, was half of DrivingHUD): camera label, blinking REC, timestamp and
    // scanlines, shown only while a feed is on screen. The timestamp is the shift clock (§2.5),
    // the same one the dash shows.
    public sealed class CctvOverlayView : MonoBehaviour, IShiftBindable {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text camLabelText;
        [SerializeField] TMP_Text timestampText;
        [SerializeField] TMP_Text recText;
        [SerializeField] RawImage scanlines;

        CCTVSystem cctv;
        ShiftClockDriver clock;
        long shownSecond = long.MinValue;

        public string Timestamp { get { return timestampText != null ? timestampText.text : ""; } }

        void Awake() {
            scanlines.texture = CreateScanlineTexture();
            panel.SetActive(false);
        }

        public void Bind(ShiftServices shift) {
            cctv = shift.Cctv;
            clock = shift.Clock;
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
            if (!panel.activeSelf) {
                return;
            }
            // Rewritten only when the second changes, so a feed allocates nothing most frames
            long second = clock != null ? (long)System.Math.Floor(clock.NowGameSeconds) : 0L;
            if (second != shownSecond && clock != null) {
                shownSecond = second;
                timestampText.text = clock.Format(ClockFormat.Cctv);
            }
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
