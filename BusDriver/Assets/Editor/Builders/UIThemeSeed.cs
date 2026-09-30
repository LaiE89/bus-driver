using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/UI/Theme.asset (§4.13): LiberationSans SDF for every role until the style
    // lock (T-M11-01). DataSeeder creates it if it's missing and never overwrites it (§0.1).
    // Batch mode:  -executeMethod BusDriver.Editor.Builders.UIThemeSeed.CreateIfMissing -quit
    public static class UIThemeSeed {
        public const string RelativePath = "UI/Theme.asset";
        public const string AssetPath = DataSeeder.DataRoot + "/" + RelativePath;
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Tools/Bus Driver/Seed/UI Theme (create if missing)")]
        public static UITheme CreateIfMissing() {
            DataSeeder.SeedMissing();
            return AssetDatabase.LoadAssetAtPath<UITheme>(AssetPath);
        }

        public static void Fill(UITheme theme) {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Color text = new Color(0.92f, 0.92f, 0.9f, 1f);
            theme.roles = new[] {
                Role(ThemeRole.Title, font, 120f, text, 4f),
                Role(ThemeRole.Button, font, 48f, text, 2f),
                Role(ThemeRole.Body, font, 32f, text, 0f),
                Role(ThemeRole.Hud, font, 30f, new Color(0.85f, 0.9f, 0.85f, 0.9f), 0f),
                Role(ThemeRole.Screen, font, 44f, text, 1f),
                Role(ThemeRole.Hint, font, 32f, new Color(1f, 1f, 1f, 0.85f), 0f),
                Role(ThemeRole.Caption, font, 30f, new Color(1f, 1f, 0.85f, 0.95f), 0f),
            };
            theme.text = text;
            theme.highlight = new Color(1f, 0.82f, 0.4f, 1f);
            theme.disabled = new Color(0.5f, 0.5f, 0.5f, 1f);
            theme.danger = new Color(0.9f, 0.25f, 0.2f, 1f);
            theme.positive = new Color(0.4f, 0.85f, 0.45f, 1f);
            theme.screenGlow = new Color(0.6f, 0.8f, 1f, 0.35f);
        }

        static UITheme.RoleStyle Role(ThemeRole role, TMP_FontAsset font, float size, Color color, float spacing) {
            return new UITheme.RoleStyle { role = role, font = font, size = size, color = color, spacing = spacing };
        }
    }
}
