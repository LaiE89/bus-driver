using TMPro;
using UnityEngine;

namespace BusDriver.UI.Theme {
    // Gives a TMP text its style from the theme (§4.13). Every TMP_Text in generated UI has one.
    // It runs in edit mode, so a theme edit shows up in the Editor at once.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ThemedText : MonoBehaviour {
        [SerializeField] UITheme theme;
        [SerializeField] ThemeRole role = ThemeRole.Body;
        [Tooltip("Use a palette colour instead of the role's colour (e.g. Danger for a warning)")]
        [SerializeField] bool usePaletteColor;
        [SerializeField] ThemeColor paletteColor = ThemeColor.Text;

        TMP_Text text;
        UITheme subscribed;

        public ThemeRole Role { get { return role; } }

        void Awake() {
            text = GetComponent<TMP_Text>();
        }

        // Builders and runtime-built UI set the style through here
        public void Configure(UITheme newTheme, ThemeRole newRole) {
            theme = newTheme;
            role = newRole;
            usePaletteColor = false;
            Resubscribe();
            Apply();
        }

        public void SetPaletteColor(ThemeColor color) {
            usePaletteColor = true;
            paletteColor = color;
            Apply();
        }

        void OnEnable() {
            Resubscribe();
            Apply();
        }

        void OnDisable() {
            Unsubscribe();
        }

        void OnValidate() {
            if (isActiveAndEnabled) {
                Resubscribe();
                Apply();
            }
        }

        public void Apply() {
            if (text == null) {
                text = GetComponent<TMP_Text>();
            }
            UITheme.RoleStyle style;
            if (theme == null || text == null || !theme.TryGet(role, out style)) {
                return;
            }
            if (style.font != null) {
                text.font = style.font;
            }
            text.fontSize = style.size;
            text.characterSpacing = style.spacing;
            text.color = usePaletteColor ? theme.Palette(paletteColor) : style.color;
        }

        void Resubscribe() {
            if (subscribed == theme) {
                return;
            }
            Unsubscribe();
            if (theme != null) {
                theme.Changed += Apply;
                subscribed = theme;
            }
        }

        void Unsubscribe() {
            if (subscribed != null) {
                subscribed.Changed -= Apply;
                subscribed = null;
            }
        }
    }
}
