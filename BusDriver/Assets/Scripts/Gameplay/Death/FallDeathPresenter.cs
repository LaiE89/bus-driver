using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using UnityEngine;

namespace BusDriver.Gameplay.Death {
    // Fall (§2.14, D14): the bus went over Dead Man's Bend. The death has already locked the input
    // (Cinematic) when this starts, the moment the hull entered the FallZone.
    // 1. The driver's view goes and FallCamera watches from the cliff edge (FallCamAnchor),
    //    tracking the bus with a pale spot light so it reads against the valley; physics keeps
    //    simulating.
    // 2. timeScale 0.5 for the first second, then 1.0.
    // 3. The engine stops and death.fall_wind plays; the first hull hit on the valley floor plays
    //    death.fall_impact and shakes the camera.
    // 4. 3.0 s after the zone was entered it fades to black with "YOU WENT OVER THE EDGE", then
    //    Game Over. The seconds are the wall clock while not paused, so the slow motion doesn't
    //    stretch them and a pause holds them.
    public sealed class FallDeathPresenter : DeathPresenter {
        public const string Caption = "YOU WENT OVER THE EDGE";

        [Tooltip("The Night_Systems FallCamera instance's camera, disabled until a fall")]
        [SerializeField] Camera fallCamera;
        [Tooltip("timeScale for the start of the fall [TUNE]")]
        [SerializeField] float slowMotionScale = 0.5f;
        [Tooltip("How long the slow motion lasts, seconds of wall clock [TUNE]")]
        [SerializeField] float slowMotionSeconds = 1f;
        [Tooltip("When the fade to black starts, seconds after the zone was entered")]
        [SerializeField] float fadeAt = 3f;
        [SerializeField] float fadeSeconds = 1f;
        [Tooltip("Black, with the caption, before Game Over")]
        [SerializeField] float holdSeconds = 1.5f;
        [Tooltip("A hull hit within this height of the valley floor is the impact, metres")]
        [SerializeField] float floorTolerance = 4f;
        [Tooltip("The impact shake [TUNE]")]
        [SerializeField] float impactShake = 0.8f;
        [SerializeField] float impactShakeSeconds = 0.6f;

        ShiftServices shift;
        CrashDetector crash;
        CameraShake shake;
        Light spot;
        Rigidbody busBody;
        bool presenting;
        bool landed;
        float shakeLeft;

        public override DeathCause Cause { get { return DeathCause.Fall; } }
        public Camera FallCamera { get { return fallCamera; } }
        // The bus has hit the valley floor
        public bool Landed { get { return landed; } }
        public bool IsPresenting { get { return presenting; } }

        public override void Init(ShiftServices services) {
            shift = services;
            crash = services.Bus.GetComponent<CrashDetector>();
            busBody = services.Bus.GetComponent<Rigidbody>();
            shake = fallCamera != null ? fallCamera.GetComponent<CameraShake>() : null;
            spot = fallCamera != null ? Rig.GetComponentInChildren<Light>(true) : null;
            if (crash != null) {
                crash.OnCrash += HandleCrash;
            }
        }

        void OnDestroy() {
            if (crash != null) {
                crash.OnCrash -= HandleCrash;
            }
        }

        public override IEnumerator Present(DeathReport report) {
            presenting = true;
            landed = false;
            Transform anchor = shift.Route != null ? shift.Route.FallCamAnchor : null;
            // The driver's view goes: back from any CCTV feed, then the fall camera takes over
            if (shift.Cctv != null) {
                shift.Cctv.ShowHome();
                shift.Cctv.enabled = false;
            }
            if (shift.DriverCamera != null) {
                shift.DriverCamera.enabled = false;
            }
            if (fallCamera != null) {
                if (anchor != null) {
                    Rig.SetPositionAndRotation(anchor.position, anchor.rotation);
                }
                Track();
                fallCamera.enabled = true;
                if (spot != null) {
                    spot.enabled = true;
                }
            }else {
                Log.Warn(LogCat.Death, "FallDeathPresenter has no fall camera");
            }
            if (shift.EngineSound != null) {
                shift.EngineSound.enabled = false;
            }
            shift.Game.Audio.Play(SoundIds.DeathFallWind);

            Time.timeScale = slowMotionScale;
            bool slow = true;
            float elapsed = 0f;
            while (elapsed < fadeAt) {
                yield return null;
                if (Time.timeScale > 0f) {
                    elapsed += Time.unscaledDeltaTime;
                }
                if (slow && elapsed >= slowMotionSeconds && Time.timeScale > 0f) {
                    slow = false;
                    Time.timeScale = 1f;
                }
                Track();
            }
            if (slow && Time.timeScale > 0f) {
                Time.timeScale = 1f;
            }
            shift.Fade.Caption = Caption;
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime) {
                shift.Fade.Set(t / fadeSeconds);
                Track();
                yield return null;
            }
            shift.Fade.Set(1f);
            yield return new WaitForSeconds(holdSeconds);
            presenting = false;
        }

        void LateUpdate() {
            if (!presenting) {
                return;
            }
            Track();
            if (shakeLeft > 0f) {
                shakeLeft -= Time.deltaTime;
                if (shakeLeft <= 0f && shake != null) {
                    shake.SetShake(this, 0f);
                }
            }
        }

        // The camera stays on the anchor and turns to follow the bus down
        void Track() {
            if (fallCamera == null || busBody == null) {
                return;
            }
            Vector3 to = busBody.worldCenterOfMass - Rig.position;
            if (to.sqrMagnitude > 0.01f) {
                Rig.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
        }

        // The FallCamera instance's root: it sits on the anchor and turns; the camera below it shakes
        Transform Rig {
            get { return fallCamera.transform.parent != null ? fallCamera.transform.parent : fallCamera.transform; }
        }

        // The first hull hit near the valley floor is the landing (the cliff face doesn't count)
        void HandleCrash(float deltaV, bool major, Collision collision) {
            if (!presenting || landed || collision.contactCount == 0) {
                return;
            }
            float floor = shift.Route != null && shift.Route.Route != null ? shift.Route.Route.generation.valleyFloorY : -40f;
            Vector3 point = collision.GetContact(0).point;
            if (point.y > floor + floorTolerance) {
                return;
            }
            landed = true;
            shift.Game.Audio.PlayAt(SoundIds.DeathFallImpact, point);
            if (shake != null) {
                shake.SetShake(this, impactShake);
                shakeLeft = impactShakeSeconds;
            }
            Log.Info(LogCat.Death, $"the bus hit the valley floor at {deltaV:0.0} m/s");
        }
    }
}
