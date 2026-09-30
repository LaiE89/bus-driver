using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sits on the road centreline with local +x pointing at the kerb. Passengers board
// when the bus door (not just its nose) is lined up with the stop and the doors open.
public class BusStop : MonoBehaviour {
    [SerializeField] List<Passenger> waiting = new List<Passenger>();

    [Header("Route")]
    [Tooltip("Order around the loop. -1 falls back to the order under the parent object.")]
    [SerializeField] int routeIndex = -1;
    [Tooltip("What passengers call this stop. Blank builds one from the route order.")]
    [SerializeField] string stopName = "";

    [Header("Spawning")]
    [Tooltip("Kept for inspector/scene data; waves use a random 0-3 count instead")]
    [SerializeField] int spawnCount = 2;
    [SerializeField] Vector3 firstWaitLocal = new Vector3(5.2f, 0f, -1.2f);
    [SerializeField] Vector3 waitLocalStep = new Vector3(0.3f, 0f, 1.2f);
    [Tooltip("How often an empty stop rolls a new random wave")]
    [SerializeField] float respawnInterval = 60f;
    [Tooltip("Inclusive max NPCs rolled when the stop is empty (0 through this value)")]
    [SerializeField] int maxWaveSize = 3;

    [Header("Zone the bus door has to be in, stop-local")]
    [SerializeField] float zoneMinX = 1.8f;
    [SerializeField] float zoneMaxX = 4.4f;
    [SerializeField] float zoneHalfLength = 6f;
    [SerializeField] float boardInterval = 1.2f;

    public int WaitingCount {
        get {
            PruneWaiting();
            return waiting.Count;
        }
    }
    public int SpawnCount { get { return Mathf.Max(0, spawnCount); } }
    public int RouteOrder { get { return routeIndex >= 0 ? routeIndex : transform.GetSiblingIndex(); } }
    public string StopName {
        get { return string.IsNullOrEmpty(stopName) ? "STOP " + (RouteOrder + 1) : stopName; }
    }

    public Vector3 WaitLocalPosition(int slot) {
        return firstWaitLocal + waitLocalStep * slot;
    }

    Coroutine boarding;
    Coroutine respawning;

    void Start() {
        // Opening wave, then the same check every minute
        TrySpawnWave();
        if (respawnInterval > 0f) {
            respawning = StartCoroutine(RespawnRoutine());
        }
    }

    void OnDisable() {
        if (respawning != null) {
            StopCoroutine(respawning);
            respawning = null;
        }
    }

    // A box test on the door position rather than a trigger: the hull would fire a
    // trigger as soon as the nose arrives, long before the door is at the stop
    public bool Contains(BusCabin cabin) {
        Vector3 local = transform.InverseTransformPoint(cabin.DoorStepWorld);
        if (local.x < zoneMinX || local.x > zoneMaxX || Mathf.Abs(local.z) > zoneHalfLength) {
            return false;
        }
        return Vector3.Dot(cabin.transform.forward, transform.forward) >= 0.8f;
    }

    public void AddWaiting(Passenger passenger) {
        if (!waiting.Contains(passenger)) {
            waiting.Add(passenger);
        }
    }

    public void BeginBoarding(BusCabin cabin) {
        if (boarding == null) {
            boarding = StartCoroutine(BoardingRoutine(cabin));
        }
    }

    // Only when nobody is waiting: roll 0..maxWaveSize and pull that many from the pool
    public int TrySpawnWave() {
        PruneWaiting();
        if (waiting.Count > 0 || SceneController.Instance == null) {
            return 0;
        }
        // Never have anyone appear out of thin air at the stop the bus is sitting at
        BusCabin cabin = SceneController.Instance.Cabin;
        if (boarding != null || (cabin != null && Contains(cabin))) {
            return 0;
        }
        int count = Random.Range(0, Mathf.Max(0, maxWaveSize) + 1);
        int spawned = 0;
        for (int slot = 0; slot < count; slot++) {
            string poolId = SceneController.Instance.PickSpawnPoolId();
            if (string.IsNullOrEmpty(poolId)) {
                break;
            }
            if (SceneController.Instance.SpawnNpcAtStop(this, poolId, slot) != null) {
                spawned++;
            }
        }
        return spawned;
    }

    IEnumerator RespawnRoutine() {
        WaitForSeconds tick = new WaitForSeconds(respawnInterval);
        while (true) {
            yield return tick;
            TrySpawnWave();
        }
    }

    IEnumerator BoardingRoutine(BusCabin cabin) {
        List<Passenger> sent = new List<Passenger>();
        while (cabin.Doors.IsOpenWanted && Contains(cabin)) {
            // Everyone getting off here clears the doorway before the queue moves up
            if (cabin.AnyLeaving) {
                yield return null;
            }else if (waiting.Count > 0 && cabin.FreeSeatCount > 0) {
                Passenger passenger = waiting[0];
                waiting.RemoveAt(0);
                if (passenger != null && passenger.Board(cabin, this)) {
                    sent.Add(passenger);
                }
                yield return new WaitForSeconds(boardInterval);
            }else {
                yield return null;
            }
        }
        // Doors are closing, anyone who hasn't made it aboard goes back to waiting
        foreach (Passenger passenger in sent) {
            if (passenger != null) {
                passenger.AbortBoarding();
            }
        }
        boarding = null;
    }

    void PruneWaiting() {
        for (int i = waiting.Count - 1; i >= 0; i--) {
            if (waiting[i] == null) {
                waiting.RemoveAt(i);
            }
        }
    }

    void OnDrawGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
        Vector3 center = new Vector3((zoneMinX + zoneMaxX) * 0.5f, 0.5f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(zoneMaxX - zoneMinX, 1f, zoneHalfLength * 2f));
    }
}
