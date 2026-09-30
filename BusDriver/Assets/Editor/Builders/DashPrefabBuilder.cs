using BusDriver.Core.Data;
using BusDriver.UI.Dash;
using BusDriver.UI.Theme;
using TMPro;
using UnityEngine;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // Part of BuildAll step 7 (§4.15): Dash.prefab, the dash's world-space screens (§4.13, D38).
    // Each is a flat canvas whose pixel size is its resolution; DashScreenView moves it onto the
    // bus's Anchor_Dash_* at Bind and scales it to the anchor's size in metres.
    public static class DashPrefabBuilder {
        public const string Path = PrefabBuilder.Folder + "/Dash.prefab";

        // Resolutions: the theme's sizes are in these pixels, so they set how big text reads
        public static readonly Vector2 ClockPixels = new Vector2(200f, 75f);

        static readonly Color Glass = new Color(0.01f, 0.012f, 0.015f, 1f);

        public static void Build() {
            GameObject root = new GameObject("Dash");
            BuildClock(root.transform);
            SaveOrOverwritePrefab(root, Path);
            Object.DestroyImmediate(root);
        }

        // A world-space canvas: no scaler, no raycaster (nothing on the dash is clicked)
        public static RectTransform ScreenCanvas(string name, Transform parent, Vector2 pixels) {
            GameObject go = UIBuild.UIObject(name, parent);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = pixels;
            rect.pivot = new Vector2(0.5f, 0.5f);
            UIBuild.Fill("Glass", rect, Glass, false);
            return rect;
        }

        static void BuildClock(Transform parent) {
            RectTransform canvas = ScreenCanvas("Dash_Clock", parent, ClockPixels);
            DashClockView view = canvas.gameObject.AddComponent<DashClockView>();
            TMP_Text time = UIBuild.Label("Time", canvas, "12:30 AM", ThemeRole.Screen, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, ClockPixels);
            UIBuild.Theme(time, ThemeRole.Screen).SetPaletteColor(ThemeColor.Positive);
            SetInt(view, "screen", (int)DashScreen.Clock);
            SetRef(view, "canvas", canvas);
            SetRef(view, "timeText", time);
        }
    }
}
