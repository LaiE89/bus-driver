using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // Creates passenger views from looks (§4.6, §4.14): the look's art view when it has one and
    // UseArt is on, otherwise the generated greybox view coloured from the look. Every view gets a
    // RendererFlicker. Changing a rider's look (the Mimic) recreates its view.
    public sealed class ViewFactory : MonoBehaviour {
        [Tooltip("Generated/Prefabs/Views/View_GreyboxPassenger (PrefabBuilder)")]
        [SerializeField] GreyboxPassengerView greyboxView;

        ShiftServices shift;
        System.Random flickerSeeds;

        // F2 flips it in development builds (Phase B, T-M10); art views are used whenever assigned
        public bool UseArt { get; set; } = true;
        public GreyboxPassengerView GreyboxPrefab { get { return greyboxView; } }

        // ShiftContext, step 5 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            flickerSeeds = services.Rng.Get(RngStreams.Mimic);
        }

        public PassengerLookDefinition Look(string lookId) {
            GameRootConfig config = shift != null && shift.Game != null ? shift.Game.Config : null;
            return config != null ? config.Look(lookId) : null;
        }

        public PassengerViewBase Create(PassengerLookDefinition look, Transform parent) {
            PassengerViewBase prefab = null;
            if (UseArt && look != null && look.artView != null) {
                prefab = look.artView.GetComponent<PassengerViewBase>();
                if (prefab == null) {
                    Log.Warn(LogCat.Content, $"look '{look.id}' has an art view without a PassengerViewBase; using the greybox");
                }
            }
            PassengerViewBase view;
            if (prefab != null) {
                view = Instantiate(prefab, parent, false);
            }else {
                GreyboxPassengerView greybox = Instantiate(greyboxView, parent, false);
                greybox.Configure(look);
                view = greybox;
            }
            view.name = "View";
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            AttachFlicker(view, flickerSeeds != null ? flickerSeeds.Next() : 0);
            return view;
        }

        // Replaces the rider's view with one for this look (first dressing, or the Mimic's copy)
        public PassengerViewBase Recreate(Passenger passenger, PassengerLookDefinition look) {
            PassengerViewBase view = Create(look, passenger.transform);
            passenger.AttachView(view, look != null ? look.id : "");
            return view;
        }

        // Also used by builders for view-only figures (the menu diorama)
        public static RendererFlicker AttachFlicker(PassengerViewBase view, int seed) {
            RendererFlicker flicker = view.GetComponent<RendererFlicker>();
            if (flicker == null) {
                flicker = view.gameObject.AddComponent<RendererFlicker>();
            }
            flicker.Init(seed);
            return flicker;
        }
    }
}
