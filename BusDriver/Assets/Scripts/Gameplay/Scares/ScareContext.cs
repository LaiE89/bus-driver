using BusDriver.Gameplay.Monsters;
using UnityEngine;

namespace BusDriver.Gameplay.Scares {
    // What a scare is about (§4.6 ScareDirector.Request): the monster that caused it, if any, and
    // the CCTV camera it belongs to (the Starer's lens scare cuts to "its" camera). The default
    // value means neither.
    public struct ScareContext {
        public MonsterBrain Source;
        // Stored one up, so the default value means "no camera"
        int cameraPlusOne;

        // The 0-based CCTV camera, or −1
        public int CctvIndex {
            get { return cameraPlusOne - 1; }
            set { cameraPlusOne = value + 1; }
        }

        public static ScareContext For(MonsterBrain source) {
            return new ScareContext { Source = source };
        }

        public static ScareContext AtCamera(int cctvIndex, MonsterBrain source = null) {
            return new ScareContext { Source = source, CctvIndex = cctvIndex };
        }
    }

    // A scare instance ScarePlayer is running; the default value is "none"
    public readonly struct ScareHandle {
        internal readonly int Id;

        internal ScareHandle(int id) {
            Id = id;
        }

        public bool IsNone { get { return Id == 0; } }
    }

    // The full-screen scare effects, as values (like ScreenFade): ScarePlayer sets them and the
    // HUD's ScareOverlayView draws them, so gameplay never references UI (§4.2). Blackouts use the
    // night's ScreenFade, which the death presenters keep black afterwards.
    public sealed class ScareOverlayState {
        // ShowOverlay's texture and opacity
        public Texture Overlay;
        public float OverlayAlpha;
        // CctvStatic's opacity
        public float StaticAlpha;
        // HandsOverCamera: 0 out of view .. 1 closed over the view
        public float HandsProgress;
    }
}
