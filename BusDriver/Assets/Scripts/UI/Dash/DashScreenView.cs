using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.UI.Dash {
    // A world-space dash screen (§4.13, D38). The canvas is built flat in Night_Systems; on Bind it
    // moves onto the bus view's Anchor_Dash_* transform, whose local scale x/y is the screen's size
    // in metres, so art can resize or move a screen without touching this code.
    public abstract class DashScreenView : MonoBehaviour, IShiftBindable {
        // Lifts the screen off the dash surface toward the driver, against z-fighting
        const float SurfaceLift = 0.004f;

        [SerializeField] DashScreen screen;
        [Tooltip("The world-space canvas; its size in pixels maps onto the anchor's size in metres")]
        [SerializeField] RectTransform canvas;

        public DashScreen Screen { get { return screen; } }
        public RectTransform Canvas { get { return canvas; } }

        public void Bind(ShiftServices shift) {
            Mount(shift.Bus.View);
            OnBind(shift);
        }

        protected abstract void OnBind(ShiftServices shift);

        // The anchor's +Z faces the driver; a canvas is read looking along its own +Z, so it turns
        // round, and its pixels are scaled to fill the anchor's unit square
        void Mount(BusViewBase view) {
            Transform anchor = view != null ? view.DashAnchor(screen) : null;
            if (anchor == null || canvas == null) {
                return;
            }
            canvas.SetParent(anchor, false);
            canvas.localPosition = new Vector3(0f, 0f, SurfaceLift / Mathf.Max(anchor.lossyScale.z, 1e-4f));
            canvas.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Vector2 size = canvas.sizeDelta;
            canvas.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.x);
        }
    }
}
