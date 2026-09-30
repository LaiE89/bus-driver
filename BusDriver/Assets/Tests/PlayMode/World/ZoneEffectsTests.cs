using System.Collections;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Dash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.World {
    // The tunnel and rumble strip zones (T-M2-14, §3.3) on the real route
    public class ZoneEffectsTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static void PlaceOnRoute(ShiftContext night, float distance) {
            RoutePose pose = night.Shift.Tracker.Path.Evaluate(distance);
            night.Bus.PlaceAt(pose.Offset(night.Route.Route.roadWidth * 0.25f, 0.3f), Quaternion.LookRotation(pose.Tangent, Vector3.up));
        }

        static TunnelZone Tunnel(ShiftContext night) {
            for (int i = 0; i < night.Route.Zones.Count; i++) {
                TunnelZone tunnel = night.Route.Zones[i].GetComponent<TunnelZone>();
                if (tunnel != null) {
                    return tunnel;
                }
            }
            return null;
        }

        static void AssertLights(TunnelZone tunnel, FlickerMode mode) {
            Assert.Greater(tunnel.Lights.Count, 0, "the tunnel has no lights to flicker");
            for (int i = 0; i < tunnel.Lights.Count; i++) {
                Assert.AreEqual(mode, tunnel.Lights[i].Mode, "tunnel light " + i);
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Tunnel_TogglesEffects() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            night.Bus.GetComponent<BusInput>().ExternalControl = true;
            TunnelZone tunnel = Tunnel(night);
            Assert.IsNotNull(tunnel, "Route01_World has no TunnelZone");
            RouteMapView gps = Object.FindAnyObjectByType<RouteMapView>();
            CCTVSystem cctv = night.Shift.Cctv;

            Assert.IsFalse(tunnel.EffectsOn);
            Assert.IsTrue(gps.HasSignal);
            Assert.AreEqual(cctv.DefaultGrain, cctv.GrainIntensity, 1e-4f);
            AssertLights(tunnel, FlickerMode.Subtle);

            PlaceOnRoute(night, 2120f);
            yield return FlowTestUtil.WaitFor(() => tunnel.IsInside, 5f, "the bus inside the tunnel");
            yield return null;
            Assert.IsTrue(tunnel.EffectsOn, "the tunnel's effects are on inside it");
            Assert.IsFalse(gps.HasSignal, "the GPS shows NO SIGNAL in the tunnel");
            Assert.AreEqual(1f, cctv.GrainIntensity, 1e-4f, "CCTV grain 1.0 in the tunnel");
            AssertLights(tunnel, FlickerMode.Unstable);
            Assert.IsTrue(tunnel.IsAmbiencePlaying, "amb.tunnel plays in the tunnel");
            Assert.IsTrue(night.Shift.Tracker.InZone(Core.Data.RouteZoneKind.Tunnel));

            PlaceOnRoute(night, 2300f);
            yield return FlowTestUtil.WaitFor(() => !tunnel.IsInside, 5f, "the bus out of the tunnel");
            yield return null;
            Assert.IsFalse(tunnel.EffectsOn, "the tunnel's effects are off after leaving");
            Assert.IsTrue(gps.HasSignal, "the GPS has its signal back");
            Assert.AreEqual(cctv.DefaultGrain, cctv.GrainIntensity, 1e-4f);
            AssertLights(tunnel, FlickerMode.Subtle);
            Assert.IsFalse(tunnel.IsAmbiencePlaying, "amb.tunnel stops after leaving");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Rumble_WheelsOverTheStripAboveTenKmh() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            RumbleZone across = null;
            for (int i = 0; i < night.Route.Zones.Count; i++) {
                ZoneVolume zone = night.Route.Zones[i];
                RumbleZone rumble = zone.GetComponent<RumbleZone>();
                if (rumble != null && zone.Start < 1440f && zone.End <= 1450.5f) {
                    across = rumble;
                }
            }
            Assert.IsNotNull(across, "no rumble strip across the road at 1435–1450");
            CameraShake shake = night.Shift.Shake;
            Assert.IsNotNull(shake, "the driver camera has no CameraShake");

            PlaceOnRoute(night, 1385f);
            yield return new WaitForFixedUpdate();
            AutoPilot pilot = night.Shift.AutoPilot;
            pilot.Engage(true);
            bool rumbled = false;
            bool shook = false;
            float slowestWhileRumbling = float.MaxValue;
            yield return FlowTestUtil.WaitFor(() => {
                if (across.IsRumbling) {
                    rumbled = true;
                    shook |= shake.Amplitude > 0f;
                    slowestWhileRumbling = Mathf.Min(slowestWhileRumbling, night.Bus.SpeedKmh);
                }
                return night.Shift.Tracker.DistanceAlong > 1470f;
            }, 60f, "the bus past the rumble strip");
            pilot.Engage(false);
            Assert.IsTrue(rumbled, "the strip never rumbled with the wheels over it");
            Assert.IsTrue(shook, "the driver camera didn't shake on the strip");
            Assert.Greater(slowestWhileRumbling, RumbleZone.MinSpeedKmh);
            Assert.IsFalse(across.IsRumbling, "it stops once the wheels are past");
            Assert.AreEqual(0f, shake.Amplitude, "the shake ends with the rumble");
        }
    }
}
