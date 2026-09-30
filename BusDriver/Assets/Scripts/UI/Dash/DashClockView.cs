using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // The dash clock (§2.5): the shift clock as "12:34 AM". It only rewrites the text when the
    // minute changes, so it allocates nothing per frame.
    public sealed class DashClockView : DashScreenView {
        [SerializeField] TMP_Text timeText;

        ShiftClockDriver clock;
        long shownMinute = long.MinValue;

        public string Text { get { return timeText != null ? timeText.text : ""; } }

        protected override void OnBind(ShiftServices shift) {
            clock = shift.Clock;
            Refresh();
        }

        void Update() {
            Refresh();
        }

        void Refresh() {
            if (clock == null || clock.Clock == null) {
                return;
            }
            long minute = (long)System.Math.Floor(clock.NowGameSeconds / 60.0);
            if (minute == shownMinute) {
                return;
            }
            shownMinute = minute;
            timeText.text = clock.Format(ClockFormat.Dash);
        }
    }
}
