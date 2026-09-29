using UnityEngine;

// Shared player-mesh helpers. Mesh creation is editor/scene-builder only;
// runtime only applies culling layers / camera masks to existing scene objects.
public static class PlayerAvatarVisuals {
    // Whole avatar (body + head) lives on this layer so FP cams can cull it; CCTV keeps it.
    public const string HeadLayerName = "PlayerHead";

    public static int AvatarLayer {
        get {
            int layer = LayerMask.NameToLayer(HeadLayerName);
            return layer >= 0 ? layer : 0;
        }
    }

#if UNITY_EDITOR
    public static Transform Create(
        Transform parent,
        Transform headAnchor,
        bool seated,
        Material bodyMaterial,
        Material faceMaterial,
        bool parentHeadToAnchor) {
        GameObject root = new GameObject("Avatar");
        Transform avatar = root.transform;
        avatar.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(avatar, false);
        if (seated) {
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        }else {
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.75f, 0.42f);
        }
        ApplyVisual(body, bodyMaterial);

        Transform headParent = parentHeadToAnchor && headAnchor != null ? headAnchor : avatar;
        GameObject headVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headVisual.name = "HeadVisual";
        headVisual.transform.SetParent(headParent, false);
        if (parentHeadToAnchor && headAnchor != null) {
            headVisual.transform.localPosition = Vector3.zero;
        }else {
            headVisual.transform.localPosition = seated
                ? new Vector3(0f, 0.94f, 0f)
                : new Vector3(0f, 1.6f, 0f);
        }
        headVisual.transform.localScale = Vector3.one * 0.26f;
        ApplyVisual(headVisual, bodyMaterial);

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Face";
        face.transform.SetParent(headVisual.transform, false);
        face.transform.localPosition = new Vector3(0f, 0.08f, 0.46f);
        face.transform.localScale = new Vector3(0.6f, 0.2f, 0.15f);
        ApplyVisual(face, faceMaterial);

        // Body under avatar root; head may sit under the FP head pivot instead
        SetLayer(root, AvatarLayer);
        SetLayer(headVisual, AvatarLayer);

        return avatar;
    }

    static void ApplyVisual(GameObject go, Material mat) {
        Collider col = go.GetComponent<Collider>();
        if (col != null) {
            Object.DestroyImmediate(col);
        }
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && mat != null) {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
#endif

    public static void ApplyCullLayer(Transform avatarRoot, Transform headAnchor = null) {
        if (avatarRoot != null) {
            SetLayer(avatarRoot.gameObject, AvatarLayer);
        }
        if (headAnchor != null) {
            Transform headVisual = headAnchor.Find("HeadVisual");
            if (headVisual != null) {
                SetLayer(headVisual.gameObject, AvatarLayer);
            }
        }
    }

    // First-person cameras should not draw the avatar; CCTV still should.
    public static void HideAvatarFromCamera(Camera camera) {
        if (camera == null) {
            return;
        }
        int layer = AvatarLayer;
        if (layer == 0 && LayerMask.NameToLayer(HeadLayerName) < 0) {
            return;
        }
        camera.cullingMask &= ~(1 << layer);
    }

    // Kept for older call sites
    public static void HideHeadFromCamera(Camera camera) {
        HideAvatarFromCamera(camera);
    }

    static void SetLayer(GameObject go, int layer) {
        if (go == null || layer < 0) {
            return;
        }
        go.layer = layer;
        foreach (Transform child in go.transform) {
            SetLayer(child.gameObject, layer);
        }
    }
}
