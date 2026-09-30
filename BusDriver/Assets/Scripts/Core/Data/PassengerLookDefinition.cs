using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // How the greybox view draws a look (§4.14): flat colours, a height scale and one accessory
    [Serializable]
    public sealed class GreyboxLook {
        public Color bodyColor = new Color(0.65f, 0.55f, 0.5f);
        public Color headColor = new Color(0.8f, 0.68f, 0.58f);
        [Range(0.95f, 1.05f)]
        public float heightScale = 1f;
        public LookAccessory accessory = LookAccessory.None;
    }

    // One passenger look (§2.6, §4.8, Data/Looks/look01..look12). No two non-monster riders aboard
    // at once share a look (D34), so a duplicate always means the Mimic.
    public sealed class PassengerLookDefinition : ScriptableObject {
        [Tooltip("lower_snake_case, unique: look01 … look12")]
        public string id = "";
        public GreyboxLook greybox = new GreyboxLook();
        [Tooltip("The artist's view prefab (a PassengerViewBase at its root), empty until Phase B. Typed as a "
            + "GameObject because Core can't see Gameplay's view types (§4.2); ViewFactory reads the component.")]
        public GameObject artView;
    }
}
