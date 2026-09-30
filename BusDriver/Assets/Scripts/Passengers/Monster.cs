// A monster is a passenger: it boards, sits and can be kicked off like anyone else.
// Abilities and quirks go in subclasses, built on the Passenger hooks (ChooseSeat,
// OnSeated, OnKickRequested, Tick, SetFocused, IsSeenBy...).
public class Monster : Passenger {
    // Whatever this thing is, it does not fill in a customer survey
    public override bool RatesRide { get { return false; } }
    // It never asks for a stop, so it is never dropped off
    public override bool RidesToDestination { get { return false; } }
}
