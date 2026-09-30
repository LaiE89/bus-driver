using BusDriver.Core.Data;

namespace BusDriver.Gameplay.Death {
    // One death (§2.14): what killed the player, where and when. RunFlow records it in the meta
    // save; the Game Over screen shows it.
    public sealed class DeathReport {
        public DeathCause Cause;
        // The monster id for MonsterKill; empty otherwise
        public string SourceId = "";
        public int NightIndex;
        // The shift clock and the distance along the route when it happened
        public double GameSeconds;
        public float DistanceAlong;

        public override string ToString() {
            return string.IsNullOrEmpty(SourceId) ? Cause.ToString() : $"{Cause} ({SourceId})";
        }
    }

    // Something that can stop a death from landing (§2.14): the Salt charm (T-M7-04) and the god
    // mode cheat (T-M4-10). Return true to prevent it; a one-use preventer consumes itself here.
    public interface IDeathPreventer {
        bool TryPrevent(DeathReport report);
    }
}
