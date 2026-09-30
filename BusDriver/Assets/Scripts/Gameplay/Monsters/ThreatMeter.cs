using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // A monster's threat meter (§2.9, §4.7) as a component beside its MonsterBrain. The rules live
    // in ThreatMeterCore; the brain ticks it, and this raises OnStageChanged with the brain, so
    // abilities, scares, the journal and the hints can listen without knowing the core.
    public sealed class ThreatMeter : MonoBehaviour {
        public ThreatMeterCore Core { get; } = new ThreatMeterCore();
        public float Value { get { return Core.Value; } }
        public ThreatStage Stage { get { return Core.Stage; } }
        public MonsterBrain Brain { get; private set; }

        // (monster, from, to)
        public event Action<MonsterBrain, ThreatStage, ThreatStage> OnStageChanged;

        // The brain, from its Bind
        internal void Init(MonsterBrain brain) {
            if (Brain != null) {
                return;
            }
            Brain = brain;
            Core.OnStageChanged += RaiseStageChanged;
        }

        void OnDestroy() {
            Core.OnStageChanged -= RaiseStageChanged;
        }

        void RaiseStageChanged(ThreatStage from, ThreatStage to) {
            if (OnStageChanged != null) {
                OnStageChanged(Brain, from, to);
            }
        }
    }
}
