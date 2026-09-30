using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.Passengers {
    // A decoy's one odd but harmless habit (§2.6), driven through its view's tell so art gets it for
    // free. NodOff drops the head for 3–6 s every 10–20 s while seated; PhoneGlow and HoodUp stay on;
    // Mutter jitters the mouth and plays a faint 3D mutter loop; FacingBackwards sits turned round.
    // The timings come from the run's `decoy` stream. Decoys are innocents for every rule.
    public sealed class DecoyDriver : MonoBehaviour {
        const float NodEveryMin = 10f;
        const float NodEveryMax = 20f;
        const float NodForMin = 3f;
        const float NodForMax = 6f;
        // Head drop and lift, per second
        const float NodSpeed = 2.5f;

        Passenger passenger;
        System.Random random;
        IAudioService audio;
        bool mutterPlaying;
        float nod;
        float nodTimer;
        bool nodding;

        public DecoyKind Kind { get; private set; }

        public void Init(DecoyKind kind, System.Random stream, IAudioService audioService) {
            Kind = kind;
            random = stream;
            audio = audioService;
            passenger = GetComponent<Passenger>();
            nodTimer = Range(NodEveryMin, NodEveryMax);
        }

        void Update() {
            if (passenger == null || Kind == DecoyKind.None) {
                return;
            }
            PassengerViewBase view = passenger.View;
            if (view == null || passenger.State == PassengerState.Gone) {
                return;
            }
            bool seated = passenger.State == PassengerState.Seated;
            switch (Kind) {
                case DecoyKind.NodOff:
                    UpdateNod(seated, Time.deltaTime);
                    view.SetTell(TellId.DecoyNodOff, nod);
                    break;
                case DecoyKind.PhoneGlow:
                    view.SetTell(TellId.DecoyPhoneGlow, 1f);
                    break;
                case DecoyKind.Mutter:
                    view.SetTell(TellId.DecoyMutter, 1f);
                    if (!mutterPlaying && audio != null) {
                        // Follows the rider and goes back to the pool when they're gone (§4.12)
                        audio.PlayAttached(SoundIds.PaxMutterLoop, passenger.Head != null ? passenger.Head : transform);
                        mutterPlaying = true;
                    }
                    break;
                case DecoyKind.FacingBackwards:
                    view.SetTell(TellId.DecoyFacingBackwards, seated ? 1f : 0f);
                    break;
                case DecoyKind.HoodUp:
                    view.SetTell(TellId.DecoyHoodUp, 1f);
                    break;
            }
        }

        // Only a seated rider dozes; standing up wakes them
        void UpdateNod(bool seated, float deltaTime) {
            if (!seated) {
                nodding = false;
            }else {
                nodTimer -= deltaTime;
                if (nodTimer <= 0f) {
                    nodding = !nodding;
                    nodTimer = nodding ? Range(NodForMin, NodForMax) : Range(NodEveryMin, NodEveryMax);
                }
            }
            nod = Mathf.MoveTowards(nod, nodding ? 1f : 0f, NodSpeed * deltaTime);
        }

        float Range(float min, float max) {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
