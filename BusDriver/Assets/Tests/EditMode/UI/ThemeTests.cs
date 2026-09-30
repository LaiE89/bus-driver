using BusDriver.Core.Data;
using BusDriver.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.UI {
    // ROADMAP §4.13: one theme asset styles every text
    public class ThemeTests {
        const string ThemePath = "Assets/Data/UI/Theme.asset";

        [Test]
        public void TheThemeAssetHasEveryRoleAndIsInTheConfig() {
            UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            Assert.IsNotNull(theme, "no theme at " + ThemePath);
            foreach (ThemeRole role in System.Enum.GetValues(typeof(ThemeRole))) {
                UITheme.RoleStyle style;
                Assert.IsTrue(theme.TryGet(role, out style), "missing role " + role);
                Assert.IsNotNull(style.font, role + " has no font");
                Assert.Greater(style.size, 0f);
            }
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            Assert.AreSame(theme, config.uiTheme);
        }

        [Test]
        public void EditingARoleRestylesEveryTextUsingIt() {
            UITheme theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath));
            GameObject body = new GameObject("body", typeof(RectTransform));
            GameObject title = new GameObject("title", typeof(RectTransform));
            try {
                TextMeshProUGUI bodyText = body.AddComponent<TextMeshProUGUI>();
                TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
                body.AddComponent<ThemedText>().Configure(theme, ThemeRole.Body);
                title.AddComponent<ThemedText>().Configure(theme, ThemeRole.Title);
                UITheme.RoleStyle style;
                theme.TryGet(ThemeRole.Body, out style);
                Assert.AreEqual(style.size, bodyText.fontSize);

                for (int i = 0; i < theme.roles.Length; i++) {
                    if (theme.roles[i].role == ThemeRole.Body) {
                        theme.roles[i].size = 77f;
                    }
                }
                float titleBefore = titleText.fontSize;
                // What OnValidate does when the asset is edited in the Inspector
                theme.NotifyChanged();
                Assert.AreEqual(77f, bodyText.fontSize);
                Assert.AreEqual(titleBefore, titleText.fontSize, "other roles are untouched");
            }finally {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(title);
                Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void APaletteColourOverridesTheRoleColour() {
            UITheme theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath));
            GameObject warning = new GameObject("warning", typeof(RectTransform));
            try {
                TextMeshProUGUI text = warning.AddComponent<TextMeshProUGUI>();
                ThemedText themed = warning.AddComponent<ThemedText>();
                themed.Configure(theme, ThemeRole.Body);
                themed.SetPaletteColor(ThemeColor.Danger);
                Assert.AreEqual(theme.danger, text.color);
            }finally {
                Object.DestroyImmediate(warning);
                Object.DestroyImmediate(theme);
            }
        }
    }
}
