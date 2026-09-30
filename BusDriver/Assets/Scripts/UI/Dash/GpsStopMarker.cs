using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // One stop on the GPS (§4.13): a ✓ once served, a ring for the next stop, a dot for the stops
    // still to come, a small grey dot once missed. Shapes differ as well as colours (§2.23).
    public sealed class GpsStopMarker : MonoBehaviour {
        const float Size = 24f;

        UIPolygon dot;
        UILineRenderer ring;
        UILineRenderer check;
        Color upcoming;
        Color missed;

        public string StopId { get; private set; }
        public RectTransform Rect { get; private set; }
        // What it shows now, for tests and the capture
        public StopState ShownState { get; private set; }
        public bool ShownAsNext { get; private set; }

        public static GpsStopMarker Create(Transform parent, string stopId, Color served, Color next, Color upcomingColor, Color missedColor) {
            GameObject go = new GameObject("Stop " + stopId, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            GpsStopMarker marker = go.AddComponent<GpsStopMarker>();
            marker.StopId = stopId;
            marker.Rect = rect;
            marker.upcoming = upcomingColor;
            marker.missed = missedColor;
            marker.dot = Child<UIPolygon>(rect, "Dot");
            marker.dot.SetPoints(UIPolygon.Circle(5f, 12));
            marker.ring = Child<UILineRenderer>(rect, "Ring");
            marker.ring.Configure(3f, false, true, 0f, 0f);
            Vector2[] circle = UIPolygon.Circle(9f, 16);
            marker.ring.SetPoints(circle);
            marker.ring.color = next;
            marker.check = Child<UILineRenderer>(rect, "Check");
            marker.check.Configure(3.5f, false, false, 0f, 0f);
            marker.check.SetPoints(new[] { new Vector2(-7f, 0f), new Vector2(-2f, -6f), new Vector2(8f, 7f) });
            marker.check.color = served;
            marker.Show(null, false);
            return marker;
        }

        static T Child<T>(RectTransform parent, string name) where T : Component {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            rect.anchoredPosition = Vector2.zero;
            T component = go.AddComponent<T>();
            UnityEngine.UI.Graphic graphic = component as UnityEngine.UI.Graphic;
            if (graphic != null) {
                graphic.raycastTarget = false;
            }
            return component;
        }

        // A null record (no progress yet) shows an upcoming dot
        public void Show(StopRecord record, bool isNext) {
            StopState state = record != null ? record.State : StopState.Pending;
            bool outside = record != null && !record.InNight;
            ShownState = state;
            ShownAsNext = isNext;
            check.enabled = state == StopState.Served;
            ring.enabled = isNext;
            dot.enabled = !isNext && state != StopState.Served;
            dot.color = state == StopState.Missed || outside ? missed : upcoming;
            dot.transform.localScale = state == StopState.Missed ? new Vector3(0.6f, 0.6f, 1f) : Vector3.one;
        }
    }
}
