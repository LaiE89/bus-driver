using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Fullscreen camera switching: exactly one camera renders at a time, so checking
// the cabin hides the road completely while the bus keeps moving.
public class CCTVSystem : MonoBehaviour {
    [SerializeField] Camera homeCamera;
    [SerializeField] Camera[] cctvCameras;
    [SerializeField] string[] cameraLabels;

    // -1 is the home (driver) view
    public int ActiveIndex { get; private set; } = -1;
    public bool IsViewingCCTV { get { return ActiveIndex >= 0; } }
    public string ActiveLabel {
        get {
            if (!IsViewingCCTV) {
                return "";
            }
            return ActiveIndex < cameraLabels.Length ? cameraLabels[ActiveIndex] : "CAM " + (ActiveIndex + 1);
        }
    }
    public event Action<int> OnViewChanged;

    Volume cctvVolume;

    void Awake() {
        CreateVolume();
        Apply();
    }

    void Update() {
        if (ingameMenus.pausedGame) {
            return;
        }
        // Read the static every frame so a rebind applies immediately
        if (Input.GetKeyDown(ControlsMenu.switchCameraKey)) {
            Cycle();
        }
    }

    void OnDisable() {
        ShowHome();
    }

    public void Cycle() {
        ActiveIndex++;
        if (ActiveIndex >= cctvCameras.Length) {
            ActiveIndex = -1;
        }
        Apply();
    }

    public void ShowHome() {
        if (ActiveIndex == -1) {
            return;
        }
        ActiveIndex = -1;
        Apply();
    }

    // For swapping in the on foot camera later
    public void SetHomeCamera(Camera newHomeCamera) {
        if (homeCamera != null) {
            homeCamera.enabled = false;
        }
        homeCamera = newHomeCamera;
        Apply();
    }

    void Apply() {
        if (homeCamera != null) {
            homeCamera.enabled = !IsViewingCCTV;
        }
        for (int i = 0; i < cctvCameras.Length; i++) {
            // Cameras can already be gone when this runs from OnDisable during scene unload
            if (cctvCameras[i] != null) {
                cctvCameras[i].enabled = i == ActiveIndex;
            }
        }
        if (cctvVolume != null) {
            cctvVolume.weight = IsViewingCCTV ? 1f : 0f;
        }
        OnViewChanged?.Invoke(ActiveIndex);
    }

    // Built at runtime so there is no profile asset to keep in sync. Only one camera
    // ever renders, so a global volume toggled by weight is enough.
    void CreateVolume() {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        ColorAdjustments color = profile.Add<ColorAdjustments>();
        color.saturation.Override(-100f);
        color.contrast.Override(25f);
        color.postExposure.Override(0.7f);

        FilmGrain grain = profile.Add<FilmGrain>();
        grain.intensity.Override(0.8f);
        grain.response.Override(0.2f);

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.45f);

        GameObject volumeObject = new GameObject("CCTV Volume");
        volumeObject.transform.SetParent(transform, false);
        cctvVolume = volumeObject.AddComponent<Volume>();
        cctvVolume.isGlobal = true;
        cctvVolume.priority = 10;
        cctvVolume.weight = 0f;
        cctvVolume.profile = profile;
    }
}
