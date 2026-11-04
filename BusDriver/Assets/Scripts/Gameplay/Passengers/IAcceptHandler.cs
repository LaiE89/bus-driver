namespace BusDriver.Gameplay.Passengers {
    // Asked when the driver waves a rider aboard. True means this accept kills the driver and
    // the rider stays on the step instead of walking to a seat.
    public interface IAcceptHandler {
        bool OnAccept(Passenger passenger);
    }
}
