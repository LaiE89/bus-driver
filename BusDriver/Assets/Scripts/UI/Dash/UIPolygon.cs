using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Dash {
    // A filled convex polygon for uGUI (the GPS's bus arrow and stop dots): a triangle fan over
    // points in the RectTransform's local space
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIPolygon : MaskableGraphic {
        [SerializeField] Vector2[] points = new Vector2[0];

        public void SetPoints(Vector2[] newPoints) {
            points = newPoints ?? new Vector2[0];
            SetVerticesDirty();
        }

        // A regular polygon approximating a circle
        public static Vector2[] Circle(float radius, int sides) {
            Vector2[] circle = new Vector2[sides];
            for (int i = 0; i < sides; i++) {
                float angle = i * Mathf.PI * 2f / sides;
                circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return circle;
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            if (points == null || points.Length < 3) {
                return;
            }
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            for (int i = 0; i < points.Length; i++) {
                vertex.position = points[i];
                vh.AddVert(vertex);
            }
            for (int i = 1; i + 1 < points.Length; i++) {
                vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
