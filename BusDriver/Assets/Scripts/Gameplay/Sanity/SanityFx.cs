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
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // Tier presentation for the simple sanity spiral: T1 random sounds + camera flicker, T2 road
    // sprites + cabin light flicker, T3 thicker fog. No HUD number.
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
        [Tooltip("World height of a spawned sprite, whatever its pixel size")]
        [SerializeField] float spriteHeight = 1.8f;
        [Tooltip("How far above the road the bottom of the picture sits")]
        [SerializeField] float spriteLift = 1f;
        [Tooltip("Fraction of speed kept when the bus runs through a picture")]
        [SerializeField] [Range(0f, 1f)] float hitSpeedKeep = 0.4f;
        [Tooltip("Sanity lost when the bus runs through a picture")]
        [SerializeField] float obstacleSanityHit = -12f;
        [SerializeField] float hitFlashSeconds = 0.7f;
        [Tooltip("1 fills the screen. Larger zooms the picture past the edges")]
        [SerializeField] float hitFlashScale = 1.85f;
        [SerializeField] float cabinFlickerSeconds = 0.7f;
        [SerializeField] FloatRange cabinFlickerGap = new FloatRange(2.5f, 5.5f);
        [SerializeField] int maxObstacles = 3;

        [Header("Heartbeat [TUNE]")]
        [Tooltip("A looping heartbeat while driving below this sanity")]
        [SerializeField] float heartbeatBelow = 25f;

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
        SoundHandle heartbeat;

        public void Init(ShiftServices services, SanitySystem system) {
            shift = services;
            sanity = system;
            rng = services.Rng != null ? services.Rng.Get(RngStreams.Hallucination) : new System.Random(1);
            system.OnTierChanged += HandleTier;
            CaptureFog();
            ApplyFogForTier(system.Tier);
            ScheduleAll(system.Tier);
            CabinShadow shadow = gameObject.GetComponent<CabinShadow>();
            if (shadow == null) {
                shadow = gameObject.AddComponent<CabinShadow>();
            }
            shadow.Init(services, system, heartbeatBelow);
        }

        void OnDestroy() {
            if (sanity != null) {
                sanity.OnTierChanged -= HandleTier;
            }
            if (shift != null && shift.CabinLights != null) {
                shift.CabinLights.Stop(lightOwner);
            }
            StopCameraFlicker();
            StopHeartbeat();
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
                StopHeartbeat();
                return;
            }
            TickHeartbeat();
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

        void TickHeartbeat() {
            if (sanity.Value < heartbeatBelow) {
                IAudioService audio = shift.Game != null ? shift.Game.Audio : null;
                if (audio != null && !audio.IsPlaying(heartbeat)) {
                    heartbeat = audio.Play(SoundIds.SanHeartbeatLoop);
                }
            } else {
                StopHeartbeat();
            }
        }

        void StopHeartbeat() {
            if (heartbeat.IsNone || shift == null || shift.Game == null || shift.Game.Audio == null) {
                heartbeat = SoundHandle.None;
                return;
            }
            shift.Game.Audio.Stop(heartbeat);
            heartbeat = SoundHandle.None;
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
            if (shift.Bus == null) {
                return;
            }
            Sprite sprite = PickSprite();
            if (sprite == null) {
                return;
            }
            Transform bus = shift.Bus.transform;
            float side = ((float)rng.NextDouble() * 2f - 1f) * obstacleSideJitter;
            Vector3 pos = bus.position + bus.forward * obstacleAheadMeters + bus.right * side;
            pos.y = RoadHeight(pos) + spriteLift;

            GameObject root = new GameObject("SanityObstacle");
            root.transform.position = pos;
            SanityObstacle obstacle = root.AddComponent<SanityObstacle>();
            obstacle.Picture = sprite;
            float width = PlaceSprite(root.transform, sprite);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            float height = Mathf.Max(0.2f, spriteHeight);
            box.size = new Vector3(Mathf.Max(0.2f, width), height, 0.4f);
            box.center = new Vector3(0f, height * 0.5f, 0f);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            obstacle.Init(shift, OnObstacleHit);
            obstacles.Add(obstacle);
        }

        // Bottom of the picture sits on the road. Scale ignores the PNG's pixel size.
        float PlaceSprite(Transform root, Sprite sprite) {
            float units = sprite.bounds.size.y;
            float scale = units > 0.001f ? spriteHeight / units : 1f;
            GameObject visual = new GameObject("Sprite");
            visual.transform.SetParent(root, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            visual.transform.localPosition = new Vector3(-sprite.bounds.center.x * scale, -sprite.bounds.min.y * scale, 0f);
            return sprite.bounds.size.x * scale;
        }

        Sprite PickSprite() {
            GameRootConfig config = shift.Game != null ? shift.Game.Config : null;
            if (config == null || config.sanityObstacles == null || config.sanityObstacles.Length == 0) {
                return null;
            }
            for (int attempt = 0; attempt < 8; attempt++) {
                Sprite sprite = config.sanityObstacles[rng.Next(config.sanityObstacles.Length)];
                if (sprite != null) {
                    return sprite;
                }
            }
            return null;
        }

        // The road surface under a spawn point. The bus origin sits in the wheels, so using it
        // plants the picture in the asphalt.
        float RoadHeight(Vector3 pos) {
            Vector3 origin = pos + Vector3.up * 6f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 16f, ~0, QueryTriggerInteraction.Ignore)) {
                return hit.point.y;
            }
            return pos.y;
        }

        void OnObstacleHit(SanityObstacle obstacle) {
            if (obstacle == null) {
                return;
            }
            Sprite picture = obstacle.Picture;
            obstacles.Remove(obstacle);
            Destroy(obstacle.gameObject);
            if (shift.Bus != null) {
                shift.Bus.KnockSpeed(hitSpeedKeep);
            }
            if (sanity != null && obstacleSanityHit != 0f) {
                sanity.Apply(obstacleSanityHit, SanityReason.Scare);
            }
            PlayJumpScare(picture);
        }

        void PlayJumpScare(Sprite picture) {
            if (shift.Game != null && shift.Game.Audio != null) {
                shift.Game.Audio.Play(SoundIds.ScareStartleSting);
            }
            if (shift.Shake != null) {
                StartCoroutine(JumpScareShake());
            }
            if (shift.ScareOverlay != null) {
                Texture texture = picture != null ? picture.texture : null;
                StartCoroutine(JumpScareFlash(texture));
            }
        }

        IEnumerator JumpScareShake() {
            shift.Shake.SetShake(shakeOwner, 1f);
            yield return new WaitForSeconds(0.35f);
            shift.Shake.SetShake(shakeOwner, 0f);
        }

        IEnumerator JumpScareFlash(Texture picture) {
            ScareOverlayState overlay = shift.ScareOverlay;
            overlay.Overlay = picture;
            overlay.OverlayScale = hitFlashScale;
            overlay.OverlayAlpha = 1f;
            yield return new WaitForSeconds(hitFlashSeconds);
            if (shift != null && shift.ScareOverlay == overlay && overlay.Overlay == picture) {
                overlay.OverlayAlpha = 0f;
                overlay.Overlay = null;
                overlay.OverlayScale = 1f;
            }
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
