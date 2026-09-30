using System.Collections.Generic;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // The GPS's map projection (§4.13): top-down and north-up (world +Z is up, +X is right), one
    // uniform scale that fits every point into an area with a margin, centred.
    public readonly struct GpsProjection {
        public readonly float Scale;
        public readonly Vector2 WorldCentre;
        public readonly Vector2 AreaCentre;

        public GpsProjection(float scale, Vector2 worldCentre, Vector2 areaCentre) {
            Scale = scale;
            WorldCentre = worldCentre;
            AreaCentre = areaCentre;
        }

        // marginFraction of the area's smaller side is left clear on every side (6 %, §4.13)
        public static GpsProjection Fit(IReadOnlyList<Vector3> world, Rect area, float marginFraction) {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < world.Count; i++) {
                Vector2 p = new Vector2(world[i].x, world[i].z);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            if (world.Count == 0) {
                min = max = Vector2.zero;
            }
            float margin = Mathf.Min(area.width, area.height) * marginFraction;
            Vector2 room = new Vector2(Mathf.Max(1f, area.width - 2f * margin), Mathf.Max(1f, area.height - 2f * margin));
            Vector2 extent = Vector2.Max(max - min, new Vector2(1f, 1f));
            float scale = Mathf.Min(room.x / extent.x, room.y / extent.y);
            return new GpsProjection(scale, (min + max) * 0.5f, area.center);
        }

        public Vector2 ToMap(Vector3 world) {
            return AreaCentre + (new Vector2(world.x, world.z) - WorldCentre) * Scale;
        }

        // A world direction's angle on the map, for rotating an icon (degrees, counter-clockwise)
        public static float MapAngle(Vector3 worldDirection) {
            return Mathf.Atan2(worldDirection.z, worldDirection.x) * Mathf.Rad2Deg;
        }
    }
}
