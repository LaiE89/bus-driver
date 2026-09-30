using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // The road ribbon (T-M2-03): 7.0 m wide (6.5 m through the cliff), no degenerate triangles,
    // normals up. Built in memory from the committed Route01 asset; nothing is written.
    public class RoadMeshTests {
        RouteDefinition route;
        RoutePath path;
        readonly List<Mesh> meshes = new List<Mesh>();

        [SetUp]
        public void SetUp() {
            route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(DataSeeder.DataRoot + "/" + RouteSeed.RelativePath);
            Assert.IsNotNull(route, "Route01 is missing; run Build All");
            path = new RoutePath(route);
        }

        [TearDown]
        public void TearDown() {
            foreach (Mesh mesh in meshes) {
                Object.DestroyImmediate(mesh);
            }
            meshes.Clear();
        }

        Mesh Chunk(Vector2 range, bool dashes = false) {
            Vector3 origin = path.Evaluate(range.x).Position;
            Mesh mesh = dashes ? RoadMeshBuilder.DashMesh(route, path, range.x, range.y, origin)
                : RoadMeshBuilder.RoadMesh(route, path, range.x, range.y, origin);
            meshes.Add(mesh);
            return mesh;
        }

        [Test]
        public void ChunksCoverTheRouteInHundredMetrePieces() {
            List<Vector2> chunks = RoadMeshBuilder.Chunks(route, path);
            Assert.AreEqual(Mathf.CeilToInt(path.TotalLength / 100f), chunks.Count);
            Assert.AreEqual(0f, chunks[0].x);
            Assert.AreEqual(path.TotalLength, chunks[chunks.Count - 1].y, 1e-3f);
            for (int i = 1; i < chunks.Count; i++) {
                Assert.AreEqual(chunks[i - 1].y, chunks[i].x);
                Assert.LessOrEqual(chunks[i].y - chunks[i].x, 100f + 1e-3f);
            }
        }

        // Every row: the road edges (columns 1 and 2) are the road width apart, and the row's
        // distance comes back from v = distance / 4
        [Test]
        public void RoadWidthIsSevenMetresAndSixPointFiveInTheCliff() {
            Assert.IsTrue(route.TryGetZone(RouteZoneKind.Cliff, out RouteZone cliff));
            int cliffRows = 0;
            int otherRows = 0;
            foreach (Vector2 range in RoadMeshBuilder.Chunks(route, path)) {
                Mesh mesh = Chunk(range);
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Assert.AreEqual(0, vertices.Length % 4);
                for (int row = 0; row < vertices.Length / 4; row++) {
                    float distance = uvs[row * 4].y * route.generation.uvLength;
                    float width = Vector3.Distance(vertices[row * 4 + 1], vertices[row * 4 + 2]);
                    bool inCliff = distance >= cliff.start && distance <= cliff.end;
                    Assert.AreEqual(inCliff ? 6.5f : 7f, width, 0.001f, $"road width at {distance:0.0} m");
                    if (inCliff) {
                        cliffRows++;
                        Assert.AreEqual(0.3f, Vector3.Distance(vertices[row * 4], vertices[row * 4 + 1]), 0.001f, $"cliff shoulder at {distance:0.0} m");
                    }else {
                        otherRows++;
                        Assert.AreEqual(1f, Vector3.Distance(vertices[row * 4], vertices[row * 4 + 1]), 0.001f, $"left shoulder at {distance:0.0} m");
                    }
                    Assert.AreEqual(1f, Vector3.Distance(vertices[row * 4 + 2], vertices[row * 4 + 3]), 0.001f, $"right shoulder at {distance:0.0} m");
                }
            }
            Assert.Greater(cliffRows, 150);
            Assert.Greater(otherRows, 2500);
        }

        [Test]
        public void NoDegenerateTrianglesAndNormalsPointUp() {
            foreach (Vector2 range in RoadMeshBuilder.Chunks(route, path)) {
                foreach (bool dashes in new[] { false, true }) {
                    Mesh mesh = Chunk(range, dashes);
                    string what = (dashes ? "dashes " : "road ") + range;
                    if (mesh.vertexCount == 0) {
                        continue;
                    }
                    Assert.Greater(RouteStrip.MinTriangleArea(mesh), 1e-4f, what + ": degenerate triangle");
                    foreach (Vector3 normal in mesh.normals) {
                        Assert.Greater(normal.y, 0.99f, what + ": normal " + normal);
                    }
                    // The winding agrees with the normals (front faces up), not just RecalculateNormals
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3) {
                        Vector3 a = vertices[triangles[i]];
                        Vector3 face = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a);
                        Assert.Greater(face.y, 0f, what + ": triangle faces down");
                    }
                }
            }
        }

        [Test]
        public void RoadAndShouldersAreSeparateSubmeshes() {
            Mesh mesh = Chunk(new Vector2(0f, 100f));
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.AreEqual(mesh.GetTriangles(RoadMeshBuilder.RoadSubmesh).Length * 2, mesh.GetTriangles(RoadMeshBuilder.ShoulderSubmesh).Length);
        }

        [Test]
        public void DashesSitOnTheCentreLineEverySixMetres() {
            Mesh mesh = Chunk(new Vector2(0f, 100f), true);
            // 0, 6, … 96: 17 dashes of 6 vertices
            Assert.AreEqual(17 * 6, mesh.vertexCount);
            Vector3 origin = path.Evaluate(0f).Position;
            Vector3 middle = (mesh.vertices[2] + mesh.vertices[3]) * 0.5f + origin;
            RouteProjection projection = path.Project(middle, 1f);
            Assert.AreEqual(1f, projection.Distance, 0.01f);
            Assert.AreEqual(0f, projection.Lateral, 0.01f);
        }

        // Saving into a mesh folder overwrites in place: the second build keeps every GUID (§4.15)
        [Test]
        public void RebuildingKeepsTheMeshAssetGuids() {
            const string folder = "Assets/_RoadMeshTest";
            AssetDatabase.DeleteAsset(folder);
            GameObject first = new GameObject("first");
            GameObject second = new GameObject("second");
            try {
                RoadMeshBuilder.Build(route, path, first.transform, folder);
                string guid = AssetDatabase.AssetPathToGUID(folder + "/Road_000.asset");
                Assert.IsNotEmpty(guid);
                List<GameObject> chunks = RoadMeshBuilder.Build(route, path, second.transform, folder);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(folder + "/Road_000.asset"));
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Road_000.asset");
                Assert.AreSame(saved, chunks[0].GetComponent<MeshCollider>().sharedMesh);
                Assert.AreSame(saved, chunks[0].GetComponent<MeshFilter>().sharedMesh);
                Assert.Greater(saved.vertexCount, 0);
                Assert.IsTrue(chunks[0].isStatic);
                Assert.IsFalse(chunks[0].transform.Find("Dashes").gameObject.isStatic, "emissive dashes stay non-static (§4.17)");
            }finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
