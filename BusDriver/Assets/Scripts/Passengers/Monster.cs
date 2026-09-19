// A monster is a passenger: it boards, sits and can be kicked off like anyone else.
// Abilities and quirks go in subclasses, built on the Passenger hooks (ChooseSeat,
// OnSeated, OnKickRequested, Tick, SetFocused, IsSeenBy...).
public class Monster : Passenger {
}
