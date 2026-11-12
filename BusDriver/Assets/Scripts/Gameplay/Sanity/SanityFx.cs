using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // Tier presentation for the simple sanity spiral. Crossing under 75 starts the sounds, the
    // CCTV flicker and the cabin-light flicker, and those keep going at every sanity below that.
    // Crossing under 50 adds the road pictures and a knock at the driver's window or the door
    // window, with a figure standing outside that glass. Those keep going below 50 too. T3 adds
    // the red fog. No HUD number.
    public sealed class SanityFx : MonoBehaviour {
        static readonly string[] AmbientSounds = {
            SoundIds.HalFootstepsBehind, SoundIds.HalDoorChime, SoundIds.HalStaticBurst, SoundIds.HalWhisperDriver,
        };

        [Header("T1 — sounds, camera flicker, cabin lights [TUNE]")]
        [SerializeField] FloatRange soundInterval = new FloatRange(10f, 22f);
        [SerializeField] FloatRange flickerInterval = new FloatRange(8f, 18f);
        [SerializeField] float flickerSeconds = 0.18f;
        [SerializeField] float flickerAmplitude = 0.55f;
        [SerializeField] float soundVolumeBoost = 1.6f;
        [Tooltip("Chance a due scare sound actually fires (rest reschedule quietly)")]
        [SerializeField] [Range(0.2f, 1f)] float soundFireChance = 0.55f;
        [SerializeField] float cabinFlickerSeconds = 0.7f;
        [SerializeField] FloatRange cabinFlickerGap = new FloatRange(2.5f, 5.5f);

        [Header("T2 — obstacles [TUNE]")]
        [SerializeField] FloatRange obstacleInterval = new FloatRange(36f, 62f);
        [SerializeField] float obstacleAheadMeters = 38f;
        [Tooltip("Metres of road kept clear between pictures")]
        [SerializeField] float obstacleSpacing = 18f;
        [Tooltip("Chance a picture spawns far up the road and walks back toward the bus")]
        [SerializeField] [Range(0f, 1f)] float walkerChance = 0.45f;
        [SerializeField] FloatRange walkerAhead = new FloatRange(80f, 120f);
        [SerializeField] float walkerMetresPerSecond = 3.4f;
        [Tooltip("World height of a picture standing on the road, whatever its pixel size")]
        [SerializeField] float spriteHeight = 3.2f;
        [Tooltip("How far above the road the bottom of the picture sits")]
        [SerializeField] float spriteLift = 0.05f;
        [Tooltip("Fraction of speed kept when the bus runs through a picture")]
        [SerializeField] [Range(0f, 1f)] float hitSpeedKeep = 0.4f;
        [Tooltip("Sanity lost when the bus runs through a picture")]
        [SerializeField] float obstacleSanityHit = -10f;
        [SerializeField] float hitFlashSeconds = 0.7f;
        [Tooltip("1 fills the screen. Larger zooms the picture past the edges")]
        [SerializeField] float hitFlashScale = 1.85f;
        [SerializeField] int maxObstacles = 3;

        [Header("Window knock, under 50 [TUNE]")]
        [SerializeField] FloatRange knockInterval = new FloatRange(22f, 48f);
        [SerializeField] float knockLookSeconds = 3.2f;
        [Tooltip("World height of the figure outside the window")]
        [SerializeField] float knockSpriteHeight = 1.8f;
        [Tooltip("How far the driver must turn, in degrees, before a knock becomes a jumpscare")]
        [SerializeField] float knockLookYaw = 40f;
        [Tooltip("Seconds the figure stays in view after the driver looks, before the jumpscare")]
        [SerializeField] float knockScareDelay = 0.5f;

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
        float knockTimer;
        bool knockActive;
        bool knockOnDoor;
        float knockLookUntil;
        float knockScareAt;
        Sprite knockSprite;
        GameObject knockFigure;
        float baseFogDensity;
        Color baseFogColor;
        bool baseFogEnabled;
        bool fogCaptured;
        bool wantRedFog;
        float fogBlend;
        int lastSoundIndex = -1;
        bool cameraFlickerRunning;
        SoundHandle heartbeat;
        // Which bands are already scheduled, so falling further does not restart them
        bool under75;
        bool under50;

        public void Init(ShiftServices services, SanitySystem system) {
            shift = services;
            sanity = system;
            rng = services.Rng != null ? services.Rng.Get(RngStreams.Hallucination) : new System.Random(1);
            system.OnTierChanged += HandleTier;
            CaptureFog();
            ApplyBands(system.Tier);
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
            ClearKnockFigure();
            ClearObstacles();
        }

        void HandleTier(SanityTier tier) {
            ApplyBands(tier);
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
            // Under 75, and still under it at 50, 25 and 0
            float value = sanity.Value;
            if (value < SanityCore.Tier1Below) {
                TickSound();
                TickCabinFlicker();
                // Camera flicker is CCTV-only so the driver POV never flashes
                if (IsViewingCctv()) {
                    TickCameraFlicker();
                }else if (cameraFlickerRunning) {
                    StopCameraFlicker();
                }
            }
            // Under 50, and still under it at 25 and 0. The under-75 effects stay on as well.
            if (value < SanityCore.Tier2Below) {
                TickObstacles();
                TickKnock();
            } else if (knockActive) {
                EndKnock();
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

        // A knock on the driver's window or the door window, only under 50 and while rolling with
        // the doors shut. The picture stands outside that pane, and the knock plays there. Looking
        // at it holds for a second, then the jumpscare plays.
        void TickKnock() {
            if (!RollingWithDoorsShut()) {
                EndKnock();
                return;
            }
            if (knockActive) {
                if (knockScareAt > 0f) {
                    if (Time.time >= knockScareAt) {
                        Sprite picture = knockSprite;
                        EndKnock();
                        if (shift.Bus != null) {
                            shift.Bus.KnockSpeed(hitSpeedKeep);
                        }
                        if (sanity != null && obstacleSanityHit != 0f) {
                            sanity.Apply(obstacleSanityHit, SanityReason.Scare);
                        }
                        PlayJumpScare(picture);
                    }
                } else if (Time.time >= knockLookUntil) {
                    EndKnock();
                } else if (LookingAtKnock()) {
                    knockScareAt = Time.time + knockScareDelay;
                }
                return;
            }
            knockTimer -= Time.deltaTime;
            if (knockTimer > 0f) {
                return;
            }
            knockTimer = Next(knockInterval);
            knockOnDoor = rng.Next(2) == 0;
            knockSprite = PickSprite();
            knockActive = true;
            knockLookUntil = Time.time + knockLookSeconds;
            ShowKnockFigure();
            if (shift.Game != null && shift.Game.Audio != null && knockFigure != null) {
                shift.Game.Audio.PlayAt(SoundIds.HalWindowKnock, knockFigure.transform.position);
            }
        }

        void EndKnock() {
            knockActive = false;
            knockScareAt = 0f;
            ClearKnockFigure();
        }

        // The picture stands just outside the glass, so looking over shows it through the pane.
        void ShowKnockFigure() {
            ClearKnockFigure();
            if (knockSprite == null || shift.Bus == null) {
                return;
            }
            knockFigure = new GameObject("SanityKnock");
            knockFigure.transform.SetParent(shift.Bus.transform, false);
            knockFigure.transform.localPosition = knockOnDoor
                ? new Vector3(1.55f, 0.7f, 4.75f)
                : new Vector3(-1.55f, 0.85f, 4.55f);
            knockFigure.transform.localRotation = Quaternion.LookRotation(knockOnDoor ? Vector3.right : Vector3.left, Vector3.up);
            PlaceSprite(knockFigure.transform, knockSprite, knockSpriteHeight);
        }

        void ClearKnockFigure() {
            if (knockFigure != null) {
                Destroy(knockFigure);
                knockFigure = null;
            }
        }

        bool RollingWithDoorsShut() {
            if (shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot) {
                return false;
            }
            if (shift.Cctv != null && shift.Cctv.IsViewingCCTV) {
                return false;
            }
            if (shift.Doors != null && !shift.Doors.IsClosed) {
                return false;
            }
            return shift.Death == null || !shift.Death.IsDying;
        }

        bool LookingAtKnock() {
            float yaw = shift.Attention != null ? shift.Attention.DriverYaw
                : (shift.DriverLook != null ? shift.DriverLook.Yaw : 0f);
            return knockOnDoor ? yaw >= knockLookYaw : yaw <= -knockLookYaw;
        }

        void TickObstacles() {
            PruneObstacles();
            MoveApproaching();
            obstacleTimer -= Time.deltaTime;
            if (obstacleTimer > 0f || obstacles.Count >= maxObstacles) {
                return;
            }
            obstacleTimer = Next(obstacleInterval);
            SpawnObstacle();
        }

        void SpawnObstacle() {
            if (shift.Bus == null || shift.Tracker == null || shift.Tracker.Path == null) {
                return;
            }
            Sprite sprite = PickSprite();
            if (sprite == null) {
                return;
            }
            bool approaches = rng.NextDouble() < walkerChance;
            float ahead = approaches ? Next(walkerAhead) : obstacleAheadMeters;
            float distance = shift.Tracker.DistanceAlong + ahead;
            RoutePath path = shift.Tracker.Path;
            if (distance > path.TotalLength - 4f || !HasGap(distance)) {
                return;
            }
            float lateral = LaneLateral(distance);

            GameObject root = new GameObject("SanityObstacle");
            SanityObstacle obstacle = root.AddComponent<SanityObstacle>();
            obstacle.Picture = sprite;
            obstacle.Approaches = approaches;
            obstacle.RouteDistance = distance;
            obstacle.Lateral = lateral;
            PlaceOnRoad(obstacle);
            float width = PlaceSprite(root.transform, sprite, spriteHeight);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            float height = Mathf.Max(0.2f, spriteHeight);
            box.size = new Vector3(Mathf.Max(0.2f, width), height, 1.5f);
            box.center = new Vector3(0f, height * 0.5f, 0f);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            obstacle.Init(shift, OnObstacleHit);
            obstacles.Add(obstacle);
        }

        // Bottom of the picture sits on the road. Scale ignores the PNG's pixel size.
        float PlaceSprite(Transform root, Sprite sprite, float height) {
            float units = sprite.bounds.size.y;
            float scale = units > 0.001f ? height / units : 1f;
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

        // In the lane the bus is actually driving, kept on the pavement.
        float LaneLateral(float distance) {
            float lateral = shift.Tracker.Lateral;
            RouteDefinition route = shift.Tracker.Route;
            if (route == null) {
                return lateral;
            }
            float half = Mathf.Max(0.4f, route.RoadWidthAt(distance) * 0.5f - 0.8f);
            return Mathf.Clamp(lateral, -half, half);
        }

        bool HasGap(float distance) {
            float gap = Mathf.Max(1f, obstacleSpacing);
            for (int i = 0; i < obstacles.Count; i++) {
                SanityObstacle other = obstacles[i];
                if (other != null && Mathf.Abs(other.RouteDistance - distance) < gap) {
                    return false;
                }
            }
            return true;
        }

        void MoveApproaching() {
            if (shift.Tracker == null || shift.Tracker.Path == null) {
                return;
            }
            float step = Mathf.Max(0f, walkerMetresPerSecond) * Time.deltaTime;
            float gap = Mathf.Max(1f, obstacleSpacing);
            for (int i = 0; i < obstacles.Count; i++) {
                SanityObstacle obstacle = obstacles[i];
                if (obstacle == null || !obstacle.Approaches) {
                    continue;
                }
                float next = obstacle.RouteDistance - step;
                for (int j = 0; j < obstacles.Count; j++) {
                    SanityObstacle other = obstacles[j];
                    if (other == null || other == obstacle) {
                        continue;
                    }
                    float limit = other.RouteDistance + gap;
                    if (other.RouteDistance < obstacle.RouteDistance && next < limit) {
                        next = limit;
                    }
                }
                if (next >= obstacle.RouteDistance) {
                    continue;
                }
                obstacle.RouteDistance = next;
                PlaceOnRoad(obstacle);
            }
        }

        void PlaceOnRoad(SanityObstacle obstacle) {
            RoutePose pose = shift.Tracker.Path.Evaluate(obstacle.RouteDistance);
            Vector3 pos = pose.Offset(obstacle.Lateral, 0f);
            pos.y = RoadHeight(pos) + spriteLift;
            obstacle.transform.position = pos;
            Rigidbody body = obstacle.GetComponent<Rigidbody>();
            if (body != null) {
                body.position = pos;
            }
        }

        // The asphalt under the lane point. Only the world layer counts: a picture's own collider,
        // or the bus, would walk the sprite up off the road. A wild hit falls back to the path.
        float RoadHeight(Vector3 pos) {
            float pathY = pos.y;
            Vector3 origin = new Vector3(pos.x, pathY + 8f, pos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, Layers.Mask(Layers.World), QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.point.y - pathY) < 2f) {
                return hit.point.y;
            }
            return pathY;
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

        // Schedules a band only when it turns on. Dropping from under 75 to under 50 leaves the
        // sounds, the CCTV flicker and the cabin lights on their existing timers.
        void ApplyBands(SanityTier tier) {
            ApplyFogForTier(tier);
            bool sounds = tier >= SanityTier.T1;
            bool pictures = tier >= SanityTier.T2;
            if (sounds && !under75) {
                soundTimer = Next(soundInterval);
                flickerTimer = Next(flickerInterval);
                cabinTimer = Next(cabinFlickerGap);
            }
            if (!sounds && under75) {
                soundTimer = 9999f;
                flickerTimer = 9999f;
                cabinTimer = 9999f;
                if (shift != null && shift.CabinLights != null) {
                    shift.CabinLights.Stop(lightOwner);
                }
            }
            under75 = sounds;
            if (pictures && !under50) {
                obstacleTimer = Next(obstacleInterval);
                knockTimer = Next(knockInterval);
            }
            if (!pictures && under50) {
                obstacleTimer = 9999f;
                knockTimer = 9999f;
                knockActive = false;
                ClearKnockFigure();
                ClearObstacles();
            }
            under50 = pictures;
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
