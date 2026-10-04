using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Passengers;

namespace BusDriver.Gameplay.World {
    // Sits on the road centreline with local +x pointing at the kerb. Passengers board
    // when the bus door (not just its nose) is lined up with the stop and the doors open.
    public class BusStop : MonoBehaviour {
        [Tooltip("The RouteDefinition stop id (§3.2); set when the route scene is built")]
        [SerializeField] string stopId = "";
        [SerializeField] List<Passenger> waiting = new List<Passenger>();
        [Header("Where riders wait, stop-local")]
        [SerializeField] Vector3 firstWaitLocal = new Vector3(5.2f, 0f, -1.2f);
        [SerializeField] Vector3 waitLocalStep = new Vector3(0.3f, 0f, 1.2f);

        [Header("Zone the bus door has to be in, stop-local")]
        [SerializeField] float zoneMinX = 1.8f;
        [SerializeField] float zoneMaxX = 4.4f;
        [SerializeField] float zoneHalfLength = 6f;
        [SerializeField] float boardInterval = 1.2f;

        public string StopId { get { return stopId; } }
        public int WaitingCount { get { return waiting.Count; } }

        public Vector3 WaitLocalPosition(int slot) {
            return firstWaitLocal + waitLocalStep * slot;
        }

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

        // Everyone still waiting here, taken off the queue (a missed stop, §2.4)
        public void TakeAllWaiting(List<Passenger> into) {
            for (int i = 0; i < waiting.Count; i++) {
                if (waiting[i] != null) {
                    into.Add(waiting[i]);
                }
            }
            waiting.Clear();
        }

        public void BeginBoarding(BusCabin cabin) {
            if (boarding == null) {
                boarding = StartCoroutine(BoardingRoutine(cabin));
            }
        }

        // §2.4: riders whose destination is this stop all stand at once and the cabin's exit queue
        // walks them off in single file, nearest the door first; the waiting riders then board one
        // at a time behind them. A leaving rider holds the doors, so they can't close on them.
        IEnumerator BoardingRoutine(BusCabin cabin) {
            List<Passenger> alighting = cabin.AlightingAt(this);
            for (int i = 0; i < alighting.Count; i++) {
                if (alighting[i] != null) {
                    alighting[i].Leave();
                }
            }
            yield return null;
            List<Passenger> sent = new List<Passenger>();
            while (cabin.Doors.IsOpenWanted && Contains(cabin)) {
                if (waiting.Count > 0 && cabin.FreeSeatCount > 0) {
                    Passenger passenger = waiting[0];
                    waiting.RemoveAt(0);
                    if (passenger != null && passenger.Board(cabin, this)) {
                        sent.Add(passenger);
                    }else if (passenger != null) {
                        // The last free seat went to somebody else; keep their place in the line
                        waiting.Insert(0, passenger);
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
}
