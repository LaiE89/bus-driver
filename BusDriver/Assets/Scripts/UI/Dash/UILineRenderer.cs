using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Dash {
    // A uGUI polyline (§4.13, the GPS): points in the RectTransform's local space, drawn as one quad
    // per segment. It can draw disjoint segments (pairs of points), a closed loop, dashes, and a
    // dimmed first part up to a fractional point index (the travelled route).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UILineRenderer : MaskableGraphic {
        [SerializeField] Vector2[] points = new Vector2[0];
        [SerializeField] float thickness = 3f;
        [Tooltip("Points are pairs of separate segments instead of one line")]
        [SerializeField] bool segments;
        [Tooltip("Joins the last point back to the first")]
        [SerializeField] bool closed;
        [Tooltip("0 for a solid line")]
        [SerializeField] float dashLength;
        [SerializeField] float gapLength;
        [Tooltip("The line before this fractional point index is drawn in dimColor")]
        [SerializeField] float dimBefore;
        [SerializeField] Color dimColor = new Color(1f, 1f, 1f, 0.3f);
        [Tooltip("The line after this fractional point index is drawn in afterColor (a night that ends early)")]
        [SerializeField] float dimAfter = float.MaxValue;
        [SerializeField] Color afterColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        public IReadOnlyList<Vector2> Points { get { return points; } }
        public float Thickness { get { return thickness; } set { thickness = value; SetVerticesDirty(); } }
        public float DimBefore { get { return dimBefore; } }
        public float DimAfter { get { return dimAfter; } }
        public bool IsDashed { get { return dashLength > 0f && gapLength > 0f; } }

        public void SetPoints(Vector2[] newPoints) {
            points = newPoints ?? new Vector2[0];
            SetVerticesDirty();
        }

        public void Configure(float lineThickness, bool asSegments, bool asClosed, float dash, float gap) {
            thickness = lineThickness;
            segments = asSegments;
            closed = asClosed;
            dashLength = dash;
            gapLength = gap;
            SetVerticesDirty();
        }

        public void SetDim(float beforeIndex, Color dim) {
            dimColor = dim;
            SetDimBefore(beforeIndex);
        }

        // Greys out the line past a fractional point index (float.MaxValue: nothing)
        public void SetDimAfter(float afterIndex, Color after) {
            dimAfter = afterIndex;
            afterColor = after;
            SetVerticesDirty();
        }

        // Only rebuilds the mesh when the split moves visibly
        public void SetDimBefore(float beforeIndex) {
            if (Mathf.Abs(beforeIndex - dimBefore) < 0.05f) {
                return;
            }
            dimBefore = beforeIndex;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            if (points == null || points.Length < 2) {
                return;
            }
            float dashPhase = 0f;
            if (segments) {
                for (int i = 0; i + 1 < points.Length; i += 2) {
                    dashPhase = 0f;
                    AddSegment(vh, points[i], points[i + 1], color, ref dashPhase);
                }
                return;
            }
            int count = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < count; i++) {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                if (i >= dimAfter) {
                    AddSegment(vh, a, b, afterColor, ref dashPhase);
                    continue;
                }
                if (i + 1 > dimAfter) {
                    // The part past the split is greyed; the part before it is drawn below
                    Vector2 cut = Vector2.Lerp(a, b, dimAfter - i);
                    AddSegment(vh, cut, b, afterColor, ref dashPhase);
                    b = cut;
                }
                if (i + 1 <= dimBefore) {
                    AddSegment(vh, a, b, dimColor, ref dashPhase);
                }else if (i >= dimBefore) {
                    AddSegment(vh, a, b, color, ref dashPhase);
                }else {
                    Vector2 split = Vector2.Lerp(a, b, dimBefore - i);
                    AddSegment(vh, a, split, dimColor, ref dashPhase);
                    AddSegment(vh, split, b, color, ref dashPhase);
                }
            }
        }

        // One quad, or its dashes; the phase carries the dash pattern across segments
        void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, Color32 tint, ref float phase) {
            float length = Vector2.Distance(a, b);
            if (length < 1e-4f) {
                return;
            }
            if (dashLength <= 0f || gapLength <= 0f) {
                AddQuad(vh, a, b, tint, true);
                return;
            }
            float period = dashLength + gapLength;
            float at = 0f;
            while (at < length) {
                float inPeriod = phase % period;
                if (inPeriod < dashLength) {
                    float run = Mathf.Min(dashLength - inPeriod, length - at);
                    AddQuad(vh, Vector2.Lerp(a, b, at / length), Vector2.Lerp(a, b, (at + run) / length), tint, false);
                    at += run;
                    phase += run;
                }else {
                    float skip = Mathf.Min(period - inPeriod, length - at);
                    at += skip;
                    phase += skip;
                }
            }
        }

        // Solid lines run half a thickness past each end, so consecutive segments overlap at the
        // joins; dashes don't, or the caps would close the gaps
        void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Color32 tint, bool capped) {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);
            Vector2 extend = capped ? direction * (thickness * 0.5f) : Vector2.zero;
            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = a - extend - normal; vh.AddVert(vertex);
            vertex.position = a - extend + normal; vh.AddVert(vertex);
            vertex.position = b + extend + normal; vh.AddVert(vertex);
            vertex.position = b + extend - normal; vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
