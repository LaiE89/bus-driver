using System;

namespace BusDriver.Core.Data {
    // One line of a monster's threat rule list (§2.9): while the condition holds, the meter moves
    // at this rate. The list is ordered and the first matching rule wins; MonsterDefinition.rules
    // holds it (T-M4-03).
    [Serializable]
    public struct ThreatRule {
        public ThreatCondition condition;
        public float ratePerSecond;

        public ThreatRule(ThreatCondition condition, float ratePerSecond) {
            this.condition = condition;
            this.ratePerSecond = ratePerSecond;
        }
    }
}
