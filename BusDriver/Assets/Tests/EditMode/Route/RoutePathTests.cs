using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Route {
    // RoutePath evaluation and projection (§3, T-M2-02)
    public class RoutePathTests {
        RoutePath path;

        [SetUp]
        public void SetUp() {
            path = new RoutePath(RouteTestData.Route01);
        }

        [Test]
        public void StartsAtTheOriginHeadingPlusZ() {
            RoutePose start = path.Evaluate(0f);
            Assert.That(Vector3.Distance(start.Position, Vector3.zero), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(start.Forward, Vector3.forward), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(start.Right, Vector3.right), Is.LessThan(1e-4f));
        }

        [Test]
        public void PositiveArcsTurnRight() {
            // Segment 2 is R 120, +30°: after it the heading is +30° and the road has moved toward +x
            RoutePose after = path.Evaluate(path.SegmentStart(2));
            Assert.AreEqual(30f, after.HeadingDeg, 0.01f);
            Assert.Greater(after.Position.x, 0f);
        }

        [Test]
        public void TotalLengthIsTheSumOfTheSegments() {
            float sum = 0f;
            foreach (RouteSegment segment in RouteTestData.Route01.segments) {
                sum += segment.Length;
            }
            Assert.AreEqual(sum, path.TotalLength, 0.01f);
        }

        // "Projection error < 0.05 m on the road": points across the whole road width, all along the
        // route, project back to their own distance and lateral offset
        [Test]
        public void ProjectionErrorIsUnderFiveCentimetresOnTheRoad() {
            float worstAlong = 0f;
            float worstLateral = 0f;
            float[] laterals = { -4.5f, -3.5f, -1.75f, 0f, 1.75f, 3.5f, 4.5f };
            for (float d = 0.5f; d < path.TotalLength - 0.5f; d += 3.7f) {
                RoutePose pose = path.Evaluate(d);
                foreach (float lateral in laterals) {
                    Vector3 point = pose.Offset(lateral, 1.2f);
                    RouteProjection projection = path.Project(point, d + 15f);
                    worstAlong = Mathf.Max(worstAlong, Mathf.Abs(projection.Distance - d));
                    worstLateral = Mathf.Max(worstLateral, Mathf.Abs(projection.Lateral - lateral));
                }
            }
            Assert.Less(worstAlong, 0.05f, "along");
            Assert.Less(worstLateral, 0.05f, "lateral");
        }

        [Test]
        public void ABadHintFallsBackToTheWholeRoute() {
            RoutePose pose = path.Evaluate(2500f);
            RouteProjection projection = path.Project(pose.Offset(2f), 100f);
            Assert.AreEqual(2500f, projection.Distance, 0.05f);
            Assert.AreEqual(2f, projection.Lateral, 0.05f);
            Assert.AreEqual(2500f, path.Project(pose.Position).Distance, 0.05f);
        }

        [Test]
        public void ProjectionClampsPastTheEnds() {
            Assert.AreEqual(0f, path.Project(new Vector3(0f, 0f, -30f), 0f).Distance, 1e-3f);
            RoutePose end = path.Evaluate(path.TotalLength);
            Assert.AreEqual(path.TotalLength, path.Project(end.Position + end.Forward * 20f, path.TotalLength).Distance, 1e-3f);
        }

        // Heading continuity: no jump at any segment join. The tightest arc is R 60, so a 0.25 m step
        // turns at most 0.24°.
        [Test]
        public void HeadingAndPositionAreContinuous() {
            const float step = 0.25f;
            RoutePose previous = path.Evaluate(0f);
            for (float d = step; d <= path.TotalLength; d += step) {
                RoutePose pose = path.Evaluate(d);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(previous.HeadingDeg, pose.HeadingDeg)), 0.3f, $"heading jump at {d} m");
                Vector3 planar = pose.Position - previous.Position;
                planar.y = 0f;
                Assert.AreEqual(step, planar.magnitude, 0.002f, $"position jump at {d} m");
                Assert.Less(Mathf.Abs(pose.Position.y - previous.Position.y), step * 0.05f, $"elevation jump at {d} m");
                previous = pose;
            }
        }

        [Test]
        public void SamplesMatchEvaluate() {
            for (int i = 0; i < path.SampleCount; i += 37) {
                RoutePose pose = path.Evaluate(Mathf.Min(i * path.SampleStep, path.TotalLength));
                Assert.Less(Vector2.Distance(path.SampleAt(i), new Vector2(pose.Position.x, pose.Position.z)), 1e-3f);
            }
        }

        // §3: elevation climbs from 0 m to about +30.8 m
        [Test]
        public void ElevationEndsNearThirtyOneMetres() {
            Assert.AreEqual(30.75f, path.ElevationAt(path.TotalLength), 0.1f);
        }
    }
}
