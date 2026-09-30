using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Route {
    // The §3 layout checks, re-made against the data (T-M2-02, §4.19)
    public class RouteLayoutTests {
        RouteDefinition route;
        RoutePath path;

        [SetUp]
        public void SetUp() {
            route = RouteTestData.Route01;
            path = new RoutePath(route);
        }

        [Test]
        public void TotalLengthIs3000() {
            Assert.AreEqual(3000f, path.TotalLength, 0.1f);
        }

        [Test]
        public void FinalHeadingIsMinusTen() {
            Assert.AreEqual(-10f, path.Evaluate(path.TotalLength).HeadingDeg, 0.5f);
        }

        // Parts of the road more than 150 m apart along it are never closer than 100 m in the plane
        [Test]
        public void TheRoadNeverPassesNearItself() {
            const float step = 2f;
            const float along = 150f;
            List<Vector2> points = new List<Vector2>();
            for (float d = 0f; d <= path.TotalLength; d += step) {
                Vector3 p = path.Evaluate(d).Position;
                points.Add(new Vector2(p.x, p.z));
            }
            int skip = Mathf.CeilToInt(along / step) + 1;
            float closest = float.MaxValue;
            float atA = 0f;
            float atB = 0f;
            for (int i = 0; i < points.Count; i++) {
                for (int j = i + skip; j < points.Count; j++) {
                    float distance = Vector2.Distance(points[i], points[j]);
                    if (distance < closest) {
                        closest = distance;
                        atA = i * step;
                        atB = j * step;
                    }
                }
            }
            Assert.GreaterOrEqual(closest, 100f, $"{closest:0.0} m between {atA} m and {atB} m");
        }

        // No road within 300 m of the cliff's outer side (D72): any road more than 150 m (along the
        // route) from the cliff that lies outward of the cliff edge is at least 300 m from it
        [Test]
        public void NoRoadBeyondTheCliffWithin300Metres() {
            Assert.IsTrue(route.TryGetZone(RouteZoneKind.Cliff, out RouteZone cliff), "Route01 has a Cliff zone");
            List<RoutePose> edge = new List<RoutePose>();
            for (float d = cliff.start; d <= cliff.end; d += 1f) {
                edge.Add(path.Evaluate(d));
            }
            float closest = float.MaxValue;
            float at = 0f;
            for (float d = 0f; d <= path.TotalLength; d += 2f) {
                if (d > cliff.start - 150f && d < cliff.end + 150f) {
                    continue;
                }
                Vector3 p = path.Evaluate(d).Position;
                int nearest = 0;
                float nearestSqr = float.MaxValue;
                for (int i = 0; i < edge.Count; i++) {
                    Vector3 offset = p - edge[i].Position;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < nearestSqr) {
                        nearestSqr = offset.sqrMagnitude;
                        nearest = i;
                    }
                }
                // Past the ends of the bend isn't "outward of the edge"
                if (nearest == 0 || nearest == edge.Count - 1) {
                    continue;
                }
                RoutePose c = edge[nearest];
                Vector3 toward = p - c.Position;
                bool outward = Vector3.Dot(toward, c.Right) < 0f;   // the cliff drops on the left
                if (outward && Mathf.Sqrt(nearestSqr) < closest) {
                    closest = Mathf.Sqrt(nearestSqr);
                    at = d;
                }
            }
            Assert.GreaterOrEqual(closest, 300f, $"road at {at} m is {closest:0.0} m beyond the cliff edge");
        }
    }
}
