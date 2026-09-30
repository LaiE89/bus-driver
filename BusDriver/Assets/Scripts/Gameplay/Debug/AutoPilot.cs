using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using UnityEngine;

namespace BusDriver.Gameplay.Debug {
    // A development and test driver (§4.18, T-M2-10): pure pursuit along the RoutePath from the rear
    // axle, 5–12 m ahead by speed, following a speed profile (40 km/h on straights, 25 km/h on arcs
    // under 100 m radius), and able to stop with the door lined up at a stop. It drives through
    // BusInput.ExternalControl, so every lock and gear rule of the real bus still applies.
    public sealed class AutoPilot : MonoBehaviour {
        const float StraightKmh = 40f;
        const float TightArcKmh = 25f;
        const float TightArcRadius = 100f;
        const float MinLookahead = 5f;
        const float MaxLookahead = 12f;
        // Lookahead seconds at speed (12 m at about 43 km/h)
        const float LookaheadTime = 1f;
        // Planning deceleration: gentler than the bus can brake, so it never overshoots
        const float PlanDecel = 1.1f;
        const float SpeedGain = 0.45f;
        // How far ahead the profile looks for a slower stretch
        const float ProfileHorizon = 90f;
        // The door's centre, ahead of the bus origin (A.2: the opening runs 4.1–5.5 m)
        public const float DoorOffset = 4.8f;
        const float ArriveTolerance = 0.4f;

        ShiftServices shift;
        BusController bus;
        BusInput input;
        RouteTracker tracker;
        RouteDefinition route;
        float stopDistance = -1f;

        public bool Engaged { get; private set; }
        // Metres right of the centreline the bus aims for (the right lane's centre by default)
        public float LaneOffset { get; set; }
        public string StopTarget { get; private set; }
        public bool Arrived { get; private set; }
        public float TargetSpeedKmh { get; private set; }

        // ShiftContext, after the input adapters (§4.5 step 3)
        public void Init(ShiftServices services) {
            shift = services;
            bus = services.Bus;
            input = services.BusInput;
            tracker = services.Tracker;
            route = tracker.Route;
            LaneOffset = route.roadWidth * 0.25f;
            if (!DevBuild.IsEnabled()) {
                return;
            }
            services.Debug.AddCheat(new DebugCheat("AutoPilot", "AutoPilot on/off", () => Engage(!Engaged)));
            services.Debug.AddCheat(new DebugCheat("AutoPilot", "Stop at the next stop", () => {
                RouteStop next = tracker.NextStop;
                if (next != null) {
                    StopAt(next.stopId);
                }
            }));
            services.Debug.Register("AutoPilot", WriteDebug);
        }

        public void Engage(bool on) {
            Engaged = on;
            input.ExternalControl = on;
            if (!on) {
                bus.SetInput(0f, 0f, false);
            }
        }

        // Drives to the stop and holds there with the door in its zone. Engages if needed.
        public void StopAt(string stopId) {
            if (!route.TryGetStop(stopId, out RouteStop stop)) {
                Log.Warn(LogCat.Route, "AutoPilot: no stop '" + stopId + "'");
                return;
            }
            StopTarget = stopId;
            stopDistance = stop.distance - DoorOffset;
            Arrived = false;
            if (!Engaged) {
                Engage(true);
            }
        }

        // Drives on from a stop
        public void Continue() {
            StopTarget = null;
            stopDistance = -1f;
            Arrived = false;
        }

        void FixedUpdate() {
            if (!Engaged || bus == null) {
                return;
            }
            float speed = bus.ForwardSpeed;
            float remaining = stopDistance >= 0f ? stopDistance - tracker.DistanceAlong : float.MaxValue;
            float target = ProfileSpeed(tracker.DistanceAlong, speed);
            if (stopDistance >= 0f) {
                target = Mathf.Min(target, Mathf.Sqrt(2f * PlanDecel * Mathf.Max(0f, remaining - ArriveTolerance)));
                if (!Arrived && remaining <= ArriveTolerance && Mathf.Abs(speed) < 0.3f) {
                    Arrived = true;
                }
            }
            TargetSpeedKmh = target * 3.6f;

            float accel = Mathf.Clamp((target - speed) * SpeedGain, -1f, 1f);
            // Holding S at a standstill would select reverse (automatic gears); let the auto-hold stop it
            if ((Arrived || target < 0.05f) && speed < 0.5f) {
                accel = 0f;
            }else if (accel < 0f && speed < 0.3f) {
                accel = 0f;
            }
            bus.SetInput(SteerInput(LaneOffset), accel, false);
        }

        // The pure-pursuit steering input (−1..1) toward a lateral offset, from the rear axle. Public
        // so the smoke test can keep a lane while it drives the pedals itself.
        public float SteerInput(float lateral) {
            Vector3 rear = bus.transform.TransformPoint(new Vector3(0f, 0f, bus.RearAxleLocalZ));
            RouteProjection here = tracker.Path.Project(rear, tracker.DistanceAlong);
            float lookahead = Mathf.Clamp(Mathf.Abs(bus.ForwardSpeed) * LookaheadTime, MinLookahead, MaxLookahead);
            Vector3 goal = tracker.Path.Evaluate(here.Distance + lookahead).Offset(lateral);
            Vector3 to = goal - rear;
            to.y = 0f;
            Vector3 forward = Vector3.ProjectOnPlane(bus.transform.forward, Vector3.up);
            float alpha = Vector3.SignedAngle(forward, to, Vector3.up) * Mathf.Deg2Rad;
            // δ = atan(2 L sin α / ld)
            float wheel = Mathf.Atan2(2f * bus.Wheelbase * Mathf.Sin(alpha), Mathf.Max(to.magnitude, 0.1f)) * Mathf.Rad2Deg;
            float max = Mathf.Max(bus.MaxSteerAngle, 1f);
            return Mathf.Clamp(wheel / max, -1f, 1f);
        }

        // m/s: the slowest of the stretches ahead, each reachable with PlanDecel
        float ProfileSpeed(float distance, float speed) {
            float target = LimitAt(distance);
            for (float ahead = 5f; ahead <= ProfileHorizon; ahead += 5f) {
                float limit = LimitAt(distance + ahead);
                target = Mathf.Min(target, Mathf.Sqrt(limit * limit + 2f * PlanDecel * ahead));
            }
            // The road's end
            float left = tracker.Path.TotalLength - 25f - distance;
            target = Mathf.Min(target, Mathf.Sqrt(2f * PlanDecel * Mathf.Max(0f, left)));
            return target;
        }

        float LimitAt(float distance) {
            RouteSegment segment = route.segments[tracker.Path.SegmentIndexAt(Mathf.Clamp(distance, 0f, tracker.Path.TotalLength))];
            bool tight = segment.kind == SegmentKind.Arc && segment.radius < TightArcRadius;
            return (tight ? TightArcKmh : StraightKmh) / 3.6f;
        }

        void WriteDebug(StringBuilder text) {
            text.Append(Engaged ? "on" : "off");
            if (Engaged) {
                text.Append("  target ").Append(TargetSpeedKmh.ToString("0")).Append(" km/h  lane ").Append(LaneOffset.ToString("0.00"));
            }
            if (StopTarget != null) {
                text.Append("  stop ").Append(StopTarget).Append(Arrived ? " (arrived)" : "");
            }
            text.Append('\n');
        }
    }
}
