using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BusDriver.Editor.Builders {
    // Every EventSystem drives UI through the Input System and our own UI map (§4.10 cut-over).
    // The module's action fields are set to the InputActionReference sub-assets the
    // .inputactions importer creates, because references made at edit time with
    // InputActionReference.Create aren't assets and would be lost when the scene is saved.
    public static class UIInputModuleSetup {
        public const string ActionsPath = "Assets/Input/BusDriver.inputactions";

        // Module field → action in the UI map; null means "not used"
        static readonly string[,] Fields = {
            { "m_PointAction", "UI/Point" },
            { "m_MoveAction", "UI/Navigate" },
            { "m_SubmitAction", "UI/Submit" },
            { "m_CancelAction", "UI/Cancel" },
            { "m_LeftClickAction", "UI/Click" },
            { "m_ScrollWheelAction", "UI/ScrollWheel" },
            { "m_MiddleClickAction", null },
            { "m_RightClickAction", null },
            { "m_TrackedDevicePositionAction", null },
            { "m_TrackedDeviceOrientationAction", null },
        };

        public static InputSystemUIInputModule Configure(GameObject eventSystem) {
            if (eventSystem.GetComponent<EventSystem>() == null) {
                eventSystem.AddComponent<EventSystem>();
            }
            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null) {
                Object.DestroyImmediate(legacy, true);
            }
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) {
                module = eventSystem.AddComponent<InputSystemUIInputModule>();
            }
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (asset == null) {
                Debug.LogError("UIInputModuleSetup: no actions asset at " + ActionsPath);
                return module;
            }
            SerializedObject so = new SerializedObject(module);
            Set(so, "m_ActionsAsset", asset);
            for (int i = 0; i < Fields.GetLength(0); i++) {
                string actionId = Fields[i, 1];
                Set(so, Fields[i, 0], actionId == null ? null : FindReference(actionId));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return module;
        }

        static void Set(SerializedObject so, string field, Object value) {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) {
                Debug.LogError($"UIInputModuleSetup: InputSystemUIInputModule has no serialized field {field}");
                return;
            }
            property.objectReferenceValue = value;
        }

        static InputActionReference FindReference(string actionId) {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            InputAction action = asset.FindAction(actionId, true);
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ActionsPath)) {
                InputActionReference reference = sub as InputActionReference;
                if (reference != null && reference.action != null && reference.action.id == action.id) {
                    return reference;
                }
            }
            Debug.LogError("UIInputModuleSetup: the importer made no InputActionReference for " + actionId);
            return null;
        }
    }
}
