using System;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Theme {
    // Explicit values, append-only (§4.1 rule 11)
    public enum ThemeRole : int { Title = 0, Button = 1, Body = 2, Hud = 3, Screen = 4, Hint = 5, Caption = 6 }
    public enum ThemeColor : int { Text = 0, Highlight = 1, Disabled = 2, Danger = 3, Positive = 4, ScreenGlow = 5 }

    // Every text style in the game (§4.13): swapping fonts or palette is a one-asset change
    // (Data/UI/Theme.asset). ThemedText applies a role; T-M11-01 puts the final fonts here.
    [CreateAssetMenu(menuName = "Bus Driver/UI Theme", fileName = "Theme")]
    public sealed class UITheme : ScriptableObject {
        [Serializable]
        public struct RoleStyle {
            public ThemeRole role;
            public TMP_FontAsset font;
            [Min(1f)] public float size;
            public Color color;
            [Tooltip("TMP character spacing, in em/100")]
            public float spacing;
        }

        public RoleStyle[] roles = new RoleStyle[0];

        [Header("Palette")]
        public Color text = Color.white;
        public Color highlight = Color.white;
        public Color disabled = Color.gray;
        public Color danger = Color.red;
        public Color positive = Color.green;
        public Color screenGlow = Color.white;

        // Raised when the asset is edited, so every ThemedText using it re-applies (in the Editor
        // too: ThemedText runs in edit mode)
        public event Action Changed;

        public bool TryGet(ThemeRole role, out RoleStyle style) {
            for (int i = 0; i < roles.Length; i++) {
                if (roles[i].role == role) {
                    style = roles[i];
                    return true;
                }
            }
            style = default(RoleStyle);
            return false;
        }

        public Color Palette(ThemeColor color) {
            switch (color) {
                case ThemeColor.Highlight: return highlight;
                case ThemeColor.Disabled: return disabled;
                case ThemeColor.Danger: return danger;
                case ThemeColor.Positive: return positive;
                case ThemeColor.ScreenGlow: return screenGlow;
                default: return text;
            }
        }

        public void NotifyChanged() {
            if (Changed != null) {
                Changed();
            }
        }

        void OnValidate() {
            NotifyChanged();
        }
    }
}
