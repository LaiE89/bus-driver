using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The root of an environment logic prefab (§4.14): which kind it is, and the empty slot its
    // view (greybox or art, from the EnvironmentViewSet) is instantiated under when the scene is
    // built. Colliders, lights and gameplay components live on the logic side, never in the view.
    public sealed class EnvironmentPiece : MonoBehaviour {
        [SerializeField] string kind = "";
        [SerializeField] Transform viewSlot;

        public string Kind { get { return kind; } }
        public Transform ViewSlot { get { return viewSlot; } }

        // The instantiated view, or null before the builder has placed one
        public GameObject View {
            get { return viewSlot != null && viewSlot.childCount > 0 ? viewSlot.GetChild(0).gameObject : null; }
        }
    }
}
