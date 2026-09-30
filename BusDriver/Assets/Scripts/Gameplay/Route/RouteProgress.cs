using System;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Route {
    // The night's stop records (§2.4, §4.6): a stop is Served the first time the doors are fully
    // open with the door in its zone, and Missed once the bus is more than missedStopMargin past it.
    // The rules live in the StopProgress core; this adapter feeds it the doors and the tracker.
    public sealed class RouteProgress : MonoBehaviour {
        ShiftServices shift;
        RouteTracker tracker;
        BusCabin cabin;
        BusDoors doors;
        StopProgress core;
        readonly List<StopRecord> missedBuffer = new List<StopRecord>();

        public IReadOnlyList<StopRecord> Stops { get { return core != null ? core.Stops : (IReadOnlyList<StopRecord>)new StopRecord[0]; } }
        // The first Pending stop of the night, or null once none is left
        public StopRecord Next { get { return core != null ? core.Next : null; } }
        public StopRecord EndStop { get { return core != null ? core.EndStop : null; } }
        public bool TerminusReached { get { return core != null && core.EndStopServed; } }

        public event Action<StopRecord> OnServed;
        public event Action<StopRecord> OnMissed;
        // The doors are fully open at the night's end stop (fires after its OnServed)
        public event Action OnTerminus;

        // ShiftContext, step 1 of the Init order (§4.5), after RouteTracker
        public void Init(ShiftServices services) {
            shift = services;
            tracker = services.Tracker;
            cabin = services.Cabin;
            doors = services.Doors;
            RouteDefinition route = tracker.Route;
            // NightDefinition.endStopId arrives with T-M3-03; until then the night runs to the terminus
            core = new StopProgress(route, route.terminusStopId);
            doors.OnFullyOpened += HandleDoorsFullyOpened;
            services.Debug.Register("Stops", WriteDebug);
        }

        // NightDefinition.endStopId (T-M3-03)
        public void SetEndStop(string stopId) {
            core.SetEndStop(stopId);
        }

        public StopRecord Find(string stopId) {
            return core != null ? core.Find(stopId) : null;
        }

        void OnDestroy() {
            if (doors != null) {
                doors.OnFullyOpened -= HandleDoorsFullyOpened;
            }
        }

        void FixedUpdate() {
            if (core == null) {
                return;
            }
            missedBuffer.Clear();
            if (core.MarkMissed(tracker.DistanceAlong, missedBuffer) == 0) {
                return;
            }
            for (int i = 0; i < missedBuffer.Count; i++) {
                if (OnMissed != null) {
                    OnMissed(missedBuffer[i]);
                }
            }
        }

        void HandleDoorsFullyOpened() {
            BusStop stop = cabin.CurrentStop;
            if (stop == null || !core.TryServe(stop.StopId, NowGameSeconds(), out StopRecord record)) {
                return;
            }
            if (OnServed != null) {
                OnServed(record);
            }
            if (record.IsEndStop && OnTerminus != null) {
                OnTerminus();
            }
        }

        // Arrival times are game-seconds on the shift clock (§2.5)
        double NowGameSeconds() {
            return shift.Clock != null ? shift.Clock.NowGameSeconds : double.NaN;
        }

        void WriteDebug(StringBuilder text) {
            IReadOnlyList<StopRecord> stops = Stops;
            for (int i = 0; i < stops.Count; i++) {
                StopRecord record = stops[i];
                text.Append(record == Next ? "> " : "  ").Append(record.StopId).Append(' ');
                if (!record.InNight) {
                    text.Append("(after the end stop)\n");
                    continue;
                }
                text.Append(record.State);
                if (record.State == StopState.Served) {
                    text.Append(' ').Append(record.Rating);
                }
                if (record.IsEndStop) {
                    text.Append("  [end]");
                }
                text.Append('\n');
            }
        }
    }
}
