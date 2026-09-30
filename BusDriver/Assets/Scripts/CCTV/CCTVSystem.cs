using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Bus {
    // Fullscreen camera switching: exactly one camera renders at a time, so checking
    // the cabin hides the road completely while the bus keeps moving.
    public class CCTVSystem : MonoBehaviour, IGameBindable {
        [SerializeField] Camera homeCamera;
        [Tooltip("Cycle order: Home → CAM 1 → CAM 2 → CAM 3 → Home (§2.2)")]
        [SerializeField] CctvCamera[] cameras = new CctvCamera[0];

        // -1 is the home (driver) view
        public int ActiveIndex { get; private set; } = -1;
        public bool IsViewingCCTV { get { return ActiveIndex >= 0; } }
        public string ActiveLabel {
            get {
                if (!IsViewingCCTV) {
                    return "";
                }
                return cameras[ActiveIndex].Label;
            }
        }
        // Whichever camera is rendering right now
        public Camera ActiveCamera { get { return IsViewingCCTV ? cameras[ActiveIndex].Camera : homeCamera; } }
        public CctvCamera ActiveCctv { get { return IsViewingCCTV ? cameras[ActiveIndex] : null; } }
        public IReadOnlyList<CctvCamera> Cameras { get { return cameras; } }
        // CCTV cameras plus the home view, one full cycle
        public int ViewCount { get { return cameras.Length + 1; } }
        public event Action<int> OnViewChanged;

        Volume cctvVolume;
        GameServices game;

        public void Bind(GameServices services) {
            game = services;
        }

        void Awake() {
            CreateVolume();
            Apply();
        }

        void Update() {
            if (game != null && game.Pause.IsPaused) {
                return;
            }
            if (game != null && game.Input.Actions.CycleCamera.WasPressedThisFrame()) {
                Cycle();
            }
        }

        void OnDisable() {
            ShowHome();
        }

        public void Cycle() {
            ActiveIndex++;
            if (ActiveIndex >= cameras.Length) {
                ActiveIndex = -1;
            }
            if (game != null) {
                game.Audio.Play(SoundIds.CctvSwitch);
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
            for (int i = 0; i < cameras.Length; i++) {
                // Cameras can already be gone when this runs from OnDisable during scene unload
                if (cameras[i] != null && cameras[i].Camera != null) {
                    cameras[i].Camera.enabled = i == ActiveIndex;
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
}
