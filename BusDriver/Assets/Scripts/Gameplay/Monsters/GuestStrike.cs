using BusDriver.Core.Data;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Letting this rider through the door kills the driver on the spot. Turning it away does not.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class GuestStrike : MonoBehaviour, IMonsterPart, IAcceptHandler {
        ShiftServices shift;

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
        }

        public bool OnAccept(Passenger passenger) {
            return shift != null && shift.Death != null && shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Guest);
        }
    }
}
