namespace BusDriver.Gameplay.Passengers {
    // Asked before a rider is kicked out (§2.13): every IKickHandler component on the rider must
    // allow it. No greybox monster refuses; the hook exists for future content.
    public interface IKickHandler {
        bool AllowKick(Passenger passenger);
    }
}
