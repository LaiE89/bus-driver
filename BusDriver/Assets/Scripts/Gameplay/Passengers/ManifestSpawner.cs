using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Passengers {
    // Spawns the night's manifest (§2.6, §4.6): every rider standing at their boarding stop in the
    // Waiting state from the night's start, bound to the night, dressed in their look and
    // registered. Monster riders are Passengers flagged by their spec's monsterId until the
    // monster prefabs arrive (T-M4-03).
    public sealed class ManifestSpawner : MonoBehaviour {
        [Tooltip("Generated/Prefabs/Passenger.prefab")]
        [SerializeField] Passenger riderPrefab;

        ShiftServices shift;

        public Manifest Manifest { get; private set; }

        // ShiftContext, step 14 of the Init order (§4.5)
        public void Init(ShiftServices services) {
            shift = services;
            NightDefinition night = services.Night;
            NightDefinition first = services.Game.Config.Night(1);
            Manifest = Manifest.FromScripted(night, first, services.Setup.NoMonsters);
            SpawnAll(Manifest);
        }

        public void SpawnAll(Manifest manifest) {
            for (int i = 0; i < manifest.Riders.Count; i++) {
                RiderSpec spec = manifest.Riders[i];
                BusStop stop = shift.Route.Stop(spec.boardStopId);
                if (stop == null) {
                    Log.Error(LogCat.Content, $"manifest rider {spec} boards at '{spec.boardStopId}', which the route doesn't have");
                    continue;
                }
                SpawnWaiting(spec, stop, null);
            }
            Log.Info(LogCat.Flow, $"night {manifest.NightIndex}: spawned {manifest.Riders.Count} riders ({manifest.MonsterCount} monsters)");
        }

        // A rider waiting in the stop's next free spot, facing the road. prefab null = the rider prefab.
        public Passenger SpawnWaiting(RiderSpec spec, BusStop stop, Passenger prefab) {
            Transform at = stop.transform;
            Vector3 position = at.TransformPoint(stop.WaitLocalPosition(stop.WaitingCount));
            Quaternion rotation = at.rotation * Quaternion.Euler(0f, -90f, 0f);
            Passenger passenger = Create(spec, prefab, position, rotation, at);
            stop.AddWaiting(passenger);
            return passenger;
        }

        // Instantiated, bound, dressed and registered, but not yet placed anywhere in the night
        public Passenger Create(RiderSpec spec, Passenger prefab, Vector3 position, Quaternion rotation, Transform parent) {
            Passenger source = prefab != null ? prefab : riderPrefab;
            Passenger passenger = Instantiate(source, position, rotation, parent);
            passenger.name = source.name + " " + spec.lookId;
            passenger.Bind(shift);
            PassengerLookDefinition look = shift.Views.Look(spec.lookId);
            if (look == null) {
                Log.Warn(LogCat.Content, $"rider look '{spec.lookId}' isn't in GameRootConfig.looks; using the greybox defaults");
            }
            shift.Views.Recreate(passenger, look);
            if (spec.decoy != DecoyKind.None) {
                passenger.gameObject.AddComponent<DecoyDriver>().Init(spec.decoy, shift.Rng.Get(RngStreams.Decoy), shift.Game.Audio);
            }
            shift.Riders.Register(spec, passenger);
            return passenger;
        }
    }
}
