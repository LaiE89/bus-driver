using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // The §3.4 profiles (T-M2-03), built in memory under a scratch root. The full containment check
    // against the generated scene is RouteContainmentTests (T-M2-08).
    public class ProfileBuilderTests {
        RouteDefinition route;
        RoutePath path;
        GameObject root;
        ProfileBuildResult result;

        [SetUp]
        public void SetUp() {
            route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DataSeeder.DataRoot + "/" + RouteSeed.RelativePath);
            Assert.IsNotNull(route, "Route01 is missing; run Build All");
            path = new RoutePath(route);
            root = new GameObject("ProfileBuilderTests");
            result = ProfileBuilder.Build(route, path, root.transform, null, null);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(root);
            foreach (Mesh mesh in result.Meshes) {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RunsMergeSegmentsAndCoverTheRoute() {
            foreach (RouteSide side in new[] { RouteSide.Left, RouteSide.Right }) {
                List<ProfileRun> runs = ProfileBuilder.Runs(route, path, side);
                Assert.AreEqual(0f, runs[0].From);
                Assert.AreEqual(path.TotalLength, runs[runs.Count - 1].To, 1e-3f);
                for (int i = 1; i < runs.Count; i++) {
                    Assert.AreEqual(runs[i - 1].To, runs[i].From, 1e-3f);
                    Assert.AreNotEqual(runs[i - 1].Profile, runs[i].Profile, "neighbouring runs are merged");
                }
            }
            // Dead Man's Bend is the one CliffDrop run, exactly segment 15
            List<ProfileRun> left = ProfileBuilder.Runs(route, path, RouteSide.Left);
            ProfileRun cliff = left.Find(r => r.Profile == SideProfile.CliffDrop);
            Assert.AreEqual(1450f, cliff.From, 0.01f);
            Assert.AreEqual(1650f, cliff.To, 0.01f);
        }

        [Test]
        public void ContainmentCollidersAreTaggedOnWorldAndFaceTheRoad() {
            Assert.Greater(result.Containment.Count, 20);
            foreach (Collider collider in result.Containment) {
                Assert.AreEqual(Tags.Containment, collider.tag, collider.name);
                Assert.AreEqual(Layers.World, collider.gameObject.layer, collider.name);
            }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>()) {
                Assert.AreEqual(Layers.World, collider.gameObject.layer, collider.name);
            }
        }

        // The cliff's left side has no containment: nothing tagged within 10 m of the bend's middle
        [Test]
        public void TheCliffHasNoContainmentOnTheLeft() {
            Physics.SyncTransforms();
            for (float d = 1460f; d <= 1640f; d += 10f) {
                RoutePose pose = path.Evaluate(d);
                Vector3 from = pose.Position + Vector3.up;
                RaycastHit[] hits = Physics.RaycastAll(from, -pose.Right, 10f);
                foreach (RaycastHit hit in hits) {
                    Assert.AreNotEqual(Tags.Containment, hit.collider.tag, $"containment on the cliff side at {d} m: {hit.collider.name}");
                }
                // The rock face on the right still contains
                Assert.IsTrue(HitsContainment(from, pose.Right), $"no containment on the right at {d} m");
            }
        }

        // Rays from the centre hit containment on both sides everywhere outside the cliff (no stubs
        // passed here, so no openings). The ribbon itself isn't built, so rays run 1 m above the road.
        [Test]
        public void EveryOtherMetreIsContainedOnBothSides() {
            Physics.SyncTransforms();
            List<string> misses = new List<string>();
            for (float d = 2f; d < path.TotalLength - 2f; d += 5f) {
                RoutePose pose = path.Evaluate(d);
                Vector3 from = pose.Position + Vector3.up;
                if (!HitsContainment(from, pose.Right)) {
                    misses.Add($"R {d}");
                }
                bool cliff = d >= 1450f && d <= 1650f;
                if (!cliff && !HitsContainment(from, -pose.Right)) {
                    misses.Add($"L {d}");
                }
            }
            Assert.IsEmpty(misses, "uncontained: " + string.Join(", ", misses));
        }

        static bool HitsContainment(Vector3 from, Vector3 direction) {
            foreach (RaycastHit hit in Physics.RaycastAll(from, direction, 10f)) {
                if (hit.collider.CompareTag(Tags.Containment)) {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void GuardrailsReachTwoMetresIntoTheCliff() {
            bool before = false;
            bool after = false;
            foreach (RailSpot rail in result.Guardrails) {
                if (rail.Side != RouteSide.Left) {
                    continue;
                }
                before |= rail.Distance <= 1450f + 0.01f && rail.Distance + rail.Length >= 1452f - 0.01f;
                after |= rail.Distance <= 1648f + 0.01f && rail.Distance + rail.Length > 1650f;
            }
            Assert.IsTrue(before, "guardrail over 1450–1452");
            Assert.IsTrue(after, "guardrail over 1648–1650");
            Assert.IsTrue(result.EndCaps.Exists(c => c.Side == RouteSide.Left && Mathf.Abs(c.Distance - 1452f) < 0.01f));
            Assert.IsTrue(result.EndCaps.Exists(c => c.Side == RouteSide.Left && Mathf.Abs(c.Distance - 1648f) < 0.01f));
        }

        [Test]
        public void NoDegenerateTriangles() {
            foreach (Mesh mesh in result.Meshes) {
                Assert.Greater(RouteStrip.MinTriangleArea(mesh), 1e-4f, mesh.name);
            }
        }

        [Test]
        public void DressingIsDeterministic() {
            GameObject again = new GameObject("again");
            ProfileBuildResult second = ProfileBuilder.Build(route, path, again.transform, null, null);
            try {
                Assert.AreEqual(result.Trees.Count, second.Trees.Count);
                Assert.Greater(result.Trees.Count, 300);
                for (int i = 0; i < result.Trees.Count; i++) {
                    Assert.AreEqual(result.Trees[i].Position, second.Trees[i].Position);
                }
                Assert.AreEqual(result.Rocks.Count, second.Rocks.Count);
                // Tunnel lights every 12 m down the 150 m tunnel, starting 6 m in
                Assert.AreEqual(12, result.TunnelLights.Count);
            }finally {
                Object.DestroyImmediate(again);
                foreach (Mesh mesh in second.Meshes) {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
