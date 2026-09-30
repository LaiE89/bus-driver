using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // One linear route (§3, §4.8). Geometry is data (D26): RouteBuilder turns it into the world
    // scene and RoutePath into the tracker's path. Seeded once from ROADMAP §3 (RouteSeed), then
    // this asset is the source of truth. Frozen at G1.
    [CreateAssetMenu(menuName = "Bus Driver/Route Definition", fileName = "Route")]
    public sealed class RouteDefinition : ScriptableObject {
        public string id = "";
        public string displayName = "";

        [Header("Cross-section (§3.1)")]
        [Tooltip("Two lanes, metres")]
        public float roadWidth = 7f;
        [Tooltip("Each side, metres")]
        public float shoulderWidth = 1f;
        [Tooltip("Road width inside the Cliff zone")]
        public float cliffRoadWidth = 6.5f;
        [Tooltip("Gravel between the road edge and the drop, inside the Cliff zone")]
        public float cliffOuterShoulder = 0.3f;

        [Header("Layout (§3.1–§3.3)")]
        public RouteSegment[] segments = new RouteSegment[0];
        [Tooltip("In route order; the last one is the terminus")]
        public RouteStop[] stops = new RouteStop[0];
        public RouteStub[] stubs = new RouteStub[0];
        public RouteZone[] zones = new RouteZone[0];
        public RouteSign[] signs = new RouteSign[0];

        [Header("Schedule (§2.4)")]
        public RouteSchedule schedule = new RouteSchedule();

        [Header("Depot and terminus")]
        [Tooltip("Distance of the bus spawn marker (right lane)")]
        public float depotSpawnDistance = 20f;
        [Tooltip("The depot yard pad runs from 0 to here, on the left")]
        public float depotPadEnd = 60f;
        public string terminusStopId = "";

        [Header("Generation (§3.3, §3.4)")]
        public RouteGeneration generation = new RouteGeneration();

        public int IndexOfStop(string stopId) {
            for (int i = 0; i < stops.Length; i++) {
                if (stops[i] != null && stops[i].stopId == stopId) {
                    return i;
                }
            }
            return -1;
        }

        public bool TryGetStop(string stopId, out RouteStop stop) {
            int index = IndexOfStop(stopId);
            stop = index >= 0 ? stops[index] : null;
            return stop != null;
        }

        // The first zone of this kind, if any
        public bool TryGetZone(RouteZoneKind kind, out RouteZone zone) {
            for (int i = 0; i < zones.Length; i++) {
                if (zones[i] != null && zones[i].kind == kind) {
                    zone = zones[i];
                    return true;
                }
            }
            zone = null;
            return false;
        }

        public bool InZone(RouteZoneKind kind, float distance) {
            for (int i = 0; i < zones.Length; i++) {
                RouteZone zone = zones[i];
                if (zone != null && zone.kind == kind && distance >= zone.start && distance <= zone.end) {
                    return true;
                }
            }
            return false;
        }

        // Road width at a distance: narrower through the cliff (§3.1)
        public float RoadWidthAt(float distance) {
            return InZone(RouteZoneKind.Cliff, distance) ? cliffRoadWidth : roadWidth;
        }
    }

    [Serializable]
    public sealed class RouteSegment {
        public SegmentKind kind;
        [Tooltip("Straights only, metres. An arc's length is always radius × angle")]
        public float length;
        [Tooltip("Arcs only, metres")]
        public float radius;
        [Tooltip("Arcs only. Positive turns right, negative turns left")]
        public float angleDeg;
        [Tooltip("Elevation change = length × grade / 100")]
        public float gradePercent;
        public SideProfile left;
        public SideProfile right;

        public float Length {
            get { return kind == SegmentKind.Arc ? Mathf.Abs(radius * angleDeg * Mathf.Deg2Rad) : length; }
        }

        public static RouteSegment Straight(float length, float grade, SideProfile left, SideProfile right) {
            return new RouteSegment { kind = SegmentKind.Straight, length = length, gradePercent = grade, left = left, right = right };
        }

        public static RouteSegment Arc(float radius, float angleDeg, float grade, SideProfile left, SideProfile right) {
            return new RouteSegment { kind = SegmentKind.Arc, radius = radius, angleDeg = angleDeg, gradePercent = grade, left = left, right = right };
        }
    }

    [Serializable]
    public sealed class RouteStop {
        public string stopId = "";
        public string displayName = "";
        public StopKind kind;
        [Tooltip("Metres along the route. Every stop is on the right, on a straight (§3.2)")]
        public float distance;
    }

    [Serializable]
    public sealed class RouteStub {
        public float distance;
        public RouteSide side;
        [Tooltip("Branch angle from the route heading")]
        public float angleDeg = 70f;
        public float length = 20f;
        public float width = 8f;
        public BlockerKind blocker;
        public BlockerVariant variant;
    }

    [Serializable]
    public sealed class RouteZone {
        public RouteZoneKind kind;
        public float start;
        public float end;
        public ZoneSpan span;
        [Tooltip("Width of an edge strip (span LeftEdge/RightEdge), metres")]
        public float edgeWidth;
    }

    [Serializable]
    public sealed class RouteSign {
        public float distance;
        public RouteSide side;
        public SignKind kind;
    }

    [Serializable]
    public sealed class RouteSchedule {
        [Tooltip("00:30 in game-seconds since midnight")]
        public float shiftStartGameSeconds = 1800f;
        public float gameSecondsPerRealSecond = 6f;
        [Tooltip("m/s used for the timetable")]
        public float scheduleSpeed = 9f;
        [Tooltip("Real seconds allowed per earlier stop")]
        public float dwellAllowanceSeconds = 40f;
        [Tooltip("Early = at least this many game-seconds before the scheduled time")]
        public float earlyThresholdGameSeconds = 60f;
        [Tooltip("Late = more than this many game-seconds after it")]
        public float lateThresholdGameSeconds = 60f;
        [Tooltip("A stop is Missed once the bus is this far past it, unserved")]
        public float missedStopMargin = 30f;
    }

    // Everything the builders need that §3.4's profile table defines, in metres from the centreline
    // unless stated otherwise
    [Serializable]
    public sealed class RouteGeneration {
        [Tooltip("Fixed so the dressing is identical on every rebuild (§4.1.8)")]
        public int dressingSeed = 1234;
        [Tooltip("Path sampling step")]
        public float sampleStep = 1f;
        [Tooltip("Road mesh chunk length")]
        public float chunkLength = 100f;
        [Tooltip("Road texture repeats every this many metres along the road (v = distance / this)")]
        public float uvLength = 4f;
        public float containmentWallHeight = 4f;

        [Header("Forest")]
        public float forestShoulderOuter = 7.5f;
        public float forestWallOffset = 7.5f;
        public float treeRowStart = 8f;
        public float treeRowEnd = 16f;
        public int treeRows = 2;
        public float treeSpacing = 4f;
        public float treeJitter = 1.5f;
        public float forestGroundWidth = 30f;
        public float forestGroundDrop = 0.05f;

        [Header("Rockface")]
        public float rockfaceOffset = 5.5f;
        public float rockfaceHeight = 10f;
        [Tooltip("Degrees, leaning away from the road")]
        public float rockfaceLean = 10f;

        [Header("Drop")]
        public float guardrailOffset = 5f;
        public float guardrailHeight = 0.8f;
        public float guardrailSegmentLength = 4f;
        public float dropSlopeAngle = 40f;
        public float dropSlopeLength = 20f;
        public float dropFlatLength = 40f;

        [Header("Cliff (D14)")]
        [Tooltip("Absolute world height")]
        public float valleyFloorY = -40f;
        [Tooltip("How far the valley floor reaches out from the cliff face")]
        public float valleyFloorDepth = 150f;
        [Tooltip("FallZone volumes start this far outside the road edge")]
        public float fallZoneInset = 1f;
        public float fallZoneWidth = 40f;
        [Tooltip("The FallZone's top, below the road")]
        public float fallZoneTopBelowRoad = 1.5f;
        [Tooltip("The FallZone's bottom, above the valley floor")]
        public float fallZoneAboveFloor = 1f;
        [Tooltip("The guardrail end caps overlap the FallZone by this much (§3.5)")]
        public float endCapOverlap = 2f;
        public float chevronSpacing = 20f;

        [Header("Water")]
        public float bridgeRailingOffset = 4.5f;
        public float creekDepth = 8f;

        [Header("Tunnel")]
        public float tunnelWallOffset = 5f;
        public float tunnelCeilingHeight = 6f;
        public float tunnelLightSpacing = 12f;
        public float tunnelLightRange = 8f;

        [Header("Map")]
        public float safetyFloorY = -80f;
        public float killPlaneY = -70f;
        [Tooltip("The NightEndBarrier stands this far past a night's end stop (§2.4)")]
        public float nightEndBarrierOffset = 60f;
    }
}
