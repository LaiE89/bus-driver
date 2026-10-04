using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Which table a rider draws from. Not serialized content: it only ever comes from a monster
    // id, and the Mimic has no entry on purpose so it draws the human lines (D34).
    public enum DialogueKind { Human, Starer, Whisperer, WeepingAngel }

    // Random one-liners for the door, the seat and the drop-off, picked per rider kind so a
    // monster never sounds like a commuter. Every draw takes the caller's stream, so the same
    // seed replays the same chatter (§4.1.8). Nobody is ever named: a monster that introduced
    // itself would give the game away.
    public static class PassengerDialogue {
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

        static readonly string[] humanEarly = {
            "Right on time. Appreciate it, driver.",
            "Smoothest ride I've had all month.",
            "Perfect. You're the only bus I trust."
        };

        static readonly string[] humanOnTime = {
            "Not bad at all.",
            "Bit of a wait, but we got here.",
            "Good driving."
        };

        static readonly string[] humanUnrated = {
            "We got there, eventually.",
            "Could've been quicker.",
            "It'll do."
        };

        static readonly string[] humanLate = {
            "That took forever.",
            "I could have walked.",
            "My shift started an hour ago."
        };

        static readonly string[] humanMissed = {
            "You drove straight past my stop!",
            "That was my stop. That was MY stop.",
            "Unbelievable. I'm miles from home now."
        };

        static readonly string[] humanKicked = {
            "You're throwing me off?",
            "I paid my fare!",
            "Out here? In the dark? Come on, driver."
        };

        static readonly string[] humanRefused = {
            "Fine. I'll wait for a driver with a heart.",
            "Seriously? I've been standing here for an hour.",
            "Your loss. Something worse than me is out tonight."
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

        static readonly string[] angelSeated = {
            "You left your seat.",
            "I prefer you where I can see you.",
            "Go back to the wheel, driver.",
            "...",
            "You should not have stopped looking."
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

        static readonly string[] starerSeated = {
            "(it does not blink)",
            "(the head turns to follow you, far too slowly)",
            "Hhhhhh...",
            "(it says nothing, and keeps looking)"
        };

        static readonly string[] whispererGreetings = {
            "(it leans in and says something you don't quite catch)",
            "...sit. drive...",
            "(a whisper, from much closer than it is standing)"
        };

        static readonly string[] whispererFarewells = {
            "(the whispering carries on for a street after it leaves)",
            "...soon...",
            "(it thanks you, in a voice a lot like your own)"
        };

        static readonly string[] whispererSeated = {
            "(it is whispering, but not to you)",
            "...keep driving, keep driving, keep...",
            "(somewhere in it you hear your own name)",
            "Hhh- no. Nothing. Go on."
        };

        public static DialogueKind KindFor(string monsterId) {
            switch (monsterId) {
                case MonsterIds.Starer:
                    return DialogueKind.Starer;
                case MonsterIds.Whisperer:
                    return DialogueKind.Whisperer;
                case MonsterIds.WeepingAngel:
                    return DialogueKind.WeepingAngel;
                default:
                    return DialogueKind.Human;
            }
        }

        // At the door, before the driver has decided. A blank destination name means the rider
        // never says where they are going.
        public static string Greeting(DialogueKind kind, string destinationName, Random rng) {
            switch (kind) {
                case DialogueKind.WeepingAngel:
                    return Pick(angelGreetings, rng);
                case DialogueKind.Starer:
                    return Pick(starerGreetings, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererGreetings, rng);
                default:
                    if (string.IsNullOrEmpty(destinationName)) {
                        return Pick(humanGreetingsNoStop, rng);
                    }
                    return string.Format(Pick(humanGreetings, rng), destinationName);
            }
        }

        // The driver walked back and struck up a conversation with a seated rider
        public static string Seated(DialogueKind kind, Random rng) {
            switch (kind) {
                case DialogueKind.WeepingAngel:
                    return Pick(angelSeated, rng);
                case DialogueKind.Starer:
                    return Pick(starerSeated, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererSeated, rng);
                default:
                    return Pick(humanSeated, rng);
            }
        }

        // Stepping off. Monsters never review the ride; a human's line comes from how their stop
        // was reached, with None standing in for a stop that was never rated.
        public static string Farewell(DialogueKind kind, ArrivalRating rating, bool kicked, Random rng) {
            switch (kind) {
                case DialogueKind.WeepingAngel:
                    return Pick(angelFarewells, rng);
                case DialogueKind.Starer:
                    return Pick(starerFarewells, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererFarewells, rng);
            }
            if (kicked) {
                return Pick(humanKicked, rng);
            }
            switch (rating) {
                case ArrivalRating.Early:
                    return Pick(humanEarly, rng);
                case ArrivalRating.OnTime:
                    return Pick(humanOnTime, rng);
                case ArrivalRating.Late:
                    return Pick(humanLate, rng);
                case ArrivalRating.Missed:
                    return Pick(humanMissed, rng);
                default:
                    return Pick(humanUnrated, rng);
            }
        }

        // Turned away at the door: no review, they just have opinions
        public static string Refused(DialogueKind kind, Random rng) {
            switch (kind) {
                case DialogueKind.WeepingAngel:
                    return "I will be at the next stop.";
                case DialogueKind.Starer:
                    return "(it does not move until the door closes)";
                case DialogueKind.Whisperer:
                    return "(it steps back, still whispering)";
                default:
                    return Pick(humanRefused, rng);
            }
        }

        // A missing stream still returns a line rather than throwing: a rider with nothing to say
        // is a worse bug than a predictable one
        static string Pick(string[] lines, Random rng) {
            return rng == null ? lines[0] : lines[rng.Next(lines.Length)];
        }
    }
}
