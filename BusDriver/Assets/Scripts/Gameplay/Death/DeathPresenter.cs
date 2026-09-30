using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // What plays between a death and the Game Over screen, for one cause (§2.14). The one base
    // class death presentation may derive from (§4.1 rule 4). Presenters sit beside
    // DeathDirector, which picks the one whose Cause matches.
    public abstract class DeathPresenter : MonoBehaviour {
        public abstract DeathCause Cause { get; }

        // DeathDirector's Init (§4.5 step 9)
        public virtual void Init(ShiftServices shift) { }

        // Runs on the DeathDirector; the Game Over screen follows when it ends. Scaled time, so
        // pausing (allowed while dying, §4.11) holds it.
        public abstract IEnumerator Present(DeathReport report);
    }
}
