using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Passengers;

namespace BusDriver.Gameplay.Bus {
    // A seat anchor on top of a bench cushion. Reserved from the moment a passenger
    // starts boarding so two of them never head for the same seat.
    public class BusSeat : MonoBehaviour {
        [Tooltip("R1 is the front row, nearest the driver (§2.6)")]
        [SerializeField] int row = 1;
        [Tooltip("0..3 = L2, L1, R1, R2")]
        [SerializeField] int column;

        public int Row { get { return row; } }
        public int Column { get { return column; } }
        public SeatZone Zone { get { return ZoneOfRow(row); } }
        public Passenger Occupant { get; private set; }
        public bool IsFree { get { return Occupant == null; } }

        public bool Reserve(Passenger passenger) {
            if (!IsFree && Occupant != passenger) {
                return false;
            }
            Occupant = passenger;
            return true;
        }

        public void Release(Passenger passenger) {
            if (Occupant == passenger) {
                Occupant = null;
            }
        }

        // Front = R1–R3, Mid = R4–R6, Rear = R7–R9 (§2.6)
        public static SeatZone ZoneOfRow(int row) {
            if (row <= 3) {
                return SeatZone.Front;
            }
            return row <= 6 ? SeatZone.Mid : SeatZone.Rear;
        }
    }
}
