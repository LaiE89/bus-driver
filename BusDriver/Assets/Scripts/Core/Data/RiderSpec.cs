using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // One rider of a night's manifest (§2.6, §2.19): who they look like, where they wait, where
    // they get off, an optional harmless oddity, and the monster they really are (empty = human).
    // The same shape serves NightDefinition's scripted list and the generator's output.
    [Serializable]
    public sealed class RiderSpec {
        public string lookId = "";
        public string boardStopId = "";
        [Tooltip("Empty for monsters: they never request a drop-off")]
        public string destinationStopId = "";
        public DecoyKind decoy = DecoyKind.None;
        public string monsterId = "";

        public bool IsMonster { get { return !string.IsNullOrEmpty(monsterId); } }

        public RiderSpec Clone() {
            return (RiderSpec)MemberwiseClone();
        }

        public override string ToString() {
            return $"{lookId} {boardStopId}->{destinationStopId}{(decoy != DecoyKind.None ? " decoy " + decoy : "")}{(IsMonster ? " [" + monsterId + "]" : "")}";
        }
    }
}
