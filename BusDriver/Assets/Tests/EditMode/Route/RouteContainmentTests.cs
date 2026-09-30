using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using BusDriver.Editor.Validation;
using BusDriver.Gameplay.Route;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Route {
    // §3.5 (T-M2-08): the bus can never leave the corridor except over the cliff. The check itself is
    // RouteContainmentCheck; here it runs against the generated scene, and against a copy of the data
    // with one guardrail stretch taken away, which it has to catch.
    public class RouteContainmentTests {
        // Segment 4 (§3.1): R 80, −45°, 420 → 482.83 m, a Drop (guardrail) on the right
        const int GuardrailSegment = 3;
        const float GuardrailFrom = 420f;
        const float GuardrailTo = 482.83f;

        [TearDown]
        public void TearDown() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Route01World_IsContainedEverywhereButTheCliff() {
            EditorSceneManager.OpenScene(RouteBuilder.ScenePath, OpenSceneMode.Single);
            RouteSceneRoot root = Object.FindAnyObjectByType<RouteSceneRoot>();
            Assert.IsNotNull(root, "Route01_World has no RouteSceneRoot; run Build All");
            Assert.IsNotNull(root.Route, "the route root has no RouteDefinition");

            List<RouteContainmentCheck.Failure> failures = RouteContainmentCheck.Run(root.Route);
            Assert.IsEmpty(failures, "containment gaps:\n" + string.Join("\n", failures));
        }

        [Test]
        public void MissingGuardrail_IsReportedAtItsDistance() {
            Assert.AreEqual(SideProfile.Drop, LoadRoute().segments[GuardrailSegment].right, "§3.1 segment 4 should have a guardrail on the right");

            // The same data unchanged passes, so the failure below is the missing guardrail's
            Assert.IsEmpty(CheckCopy(null), "the unmodified copy should pass");

            List<RouteContainmentCheck.Failure> failures = CheckCopy(copy => copy.segments[GuardrailSegment].right = SideProfile.CliffDrop);
            Assert.IsNotEmpty(failures, "removing the guardrail went unnoticed");
            foreach (RouteContainmentCheck.Failure failure in failures) {
                Assert.AreEqual(RouteSide.Right, failure.Side, "unexpected failure: " + failure);
                Assert.That(failure.Distance, Is.InRange(GuardrailFrom - RouteContainmentCheck.SampleStep, GuardrailTo + RouteContainmentCheck.SampleStep),
                    "unexpected failure: " + failure);
            }
            StringAssert.Contains("d=450", string.Join("\n", failures), "the report should name the distances");
        }

        static RouteDefinition LoadRoute() {
            RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DataSeeder.DataRoot + "/" + RouteSeed.RelativePath);
            Assert.IsNotNull(route, "Route01 is missing; run Build All");
            return route;
        }

        // A temporary copy of the data, built into a fresh scene with its meshes in memory. The asset
        // is loaded after the new scene, whose creation unloads unused assets.
        static List<RouteContainmentCheck.Failure> CheckCopy(System.Action<RouteDefinition> modify) {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RouteDefinition copy = Object.Instantiate(LoadRoute());
            GameObject built = null;
            try {
                if (modify != null) {
                    modify(copy);
                }
                built = RouteBuilder.BuildContainment(copy);
                return RouteContainmentCheck.Run(copy);
            }finally {
                if (built != null) {
                    DestroyWithMeshes(built);
                }
                Object.DestroyImmediate(copy);
            }
        }

        static void DestroyWithMeshes(GameObject root) {
            HashSet<Mesh> meshes = new HashSet<Mesh>();
            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true)) {
                meshes.Add(collider.sharedMesh);
            }
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                meshes.Add(filter.sharedMesh);
            }
            Object.DestroyImmediate(root);
            foreach (Mesh mesh in meshes) {
                if (mesh != null && !AssetDatabase.Contains(mesh)) {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
