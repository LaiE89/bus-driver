using BusDriver.Gameplay.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // The loading overlay (§4.3). GameRoot instantiates it under itself, so unlike every other
    // piece of UI it survives a scene change: a night is two loads (Night_Systems replaces the
    // menu, then Route01_World comes in beside it), and the menu's own canvas dies between them.
    // It also carries a camera that clears to black, because between the menu going and the bus
    // being switched on there is no enabled camera in the game at all.
    public sealed class LoadingView : MonoBehaviour, IGameBindable {
        // Long enough to read "100%" before the night camera takes over
        const float HoldAtFullSeconds = 0.25f;

        [SerializeField] GameObject panel;
        [SerializeField] Canvas canvas;
        [SerializeField] Camera blankCamera;
        [SerializeField] Slider bar;
        [SerializeField] TMP_Text percentText;

        GameServices game;
        bool shown;
        bool wasLoading;
        // Displayed fill: eases toward SceneLoader.Progress the way the MVP menu bar did
        float displayed;
        // Grows while the overlay is up; Lerp(current, target, ease) catches up over the first second
        float ease;
        // < 0 while a load is in flight; otherwise the unscaled time the overlay may hide
        float hideAt = -1f;

        // Tests
        internal bool IsShown { get { return shown; } }
        internal float Shown01 { get { return bar != null ? bar.value : 0f; } }

        public void Bind(GameServices services) {
            game = services;
            hideAt = -1f;
            displayed = 0f;
            ease = 0f;
            Apply(false);
        }

        void Awake() {
            Apply(false);
        }

        void Update() {
            if (game == null) {
                return;
            }
            SceneLoader scenes = game.Scenes;
            // A night's two loads are separate operations, so IsLoading dips between them. The
            // overlay stays up until progress itself reaches the end, then eases the bar to 100%
            // and holds so the finish is readable.
            bool busy = scenes.IsLoading || scenes.Progress < 1f;
            if (busy) {
                hideAt = -1f;
                if (!shown) {
                    Apply(true);
                    displayed = 0f;
                    ease = 0f;
                }
                // A night is two loads; re-ease into each one so the bar doesn't snap after the first
                if (scenes.IsLoading && !wasLoading) {
                    ease = 0f;
                }
                wasLoading = scenes.IsLoading;
                ease += Time.unscaledDeltaTime;
                displayed = Mathf.Lerp(displayed, scenes.Progress, Mathf.Clamp01(ease));
                Paint(displayed);
                return;
            }
            wasLoading = false;
            if (!shown) {
                return;
            }
            ease += Time.unscaledDeltaTime;
            displayed = Mathf.Lerp(displayed, 1f, Mathf.Clamp01(ease));
            Paint(displayed);
            if (displayed < 0.999f) {
                return;
            }
            Paint(1f);
            if (hideAt < 0f) {
                hideAt = Time.unscaledTime + HoldAtFullSeconds;
            }
            if (Time.unscaledTime >= hideAt) {
                hideAt = -1f;
                Apply(false);
            }
        }

        void Paint(float progress01) {
            bar.SetValueWithoutNotify(progress01);
            // Two decimals, matching the MVP bar so the digits move with the fill
            percentText.SetText($"{(progress01 * 100f).ToString("N2")}%");
        }

        void Apply(bool loading) {
            shown = loading;
            if (panel != null) {
                panel.SetActive(loading);
            }
            if (canvas != null) {
                canvas.enabled = loading;
            }
            // Off whenever the overlay is down, so it never draws over a real camera
            if (blankCamera != null) {
                blankCamera.enabled = loading;
            }
        }
    }
}
