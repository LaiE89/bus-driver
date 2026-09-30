using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Editor.Builders;
using BusDriver.Tests.EditMode.Route;
using BusDriver.UI.Dash;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.UI {
    // The dash GPS's layout (T-M2-13, §4.13) on the generated Dash prefab and Route01
    public class GpsLayoutTests {
        GameObject instance;
        RouteMapView view;
        RouteDefinition route;
        RoutePath path;

        [SetUp]
        public void SetUp() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DashPrefabBuilder.Path);
            Assert.IsNotNull(prefab, DashPrefabBuilder.Path + " is missing; run Build All");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            view = instance.GetComponentInChildren<RouteMapView>(true);
            Assert.IsNotNull(view, "the Dash prefab has no RouteMapView");
            route = RouteTestData.Route01;
            path = new RoutePath(route);
            view.Layout(route, path);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(instance);
        }

        static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> line) {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++) {
                Vector2 a = line[i];
                Vector2 b = line[i + 1];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
            }
            return best;
        }

        [Test]
        public void EveryStopMarkerLiesOnTheRouteLine() {
            Assert.AreEqual(route.stops.Length, view.Markers.Count);
            for (int i = 0; i < view.Markers.Count; i++) {
                GpsStopMarker marker = view.Markers[i];
                Assert.AreEqual(route.stops[i].stopId, marker.StopId);
                Vector2 expected = view.Projection.ToMap(path.Evaluate(route.stops[i].distance).Position);
                Assert.Less(Vector2.Distance(expected, marker.Rect.anchoredPosition), 2f, marker.StopId + " is off its projected route point");
                Assert.Less(DistanceToPolyline(marker.Rect.anchoredPosition, view.RouteLine.Points), 2f, marker.StopId + " isn't on the drawn route");
            }
        }

        [Test]
        public void TheRouteFitsNorthUpWithTheMargin() {
            Rect map = new Rect(Vector2.zero, new Vector2(DashPrefabBuilder.GpsMapSize, DashPrefabBuilder.GpsMapSize));
            float margin = DashPrefabBuilder.GpsMapSize * 0.06f - 0.01f;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 p in view.RouteLine.Points) {
                Assert.IsTrue(p.x >= map.xMin + margin && p.x <= map.xMax - margin && p.y >= map.yMin + margin && p.y <= map.yMax - margin,
                    $"{p} is outside the map's 6 % margin");
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            // Fitted: the longer side touches the margin
            float usable = DashPrefabBuilder.GpsMapSize - 2f * DashPrefabBuilder.GpsMapSize * 0.06f;
            Assert.AreEqual(usable, Mathf.Max(max.x - min.x, max.y - min.y), 1f);
            // North-up: the route starts heading +Z, which is up on the map
            Assert.Greater(view.RouteLine.Points[4].y, view.RouteLine.Points[0].y);
            Assert.AreEqual(view.RouteLine.Points[4].x, view.RouteLine.Points[0].x, 0.01f);
        }

        [Test]
        public void TheCliffIsRedAndDashed() {
            Assert.Greater(view.CliffLine.Points.Count, 2);
            Assert.IsTrue(view.CliffLine.IsDashed, "the cliff must be dashed as well as red (§2.23)");
            Assert.Greater(view.CliffLine.color.r, 0.6f);
            Assert.Less(view.CliffLine.color.g, 0.5f);
            RouteZone cliff;
            Assert.IsTrue(route.TryGetZone(RouteZoneKind.Cliff, out cliff));
            Assert.Less(Vector2.Distance(view.CliffLine.Points[0], view.Projection.ToMap(path.Evaluate(cliff.start).Position)), 0.5f);
        }

        [Test]
        public void StubsAreShortDeadEnds() {
            UILineRenderer stubs = null;
            foreach (UILineRenderer line in instance.GetComponentsInChildren<UILineRenderer>(true)) {
                if (line.name == "Stubs") {
                    stubs = line;
                }
            }
            Assert.IsNotNull(stubs);
            Assert.AreEqual(route.stubs.Length * 2, stubs.Points.Count);
            for (int i = 0; i + 1 < stubs.Points.Count; i += 2) {
                Assert.AreEqual(8f, Vector2.Distance(stubs.Points[i], stubs.Points[i + 1]), 0.01f);
            }
        }
    }
}
