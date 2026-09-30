using System.Collections;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Route {
    // Where the bus is along the route (§4.6): projects it onto the RoutePath every FixedUpdate.
    // It also owns the KillPlane respawn (§2.3): a bus that falls through the world anywhere but the
    // cliff is faded out, put back on the road and an error is logged, because that must never happen.
    public sealed class RouteTracker : MonoBehaviour {
        // The ETA's moving average (§4.6), sampled once a second
        const float SpeedWindowSeconds = 30f;
        const int SpeedSamples = 31;
        const float RespawnFadeOut = 1f;
        const float RespawnFadeIn = 0.5f;
        const float RespawnLift = 0.3f;
        // Keeps a respawn off the route's very ends
        const float RespawnEndMargin = 15f;

        ShiftServices shift;
        RouteDefinition route;
        RouteSceneRoot routeRoot;
        BusController bus;
        Rigidbody busBody;

        readonly float[] sampleDistance = new float[SpeedSamples];
        int sampleCount;
        int sampleHead;
        float nextSampleTime;
        bool respawning;
        // A bus that went over the cliff is the fall death's (T-M4-09), not the KillPlane's
        bool enteredFallZone;

        public RoutePath Path { get; private set; }
        public RouteDefinition Route { get { return route; } }
        public float DistanceAlong { get; private set; }
        // Signed, + is right of the centreline in the route direction
        public float Lateral { get; private set; }
        public float Progress01 { get { return Path != null ? Mathf.Clamp01(DistanceAlong / Path.TotalLength) : 0f; } }
        // Metres per second along the route over the last 30 s; never negative
        public float AverageSpeed { get; private set; }
        public int RespawnCount { get; private set; }

        // The next stop of the night: RouteProgress's first Pending stop (§2.4), or before it's
        // wired the first stop still ahead; −1 when none is left
        public int NextStopIndex {
            get {
                if (shift != null && shift.Progress != null && shift.Progress.Stops.Count > 0) {
                    StopRecord next = shift.Progress.Next;
                    return next != null ? next.Index : -1;
                }
                for (int i = 0; route != null && i < route.stops.Length; i++) {
                    if (route.stops[i].distance >= DistanceAlong) {
                        return i;
                    }
                }
                return -1;
            }
        }

        public RouteStop NextStop {
            get {
                int index = NextStopIndex;
                return index >= 0 ? route.stops[index] : null;
            }
        }

        public float DistanceToNextStop {
            get {
                RouteStop next = NextStop;
                return next != null ? Mathf.Max(0f, next.distance - DistanceAlong) : 0f;
            }
        }

        // Game-seconds to the next stop at the average speed; infinity while standing still
        public float EtaGameSeconds {
            get {
                if (AverageSpeed < 0.5f || NextStop == null) {
                    return float.PositiveInfinity;
                }
                return DistanceToNextStop / AverageSpeed * route.schedule.gameSecondsPerRealSecond;
            }
        }

        public bool InZone(RouteZoneKind kind) {
            return route != null && route.InZone(kind, DistanceAlong);
        }

        // ShiftContext, step 1 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            routeRoot = services.Route;
            route = routeRoot.Route;
            bus = services.Bus;
            busBody = bus.GetComponent<Rigidbody>();
            Path = new RoutePath(route);
            Project(-1f);
            nextSampleTime = Time.time;
            if (routeRoot.KillPlane != null) {
                routeRoot.KillPlane.OnBusEntered += HandleKillPlane;
            }
            for (int i = 0; i < routeRoot.FallZones.Count; i++) {
                routeRoot.FallZones[i].Volume.OnBusInsideChanged += HandleFallZone;
            }
            services.Debug.Register("Route", WriteDebug);
        }

        void OnDestroy() {
            if (routeRoot == null) {
                return;
            }
            if (routeRoot.KillPlane != null) {
                routeRoot.KillPlane.OnBusEntered -= HandleKillPlane;
            }
            for (int i = 0; i < routeRoot.FallZones.Count; i++) {
                if (routeRoot.FallZones[i] != null) {
                    routeRoot.FallZones[i].Volume.OnBusInsideChanged -= HandleFallZone;
                }
            }
        }

        void FixedUpdate() {
            if (Path == null) {
                return;
            }
            Project(DistanceAlong);
            if (Time.time >= nextSampleTime) {
                nextSampleTime += 1f;
                Sample();
            }
        }

        // A negative hint searches the whole route
        void Project(float hint) {
            RouteProjection projection = hint < 0f ? Path.Project(busBody.position) : Path.Project(busBody.position, hint);
            DistanceAlong = projection.Distance;
            Lateral = projection.Lateral;
        }

        void Sample() {
            sampleDistance[sampleHead] = DistanceAlong;
            sampleHead = (sampleHead + 1) % SpeedSamples;
            sampleCount = Mathf.Min(sampleCount + 1, SpeedSamples);
            if (sampleCount < 2) {
                AverageSpeed = 0f;
                return;
            }
            int oldest = (sampleHead - sampleCount + SpeedSamples) % SpeedSamples;
            float span = Mathf.Min(sampleCount - 1, SpeedWindowSeconds);
            AverageSpeed = Mathf.Max(0f, (DistanceAlong - sampleDistance[oldest]) / span);
        }

        void HandleFallZone(bool inside) {
            if (inside) {
                enteredFallZone = true;
            }
        }

        // Over the cliff is the fall death's, whether or not the hull touched a FallZone first
        void HandleKillPlane() {
            if (respawning || enteredFallZone || InZone(RouteZoneKind.Cliff)) {
                return;
            }
            Log.Error(LogCat.Route, $"containment breach at d={DistanceAlong:0.0} (lateral {Lateral:0.0}); the bus fell through the world and is respawned");
            StartCoroutine(Respawn());
        }

        // §2.3: fade out for 1 s, then upright on the nearest road point, in the right lane, facing
        // the route direction
        IEnumerator Respawn() {
            respawning = true;
            yield return shift.Fade.FadeTo(1f, RespawnFadeOut);
            float d = Mathf.Clamp(DistanceAlong, RespawnEndMargin, Path.TotalLength - RespawnEndMargin);
            RoutePose pose = Path.Evaluate(d);
            float lane = route.RoadWidthAt(d) * 0.25f;
            bus.PlaceAt(pose.Offset(lane, RespawnLift), Quaternion.LookRotation(pose.Tangent, Vector3.up));
            Project(-1f);
            RespawnCount++;
            respawning = false;
            yield return shift.Fade.FadeTo(0f, RespawnFadeIn);
        }

        void WriteDebug(StringBuilder text) {
            text.Append("d ").Append(DistanceAlong.ToString("0.0")).Append(" m (").Append((Progress01 * 100f).ToString("0.0"))
                .Append(" %)  lateral ").Append(Lateral.ToString("0.00")).Append('\n');
            RouteStop next = NextStop;
            text.Append("next ").Append(next != null ? next.stopId : "—")
                .Append("  in ").Append(DistanceToNextStop.ToString("0")).Append(" m  eta ")
                .Append(float.IsInfinity(EtaGameSeconds) ? "—" : EtaGameSeconds.ToString("0") + " game-s")
                .Append("  avg ").Append((AverageSpeed * 3.6f).ToString("0")).Append(" km/h\n");
            text.Append("zones");
            bool any = false;
            for (int i = 0; i < route.zones.Length; i++) {
                if (route.zones[i] != null && DistanceAlong >= route.zones[i].start && DistanceAlong <= route.zones[i].end) {
                    text.Append(' ').Append(route.zones[i].kind);
                    any = true;
                }
            }
            text.Append(any ? "\n" : " —\n");
            if (RespawnCount > 0) {
                text.Append("KillPlane respawns ").Append(RespawnCount).Append('\n');
            }
        }
    }
}
