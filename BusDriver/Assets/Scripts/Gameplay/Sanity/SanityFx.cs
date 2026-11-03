using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // Tier presentation for the simple sanity spiral: T1 random sounds + camera flicker, T2 road
    // obstacles (NPC models) + cabin light flicker, T3 thicker fog. No HUD number.
    public sealed class SanityFx : MonoBehaviour {
        static readonly string[] AmbientSounds = {
            SoundIds.HalFootstepsBehind, SoundIds.HalDoorChime, SoundIds.HalWindowKnock,
            SoundIds.HalStaticBurst, SoundIds.HalWhisperDriver,
        };

        [Header("T1 — sounds + camera flicker [TUNE]")]
        [SerializeField] FloatRange soundInterval = new FloatRange(22f, 55f);
        [SerializeField] FloatRange flickerInterval = new FloatRange(8f, 18f);
        [SerializeField] float flickerSeconds = 0.18f;
        [SerializeField] float flickerAmplitude = 0.55f;
        [SerializeField] float soundVolumeBoost = 1.6f;
        [Tooltip("Chance a due scare sound actually fires (rest reschedule quietly)")]
        [SerializeField] [Range(0.2f, 1f)] float soundFireChance = 0.55f;

        [Header("T2 — obstacles + cabin lights [TUNE]")]
        [SerializeField] FloatRange obstacleInterval = new FloatRange(14f, 26f);
        [SerializeField] float obstacleAheadMeters = 38f;
        [SerializeField] float obstacleSideJitter = 1.2f;
        [SerializeField] float cabinFlickerSeconds = 0.7f;
        [SerializeField] FloatRange cabinFlickerGap = new FloatRange(2.5f, 5.5f);
        [SerializeField] int maxObstacles = 3;

        [Header("T3 — fog [TUNE]")]
        [SerializeField] float fogDensityAtT3 = 0.055f;
        [SerializeField] Color fogColorAtT3 = new Color(0.45f, 0.02f, 0.02f, 1f);
        [SerializeField] float fogFadeSeconds = 12f;

        ShiftServices shift;
        SanitySystem sanity;
        System.Random rng;
        readonly List<SanityObstacle> obstacles = new List<SanityObstacle>();
        readonly object lightOwner = new object();
        readonly object shakeOwner = new object();

        float soundTimer;
        float flickerTimer;
        float obstacleTimer;
        float cabinTimer;
        float baseFogDensity;
        Color baseFogColor;
        bool baseFogEnabled;
        bool fogCaptured;
        bool wantRedFog;
        float fogBlend;
        int lastSoundIndex = -1;
        bool cameraFlickerRunning;

        public void Init(ShiftServices services, SanitySystem system) {
            shift = services;
            sanity = system;
            rng = services.Rng != null ? services.Rng.Get(RngStreams.Hallucination) : new System.Random(1);
            system.OnTierChanged += HandleTier;
            CaptureFog();
            ApplyFogForTier(system.Tier);
            ScheduleAll(system.Tier);
        }

        void OnDestroy() {
            if (sanity != null) {
                sanity.OnTierChanged -= HandleTier;
            }
            if (shift != null && shift.CabinLights != null) {
                shift.CabinLights.Stop(lightOwner);
            }
            StopCameraFlicker();
            RestoreFog();
            ClearObstacles();
        }

        void HandleTier(SanityTier tier) {
            ApplyFogForTier(tier);
            ScheduleAll(tier);
            if (tier < SanityTier.T2) {
                ClearObstacles();
                if (shift != null && shift.CabinLights != null) {
                    shift.CabinLights.Stop(lightOwner);
                }
            }
        }

        void Update() {
            if (shift == null || sanity == null) {
                return;
            }
            // Fog keeps easing even outside Driving so a mid-fade exit doesn't snap
            TickFog(Time.deltaTime);
            if (shift.Director == null || shift.Director.State != ShiftState.Driving) {
                return;
            }
            SanityTier tier = sanity.Tier;
            if (tier >= SanityTier.T1) {
                TickSound();
                // Camera flicker is CCTV-only so the driver POV never flashes
                if (IsViewingCctv()) {
                    TickCameraFlicker();
                }else if (cameraFlickerRunning) {
                    StopCameraFlicker();
                }
            }
            if (tier >= SanityTier.T2) {
                TickCabinFlicker();
                TickObstacles();
            }
        }

        bool IsViewingCctv() {
            return shift.Cctv != null && shift.Cctv.IsViewingCCTV;
        }

        void TickSound() {
            soundTimer -= Time.deltaTime;
            if (soundTimer > 0f) {
                return;
            }
            soundTimer = Next(soundInterval);
            // Miss some rolls so timing feels less metronomic
            if (rng.NextDouble() > soundFireChance) {
                return;
            }
            if (shift.Game == null || shift.Game.Audio == null) {
                return;
            }
            int index = rng.Next(AmbientSounds.Length);
            if (AmbientSounds.Length > 1 && index == lastSoundIndex) {
                index = (index + 1 + rng.Next(AmbientSounds.Length - 1)) % AmbientSounds.Length;
            }
            lastSoundIndex = index;
            string id = AmbientSounds[index];
            SoundHandle handle = shift.Game.Audio.Play(id);
            if (!handle.IsNone) {
                shift.Game.Audio.SetVolume(handle, soundVolumeBoost);
            }
        }

        void TickCameraFlicker() {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer > 0f || cameraFlickerRunning) {
                return;
            }
            flickerTimer = Next(flickerInterval);
            StartCoroutine(CameraFlickerRoutine());
        }

        IEnumerator CameraFlickerRoutine() {
            cameraFlickerRunning = true;
            CameraShake shake = shift.Shake;
            ScreenFade fade = shift.Fade;
            if (shake != null) {
                shake.SetShake(shakeOwner, flickerAmplitude);
            }
            if (fade != null) {
                fade.Set(0.35f);
            }
            yield return new WaitForSeconds(flickerSeconds);
            // Only clear if we are still the active flicker (leaving CCTV calls StopCameraFlicker)
            if (cameraFlickerRunning) {
                StopCameraFlicker();
            }
        }

        void StopCameraFlicker() {
            cameraFlickerRunning = false;
            if (shift != null && shift.Shake != null) {
                shift.Shake.SetShake(shakeOwner, 0f);
            }
            if (shift != null && shift.Fade != null) {
                shift.Fade.Set(0f);
            }
        }

        void TickCabinFlicker() {
            cabinTimer -= Time.deltaTime;
            if (cabinTimer > 0f || shift.CabinLights == null) {
                return;
            }
            cabinTimer = Next(cabinFlickerGap);
            shift.CabinLights.Flicker(lightOwner, cabinFlickerSeconds);
        }

        void TickObstacles() {
            PruneObstacles();
            obstacleTimer -= Time.deltaTime;
            if (obstacleTimer > 0f || obstacles.Count >= maxObstacles) {
                return;
            }
            obstacleTimer = Next(obstacleInterval);
            SpawnObstacle();
        }

        void SpawnObstacle() {
            if (shift.Bus == null || shift.Views == null) {
                return;
            }
            Transform bus = shift.Bus.transform;
            float side = ((float)rng.NextDouble() * 2f - 1f) * obstacleSideJitter;
            Vector3 pos = bus.position + bus.forward * obstacleAheadMeters + bus.right * side;
            pos.y = bus.position.y;

            GameObject root = new GameObject("SanityObstacle");
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-bus.forward, Vector3.up));
            SanityObstacle obstacle = root.AddComponent<SanityObstacle>();
            PassengerLookDefinition look = PickLook();
            PassengerViewBase view = shift.Views.Create(look, root.transform);
            if (view != null) {
                view.transform.localPosition = Vector3.zero;
            }
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.isTrigger = true;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            obstacle.Init(shift, OnObstacleHit);
            obstacles.Add(obstacle);
        }

        PassengerLookDefinition PickLook() {
            GameRootConfig config = shift.Game != null ? shift.Game.Config : null;
            if (config == null || config.looks == null || config.looks.Length == 0) {
                return null;
            }
            PassengerLookDefinition look = null;
            for (int attempt = 0; attempt < 8 && look == null; attempt++) {
                look = config.looks[rng.Next(config.looks.Length)];
            }
            return look;
        }

        void OnObstacleHit(SanityObstacle obstacle) {
            if (obstacle == null) {
                return;
            }
            obstacles.Remove(obstacle);
            PlayJumpScare();
            Destroy(obstacle.gameObject);
        }

        void PlayJumpScare() {
            if (shift.Game != null && shift.Game.Audio != null) {
                shift.Game.Audio.Play(SoundIds.ScareStartleSting);
            }
            if (shift.Shake != null) {
                StartCoroutine(JumpScareShake());
            }
            if (shift.ScareOverlay != null) {
                StartCoroutine(JumpScareFlash());
            }
        }

        IEnumerator JumpScareShake() {
            shift.Shake.SetShake(shakeOwner, 1f);
            yield return new WaitForSeconds(0.35f);
            shift.Shake.SetShake(shakeOwner, 0f);
        }

        IEnumerator JumpScareFlash() {
            ScareOverlayState overlay = shift.ScareOverlay;
            overlay.OverlayAlpha = 0.85f;
            yield return new WaitForSeconds(0.12f);
            overlay.OverlayAlpha = 0f;
        }

        void PruneObstacles() {
            Transform bus = shift.Bus != null ? shift.Bus.transform : null;
            for (int i = obstacles.Count - 1; i >= 0; i--) {
                SanityObstacle obstacle = obstacles[i];
                if (obstacle == null) {
                    obstacles.RemoveAt(i);
                    continue;
                }
                if (bus == null) {
                    continue;
                }
                // Despawn once the bus has driven past
                Vector3 toObstacle = obstacle.transform.position - bus.position;
                if (Vector3.Dot(bus.forward, toObstacle) < -8f) {
                    Destroy(obstacle.gameObject);
                    obstacles.RemoveAt(i);
                }
            }
        }

        void ClearObstacles() {
            for (int i = 0; i < obstacles.Count; i++) {
                if (obstacles[i] != null) {
                    Destroy(obstacles[i].gameObject);
                }
            }
            obstacles.Clear();
        }

        void ScheduleAll(SanityTier tier) {
            // Full random waits — no front-loaded burst when a tier is entered
            soundTimer = tier >= SanityTier.T1 ? Next(soundInterval) : 9999f;
            flickerTimer = tier >= SanityTier.T1 ? Next(flickerInterval) : 9999f;
            obstacleTimer = tier >= SanityTier.T2 ? Next(obstacleInterval) : 9999f;
            cabinTimer = tier >= SanityTier.T2 ? Next(cabinFlickerGap) : 9999f;
        }

        void CaptureFog() {
            if (fogCaptured) {
                return;
            }
            baseFogDensity = RenderSettings.fogDensity;
            baseFogColor = RenderSettings.fogColor;
            baseFogEnabled = RenderSettings.fog;
            fogCaptured = true;
        }

        void ApplyFogForTier(SanityTier tier) {
            CaptureFog();
            wantRedFog = tier >= SanityTier.T3;
            // Immediate apply of current blend so entering T3 starts from wherever we were
            ApplyFogBlend();
        }

        void TickFog(float dt) {
            if (!fogCaptured) {
                return;
            }
            float target = wantRedFog ? 1f : 0f;
            float speed = fogFadeSeconds > 0.01f ? 1f / fogFadeSeconds : 1f;
            fogBlend = Mathf.MoveTowards(fogBlend, target, speed * dt);
            ApplyFogBlend();
        }

        void ApplyFogBlend() {
            if (fogBlend <= 0.001f) {
                RestoreFog();
                return;
            }
            RenderSettings.fog = true;
            RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, fogDensityAtT3, fogBlend);
            RenderSettings.fogColor = Color.Lerp(baseFogColor, fogColorAtT3, fogBlend);
        }

        void RestoreFog() {
            if (!fogCaptured) {
                return;
            }
            RenderSettings.fog = baseFogEnabled;
            RenderSettings.fogDensity = baseFogDensity;
            RenderSettings.fogColor = baseFogColor;
        }

        float Next(FloatRange range) {
            float min = range.min;
            float max = range.max;
            if (max < min) {
                float swap = min;
                min = max;
                max = swap;
            }
            if (max <= min) {
                return min;
            }
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
