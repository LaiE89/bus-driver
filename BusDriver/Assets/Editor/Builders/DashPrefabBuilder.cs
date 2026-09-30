using BusDriver.Core.Data;
using BusDriver.UI.Dash;
using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
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
        // The fare box anchor is 0.18 × 0.10 m
        public static readonly Vector2 FareBoxPixels = new Vector2(216f, 120f);
        public static readonly Vector2 GpsPixels = new Vector2(800f, 500f);
        // The square map on the GPS's left; the text column fills the rest
        public const float GpsMapSize = 500f;

        static readonly Color Glass = new Color(0.01f, 0.012f, 0.015f, 1f);

        public static void Build() {
            GameObject root = new GameObject("Dash");
            BuildClock(root.transform);
            BuildFareBox(root.transform);
            BuildGps(root.transform);
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

        // The fare box (§2.7, T-M3-07): the night's total on top, the ±delta pop under it
        static void BuildFareBox(Transform parent) {
            UITheme theme = UIBuild.ThemeAsset;
            RectTransform canvas = ScreenCanvas("Dash_FareBox", parent, FareBoxPixels);
            FareBoxView view = canvas.gameObject.AddComponent<FareBoxView>();
            Vector2 center = new Vector2(0.5f, 0.5f);
            TMP_Text total = UIBuild.Label("Total", canvas, "$0.00", ThemeRole.Screen, TextAlignmentOptions.Center,
                center, new Vector2(0f, 24f), new Vector2(FareBoxPixels.x, 60f));
            TMP_Text delta = UIBuild.Label("Delta", canvas, "", ThemeRole.Hud, TextAlignmentOptions.Center,
                center, new Vector2(0f, -30f), new Vector2(FareBoxPixels.x, 50f));
            SetInt(view, "screen", (int)DashScreen.FareBox);
            SetRef(view, "canvas", canvas);
            SetRef(view, "totalText", total);
            SetRef(view, "deltaText", delta);
            SetColor(view, "upColor", theme.Palette(ThemeColor.Positive));
            SetColor(view, "downColor", theme.Palette(ThemeColor.Danger));
        }

        // The GPS (§4.13, T-M2-13): a 500 px square map on the left, the next stop, distance, ETA,
        // rating and clock in the column on the right, and NO SIGNAL over everything in the tunnel
        static void BuildGps(Transform parent) {
            UITheme theme = UIBuild.ThemeAsset;
            RectTransform canvas = ScreenCanvas("Dash_Gps", parent, GpsPixels);
            RouteMapView view = canvas.gameObject.AddComponent<RouteMapView>();
            RectTransform group = UIBuild.Panel("Map Group", canvas);

            RectTransform map = (RectTransform)UIBuild.UIObject("Map", group).transform;
            UIBuild.Place(map, Vector2.zero, Vector2.zero, new Vector2(GpsMapSize, GpsMapSize));
            UILineRenderer route = Line("Route", map, 4f, false, 0f, 0f, theme.Palette(ThemeColor.Text));
            UILineRenderer stubs = Line("Stubs", map, 3f, true, 0f, 0f, theme.Palette(ThemeColor.Disabled));
            // Red and dashed (§2.23: colour is never the only cue)
            UILineRenderer cliff = Line("Cliff", map, 6f, false, 8f, 7f, theme.Palette(ThemeColor.Danger));
            RectTransform markers = (RectTransform)UIBuild.UIObject("Markers", map).transform;
            FillFromOrigin(markers);
            RectTransform arrow = (RectTransform)UIBuild.UIObject("Bus", map).transform;
            UIBuild.Place(arrow, Vector2.zero, Vector2.zero, new Vector2(24f, 24f));
            arrow.pivot = new Vector2(0.5f, 0.5f);
            UIPolygon arrowShape = arrow.gameObject.AddComponent<UIPolygon>();
            arrowShape.SetPoints(new[] { new Vector2(0f, 13f), new Vector2(-8f, -9f), new Vector2(0f, -4f), new Vector2(8f, -9f) });
            arrowShape.color = theme.Palette(ThemeColor.Highlight);
            arrowShape.raycastTarget = false;

            float x = GpsMapSize + 16f;
            float width = GpsPixels.x - x - 16f;
            Vector2 topLeft = new Vector2(0f, 1f);
            TMP_Text nextLabel = UIBuild.Label("Next Label", group, "NEXT", ThemeRole.Caption, TextAlignmentOptions.TopLeft, topLeft, new Vector2(x, -24f), new Vector2(width, 36f));
            UIBuild.Theme(nextLabel, ThemeRole.Caption).SetPaletteColor(ThemeColor.Disabled);
            TMP_Text next = UIBuild.Label("Next Stop", group, "", ThemeRole.Body, TextAlignmentOptions.TopLeft, topLeft, new Vector2(x, -62f), new Vector2(width, 90f));
            next.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text detail = UIBuild.Label("Detail", group, "", ThemeRole.Caption, TextAlignmentOptions.TopLeft, topLeft, new Vector2(x, -170f), new Vector2(width, 80f));
            detail.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text rating = UIBuild.Label("Rating", group, "", ThemeRole.Screen, TextAlignmentOptions.TopLeft, topLeft, new Vector2(x, -262f), new Vector2(width, 60f));
            TMP_Text clock = UIBuild.Label("Clock", canvas, "", ThemeRole.Screen, TextAlignmentOptions.BottomLeft, Vector2.zero, new Vector2(x, 24f), new Vector2(width, 60f));
            UIBuild.Theme(clock, ThemeRole.Screen).SetPaletteColor(ThemeColor.Positive);
            TMP_Text noSignal = UIBuild.Label("No Signal", canvas, "NO SIGNAL", ThemeRole.Screen, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(GpsPixels.x, 80f));
            UIBuild.Theme(noSignal, ThemeRole.Screen).SetPaletteColor(ThemeColor.Danger);
            noSignal.gameObject.SetActive(false);

            SetInt(view, "screen", (int)DashScreen.Gps);
            SetRef(view, "canvas", canvas);
            SetRef(view, "mapArea", map);
            SetRef(view, "mapGroup", group.gameObject);
            SetRef(view, "routeLine", route);
            SetRef(view, "cliffLine", cliff);
            SetRef(view, "stubLines", stubs);
            SetRef(view, "markerRoot", markers);
            SetRef(view, "busArrow", arrow);
            SetRef(view, "nextLabel", nextLabel);
            SetRef(view, "nextText", next);
            SetRef(view, "detailText", detail);
            SetRef(view, "ratingText", rating);
            SetRef(view, "clockText", clock);
            SetRef(view, "noSignalText", noSignal);
            Color disabled = theme.Palette(ThemeColor.Disabled);
            SetColor(view, "routeColor", theme.Palette(ThemeColor.Text));
            SetColor(view, "travelledColor", new Color(disabled.r, disabled.g, disabled.b, 0.45f));
            SetColor(view, "servedColor", theme.Palette(ThemeColor.Positive));
            SetColor(view, "nextColor", theme.Palette(ThemeColor.Highlight));
            SetColor(view, "upcomingColor", theme.Palette(ThemeColor.Text));
            SetColor(view, "missedColor", disabled);
            SetColor(view, "earlyColor", theme.Palette(ThemeColor.Positive));
            SetColor(view, "lateColor", theme.Palette(ThemeColor.Danger));
            SetColor(view, "onTimeColor", theme.Palette(ThemeColor.Text));
        }

        // Stretched over its parent with the origin at the bottom-left, so child points are pixels
        // from the map's corner
        static void FillFromOrigin(RectTransform rect) {
            UIBuild.Stretch(rect);
            rect.pivot = Vector2.zero;
        }

        static UILineRenderer Line(string name, RectTransform map, float thickness, bool segments, float dash, float gap, Color color) {
            RectTransform rect = (RectTransform)UIBuild.UIObject(name, map).transform;
            FillFromOrigin(rect);
            UILineRenderer line = rect.gameObject.AddComponent<UILineRenderer>();
            line.Configure(thickness, segments, false, dash, gap);
            line.color = color;
            line.raycastTarget = false;
            return line;
        }

        static void SetColor(Object target, string prop, Color value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(prop);
            if (property == null) {
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{prop}'");
            }
            property.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
