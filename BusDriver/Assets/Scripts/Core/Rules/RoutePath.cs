using System;
using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Core.Rules {
    // A point on the route: where the centreline is at a distance and which way it runs.
    // Forward and Right are horizontal unit vectors; Position includes the elevation.
    public struct RoutePose {
        public float Distance;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Right;
        // Degrees clockwise from +Z, seen from above (a right turn increases it)
        public float HeadingDeg;
        public float GradePercent;
        public int SegmentIndex;

        // Up the slope, for orienting things that sit on the road
        public Vector3 Tangent {
            get { return (Forward + Vector3.up * (GradePercent / 100f)).normalized; }
        }

        public Quaternion Rotation {
            get { return Quaternion.LookRotation(Forward, Vector3.up); }
        }

        public Vector3 Offset(float lateral, float up = 0f) {
            return Position + Right * lateral + Vector3.up * up;
        }
    }

    // Where a world point sits relative to the route (horizontal plane; elevation is ignored)
    public struct RouteProjection {
        public float Distance;
        // Signed: + is right of the centreline in the route direction
        public float Lateral;
        // The centreline point at Distance
        public Vector3 Point;
        public int SegmentIndex;
    }

    // The route's centreline as a pure path (§3, §4.6): built once from the RouteDefinition's
    // segments, starting at the world origin heading +Z. Evaluate is exact per segment; Project
    // uses the 1 m samples to find the right segment, then projects onto it exactly. No per-call
    // allocations, so RouteTracker can call it every FixedUpdate.
    public sealed class RoutePath {
        // One segment's exact geometry, in the plane
        struct Piece {
            public SegmentKind Kind;
            public float Start;
            public float Length;
            public Vector2 Origin;       // x, z at the start
            public float Heading0;       // radians
            public float Radius;
            public float Turn;           // +1 right, −1 left (arcs)
            public Vector2 Centre;       // arcs
            public float Elevation0;
            public float Grade;          // percent
        }

        readonly Piece[] pieces;
        readonly Vector2[] samples;
        readonly float step;

        public float TotalLength { get; private set; }
        public int SampleCount { get { return samples.Length; } }
        public float SampleStep { get { return step; } }
        public int SegmentCount { get { return pieces.Length; } }

        // How far either side of the hint Project searches before it falls back to the whole route
        public const float ProjectWindow = 50f;
        // A window result further than this from the road is treated as a miss
        const float WindowMissDistance = 25f;

        public RoutePath(RouteDefinition route) : this(route.segments, route.generation != null ? route.generation.sampleStep : 1f) {
        }

        public RoutePath(RouteSegment[] segments, float sampleStep = 1f) {
            if (segments == null || segments.Length == 0) {
                throw new ArgumentException("a route needs at least one segment");
            }
            step = sampleStep > 0f ? sampleStep : 1f;
            pieces = new Piece[segments.Length];
            Vector2 position = Vector2.zero;
            float heading = 0f;
            float elevation = 0f;
            float distance = 0f;
            for (int i = 0; i < segments.Length; i++) {
                RouteSegment segment = segments[i];
                Piece piece = new Piece {
                    Kind = segment.kind,
                    Start = distance,
                    Length = segment.Length,
                    Origin = position,
                    Heading0 = heading,
                    Elevation0 = elevation,
                    Grade = segment.gradePercent,
                };
                if (segment.kind == SegmentKind.Arc) {
                    piece.Radius = segment.radius;
                    piece.Turn = segment.angleDeg >= 0f ? 1f : -1f;
                    piece.Centre = position + RightOf(heading) * (segment.radius * piece.Turn);
                }
                pieces[i] = piece;
                PlanarAt(ref piece, piece.Length, out position, out heading);
                elevation += piece.Length * piece.Grade / 100f;
                distance += piece.Length;
            }
            TotalLength = distance;

            int count = Mathf.FloorToInt(TotalLength / step) + 1;
            bool partial = count * step - step < TotalLength - 1e-4f;
            samples = new Vector2[partial ? count + 1 : count];
            for (int i = 0; i < samples.Length; i++) {
                float d = Mathf.Min(i * step, TotalLength);
                int index = SegmentIndexAt(d);
                PlanarAt(ref pieces[index], d - pieces[index].Start, out samples[i], out _);
            }
        }

        // ------------------------------------------------------------------ evaluation

        public RoutePose Evaluate(float distance) {
            float d = Mathf.Clamp(distance, 0f, TotalLength);
            int index = SegmentIndexAt(d);
            Piece piece = pieces[index];
            float along = d - piece.Start;
            PlanarAt(ref piece, along, out Vector2 planar, out float heading);
            float y = piece.Elevation0 + along * piece.Grade / 100f;
            Vector2 forward = ForwardOf(heading);
            Vector2 right = RightOf(heading);
            return new RoutePose {
                Distance = d,
                Position = new Vector3(planar.x, y, planar.y),
                Forward = new Vector3(forward.x, 0f, forward.y),
                Right = new Vector3(right.x, 0f, right.y),
                HeadingDeg = heading * Mathf.Rad2Deg,
                GradePercent = piece.Grade,
                SegmentIndex = index,
            };
        }

        public float ElevationAt(float distance) {
            float d = Mathf.Clamp(distance, 0f, TotalLength);
            Piece piece = pieces[SegmentIndexAt(d)];
            return piece.Elevation0 + (d - piece.Start) * piece.Grade / 100f;
        }

        public float SegmentStart(int index) {
            return pieces[index].Start;
        }

        public float SegmentLength(int index) {
            return pieces[index].Length;
        }

        // The segment containing a distance; a boundary belongs to the later segment
        public int SegmentIndexAt(float distance) {
            int lo = 0;
            int hi = pieces.Length - 1;
            while (lo < hi) {
                int mid = (lo + hi + 1) / 2;
                if (pieces[mid].Start <= distance) {
                    lo = mid;
                }else {
                    hi = mid - 1;
                }
            }
            return lo;
        }

        // Sample i's planar position (x, z)
        public Vector2 SampleAt(int index) {
            return samples[index];
        }

        // ------------------------------------------------------------------ projection

        // Searches ±ProjectWindow around the hint first (the bus moves a few cm per physics step), and
        // the whole route when the window misses: a teleport, a respawn or a bad hint.
        public RouteProjection Project(Vector3 worldPos, float hintDistance) {
            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            int first = Mathf.Clamp(Mathf.FloorToInt((hintDistance - ProjectWindow) / step), 0, samples.Length - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt((hintDistance + ProjectWindow) / step), 0, samples.Length - 1);
            int best = NearestSample(p, first, last, out float bestSqr);
            bool atWindowEdge = (best == first && first > 0) || (best == last && last < samples.Length - 1);
            if (atWindowEdge || bestSqr > WindowMissDistance * WindowMissDistance) {
                best = NearestSample(p, 0, samples.Length - 1, out _);
            }
            return Refine(p, best);
        }

        // The whole route, with no hint
        public RouteProjection Project(Vector3 worldPos) {
            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            return Refine(p, NearestSample(p, 0, samples.Length - 1, out _));
        }

        int NearestSample(Vector2 p, int first, int last, out float bestSqr) {
            int best = first;
            bestSqr = float.MaxValue;
            for (int i = first; i <= last; i++) {
                float sqr = (samples[i] - p).sqrMagnitude;
                if (sqr < bestSqr) {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        // Exact projection onto the nearest sample's segment and its neighbours; the closest wins
        RouteProjection Refine(Vector2 p, int sample) {
            int index = SegmentIndexAt(Mathf.Min(sample * step, TotalLength));
            RouteProjection best = default(RouteProjection);
            float bestSqr = float.MaxValue;
            for (int k = Mathf.Max(0, index - 1); k <= Mathf.Min(pieces.Length - 1, index + 1); k++) {
                float along = ProjectOnto(ref pieces[k], p);
                PlanarAt(ref pieces[k], along, out Vector2 point, out float heading);
                float sqr = (p - point).sqrMagnitude;
                if (sqr < bestSqr) {
                    bestSqr = sqr;
                    Vector2 right = RightOf(heading);
                    float d = pieces[k].Start + along;
                    best = new RouteProjection {
                        Distance = d,
                        Lateral = Vector2.Dot(p - point, right),
                        Point = new Vector3(point.x, pieces[k].Elevation0 + along * pieces[k].Grade / 100f, point.y),
                        SegmentIndex = k,
                    };
                }
            }
            return best;
        }

        // Distance along the piece (clamped to it) of the point's foot on its centreline
        static float ProjectOnto(ref Piece piece, Vector2 p) {
            if (piece.Kind == SegmentKind.Straight) {
                return Mathf.Clamp(Vector2.Dot(p - piece.Origin, ForwardOf(piece.Heading0)), 0f, piece.Length);
            }
            Vector2 v = p - piece.Centre;
            if (v.sqrMagnitude < 1e-8f) {
                return piece.Length * 0.5f;
            }
            // The heading whose centreline point lies in v's direction: Right(h) = −Turn · v̂
            float heading = Mathf.Atan2(piece.Turn * v.y, -piece.Turn * v.x);
            // Unwrap around the arc's middle so both ends clamp correctly
            float sweep = piece.Length / piece.Radius;
            float middle = piece.Heading0 + piece.Turn * sweep * 0.5f;
            float delta = Mathf.DeltaAngle(middle * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float along = (piece.Turn * (middle + delta - piece.Heading0)) * piece.Radius;
            return Mathf.Clamp(along, 0f, piece.Length);
        }

        // ------------------------------------------------------------------ plane geometry

        static void PlanarAt(ref Piece piece, float along, out Vector2 position, out float heading) {
            if (piece.Kind == SegmentKind.Straight) {
                heading = piece.Heading0;
                position = piece.Origin + ForwardOf(heading) * along;
                return;
            }
            heading = piece.Heading0 + piece.Turn * along / piece.Radius;
            position = piece.Centre - RightOf(heading) * (piece.Radius * piece.Turn);
        }

        // Heading 0 is +Z; positive turns right (clockwise from above), as §3 defines
        static Vector2 ForwardOf(float heading) {
            return new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
        }

        static Vector2 RightOf(float heading) {
            return new Vector2(Mathf.Cos(heading), -Mathf.Sin(heading));
        }
    }
}
