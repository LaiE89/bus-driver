using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Economy;
using BusDriver.Gameplay.Flow;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // The fare box on the dash (§2.7, §4.13): the night's total, and a pop for every ledger entry,
    // "+$3.50" in green or "−$3.50" in red. The sign is always there, so colour is never the only cue
    // (§2.23). A fare taps the box (bus.fare_tap); other money chimes up or down.
    public sealed class FareBoxView : DashScreenView {
        [SerializeField] TMP_Text totalText;
        [SerializeField] TMP_Text deltaText;
        [SerializeField] Color upColor = new Color(0.4f, 0.85f, 0.45f, 1f);
        [SerializeField] Color downColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [Tooltip("How long a pop stays up, in unscaled seconds")]
        [SerializeField] float popSeconds = 1.6f;
        [Tooltip("How far a pop rises while it fades, in canvas pixels")]
        [SerializeField] float popRise = 12f;

        ShiftLedger ledger;
        IAudioService audio;
        Vector2 deltaHome;
        float popAge = float.MaxValue;
        Color popColor;

        public string TotalText { get { return totalText != null ? totalText.text : ""; } }
        public string DeltaText { get { return deltaText != null ? deltaText.text : ""; } }
        // Unscaled time of the last pop
        public float LastPopTime { get; private set; } = float.NegativeInfinity;
        public bool IsPopping { get { return popAge < popSeconds; } }

        void Awake() {
            if (deltaText != null) {
                deltaHome = deltaText.rectTransform.anchoredPosition;
                deltaText.text = "";
            }
        }

        protected override void OnBind(ShiftServices shift) {
            ledger = shift.Ledger;
            audio = shift.Game != null ? shift.Game.Audio : null;
            ledger.OnEntry += HandleEntry;
            ShowTotal();
        }

        void OnDestroy() {
            if (ledger != null) {
                ledger.OnEntry -= HandleEntry;
            }
        }

        void HandleEntry(LedgerEntry entry) {
            ShowTotal();
            deltaText.text = Money.FormatDelta(entry.AmountCents);
            popColor = entry.AmountCents >= 0 ? upColor : downColor;
            popAge = 0f;
            LastPopTime = Time.unscaledTime;
            Animate();
            if (audio != null) {
                string sound = entry.Kind == LedgerKind.Fare ? SoundIds.BusFareTap
                    : entry.AmountCents >= 0 ? SoundIds.UiMoneyUp : SoundIds.UiMoneyDown;
                audio.PlayAt(sound, transform.position);
            }
        }

        void ShowTotal() {
            totalText.text = Money.Format(ledger.Totals.NetCents);
        }

        // UI animation runs on unscaled time (§4.1 rule 9)
        void Update() {
            if (popAge >= popSeconds) {
                return;
            }
            popAge += Time.unscaledDeltaTime;
            Animate();
        }

        void Animate() {
            float t = Mathf.Clamp01(popAge / popSeconds);
            Color color = popColor;
            // Holds for the first half, then fades
            color.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            deltaText.color = color;
            deltaText.rectTransform.anchoredPosition = deltaHome + new Vector2(0f, popRise * t);
            if (t >= 1f) {
                deltaText.text = "";
            }
        }
    }
}
