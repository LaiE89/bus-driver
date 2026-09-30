using TMPro;
using UnityEngine;

namespace BusDriver.UI.Menu {
    // Bottom-right build label on the menu, so bug reports name the build (§4.18, §4.20).
    // MenuContext hands it GameServices.Build.Label.
    [RequireComponent(typeof(TMP_Text))]
    public class BuildLabelView : MonoBehaviour {
        public void Show(string label) {
            GetComponent<TMP_Text>().text = label;
        }
    }
}
