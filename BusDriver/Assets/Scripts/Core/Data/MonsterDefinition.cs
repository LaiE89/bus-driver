using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // How a kill sequence's telegraph is escaped (§2.14, D31, D47): observed continuously for the
    // seconds (the Starer, the Weeping Angel), unobserved for them (the Mimic), or not at all (the
    // Whisperer never starts one). The threat is then set to resetThreat.
    [Serializable]
    public struct MonsterEscape {
        public EscapeKind kind;
        public float seconds;
        public float resetThreat;

        public MonsterEscape(EscapeKind kind, float seconds, float resetThreat = 60f) {
            this.kind = kind;
            this.seconds = seconds;
            this.resetThreat = resetThreat;
        }
    }

    // The monster's journal page (§2.20): Sighting, Tells and Weakness sections
    [Serializable]
    public sealed class MonsterJournal {
        [Tooltip("The Sighting section's one-line description")]
        public string sightingText = "";
        [Tooltip("The Tells section's bullet list")]
        public string[] tellsText = new string[0];
        [Tooltip("The Weakness section, and the Game Over hint when it kills (§2.21)")]
        public string hint = "";
        [Tooltip("The silhouette icon (IconBuilder, T-M7-06)")]
        public Sprite icon;
    }

    // A monster's ability numbers (§4.8 [SerializeReference]). One subclass per ability; the
    // abilities themselves arrive with their monsters (the Starer T-M4-08, the Whisperer T-M5-06,
    // the Mimic T-M6-02, the Weeping Angel T-M6-05). Data only: nothing writes these during play.
    [Serializable]
    public abstract class MonsterAbilityConfig { }

    // §2.10: it creeps forward a row at a time while nobody watches
    [Serializable]
    public sealed class StarerAdvanceConfig : MonsterAbilityConfig {
        [Tooltip("Unobserved this long before it may move to its target row, seconds")]
        public float unobservedBeforeAdvance = 1f;
        [Tooltip("The head-track turn rate, degrees per second (the MVP StaringMonster number)")]
        public float headTrackDegreesPerSecond = 25f;
        public float headTrackYawLimit = 110f;
        public float headTrackPitchLimit = 35f;
        [Tooltip("The lens scare waits at most this long for the next CCTV cycle after it first turns Aggressive")]
        public float lensScareWindowSeconds = 20f;
    }

    // §2.11: sanity drain per second by stage, and the whisper loop's volume by stage
    [Serializable]
    public sealed class WhispererDrainConfig : MonsterAbilityConfig {
        [Tooltip("Dormant, Unsettled, Aggressive, Lethal")]
        public float[] drainPerSecondByStage = { 0f, 0.25f, 0.6f, 1.2f };
        [Tooltip("Dormant, Unsettled, Aggressive, Lethal")]
        public float[] whisperVolumeByStage = { 0f, 0.3f, 0.6f, 1f };
    }

    // §2.12 and D22: it copies a seated rider, then replaces them
    [Serializable]
    public sealed class MimicCopyConfig : MonsterAbilityConfig {
        [Tooltip("A replace happens this long after boarding or the last replace, if Aggressive didn't trigger one first")]
        public float replaceIntervalSeconds = 120f;
        [Tooltip("The threat after a replace")]
        public float threatAfterReplace = 20f;
        [Tooltip("The cabin lights flicker this long on a replace")]
        public float replaceFlickerSeconds = 0.6f;
        [Tooltip("Blink gap at threat 0 and at threat 100, seconds (lerped by threat)")]
        public float flickerGapAtZero = 8f;
        public float flickerGapAtMax = 2f;
        [Tooltip("One blink's length, seconds")]
        public float blinkMin = 0.05f;
        public float blinkMax = 0.1f;
        [Tooltip("The flashlight reveal: focus this long on foot, then MimicReveal for revealSeconds")]
        public float revealFocusSeconds = 0.75f;
        public float revealSeconds = 2f;
    }

    // §2.12b: its position follows its threat, and it only moves unobserved
    [Serializable]
    public sealed class AngelStalkConfig : MonsterAbilityConfig {
        [Tooltip("The aisle walk, metres per second")]
        public float walkSpeed = 0.55f;
        [Tooltip("Stuck this long while unobserved, it teleports to its target")]
        public float stuckTeleportSeconds = 3f;
        [Tooltip("It stands up into the aisle at this threat (Unsettled)")]
        public float standAtThreat = 25f;
    }

    // One monster type (§2.9–§2.12b, §4.8). Seeded once from §2 by DataSeeder, then the source of
    // truth (§0.1); nothing writes it during play (§4.1 rule 2). logicPrefab is the one field the
    // builder owns: PrefabBuilder generates the monster's prefab variant and points it here (D98).
    public sealed class MonsterDefinition : ScriptableObject {
        [Tooltip("lower_snake_case: starer, whisperer, mimic, weeping_angel")]
        public string id = "";
        [Tooltip("As the Game Over screen names it: \"The Starer\" → THE STARER GOT YOU (§2.21)")]
        public string displayName = "";

        [Header("Threat (§2.9)")]
        [Tooltip("Ordered: the first rule whose condition holds sets the rate for the frame")]
        public ThreatRule[] rules = new ThreatRule[0];
        [Tooltip("Which observer kinds count as watching it (§2.8, D35)")]
        public ObserverKinds observerKinds = ObserverKinds.Cctv | ObserverKinds.Driver | ObserverKinds.OnFoot | ObserverKinds.Mirror;
        [Tooltip("The meter doesn't move for this long after it sits down, seconds")]
        public float graceSeconds = 10f;

        [Header("Seating (§2.6)")]
        public SeatZone seatZonePreference = SeatZone.Any;
        [Tooltip("Tried when the preferred zone is full, before any seat")]
        public SeatZone seatZoneFallback = SeatZone.Any;

        [Header("Kill sequence (§2.14)")]
        public float killTelegraphSeconds = 4f;
        public MonsterEscape escape = new MonsterEscape(EscapeKind.ObserveFor, 1.5f);

        [Header("Money (§2.7), cents")]
        public int bountyCents = 500;

        [Header("Ability")]
        [SerializeReference] public MonsterAbilityConfig ability;

        [Header("Journal (§2.20)")]
        public MonsterJournal journal = new MonsterJournal();

        [Header("Generated")]
        [Tooltip("Generated/Prefabs/Monsters/Monster_<id>.prefab, written by PrefabBuilder")]
        public GameObject logicPrefab;

        // Whether the monster counts this observer kind as watching it
        public bool Counts(ObserverKinds kinds) {
            return (observerKinds & kinds) != 0;
        }

        public T Ability<T>() where T : MonsterAbilityConfig {
            return ability as T;
        }
    }
}
