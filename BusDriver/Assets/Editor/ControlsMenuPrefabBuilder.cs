using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Bakes handbrake/doors/leave seat/interact rows into the Controls Menu prefab.
public static class ControlsMenuPrefabBuilder {
    const string PrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Controls Menu.prefab";
    const float RowSpacing = 55f;

    [MenuItem("Tools/Bus Driver/Upgrade Controls Menu Prefab")]
    public static void Upgrade() {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            ControlsMenu menu = prefabRoot.GetComponent<ControlsMenu>();
            if (menu == null) {
                Debug.LogError("ControlsMenuPrefabBuilder: ControlsMenu missing on prefab.");
                return;
            }

            Transform root = prefabRoot.transform;
            Transform switchLabel = root.Find("Switch Camera");
            Transform switchButton = root.Find("Switch Camera Button");
            if (switchLabel == null || switchButton == null) {
                Debug.LogError("ControlsMenuPrefabBuilder: Switch Camera row missing.");
                return;
            }

            TMP_Text switchValue = switchButton.GetComponentInChildren<TMP_Text>(true);
            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("switchCameraText").objectReferenceValue = switchValue;

            Button switchUiButton = switchButton.GetComponent<Button>();
            if (switchUiButton != null) {
                while (switchUiButton.onClick.GetPersistentEventCount() > 0) {
                    UnityEventTools.RemovePersistentListener(switchUiButton.onClick, 0);
                }
                UnityEventTools.AddPersistentListener(switchUiButton.onClick, menu.StartRebindSwitchCamera);
            }

            so.FindProperty("handbrakeText").objectReferenceValue = EnsureBindRow(
                root, switchLabel, switchButton, menu,
                "HANDBRAKE", "Handbrake Button", "handbrakeKey",
                -RowSpacing, nameof(ControlsMenu.StartRebindHandbrake));
            so.FindProperty("doorsText").objectReferenceValue = EnsureBindRow(
                root, switchLabel, switchButton, menu,
                "DOORS", "Doors Button", "doorsKey",
                -RowSpacing * 2f, nameof(ControlsMenu.StartRebindDoors));
            so.FindProperty("leaveSeatText").objectReferenceValue = EnsureBindRow(
                root, switchLabel, switchButton, menu,
                "LEAVE SEAT", "Leave Seat Button", "leaveSeatKey",
                -RowSpacing * 3f, nameof(ControlsMenu.StartRebindLeaveSeat));
            so.FindProperty("interactText").objectReferenceValue = EnsureBindRow(
                root, switchLabel, switchButton, menu,
                "INTERACT", "Interact Button", "interactKey",
                -RowSpacing * 4f, nameof(ControlsMenu.StartRebindInteract));
            so.FindProperty("kickOutText").objectReferenceValue = EnsureBindRow(
                root, switchLabel, switchButton, menu,
                "KICK OUT", "Kick Out Button", "kickOutKey",
                -RowSpacing * 5f, nameof(ControlsMenu.StartRebindKickOut));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log("Controls Menu prefab upgraded with baked bind rows.");
        }finally {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [InitializeOnLoadMethod]
    static void AutoUpgradeOnce() {
        EditorApplication.delayCall += () => {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return;
            }
            try {
                if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
                    return;
                }
            }catch {
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) {
                return;
            }
            Transform doorsButton = prefab.transform.Find("Doors Button");
            if (doorsButton == null || !doorsButton.gameObject.activeSelf
                || prefab.transform.Find("Kick Out Button") == null) {
                Upgrade();
            }
        };
    }

    static TMP_Text EnsureBindRow(
        Transform root,
        Transform templateLabel,
        Transform templateButton,
        ControlsMenu menu,
        string labelName,
        string buttonName,
        string valueName,
        float yOffset,
        string methodName) {
        Transform label = root.Find(labelName);
        if (label == null) {
            GameObject labelGo = Object.Instantiate(templateLabel.gameObject, root);
            labelGo.name = labelName;
            labelGo.SetActive(true);
            label = labelGo.transform;
        }
        label.gameObject.SetActive(true);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        RectTransform templateLabelRect = templateLabel.GetComponent<RectTransform>();
        labelRect.anchoredPosition = templateLabelRect.anchoredPosition + new Vector2(0f, yOffset);
        TMP_Text labelText = label.GetComponent<TMP_Text>();
        if (labelText != null) {
            labelText.text = labelName;
        }

        Transform button = root.Find(buttonName);
        if (button == null) {
            GameObject buttonGo = Object.Instantiate(templateButton.gameObject, root);
            buttonGo.name = buttonName;
            buttonGo.SetActive(true);
            button = buttonGo.transform;
        }
        button.gameObject.SetActive(true);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        RectTransform templateButtonRect = templateButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = templateButtonRect.anchoredPosition + new Vector2(0f, yOffset);

        Transform value = button.Find(valueName);
        if (value == null) {
            Transform keyChild = button.childCount > 0 ? button.GetChild(0) : null;
            if (keyChild != null) {
                keyChild.name = valueName;
                value = keyChild;
            }
        }

        Button uiButton = button.GetComponent<Button>();
        if (uiButton != null) {
            uiButton.enabled = true;
            while (uiButton.onClick.GetPersistentEventCount() > 0) {
                UnityEventTools.RemovePersistentListener(uiButton.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(uiButton.onClick, GetRebindAction(menu, methodName));
        }

        return value != null ? value.GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>(true);
    }

    static UnityEngine.Events.UnityAction GetRebindAction(ControlsMenu menu, string methodName) {
        switch (methodName) {
            case nameof(ControlsMenu.StartRebindHandbrake): return menu.StartRebindHandbrake;
            case nameof(ControlsMenu.StartRebindDoors): return menu.StartRebindDoors;
            case nameof(ControlsMenu.StartRebindLeaveSeat): return menu.StartRebindLeaveSeat;
            case nameof(ControlsMenu.StartRebindInteract): return menu.StartRebindInteract;
            case nameof(ControlsMenu.StartRebindKickOut): return menu.StartRebindKickOut;
            default: return menu.StartRebindSwitchCamera;
        }
    }
}
