using UnityEngine;

// A seat anchor on top of a bench cushion. Reserved from the moment a passenger
// starts boarding so two of them never head for the same seat.
public class BusSeat : MonoBehaviour {
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
}
