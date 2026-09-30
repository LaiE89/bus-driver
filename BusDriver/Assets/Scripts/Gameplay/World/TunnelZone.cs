using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The tunnel (§3.3). While the bus is inside: the GPS loses its signal (RouteMapView listens to
    // OnInsideChanged), the CCTV grain goes to 1.0, the tunnel lights waver in Unstable mode, the
    // mixer takes the Tunnel snapshot and amb.tunnel plays. Everything reverts on leaving. IsInside
    // feeds the sanity drain (M5).
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class TunnelZone : MonoBehaviour, IShiftBindable {
        const float TunnelGrain = 1f;

        [Tooltip("The tunnel's ceiling lamps (RouteBuilder); Subtle outside, Unstable while the bus is inside")]
        [SerializeField] LightFlicker[] lights = new LightFlicker[0];

        ZoneVolume volume;
        ShiftServices shift;
        SoundHandle ambience;

        public bool IsInside { get { return volume != null && volume.IsBusInside; } }
        // The tunnel's effects are applied (they follow IsInside once bound)
        public bool EffectsOn { get; private set; }
        public bool IsAmbiencePlaying { get { return shift != null && shift.Game.Audio.IsPlaying(ambience); } }
        public IReadOnlyList<LightFlicker> Lights { get { return lights; } }
        public event Action<bool> OnInsideChanged;

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }

        // ShiftContext, with the route scene's bindables (§4.5 step 15)
        public void Bind(ShiftServices services) {
            shift = services;
            volume.OnBusInsideChanged += HandleInside;
            SetLights(FlickerMode.Subtle);
            if (volume.IsBusInside) {
                HandleInside(true);
            }
        }

        void OnDestroy() {
            if (volume != null) {
                volume.OnBusInsideChanged -= HandleInside;
            }
            // The mixer outlives the night
            if (EffectsOn && shift != null) {
                shift.Game.Audio.SetSnapshot(AudioSnapshot.Default, 0f);
            }
        }

        void HandleInside(bool inside) {
            if (shift == null || inside == EffectsOn) {
                return;
            }
            EffectsOn = inside;
            shift.Cctv.SetGrainIntensity(inside ? TunnelGrain : shift.Cctv.DefaultGrain);
            SetLights(inside ? FlickerMode.Unstable : FlickerMode.Subtle);
            IAudioService audio = shift.Game.Audio;
            audio.SetSnapshot(inside ? AudioSnapshot.Tunnel : AudioSnapshot.Default, AudioService.DefaultSnapshotFade);
            if (inside) {
                ambience = audio.Play(SoundIds.AmbTunnel);
            }else {
                audio.Stop(ambience);
                ambience = SoundHandle.None;
            }
            if (OnInsideChanged != null) {
                OnInsideChanged(inside);
            }
        }

        void SetLights(FlickerMode mode) {
            for (int i = 0; i < lights.Length; i++) {
                if (lights[i] != null) {
                    lights[i].Mode = mode;
                }
            }
        }
    }
}
