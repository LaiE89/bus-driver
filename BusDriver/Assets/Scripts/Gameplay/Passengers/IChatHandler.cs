namespace BusDriver.Gameplay.Passengers {
    // Asked when the driver talks to a seated rider. True means this talk is consumed:
    // no line is spoken.
    public interface IChatHandler {
        bool OnChat(Passenger passenger);
    }
}
