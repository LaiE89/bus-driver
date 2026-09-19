using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sits on the road centreline with local +x pointing at the kerb. Passengers board
// when the bus door (not just its nose) is lined up with the stop and the doors open.
public class BusStop : MonoBehaviour {
    [SerializeField] List<Passenger> waiting = new List<Passenger>();
    [Header("Zone the bus door has to be in, stop-local")]
    [SerializeField] float zoneMinX = 1.8f;
    [SerializeField] float zoneMaxX = 4.4f;
    [SerializeField] float zoneHalfLength = 6f;
    [SerializeField] float boardInterval = 1.2f;

    public int WaitingCount { get { return waiting.Count; } }

    Coroutine boarding;

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

    IEnumerator BoardingRoutine(BusCabin cabin) {
        List<Passenger> sent = new List<Passenger>();
        while (cabin.Doors.IsOpenWanted && Contains(cabin)) {
            if (waiting.Count > 0 && cabin.FreeSeatCount > 0) {
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

    void OnDrawGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
        Vector3 center = new Vector3((zoneMinX + zoneMaxX) * 0.5f, 0.5f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(zoneMaxX - zoneMinX, 1f, zoneHalfLength * 2f));
    }
}
