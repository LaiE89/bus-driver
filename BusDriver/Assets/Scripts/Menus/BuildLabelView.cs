using BusDriver.Core.Util;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Menu {
    // Bottom-right build label on the menu, so bug reports name the build (§4.18, §4.20)
    [RequireComponent(typeof(TMP_Text))]
    public class BuildLabelView : MonoBehaviour {
        void Start() {
            GetComponent<TMP_Text>().text = BuildLabel.Current;
        }
    }
}
