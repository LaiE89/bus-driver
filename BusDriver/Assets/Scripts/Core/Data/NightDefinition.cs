using UnityEngine;

namespace BusDriver.Core.Data {
    // One night of the run (§2.19, §4.8, Data/Nights/Night1..Night5): where it ends, how hard its
    // monsters push, its hints, and either a scripted rider list (night 1) or the parameters the
    // manifest generator draws from (nights 2–5, T-M7-01).
    public sealed class NightDefinition : ScriptableObject {
        [Tooltip("1..5")]
        public int nightIndex = 1;
        [Tooltip("The stop that ends the night: church on night 1 (D43), lodge otherwise")]
        public string endStopId = "lodge";
        [Tooltip("Multiplies every positive threat rate (§2.9)")]
        public float threatRateMultiplier = 1f;
        public bool hintsEnabled;
        [Tooltip("Night net must reach this many cents by the end stop, or the run is lost")]
        public int quotaCents = 0;

        [Tooltip("The riders, in order; when set, the generator is skipped (night 1)")]
        public RiderSpec[] scripted = new RiderSpec[0];

        [Header("Generator (nights 2–5, T-M7-01)")]
        public int riderCountMin;
        public int riderCountMax;
        public int decoyCount;
        public int monsterCountMin;
        public int monsterCountMax;
        [Tooltip("Monster ids that always board this night")]
        public string[] requiredMonsters = new string[0];
        [Tooltip("Monster ids the rest are drawn from")]
        public string[] monsterPool = new string[0];
        [Tooltip("At least this many different monster types (0 = no rule)")]
        public int minDistinctTypes;
        [Tooltip("At most this many of one type (0 = no rule)")]
        public int maxPerType;
        public string monsterBoardFirstStop = "";
        public string monsterBoardLastStop = "";
        [Tooltip("The Mimic's own boarding window, when it differs")]
        public string mimicBoardFirstStop = "";
        public string mimicBoardLastStop = "";

        public bool IsScripted { get { return scripted != null && scripted.Length > 0; } }
    }
}
