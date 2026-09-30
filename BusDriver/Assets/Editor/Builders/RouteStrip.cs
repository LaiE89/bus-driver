using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // Which way a strip's front faces point. Single-sided renderers and raycasts (the containment
    // test's rays, §3.5) only see front faces, so every strip picks its side explicitly.
    public enum StripFacing { Up, Down, TowardRoad, AwayFromRoad }

    // One column edge of a strip: an unsigned distance from the centreline (the side decides the
    // sign) and a height above the road, or an absolute world height.
    public struct StripEdge {
        public float Lateral;
        public float Height;
        public bool AbsoluteY;

        public StripEdge(float lateral, float height, bool absoluteY = false) {
            Lateral = lateral;
            Height = height;
            AbsoluteY = absoluteY;
        }
    }

    // Builds meshes that follow the route row by row (§4.15): one row of edges per sample distance,
    // quads between neighbouring rows and columns. Vertices are relative to an origin, so a 100 m
    // chunk keeps full float precision; the GameObject sits at that origin.
    public static class RouteStrip {
        // The distances of the rows over [from, to]: every step, plus both ends exactly
        public static List<float> Rows(float from, float to, float step) {
            List<float> rows = new List<float>();
            if (to <= from) {
                return rows;
            }
            rows.Add(from);
            float next = (Mathf.Floor(from / step) + 1f) * step;
            while (next < to - 1e-3f) {
                if (next > from + 1e-3f) {
                    rows.Add(next);
                }
                next += step;
            }
            rows.Add(to);
            return rows;
        }

        public static float SideSign(RouteSide side) {
            return side == RouteSide.Left ? -1f : 1f;
        }

        // columnSubmesh (optional) gives each column band its submesh; the default is one submesh
        public static Mesh Build(RoutePath path, IList<float> rows, RouteSide side, Func<float, StripEdge[]> edgesAt,
                                 Vector3 origin, StripFacing facing, float uvLength, int[] columnSubmesh = null, string name = "Strip") {
            if (rows.Count < 2) {
                throw new ArgumentException("a strip needs at least two rows");
            }
            float sign = SideSign(side);
            int columns = edgesAt(rows[0]).Length;
            Vector3[] vertices = new Vector3[rows.Count * columns];
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int r = 0; r < rows.Count; r++) {
                RoutePose pose = path.Evaluate(rows[r]);
                StripEdge[] edges = edgesAt(rows[r]);
                if (edges.Length != columns) {
                    throw new ArgumentException("every row of a strip needs the same number of edges");
                }
                for (int c = 0; c < columns; c++) {
                    Vector3 p = pose.Position + pose.Right * (edges[c].Lateral * sign);
                    p.y = edges[c].AbsoluteY ? edges[c].Height : pose.Position.y + edges[c].Height;
                    vertices[r * columns + c] = p - origin;
                    uvs[r * columns + c] = new Vector2(columns > 1 ? (float)c / (columns - 1) : 0f, rows[r] / uvLength);
                }
            }

            int bands = columns - 1;
            int submeshes = 1;
            if (columnSubmesh != null) {
                for (int i = 0; i < columnSubmesh.Length; i++) {
                    submeshes = Mathf.Max(submeshes, columnSubmesh[i] + 1);
                }
            }
            List<int>[] triangles = new List<int>[submeshes];
            for (int s = 0; s < submeshes; s++) {
                triangles[s] = new List<int>();
            }
            for (int r = 0; r < rows.Count - 1; r++) {
                for (int c = 0; c < bands; c++) {
                    int a = r * columns + c;
                    int b = a + 1;
                    int next = a + columns;
                    int nextB = next + 1;
                    List<int> list = triangles[columnSubmesh != null ? columnSubmesh[c] : 0];
                    list.Add(a); list.Add(next); list.Add(b);
                    list.Add(b); list.Add(next); list.Add(nextB);
                }
            }

            // Face the requested way: the first quad of the first band decides for the whole strip,
            // since every band of a strip shares the row direction
            RoutePose first = path.Evaluate(rows[0]);
            Vector3 want = Wanted(facing, first.Right * sign);
            Vector3 v0 = vertices[0];
            Vector3 normal = Vector3.Cross(vertices[columns] - v0, vertices[1] - v0);
            if (Vector3.Dot(normal, want) < 0f) {
                for (int s = 0; s < submeshes; s++) {
                    List<int> list = triangles[s];
                    for (int i = 0; i < list.Count; i += 3) {
                        int swap = list[i + 1];
                        list[i + 1] = list[i + 2];
                        list[i + 2] = swap;
                    }
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = submeshes;
            for (int s = 0; s < submeshes; s++) {
                mesh.SetTriangles(triangles[s], s);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat quad across the road direction at one distance, between two laterals and heights:
        // closes the gap where a side's containment line jumps between offsets
        public static Mesh Joint(RoutePath path, float distance, RouteSide side, float lateralA, float lateralB,
                                 float bottom, float top, Vector3 origin, string name = "Joint") {
            RoutePose pose = path.Evaluate(distance);
            float sign = SideSign(side);
            Vector3 a = pose.Position + pose.Right * (lateralA * sign);
            Vector3 b = pose.Position + pose.Right * (lateralB * sign);
            Vector3[] vertices = {
                a + Vector3.up * bottom - origin, b + Vector3.up * bottom - origin,
                a + Vector3.up * top - origin, b + Vector3.up * top - origin,
            };
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Wanted(StripFacing facing, Vector3 outward) {
            switch (facing) {
                case StripFacing.Up: return Vector3.up;
                case StripFacing.Down: return Vector3.down;
                case StripFacing.TowardRoad: return -outward;
                default: return outward;
            }
        }

        // Smallest triangle area, for the degenerate-triangle checks
        public static float MinTriangleArea(Mesh mesh) {
            Vector3[] vertices = mesh.vertices;
            float min = float.MaxValue;
            for (int s = 0; s < mesh.subMeshCount; s++) {
                int[] triangles = mesh.GetTriangles(s);
                for (int i = 0; i < triangles.Length; i += 3) {
                    Vector3 a = vertices[triangles[i]];
                    float area = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a).magnitude * 0.5f;
                    min = Mathf.Min(min, area);
                }
            }
            return min;
        }
    }
}
