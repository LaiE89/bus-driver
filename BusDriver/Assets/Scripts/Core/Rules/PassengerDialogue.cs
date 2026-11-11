using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Which table a rider draws from. Not serialized content: it only ever comes from a monster
    // id, and the Mimic has no entry on purpose so it draws the human lines (D34).
    public enum DialogueKind { Human, Starer, Whisperer }

    // Random one-liners for the door, the seat and the drop-off, picked per rider kind so a
    // monster never sounds like a commuter. Every draw takes the caller's stream, so the same
    // seed replays the same chatter (§4.1.8). Nobody is ever named: a monster that introduced
    // itself would give the game away.
    public static class PassengerDialogue {
        static readonly string[] humanGreetings = {
            "Good evening. I'm headed to {0} tonight!",
            "I'm just going to {0}.",
            "It's a quick ride to {0}, right?",
            "This goes to {0}, right?",
        };

        static readonly string[] humanGreetingsNoStop = {
            "Good evening.",
            "What a cold night.",
            "Hello there.",
            "You're the first bus in a while. Thanks for coming.",
            "Lovely weather tonight, isn't it?"
        };

        static readonly string[] humanEarly = {
            "Appreciate it.",
            "Smoothest ride I've had all month.",
            "Nice ride, thanks."
        };

        static readonly string[] humanOnTime = {
            "Have a nice day.",
            "Great, we're here.",
            "Nice driving."
        };

        static readonly string[] humanUnrated = {
            "Finally.",
            "Could've been quicker.",
            "It'll do."
        };

        static readonly string[] humanLate = {
            "That took forever.",
            "I could have walked.",
            "You took your sweet time."
        };

        static readonly string[] humanMissed = {
            "You drove straight past my stop!",
            "Hey! That was my stop!",
            "Unbelievable. I'm so far from home now."
        };

        static readonly string[] humanKicked = {
            "What? You're throwing me off?",
            "Hey! I paid my fare!",
            "Out here in the dark? Come on."
        };

        // we don't need this anymore        
        static readonly string[] humanRefused = {
            "Fine. I'll wait for a driver with a heart.",
            "Seriously? I've been standing here for an hour.",
            "Your loss. Something worse than me is out tonight."
        }; 

        // Chatting to someone already in a seat, driver out of the cab
        static readonly string[] humanSeated = {
            "Shouldn't you be up front?",
            "How are you doing today?",
            "I am so tired. Can't wait to get home.",
            "Do you ever see anything strange out here at night?",
            "Sorry, I don't really feel like talking.",
            "I like that this bus is nice and warm.",
            "Is it always this quiet over here?",
            "Wake me up when we get there, would you?",
            "Should I have caught the earlier bus..."
        };

        static readonly string[] starerGreetings = {
            "Hi there.",
            "Lovely evening, is it not?",
            "Hello driver."
        };

        static readonly string[] starerFarewells = {
            "Goodbye. For now.",
            "Interesting.",
            "See you soon."
        };

        static readonly string[] starerSeated = {
            "Hello.",
            "Can I help you?",
            "...",
        };

        static readonly string[] whispererGreetings = {
            "Evening.",
            "It's pretty quiet tonight.",
            "How are you doing?"
        };

        static readonly string[] whispererFarewells = {
            "Farewell, driver.",
            "You will hear me again.",
            "Thank you."
        };

        static readonly string[] whispererSeated = {
            "Could you keep driving?",
            "Shh- it's too loud in here.",
            "Can you be quiet for a moment?",
            "Did you hear something?"
        };

        public static DialogueKind KindFor(string monsterId) {
            switch (monsterId) {
                case MonsterIds.Starer:
                    return DialogueKind.Starer;
                case MonsterIds.Whisperer:
                    return DialogueKind.Whisperer;
                default:
                    return DialogueKind.Human;
            }
        }

        // At the door, before the driver has decided. A blank destination name means the rider
        // never says where they are going.
        public static string Greeting(DialogueKind kind, string destinationName, Random rng) {
            switch (kind) {
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
