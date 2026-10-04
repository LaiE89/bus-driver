using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Economy;
using BusDriver.Gameplay.Flow;
using BusDriver.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Dash {
    // The fare box on the dash (§2.7, §4.13): a small quota line, a thin fill toward that quota,
    // the night's running total as the hero number, and a signed ±pop for each ledger entry.
    // Colour never carries meaning alone (§2.23): the pop always includes + / −.
    public sealed class FareBoxView : DashScreenView {
        [SerializeField] TMP_Text quotaText;
        [SerializeField] TMP_Text totalText;
        [SerializeField] TMP_Text deltaText;
        [SerializeField] Image progressTrack;
        [SerializeField] Image progressFill;
        [SerializeField] Color upColor = new Color(0.4f, 0.85f, 0.45f, 1f);
        [SerializeField] Color downColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField] Color metColor = new Color(0.45f, 0.9f, 0.55f, 1f);
        [SerializeField] Color shortColor = new Color(0.92f, 0.78f, 0.35f, 1f);
        [Tooltip("How long a pop stays up, in unscaled seconds")]
        [SerializeField] float popSeconds = 1.6f;
        [Tooltip("How far a pop rises while it fades, in canvas pixels")]
        [SerializeField] float popRise = 10f;

        ShiftLedger ledger;
        int quotaCents;
        IAudioService audio;
        Vector2 deltaHome;
        float popAge = float.MaxValue;
        Color popColor;
        UITheme theme;

        public string TotalText { get { return totalText != null ? totalText.text : ""; } }
        public string QuotaText { get { return quotaText != null ? quotaText.text : ""; } }
        public string DeltaText { get { return deltaText != null ? deltaText.text : ""; } }
        public float LastPopTime { get; private set; } = float.NegativeInfinity;
        public bool IsPopping { get { return popAge < popSeconds; } }

        void Awake() {
            EnsureLayout();
            if (deltaText != null) {
                deltaHome = deltaText.rectTransform.anchoredPosition;
                deltaText.text = "";
            }
        }

        protected override void OnBind(ShiftServices shift) {
            EnsureLayout();
            ledger = shift.Ledger;
            quotaCents = shift.Night != null ? shift.Night.quotaCents : 0;
            audio = shift.Game != null ? shift.Game.Audio : null;
            if (shift.Game != null && shift.Game.Config != null) {
                theme = shift.Game.Config.uiTheme as UITheme;
            }
            ledger.OnEntry += HandleEntry;
            Refresh();
        }

        void OnDestroy() {
            if (ledger != null) {
                ledger.OnEntry -= HandleEntry;
            }
        }

        void HandleEntry(LedgerEntry entry) {
            Refresh();
            if (deltaText == null) {
                return;
            }
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

        void Refresh() {
            int made = ledger != null ? ledger.Totals.NetCents : 0;
            bool hasQuota = quotaCents > 0;
            if (quotaText != null) {
                quotaText.gameObject.SetActive(hasQuota);
                quotaText.text = hasQuota ? "QUOTA  " + Money.Format(quotaCents) : "";
            }
            if (progressTrack != null) {
                progressTrack.gameObject.SetActive(hasQuota);
            }
            if (progressFill != null && hasQuota) {
                float t = Mathf.Clamp01(made / (float)quotaCents);
                progressFill.rectTransform.anchorMax = new Vector2(t, 1f);
                bool met = made >= quotaCents;
                progressFill.color = met ? metColor : shortColor;
            }
            if (totalText != null) {
                totalText.text = Money.Format(made);
                if (hasQuota) {
                    totalText.color = made >= quotaCents ? metColor : shortColor;
                }else if (theme != null) {
                    totalText.color = theme.Palette(ThemeColor.Positive);
                }
            }
        }

        // Older Dash.prefabs only had Total + Delta; spin up the quota row and bar once
        void EnsureLayout() {
            if (totalText == null) {
                return;
            }
            RectTransform root = totalText.rectTransform.parent as RectTransform;
            if (root == null) {
                return;
            }

            if (quotaText == null) {
                // Upgrade path for older Dash.prefabs that only had Total + Delta
                quotaText = CreateLabel(root, "Quota", "QUOTA  $0.00", 18f, FontStyles.Normal,
                    new Vector2(0f, 42f), new Vector2(200f, 28f), totalText.font);
                quotaText.color = new Color(0.55f, 0.56f, 0.52f, 1f);
                quotaText.characterSpacing = 4f;
            }

            if (progressTrack == null) {
                progressTrack = CreateBar(root, "QuotaTrack", new Vector2(0f, 22f), new Vector2(168f, 5f),
                    new Color(1f, 1f, 1f, 0.12f));
                progressFill = CreateBar(progressTrack.rectTransform, "QuotaFill", Vector2.zero, Vector2.zero,
                    shortColor);
                RectTransform fill = progressFill.rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0f, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                fill.pivot = new Vector2(0f, 0.5f);
            }

            // Hero total sits under the quota row; theme Screen size (~44) does the weight
            Place(totalText.rectTransform, new Vector2(0f, -6f), new Vector2(208f, 48f));
            totalText.fontStyle = FontStyles.Bold;
            totalText.alignment = TextAlignmentOptions.Center;
            totalText.textWrappingMode = TextWrappingModes.NoWrap;

            if (deltaText != null) {
                Place(deltaText.rectTransform, new Vector2(0f, -44f), new Vector2(200f, 28f));
                deltaText.alignment = TextAlignmentOptions.Center;
                deltaHome = deltaText.rectTransform.anchoredPosition;
            }
        }

        static TMP_Text CreateLabel(RectTransform parent, string name, string text, float size, FontStyles style,
            Vector2 position, Vector2 box, TMP_FontAsset font) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null) {
                label.font = font;
            }
            Place(label.rectTransform, position, box);
            return label;
        }

        static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 box, Color color) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            if (box.sqrMagnitude > 0f) {
                Place(rect, position, box);
            }else {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            return image;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        void Update() {
            if (popAge >= popSeconds) {
                return;
            }
            popAge += Time.unscaledDeltaTime;
            Animate();
        }

        void Animate() {
            if (deltaText == null) {
                return;
            }
            float t = Mathf.Clamp01(popAge / popSeconds);
            Color color = popColor;
            color.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            deltaText.color = color;
            deltaText.rectTransform.anchoredPosition = deltaHome + new Vector2(0f, popRise * t);
            if (t >= 1f) {
                deltaText.text = "";
            }
        }
    }
}
