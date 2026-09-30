using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Routes/Route01.asset: ROADMAP §3.1–§3.3 exactly. DataSeeder creates the
    // asset if it's missing and never overwrites it (§0.1); after that, the asset is authoritative.
    public static class RouteSeed {
        public const string RelativePath = "Routes/Route01.asset";
        public const string RouteId = "route01";

        const SideProfile Forest = SideProfile.Forest;
        const SideProfile Rockface = SideProfile.Rockface;
        const SideProfile Drop = SideProfile.Drop;
        const SideProfile CliffDrop = SideProfile.CliffDrop;
        const SideProfile Water = SideProfile.Water;
        const SideProfile TunnelWall = SideProfile.TunnelWall;

        public static void Fill(RouteDefinition route) {
            route.id = RouteId;
            route.displayName = "Hollow Pines Line";
            route.roadWidth = 7f;
            route.shoulderWidth = 1f;
            route.cliffRoadWidth = 6.5f;
            route.cliffOuterShoulder = 0.3f;
            route.segments = Segments();
            route.stops = Stops();
            route.stubs = Stubs();
            route.zones = Zones();
            route.signs = Signs();
            route.schedule = new RouteSchedule();
            route.depotSpawnDistance = 20f;
            route.depotPadEnd = 60f;
            route.terminusStopId = "lodge";
            route.generation = new RouteGeneration();
        }

        // §3.1, rows 1–26
        static RouteSegment[] Segments() {
            return new[] {
                RouteSegment.Straight(150f, 0f, Forest, Forest),              // 1  depot yard, bus spawn at 20 m
                RouteSegment.Arc(120f, 30f, 1f, Forest, Forest),              // 2
                RouteSegment.Straight(207.17f, 2f, Forest, Forest),           // 3  farm_gate at 350
                RouteSegment.Arc(80f, -45f, 0f, Forest, Drop),                // 4
                RouteSegment.Straight(77.17f, 1f, Forest, Forest),            // 5
                RouteSegment.Arc(60f, 90f, 3f, Drop, Rockface),               // 6
                RouteSegment.Straight(245.75f, 0f, Forest, Forest),           // 7  gas_station at 800
                RouteSegment.Straight(100f, -2f, Forest, Forest),             // 8  down to the creek
                RouteSegment.Straight(60f, 0f, Water, Water),                 // 9  bridge
                RouteSegment.Straight(60f, 2f, Forest, Forest),               // 10
                RouteSegment.Arc(100f, -60f, 3f, Rockface, Drop),             // 11
                RouteSegment.Straight(105.28f, 1f, Forest, Forest),           // 12 campground at 1250
                RouteSegment.Arc(150f, 20f, 4f, Drop, Rockface),              // 13
                RouteSegment.Straight(67.64f, 4f, Drop, Rockface),            // 14 rumble strip 1435–1450
                RouteSegment.Arc(127.324f, 90f, 2f, CliffDrop, Rockface),     // 15 Dead Man's Bend
                RouteSegment.Straight(50f, 0f, Drop, Rockface),               // 16 guardrail resumes
                RouteSegment.Arc(120f, -60f, 3f, Rockface, Drop),             // 17
                RouteSegment.Straight(224.34f, 1f, Forest, Forest),           // 18 church at 1900
                RouteSegment.Straight(150f, 0f, TunnelWall, TunnelWall),      // 19 tunnel
                RouteSegment.Straight(60f, -1f, Forest, Forest),              // 20
                RouteSegment.Arc(60f, -90f, -2f, Forest, Drop),               // 21
                RouteSegment.Straight(205.75f, 0f, Forest, Forest),           // 22 clinic at 2400
                RouteSegment.Arc(80f, 45f, 3f, Drop, Rockface),               // 23
                RouteSegment.Straight(207.17f, 2f, Forest, Forest),           // 24 trailhead at 2750
                RouteSegment.Arc(120f, -30f, 1f, Forest, Forest),             // 25
                RouteSegment.Straight(107.17f, 0f, Forest, Forest),           // 26 lodge at 2960, end wall at 3000
            };
        }

        // §3.2
        static RouteStop[] Stops() {
            return new[] {
                Stop("farm_gate", "Mill Road Farm", StopKind.FarmGate, 350f),
                Stop("gas_station", "Pinecrest Gas", StopKind.GasStation, 800f),
                Stop("campground", "Hollow Creek Campground", StopKind.Campground, 1250f),
                Stop("church", "St. Agnes Church", StopKind.Church, 1900f),
                Stop("clinic", "Ridge Clinic", StopKind.Clinic, 2400f),
                Stop("trailhead", "Summit Trailhead", StopKind.Trailhead, 2750f),
                Stop("lodge", "Summit Lodge", StopKind.Terminus, 2960f),
            };
        }

        // §3.3 side stubs
        static RouteStub[] Stubs() {
            return new[] {
                Stub(260f, RouteSide.Left, BlockerKind.FallenTree, BlockerVariant.A),
                Stub(720f, RouteSide.Right, BlockerKind.FenceRoadClosed, BlockerVariant.A),
                Stub(1300f, RouteSide.Right, BlockerKind.Gate, BlockerVariant.A),
                Stub(1980f, RouteSide.Left, BlockerKind.ConcreteBarriers, BlockerVariant.A),
                Stub(2500f, RouteSide.Right, BlockerKind.CollapsedBridge, BlockerVariant.A),
                Stub(2680f, RouteSide.Left, BlockerKind.FallenTree, BlockerVariant.B),
            };
        }

        // §3.3 zones. The rumble strip is two zones: across the road before the bend, then along its
        // left edge (0.8 m wide) through it.
        static RouteZone[] Zones() {
            return new[] {
                Zone(RouteZoneKind.Tunnel, 2040f, 2210f, ZoneSpan.Across, 0f),
                Zone(RouteZoneKind.Bridge, 1000f, 1060f, ZoneSpan.Across, 0f),
                Zone(RouteZoneKind.Cliff, 1450f, 1650f, ZoneSpan.Across, 0f),
                Zone(RouteZoneKind.RumbleStrip, 1435f, 1450f, ZoneSpan.Across, 0f),
                Zone(RouteZoneKind.RumbleStrip, 1450f, 1650f, ZoneSpan.LeftEdge, 0.8f),
            };
        }

        // §3.3 signs; the stop signs are part of the stop prefabs
        static RouteSign[] Signs() {
            List<RouteSign> signs = new List<RouteSign> {
                Sign(970f, RouteSide.Right, SignKind.BridgeAhead),
                Sign(1360f, RouteSide.Right, SignKind.NoGuardrailAhead),
                Sign(1400f, RouteSide.Right, SignKind.SharpCurveRight),
            };
            for (int i = 0; i <= 10; i++) {
                signs.Add(Sign(1450f + 20f * i, RouteSide.Left, SignKind.Chevron));
            }
            signs.Add(Sign(2020f, RouteSide.Right, SignKind.TunnelAhead));
            return signs.ToArray();
        }

        static RouteStop Stop(string id, string name, StopKind kind, float distance) {
            return new RouteStop { stopId = id, displayName = name, kind = kind, distance = distance };
        }

        static RouteStub Stub(float distance, RouteSide side, BlockerKind blocker, BlockerVariant variant) {
            return new RouteStub { distance = distance, side = side, angleDeg = 70f, length = 20f, width = 8f, blocker = blocker, variant = variant };
        }

        static RouteZone Zone(RouteZoneKind kind, float start, float end, ZoneSpan span, float edgeWidth) {
            return new RouteZone { kind = kind, start = start, end = end, span = span, edgeWidth = edgeWidth };
        }

        static RouteSign Sign(float distance, RouteSide side, SignKind kind) {
            return new RouteSign { distance = distance, side = side, kind = kind };
        }
    }
}
