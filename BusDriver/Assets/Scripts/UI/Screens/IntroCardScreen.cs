using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Screens {
    // The intro card (§2.21): NIGHT N · 12:30 AM · HOLLOW PINES LINE over black while the shift is
    // in Intro, fading into the driver view once it turns to Driving. It has nothing to select, so
    // it is an overlay on the Screens canvas rather than a ScreenView on the router's stack; Esc
    // does nothing during it because pausing isn't allowed in Intro (§4.11).
    public sealed class IntroCardScreen : MonoBehaviour, IShiftBindable {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text nightText;
        [SerializeField] TMP_Text clockText;
        [SerializeField] TMP_Text routeText;
        [Tooltip("Fade out after the intro, in unscaled seconds")]
        [SerializeField] float fadeOutSeconds = 0.6f;

        ShiftDirector director;
        ShiftClockDriver clock;
        bool fading;

        public bool IsShowing { get { return group != null && group.alpha > 0f; } }

        void Awake() {
            SetAlpha(0f);
        }

        public void Bind(ShiftServices shift) {
            director = shift.Director;
            clock = shift.Clock;
            director.OnStateChanged += HandleStateChanged;
            HandleStateChanged(director.State);
        }

        void OnDestroy() {
            if (director != null) {
                director.OnStateChanged -= HandleStateChanged;
            }
        }

        void HandleStateChanged(ShiftState state) {
            if (state == ShiftState.Intro) {
                nightText.text = string.Format(UIText.IntroNight, director.NightIndex);
                // The shift clock holds at the route's shift start until Driving (§2.5)
                clockText.text = clock != null ? clock.Format(ClockFormat.Dash) : "";
                routeText.text = UIText.RouteName;
                fading = false;
                SetAlpha(1f);
            }else if (group != null && group.alpha > 0f) {
                fading = true;
            }
        }

        // UI animation runs on unscaled time (§4.1.9)
        void Update() {
            if (!fading) {
                return;
            }
            float alpha = group.alpha - (fadeOutSeconds > 0f ? Time.unscaledDeltaTime / fadeOutSeconds : 1f);
            SetAlpha(Mathf.Max(0f, alpha));
            if (alpha <= 0f) {
                fading = false;
            }
        }

        void SetAlpha(float alpha) {
            if (group == null) {
                return;
            }
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
