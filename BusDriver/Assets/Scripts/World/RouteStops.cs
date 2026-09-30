using System.Collections.Generic;
using UnityEngine;

// The stops in the order the bus meets them around the loop. Passengers pick a
// destination from here, and the driver missing a drop-off falls through to the next one.
public static class RouteStops {
    static readonly List<BusStop> ordered = new List<BusStop>();

    public static IReadOnlyList<BusStop> All {
        get {
            EnsureBuilt();
            return ordered;
        }
    }

    public static int IndexOf(BusStop stop) {
        EnsureBuilt();
        return ordered.IndexOf(stop);
    }

    // Wraps around: the loop has no last stop
    public static BusStop Next(BusStop stop) {
        EnsureBuilt();
        if (ordered.Count == 0) {
            return null;
        }
        int index = ordered.IndexOf(stop);
        if (index < 0) {
            return ordered[0];
        }
        return ordered[(index + 1) % ordered.Count];
    }

    // Somewhere else on the loop. A one stop route has nowhere to drop anybody.
    public static BusStop PickDestination(BusStop from) {
        EnsureBuilt();
        if (ordered.Count == 0) {
            return null;
        }
        if (ordered.Count == 1) {
            return ordered[0] == from ? null : ordered[0];
        }
        BusStop pick;
        do {
            pick = ordered[Random.Range(0, ordered.Count)];
        }while (pick == from || pick == null);
        return pick;
    }

    // Statics outlive a scene reload, so a stale list shows up as null entries
    static void EnsureBuilt() {
        if (ordered.Count > 0) {
            bool stale = false;
            for (int i = 0; i < ordered.Count; i++) {
                if (ordered[i] == null) {
                    stale = true;
                    break;
                }
            }
            if (!stale) {
                return;
            }
            ordered.Clear();
        }
        ordered.AddRange(Object.FindObjectsByType<BusStop>());
        ordered.Sort((a, b) => a.RouteOrder.CompareTo(b.RouteOrder));
    }
}
