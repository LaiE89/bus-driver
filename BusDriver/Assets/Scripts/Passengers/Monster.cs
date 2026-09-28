// A monster is a passenger: it boards, sits and can be kicked off like anyone else.
// Abilities and quirks go in subclasses, built on the Passenger hooks (ChooseSeat,
// OnSeated, OnKickRequested, Tick, SetFocused, IsSeenBy...).
public class Monster : Passenger {
    // Unlike normal passengers, a monster never picks its own stop to get off at:
    // it stays aboard until the driver kicks it out.
    protected override BusStop ChooseDestination(BusCabin cabin, BusStop boardingStop) { return null; }
}
