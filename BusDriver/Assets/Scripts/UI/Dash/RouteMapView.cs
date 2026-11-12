using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.World;
using BusDriver.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BusDriver.UI.Dash {
    // The dash GPS (§4.13, D12: glance only). North-up. While driving, the map is zoomed on the bus
    // so only the nearby route shows; the layout pass still fits the whole route for tests. The
    // travelled part is dimmed; the bus is an arrow; stops are ✓ (served), a ring (next) or a dot
    // (upcoming), in shape as well as colour; stubs are short grey dead ends; the cliff is red and
    // dashed. Beside it: the next stop, its distance, the ETA, the stop's scheduled time (D107) and
    // the clock. SetSignal(false) (the tunnel, T-M2-14) swaps it all for NO SIGNAL.
    public sealed class RouteMapView : DashScreenView {
        // Pixels (§4.13)
        const float MapMargin = 0.06f;
        const float StubPixels = 8f;
        const float SampleStep = 5f;
        const float TextRefreshSeconds = 0.25f;
        // Metres of ground across the map while driving. The whole route is several kilometres.
        const float ViewMetres = 500f;

        [SerializeField] RectTransform mapArea;
        [SerializeField] GameObject mapGroup;
        [SerializeField] UILineRenderer routeLine;
        [SerializeField] UILineRenderer cliffLine;
        [SerializeField] UILineRenderer stubLines;
        [SerializeField] RectTransform markerRoot;
        [SerializeField] RectTransform busArrow;
        [SerializeField] TMP_Text nextLabel;
        [SerializeField] TMP_Text nextText;
        [SerializeField] TMP_Text detailText;
        [FormerlySerializedAs("ratingText")]
        [SerializeField] TMP_Text scheduleText;
        [SerializeField] TMP_Text clockText;
        [SerializeField] TMP_Text noSignalText;

        [Header("Colours (from the theme's palette, set by the builder)")]
        [SerializeField] Color routeColor = Color.white;
        [SerializeField] Color travelledColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] Color servedColor = Color.green;
        [SerializeField] Color nextColor = Color.yellow;
        [SerializeField] Color upcomingColor = Color.white;
        [SerializeField] Color missedColor = Color.gray;
        [Tooltip("The route past a night's early end (D43)")]
        [SerializeField] Color closedColor = new Color(0.45f, 0.45f, 0.45f, 0.3f);

        RouteTracker tracker;
        RouteProgress progress;
        ShiftClockDriver clock;
        RouteDefinition route;
        RoutePath path;
        GpsProjection projection;
        Vector3[] routeWorld;
        Vector2[] routePoints;
        Vector3[] cliffWorld;
        Vector2[] cliffPoints;
        Vector3[] stubMouths;
        Vector2[] stubDirections;
        Vector2[] stubPoints;
        Vector3[] stopWorld;
        readonly List<GpsStopMarker> markers = new List<GpsStopMarker>();
        readonly List<TunnelZone> tunnels = new List<TunnelZone>();
        float nextTextRefresh;

        public bool HasSignal { get; private set; } = true;
        public GpsProjection Projection { get { return projection; } }
        public IReadOnlyList<GpsStopMarker> Markers { get { return markers; } }
        public UILineRenderer RouteLine { get { return routeLine; } }
        public UILineRenderer CliffLine { get { return cliffLine; } }
        public RectTransform BusArrow { get { return busArrow; } }
        public string ScheduleText { get { return scheduleText != null ? scheduleText.text : ""; } }

        protected override void OnBind(ShiftServices shift) {
            tracker = shift.Tracker;
            progress = shift.Progress;
            clock = shift.Clock;
            Layout(tracker.Route, tracker.Path);
            if (busArrow != null) {
                busArrow.localScale = new Vector3(1.4f, 1.4f, 1f);
            }
            if (mapArea != null && mapArea.GetComponent<RectMask2D>() == null) {
                mapArea.gameObject.AddComponent<RectMask2D>();
            }
            // A night that ends before the route does greys out everything past its end stop (§2.4)
            if (progress.EndStop != null && progress.EndStop.StopId != route.terminusStopId) {
                routeLine.SetDimAfter(progress.EndStop.Distance / SampleStep, closedColor);
            }
            SetSignal(true);
            Refresh(true);
            // The tunnel takes the signal away (§3.3)
            for (int i = 0; i < shift.Route.Zones.Count; i++) {
                TunnelZone tunnel = shift.Route.Zones[i] != null ? shift.Route.Zones[i].GetComponent<TunnelZone>() : null;
                if (tunnel != null) {
                    tunnel.OnInsideChanged += HandleTunnel;
                    tunnels.Add(tunnel);
                }
            }
        }

        void OnDestroy() {
            for (int i = 0; i < tunnels.Count; i++) {
                if (tunnels[i] != null) {
                    tunnels[i].OnInsideChanged -= HandleTunnel;
                }
            }
        }

        void HandleTunnel(bool inside) {
            SetSignal(!inside);
        }

        // The tunnel's NO SIGNAL (§3.3)
        public void SetSignal(bool on) {
            HasSignal = on;
            if (mapGroup != null) {
                mapGroup.SetActive(on);
            }
            if (noSignalText != null) {
                noSignalText.gameObject.SetActive(!on);
                noSignalText.text = UIText.GpsNoSignal;
            }
            // Re-enabled texts took their theme colours back; show the real state at once
            nextTextRefresh = 0f;
        }

        // Builds the lines and markers for a route. Internal so GpsLayoutTests can lay it out alone.
        internal void Layout(RouteDefinition routeDefinition, RoutePath routePath) {
            route = routeDefinition;
            path = routePath;
            Rect area = new Rect(Vector2.zero, mapArea.rect.size);
            int count = Mathf.CeilToInt(path.TotalLength / SampleStep) + 1;
            routeWorld = new Vector3[count];
            routePoints = new Vector2[count];
            for (int i = 0; i < count; i++) {
                routeWorld[i] = path.Evaluate(Mathf.Min(i * SampleStep, path.TotalLength)).Position;
            }
            projection = GpsProjection.Fit(routeWorld, area, MapMargin);
            for (int i = 0; i < count; i++) {
                routePoints[i] = projection.ToMap(routeWorld[i]);
            }
            routeLine.color = routeColor;
            routeLine.SetPoints(routePoints);
            routeLine.SetDim(0f, travelledColor);
            LayoutCliff();
            LayoutStubs();
            LayoutMarkers();
        }

        void LayoutCliff() {
            if (!route.TryGetZone(RouteZoneKind.Cliff, out RouteZone cliff)) {
                cliffLine.SetPoints(new Vector2[0]);
                return;
            }
            int count = Mathf.Max(2, Mathf.CeilToInt((cliff.end - cliff.start) / SampleStep) + 1);
            cliffWorld = new Vector3[count];
            cliffPoints = new Vector2[count];
            for (int i = 0; i < count; i++) {
                float d = Mathf.Lerp(cliff.start, cliff.end, i / (float)(count - 1));
                cliffWorld[i] = path.Evaluate(d).Position;
                cliffPoints[i] = projection.ToMap(cliffWorld[i]);
            }
            cliffLine.SetPoints(cliffPoints);
        }

        // Each stub a short segment off the route at its branch angle, on its side
        void LayoutStubs() {
            stubMouths = new Vector3[route.stubs.Length];
            stubDirections = new Vector2[route.stubs.Length];
            stubPoints = new Vector2[route.stubs.Length * 2];
            for (int i = 0; i < route.stubs.Length; i++) {
                RouteStub stub = route.stubs[i];
                RoutePose pose = path.Evaluate(stub.distance);
                float side = stub.side == RouteSide.Right ? 1f : -1f;
                // Turned from the route's heading toward its side, in the ground plane
                Vector3 direction = Quaternion.AngleAxis(side * stub.angleDeg, Vector3.up) * pose.Tangent;
                stubMouths[i] = pose.Offset(side * route.RoadWidthAt(stub.distance) * 0.5f);
                stubDirections[i] = new Vector2(direction.x, direction.z).normalized;
                Vector2 mouth = projection.ToMap(stubMouths[i]);
                stubPoints[i * 2] = mouth;
                stubPoints[i * 2 + 1] = mouth + stubDirections[i] * StubPixels;
            }
            stubLines.SetPoints(stubPoints);
        }

        void LayoutMarkers() {
            for (int i = 0; i < markers.Count; i++) {
                if (markers[i] != null) {
                    DestroyMarker(markers[i]);
                }
            }
            markers.Clear();
            stopWorld = new Vector3[route.stops.Length];
            for (int i = 0; i < route.stops.Length; i++) {
                RouteStop stop = route.stops[i];
                stopWorld[i] = path.Evaluate(stop.distance).Position;
                GpsStopMarker marker = GpsStopMarker.Create(markerRoot, stop.stopId, servedColor, nextColor, upcomingColor, missedColor);
                marker.Rect.anchoredPosition = projection.ToMap(stopWorld[i]);
                markers.Add(marker);
            }
        }

        static void DestroyMarker(GpsStopMarker marker) {
            if (Application.isPlaying) {
                Destroy(marker.gameObject);
            }else {
                DestroyImmediate(marker.gameObject);
            }
        }

        void Update() {
            if (tracker == null) {
                return;
            }
            // The arrow and the travelled split every frame. The map stays centred on the bus.
            Vector3 busPosition = tracker.Path.Evaluate(tracker.DistanceAlong).Position;
            FollowBus(busPosition);
            busArrow.anchoredPosition = projection.ToMap(busPosition);
            busArrow.localRotation = Quaternion.Euler(0f, 0f, GpsProjection.MapAngle(tracker.Path.Evaluate(tracker.DistanceAlong).Tangent) - 90f);
            routeLine.SetDimBefore(tracker.DistanceAlong / SampleStep);
            Refresh(false);
        }

        void FollowBus(Vector3 busPosition) {
            if (mapArea == null || routeWorld == null) {
                return;
            }
            Rect area = new Rect(Vector2.zero, mapArea.rect.size);
            projection = GpsProjection.Around(busPosition, ViewMetres, area);
            for (int i = 0; i < routeWorld.Length; i++) {
                routePoints[i] = projection.ToMap(routeWorld[i]);
            }
            routeLine.SetPoints(routePoints);
            if (cliffWorld != null) {
                for (int i = 0; i < cliffWorld.Length; i++) {
                    cliffPoints[i] = projection.ToMap(cliffWorld[i]);
                }
                cliffLine.SetPoints(cliffPoints);
            }
            if (stubMouths != null) {
                for (int i = 0; i < stubMouths.Length; i++) {
                    Vector2 mouth = projection.ToMap(stubMouths[i]);
                    stubPoints[i * 2] = mouth;
                    stubPoints[i * 2 + 1] = mouth + stubDirections[i] * StubPixels;
                }
                stubLines.SetPoints(stubPoints);
            }
            for (int i = 0; i < markers.Count; i++) {
                markers[i].Rect.anchoredPosition = projection.ToMap(stopWorld[i]);
            }
        }

        void Refresh(bool force) {
            if (!force && Time.unscaledTime < nextTextRefresh) {
                return;
            }
            nextTextRefresh = Time.unscaledTime + TextRefreshSeconds;
            StopRecord next = progress != null ? progress.Next : null;
            for (int i = 0; i < markers.Count; i++) {
                StopRecord record = progress != null ? progress.Find(markers[i].StopId) : null;
                markers[i].Show(record, record != null && record == next);
            }
            clockText.text = clock != null ? clock.Format(ClockFormat.Dash) : "";
            if (next == null) {
                nextLabel.text = "";
                nextText.text = UIText.GpsEndOfLine;
                detailText.text = "";
                scheduleText.text = "";
                return;
            }
            nextLabel.text = UIText.GpsNext;
            nextText.text = next.DisplayName;
            // The player judges early or late themselves from the ETA against the timetable (D107)
            scheduleText.text = string.Format(UIText.GpsScheduled, ClockText.Format(next.ScheduledGameSeconds, ClockFormat.Dash));
            double now = clock != null ? clock.NowGameSeconds : double.NaN;
            float eta = tracker.EtaGameSeconds;
            string distance = FormatDistance(Mathf.Max(0f, next.Distance - tracker.DistanceAlong));
            if (float.IsInfinity(eta) || double.IsNaN(now)) {
                detailText.text = distance + "\n" + UIText.GpsEtaUnknown;
                return;
            }
            double arrival = now + eta;
            detailText.text = distance + "\n" + string.Format(UIText.GpsEta, ClockText.Format(arrival, ClockFormat.Dash));
        }

        static string FormatDistance(float metres) {
            return metres >= 1000f ? (metres / 1000f).ToString("0.0") + " km" : (Mathf.Round(metres / 10f) * 10f).ToString("0") + " m";
        }
    }
}
