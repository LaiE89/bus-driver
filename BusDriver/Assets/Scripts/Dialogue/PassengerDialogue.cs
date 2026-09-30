using UnityEngine;

// Random one liners for the door and the drop off, picked per NPC kind so a monster
// never sounds like a commuter. Feeds the existing DialogueController.
public static class PassengerDialogue {
    const string HumanKind = "Passenger";

    static readonly string[] humanGreetings = {
        "Evening. {0} for me, when you get a chance.",
        "Cold out there. I'm going as far as {0}.",
        "You're the first bus in forty minutes. {0}, please.",
        "Quiet night, huh? I get off at {0}.",
        "Just {0}. I won't be any trouble.",
        "Long shift? Same. {0}, thanks."
    };

    static readonly string[] humanGreetingsNoStop = {
        "Evening, driver.",
        "Cold night for this, isn't it?",
        "Don't mind me, I'll just sit at the back."
    };

    static readonly string[] humanFiveStar = {
        "Right on time. Five stars, driver.",
        "Smoothest ride I've had all month. Five stars.",
        "Perfect. You're the only bus I trust."
    };

    static readonly string[] humanFourStar = {
        "Not bad at all. Four stars.",
        "Bit of a wait, but we got here. Four stars.",
        "Good driving. Four stars."
    };

    static readonly string[] humanThreeStar = {
        "We got there, eventually. Three stars.",
        "Could've been quicker. Three stars.",
        "It'll do. Three stars."
    };

    static readonly string[] humanTwoStar = {
        "That took forever. Two stars.",
        "I could have walked. Two stars.",
        "My shift started an hour ago. Two stars."
    };

    static readonly string[] humanOneStar = {
        "Never again. One star.",
        "I'm filing a complaint. One star.",
        "You drive like the road owes you money. One star."
    };

    static readonly string[] humanMissed = {
        "You drove straight past my stop! Zero stars.",
        "That was my stop. That was MY stop. Zero.",
        "Unbelievable. I'm miles from home now. Zero stars."
    };

    static readonly string[] humanKicked = {
        "You're throwing me off? Zero stars.",
        "I paid my fare! Zero.",
        "Out here? In the dark? Zero stars, driver."
    };

    static readonly string[] humanRefused = {
        "Fine. I'll wait for a driver with a heart.",
        "Seriously? I've been standing here for an hour.",
        "Your loss. Something worse than me is out tonight."
    };

    static readonly string[] angelGreetings = {
        "...",
        "Do not look away, driver.",
        "I have waited at this stop a very long time.",
        "Such kind eyes. Keep them on the road."
    };

    static readonly string[] angelFarewells = {
        "We will ride together again.",
        "Thank you for not looking.",
        "..."
    };

    static readonly string[] starerGreetings = {
        "(it does not answer, and steps aboard)",
        "Hhhhhh...",
        "(it tilts its head at you until you look away)"
    };

    static readonly string[] starerFarewells = {
        "(it steps off backwards, still watching)",
        "(the staring stops only when the door shuts)",
        "Hhhhh..."
    };

    // Chatting to someone already in a seat, driver out of the cab
    static readonly string[] humanSeated = {
        "Shouldn't you be up front, driving?",
        "I'm fine, thanks. Just tired.",
        "My feet are killing me. Long shift.",
        "Do you ever see anything strange out here at night?",
        "Sorry, I don't really feel like talking.",
        "Nice bus. Smells a bit, but nice.",
        "Is it always this quiet on this route?",
        "Wake me up when we get there, would you?",
        "Yeah, I know, I should've got the earlier one."
    };

    static readonly string[] angelSeated = {
        "You left your seat.",
        "I prefer you where I can see you.",
        "Go back to the wheel, driver.",
        "...",
        "You should not have stopped looking."
    };

    static readonly string[] starerSeated = {
        "(it does not blink)",
        "(the head turns to follow you, far too slowly)",
        "Hhhhhh...",
        "(it says nothing, and keeps looking)"
    };

    public static Dialogue Greeting(Passenger passenger, BusStop destination) {
        if (passenger == null) {
            return null;
        }
        switch (passenger.DialogueKind) {
            case "WeepingAngel":
                return Line(passenger, Pick(angelGreetings));
            case "StaringMonster":
                return Line(passenger, Pick(starerGreetings));
            default:
                if (destination == null) {
                    return Line(passenger, Pick(humanGreetingsNoStop));
                }
                return Line(passenger, string.Format(Pick(humanGreetings), destination.StopName));
        }
    }

    // The driver walked back and struck up a conversation with a seated rider
    public static Dialogue Seated(Passenger passenger) {
        if (passenger == null) {
            return null;
        }
        switch (passenger.DialogueKind) {
            case "WeepingAngel":
                return Line(passenger, Pick(angelSeated));
            case "StaringMonster":
                return Line(passenger, Pick(starerSeated));
            default:
                return Line(passenger, Pick(humanSeated));
        }
    }

    // stars below 0 means this NPC never leaves a review
    public static Dialogue Farewell(Passenger passenger, int stars, bool kicked, bool missed) {
        if (passenger == null) {
            return null;
        }
        switch (passenger.DialogueKind) {
            case "WeepingAngel":
                return Line(passenger, Pick(angelFarewells));
            case "StaringMonster":
                return Line(passenger, Pick(starerFarewells));
        }
        if (kicked) {
            return Line(passenger, Pick(humanKicked));
        }
        if (missed) {
            return Line(passenger, Pick(humanMissed));
        }
        switch (stars) {
            case 5: return Line(passenger, Pick(humanFiveStar));
            case 4: return Line(passenger, Pick(humanFourStar));
            case 3: return Line(passenger, Pick(humanThreeStar));
            case 2: return Line(passenger, Pick(humanTwoStar));
            default: return Line(passenger, Pick(humanOneStar));
        }
    }

    // Turned away at the door: no review, they just have opinions
    public static Dialogue Refused(Passenger passenger) {
        if (passenger == null) {
            return null;
        }
        switch (passenger.DialogueKind) {
            case "WeepingAngel":
                return Line(passenger, "I will be at the next stop.");
            case "StaringMonster":
                return Line(passenger, "(it does not move until the door closes)");
            default:
                return Line(passenger, Pick(humanRefused));
        }
    }

    static Dialogue Line(Passenger passenger, string sentence) {
        return new Dialogue {
            name = string.IsNullOrEmpty(passenger.DisplayName) ? HumanKind : passenger.DisplayName,
            sentences = new[] { sentence }
        };
    }

    static string Pick(string[] lines) {
        return lines[Random.Range(0, lines.Length)];
    }
}
