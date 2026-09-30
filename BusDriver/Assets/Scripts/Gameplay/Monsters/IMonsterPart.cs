using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Monsters {
    // A component that makes up a monster beside its MonsterBrain (§4.1 rule 4): its KillSequence,
    // its ability. The brain binds each one at the end of its own Bind, so every part can already
    // read the brain's definition, meter and passenger.
    public interface IMonsterPart {
        void Bind(ShiftServices shift, MonsterBrain brain);
    }
}
