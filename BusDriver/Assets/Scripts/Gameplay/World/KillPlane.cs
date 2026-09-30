using UnityEngine;

namespace BusDriver.Gameplay.World {
    // A trigger under the whole map (§3.4). A bus that falls through the world anywhere but the
    // cliff is put back on the road (§2.3); the respawn arrives with RouteTracker in T-M2-09.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KillPlane : MonoBehaviour {
    }
}
