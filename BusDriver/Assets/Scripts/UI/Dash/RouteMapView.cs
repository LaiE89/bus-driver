using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using BusDriver.UI.Screens;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // The dash GPS (§4.13, D12: glance only). The whole route, north-up, fitted with a 6 % margin;
    // the travelled part dimmed; the bus as an arrow; stops as ✓ (served), a ring (next) or a dot
    // (upcoming), in shape as well as colour; stubs as short grey dead ends; the cliff red and
    // dashed. Beside it: the next stop, its distance, the ETA with EARLY / ON TIME / LATE as text,
    // and the clock. SetSignal(false) (the tunnel, T-M2-14) swaps it all for NO SIGNAL.
    public sealed class RouteMapView : DashScreenView {
        // Pixels (§4.13)
        const float MapMargin = 0.06f;
        const float StubPixels = 8f;
        const float SampleStep = 5f;
        const float TextRefreshSeconds = 0.25f;

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
        [SerializeField] TMP_Text ratingText;
        [SerializeField] TMP_Text clockText;
        [SerializeField] TMP_Text noSignalText;

        [Header("Colours (from the theme's palette, set by the builder)")]
        [SerializeField] Color routeColor = Color.white;
        [SerializeField] Color travelledColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] Color servedColor = Color.green;
        [SerializeField] Color nextColor = Color.yellow;
        [SerializeField] Color upcomingColor = Color.white;
        [SerializeField] Color missedColor = Color.gray;
        [SerializeField] Color earlyColor = Color.green;
        [SerializeField] Color lateColor = Color.red;
        [SerializeField] Color onTimeColor = Color.white;

        RouteTracker tracker;
        RouteProgress progress;
        ShiftClockDriver clock;
        RouteDefinition route;
        RoutePath path;
        GpsProjection projection;
        readonly List<GpsStopMarker> markers = new List<GpsStopMarker>();
        float nextTextRefresh;

        public bool HasSignal { get; private set; } = true;
        public GpsProjection Projection { get { return projection; } }
        public IReadOnlyList<GpsStopMarker> Markers { get { return markers; } }
        public UILineRenderer RouteLine { get { return routeLine; } }
        public UILineRenderer CliffLine { get { return cliffLine; } }
        public RectTransform BusArrow { get { return busArrow; } }
        public string RatingText { get { return ratingText != null ? ratingText.text : ""; } }

        protected override void OnBind(ShiftServices shift) {
            tracker = shift.Tracker;
            progress = shift.Progress;
            clock = shift.Clock;
            Layout(tracker.Route, tracker.Path);
            SetSignal(true);
            Refresh(true);
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
            Vector3[] world = new Vector3[count];
            for (int i = 0; i < count; i++) {
                world[i] = path.Evaluate(Mathf.Min(i * SampleStep, path.TotalLength)).Position;
            }
            projection = GpsProjection.Fit(world, area, MapMargin);
            Vector2[] line = new Vector2[count];
            for (int i = 0; i < count; i++) {
                line[i] = projection.ToMap(world[i]);
            }
            routeLine.color = routeColor;
            routeLine.SetPoints(line);
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
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++) {
                float d = Mathf.Lerp(cliff.start, cliff.end, i / (float)(count - 1));
                points[i] = projection.ToMap(path.Evaluate(d).Position);
            }
            cliffLine.SetPoints(points);
        }

        // Each stub a short segment off the route at its branch angle, on its side
        void LayoutStubs() {
            Vector2[] points = new Vector2[route.stubs.Length * 2];
            for (int i = 0; i < route.stubs.Length; i++) {
                RouteStub stub = route.stubs[i];
                RoutePose pose = path.Evaluate(stub.distance);
                float side = stub.side == RouteSide.Right ? 1f : -1f;
                // Turned from the route's heading toward its side, in the ground plane
                Vector3 direction = Quaternion.AngleAxis(side * stub.angleDeg, Vector3.up) * pose.Tangent;
                Vector2 mouth = projection.ToMap(pose.Offset(side * route.RoadWidthAt(stub.distance) * 0.5f));
                Vector2 mapDirection = new Vector2(direction.x, direction.z).normalized;
                points[i * 2] = mouth;
                points[i * 2 + 1] = mouth + mapDirection * StubPixels;
            }
            stubLines.SetPoints(points);
        }

        void LayoutMarkers() {
            for (int i = 0; i < markers.Count; i++) {
                if (markers[i] != null) {
                    DestroyMarker(markers[i]);
                }
            }
            markers.Clear();
            for (int i = 0; i < route.stops.Length; i++) {
                RouteStop stop = route.stops[i];
                GpsStopMarker marker = GpsStopMarker.Create(markerRoot, stop.stopId, servedColor, nextColor, upcomingColor, missedColor);
                marker.Rect.anchoredPosition = projection.ToMap(path.Evaluate(stop.distance).Position);
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
            // The arrow and the travelled split every frame (no allocation); texts a few times a second
            Vector3 busPosition = tracker.Path.Evaluate(tracker.DistanceAlong).Position;
            busArrow.anchoredPosition = projection.ToMap(busPosition);
            busArrow.localRotation = Quaternion.Euler(0f, 0f, GpsProjection.MapAngle(tracker.Path.Evaluate(tracker.DistanceAlong).Tangent) - 90f);
            routeLine.SetDimBefore(tracker.DistanceAlong / SampleStep);
            Refresh(false);
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
                ratingText.text = "";
                return;
            }
            nextLabel.text = UIText.GpsNext;
            nextText.text = next.DisplayName;
            double now = clock != null ? clock.NowGameSeconds : double.NaN;
            float eta = tracker.EtaGameSeconds;
            string distance = FormatDistance(Mathf.Max(0f, next.Distance - tracker.DistanceAlong));
            if (float.IsInfinity(eta) || double.IsNaN(now)) {
                detailText.text = distance + "\n" + UIText.GpsEtaUnknown;
                // Standing still can still be already late
                bool late = !double.IsNaN(now) && now - next.ScheduledGameSeconds > route.schedule.lateThresholdGameSeconds;
                SetRating(late ? ArrivalRating.Late : ArrivalRating.None);
                return;
            }
            double arrival = now + eta;
            detailText.text = distance + "\n" + string.Format(UIText.GpsEta, ClockText.Format(arrival, ClockFormat.Dash));
            SetRating(ScheduleMath.Rate(route.schedule, arrival, next.ScheduledGameSeconds));
        }

        void SetRating(ArrivalRating rating) {
            switch (rating) {
                case ArrivalRating.Early: ratingText.text = UIText.GpsEarly; ratingText.color = earlyColor; break;
                case ArrivalRating.Late: ratingText.text = UIText.GpsLate; ratingText.color = lateColor; break;
                case ArrivalRating.OnTime: ratingText.text = UIText.GpsOnTime; ratingText.color = onTimeColor; break;
                default: ratingText.text = ""; break;
            }
        }

        static string FormatDistance(float metres) {
            return metres >= 1000f ? (metres / 1000f).ToString("0.0") + " km" : (Mathf.Round(metres / 10f) * 10f).ToString("0") + " m";
        }
    }
}
