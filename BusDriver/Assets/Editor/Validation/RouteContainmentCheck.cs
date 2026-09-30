using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Editor.Validation {
    // The containment rules of §3.5 as a physics check (T-M2-08), run against whatever route geometry
    // is loaded: every 5 m along the route, a ray from 1 m above the road centre toward each side
    // must hit a Containment collider within 10 m, or, at a stub mouth, a stub wall or blocker within
    // 30 m. On the cliff's left side there's no containment by design (D14); a FallZone must lie
    // within 6 m instead.
    public static class RouteContainmentCheck {
        public const float SampleStep = 5f;
        public const float RayHeight = 1f;
        public const float ContainmentReach = 10f;
        public const float StubReach = 30f;
        public const float FallZoneReach = 6f;
        // The route's two ends sit exactly on the walls' edges, where a ray can slip past a triangle
        const float EndInset = 0.1f;
        // A mouth sample within this much of an opening counts as at the mouth
        const float MouthMargin = 1f;

        public struct Failure {
            public float Distance;
            public RouteSide Side;
            public string Reason;

            public override string ToString() {
                return $"d={Distance:0.#} {Side}: {Reason}";
            }
        }

        public static List<Failure> Run(RouteDefinition route) {
            RoutePath path = new RoutePath(route);
            List<RouteOpening> openings = RouteBuilder.StubOpenings(route, path);
            List<Failure> failures = new List<Failure>();
            Physics.SyncTransforms();
            RaycastHit[] hits = new RaycastHit[64];
            for (float d = 0f; d <= path.TotalLength + 1e-3f; d += SampleStep) {
                float at = Mathf.Clamp(d, EndInset, path.TotalLength - EndInset);
                RoutePose pose = path.Evaluate(at);
                foreach (RouteSide side in new[] { RouteSide.Left, RouteSide.Right }) {
                    float sign = RouteStrip.SideSign(side);
                    if (side == RouteSide.Left && route.InZone(RouteZoneKind.Cliff, at)) {
                        if (!FallZoneWithin(pose, sign, hits)) {
                            failures.Add(new Failure { Distance = d, Side = side, Reason = $"no FallZone within {FallZoneReach} m of the cliff's edge" });
                        }
                        continue;
                    }
                    bool mouth = AtMouth(openings, side, at);
                    float reach = mouth ? StubReach : ContainmentReach;
                    float nearest = NearestContainment(pose.Position + Vector3.up * RayHeight, pose.Right * sign, hits);
                    if (nearest > reach) {
                        string found = nearest < float.MaxValue ? $"the nearest is {nearest:0.0} m away" : "none within " + StubReach + " m";
                        string what = mouth ? "no stub wall or blocker" : "no Containment collider";
                        failures.Add(new Failure { Distance = d, Side = side, Reason = $"{what} within {reach} m ({found})" });
                    }
                }
            }
            return failures;
        }

        static bool AtMouth(List<RouteOpening> openings, RouteSide side, float distance) {
            for (int i = 0; i < openings.Count; i++) {
                if (openings[i].Side == side && distance >= openings[i].From - MouthMargin && distance <= openings[i].To + MouthMargin) {
                    return true;
                }
            }
            return false;
        }

        // Tagged colliders only: sign posts, lamp poles and shelters are in the way sometimes
        static float NearestContainment(Vector3 origin, Vector3 direction, RaycastHit[] hits) {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, StubReach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++) {
                if (hits[i].collider.CompareTag(Tags.Containment) && hits[i].distance < nearest) {
                    nearest = hits[i].distance;
                }
            }
            return nearest;
        }

        // The fall zone lies below the road (its top is 1.5 m under it), so a sideways ray passes over
        // it: look straight down from the ray's height, FallZoneReach out from the centreline
        static bool FallZoneWithin(RoutePose pose, float sign, RaycastHit[] hits) {
            Vector3 origin = pose.Offset(FallZoneReach * sign, RayHeight);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 20f, Layers.Mask(Layers.Zone), QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) {
                if (hits[i].collider.GetComponentInParent<FallZone>() != null) {
                    return true;
                }
            }
            return false;
        }
    }
}
