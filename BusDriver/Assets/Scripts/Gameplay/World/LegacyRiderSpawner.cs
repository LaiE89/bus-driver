using System;
using System.Collections.Generic;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    [Serializable]
    public class NpcSpawnEntry {
        [Tooltip("Must match an ObjectPooling pool id")]
        public string poolId = "Passenger";
        [Min(0f)] public float weight = 1f;
    }

    // The MVP's (PR #5) waiting riders: pooled NPCs dealt to the route's stops by weight. It lives
    // on the route root until the night manifest replaces it (T-M2-07); it was part of
    // SceneController before T-M1-15. Picks come from the run's manifest stream (§4.1 rule 8).
    public sealed class LegacyRiderSpawner : MonoBehaviour {
        [SerializeField] ObjectPooling npcPool;
        [Tooltip("Weighted list of NPCs that can appear at bus stops")]
        [SerializeField] NpcSpawnEntry[] spawnableNpcs = new NpcSpawnEntry[0];
        [SerializeField] bool populateStopsOnInit = true;
        [Tooltip("If set, the first waiter at the busiest stop uses this pool id (useful for testing monsters)")]
        [SerializeField] string guaranteedFirstNpcId = "WeepingAngel";

        ShiftServices shift;
        System.Random rng;

        public ObjectPooling NpcPool { get { return npcPool; } }

        // ShiftContext, where ManifestSpawner will be in the Init order (§4.5 step 14)
        public void Init(ShiftServices services) {
            shift = services;
            rng = services.Rng.Get(RngStreams.Manifest);
            if (populateStopsOnInit) {
                PopulateBusStops();
            }
        }

        public void PopulateBusStops() {
            if (npcPool == null || spawnableNpcs == null || spawnableNpcs.Length == 0 || shift == null) {
                return;
            }
            List<BusStop> stops = new List<BusStop>(shift.Route.Stops);
            stops.Sort((a, b) => b.SpawnCount.CompareTo(a.SpawnCount));
            bool placedGuaranteed = string.IsNullOrEmpty(guaranteedFirstNpcId);
            for (int i = 0; i < stops.Count; i++) {
                BusStop stop = stops[i];
                if (stop == null || stop.WaitingCount > 0) {
                    continue;
                }
                int count = stop.SpawnCount;
                for (int slot = 0; slot < count; slot++) {
                    string poolId;
                    if (!placedGuaranteed && slot == 0 && npcPool.HasPool(guaranteedFirstNpcId)) {
                        poolId = guaranteedFirstNpcId;
                        placedGuaranteed = true;
                    }else {
                        poolId = PickSpawnPoolId();
                    }
                    if (string.IsNullOrEmpty(poolId)) {
                        continue;
                    }
                    SpawnNpcAtStop(stop, poolId, slot);
                }
            }
        }

        public Passenger SpawnNpcAtStop(BusStop stop, string poolId, int slot) {
            if (stop == null || npcPool == null) {
                return null;
            }
            Vector3 local = stop.WaitLocalPosition(slot);
            Vector3 worldPos = stop.transform.TransformPoint(local);
            Quaternion worldRot = stop.transform.rotation * Quaternion.Euler(0f, -90f, 0f);
            GameObject go = npcPool.Spawn(poolId, worldPos, worldRot, stop.transform);
            if (go == null) {
                return null;
            }
            go.name = poolId;
            Passenger passenger = go.GetComponent<Passenger>();
            if (passenger == null) {
                npcPool.Despawn(go);
                Log.Warn(LogCat.Flow, $"LegacyRiderSpawner: pooled '{poolId}' has no Passenger component");
                return null;
            }
            passenger.Bind(shift);
            passenger.PrepareForWaiting(stop, worldPos, worldRot);
            stop.AddWaiting(passenger);
            return passenger;
        }

        // Kicked riders never come back (§2.13, D50), so they are not returned to the pool
        public void Despawn(Passenger passenger) {
            if (passenger == null) {
                return;
            }
            if (!passenger.WasKicked && npcPool != null && npcPool.Despawn(passenger.gameObject)) {
                return;
            }
            Destroy(passenger.gameObject);
        }

        public string PickSpawnPoolId() {
            if (spawnableNpcs == null || spawnableNpcs.Length == 0) {
                return null;
            }
            float total = 0f;
            for (int i = 0; i < spawnableNpcs.Length; i++) {
                if (Usable(spawnableNpcs[i])) {
                    total += spawnableNpcs[i].weight;
                }
            }
            if (total <= 0f) {
                return null;
            }
            float roll = (float)(rng.NextDouble() * total);
            for (int i = 0; i < spawnableNpcs.Length; i++) {
                NpcSpawnEntry entry = spawnableNpcs[i];
                if (!Usable(entry)) {
                    continue;
                }
                if (roll < entry.weight) {
                    return entry.poolId;
                }
                roll -= entry.weight;
            }
            return spawnableNpcs[0].poolId;
        }

        bool Usable(NpcSpawnEntry entry) {
            return entry != null && entry.weight > 0f && npcPool != null && npcPool.HasPool(entry.poolId);
        }
    }
}
