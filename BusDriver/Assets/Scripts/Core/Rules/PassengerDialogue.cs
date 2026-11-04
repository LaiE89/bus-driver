using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Which table a rider draws from. Not serialized content: it only ever comes from a monster
    // id, and the Mimic has no entry on purpose so it draws the human lines (D34).
    public enum DialogueKind { Human, Starer, Whisperer, Gazer, Guest }

    // Random one-liners for the door, the seat and the drop-off. Humans mix ordinary riders with
    // people who already sound wrong: drunk, homeless, worked past sense. Monsters use the same
    // kind of speech, so a strange line is not a verdict. Every draw takes the caller's stream,
    // so the same seed replays the same chatter (§4.1.8). Nobody is ever named.
    public static class PassengerDialogue {
        static readonly string[] humanGreetings = {
            "Evening. {0} for me, when you get a chance.",
            "Cold out there. I'm going as far as {0}.",
            "You're the first bus in forty minutes. {0}, please.",
            "Quiet night, huh? I get off at {0}.",
            "Just {0}. I won't be any trouble.",
            "Long shift? Same. {0}, thanks.",
            "Been on my feet since the plant let out. {0}, if the heat's on.",
            "I can pay. Mostly. {0}. I'll sit in the back and keep to myself.",
            "Ha. Sorry. {0}, I think. Don't let me sleep through it.",
            "You're a saint. {0}. Or the one after. You'll know it.",
            "Third shift. Don't talk to me unless we're at {0}.",
            "If I pass out, {0}. Actually kick the seat. I won't feel a tap.",
            "Clock says I should already be home. {0}. Then I'm done.",
            "Paper says {0}. I've been walking toward it since noon."
        };

        static readonly string[] humanGreetingsNoStop = {
            "Evening, driver.",
            "Cold night for this, isn't it?",
            "Don't mind me, I'll just sit at the back.",
            "Anywhere warm. I don't need a whole seat.",
            "Sorry. Lost the thread. Just let me ride a while.",
            "You stopping? Good. I was about to sit down in the road.",
            "Don't mind the smell. Long week. I'll take the back.",
            "Eyes won't stay open. Wake me if I start talking to the window.",
            "I'll tell you when we're close. Don't want to miss it.",
            "Not far. I'll get up when I see it.",
            "Same stop as usual. You'll know it.",
            "Wherever the heat holds out. I'll say when."
        };

        static readonly string[] humanEarly = {
            "Right on time. Appreciate it, driver.",
            "Smoothest ride I've had all month.",
            "Perfect. You're the only bus I trust.",
            "Huh. We're here already? I must've dropped off.",
            "Early. Good. I can sit on the curb and feel my feet again."
        };

        static readonly string[] humanOnTime = {
            "Not bad at all.",
            "Bit of a wait, but we got here.",
            "Good driving.",
            "There it is. I was starting to argue with the seat in front of me.",
            "On time. Miracle. I'm still late for the rest of my life."
        };

        static readonly string[] humanUnrated = {
            "We got there, eventually.",
            "Could've been quicker.",
            "It'll do.",
            "I think this is it. If it isn't, I'm too tired to care.",
            "Warm enough. That's the review."
        };

        static readonly string[] humanLate = {
            "That took forever.",
            "I could have walked.",
            "My shift started an hour ago.",
            "I sobered up halfway here. Wish I hadn't.",
            "My supervisor's going to smell this night on me. Thanks for the wait."
        };

        static readonly string[] humanMissed = {
            "You drove straight past my stop!",
            "That was my stop. That was MY stop.",
            "Unbelievable. I'm miles from home now.",
            "I asked you. I think I asked you. That was the stop.",
            "Now I've got to walk back. In these shoes. At this hour."
        };

        static readonly string[] humanKicked = {
            "You're throwing me off?",
            "I paid my fare!",
            "Out here? In the dark? Come on, driver.",
            "I paid. Most of the fare. You can't leave me in the dark.",
            "Throwing me off for sitting quiet? I was trying to sleep."
        };

        static readonly string[] humanRefused = {
            "Fine. I'll wait for a driver with a heart.",
            "Seriously? I've been standing here for an hour.",
            "Your loss. Something worse than me is out tonight.",
            "Alright. I've slept colder than this.",
            "Okay. I'll walk. Done it drunker.",
            "Fair. Bus is full of people who still have houses."
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
            "Yeah, I know, I should've got the earlier one.",
            "If I start snoring, leave it. That's the first rest I've had.",
            "Don't suppose you've got a spare sandwich. No? Worth a try.",
            "Sorry. Thought you were someone from the bar. Carry on.",
            "I keep counting the stops and losing the number. It's fine.",
            "You ever just ride to the end because the seat's warmer than home?",
            "My eyes are open. That's the job. That's the whole job tonight."
        };

        static readonly string[] starerGreetings = {
            "Evening. I'll sit where I can see the road, if that's alright.",
            "Cold. You don't have to talk. I just like a window.",
            "Long night. Mind if I stay near the front? I get sick in the back.",
            "Quiet out here. I'll keep an eye out with you.",
            "Not far. I'll get up when I see it.",
            "I'll tell you when we're close. I know the road."
        };

        static readonly string[] starerFarewells = {
            "Thanks. I'll watch the door till it shuts.",
            "Alright. I'll stand a second. Eyes need to adjust.",
            "Good driving. I was watching. In a good way."
        };

        static readonly string[] starerSeated = {
            "You can sit back down. I'm just looking out.",
            "Quiet in here. I watch the lights when I can't sleep.",
            "Hm? No, I'm fine. Eyes are tired, so they stare.",
            "Don't mind me. Habit. I face the front on night buses."
        };

        static readonly string[] whispererGreetings = {
            "Sorry. Throat's gone. Just a seat, I'll be quiet.",
            "Hey. You hear the heater too, or is that just me?",
            "I'll keep it down. Been talking all day and none of it landed.",
            "Evening. Don't lean in, I'll say it louder. Seat's fine.",
            "Same stop as usual. You'll know it when I stand up.",
            "Wherever the heat holds out. I'll say when."
        };

        static readonly string[] whispererFarewells = {
            "Thanks. I'll stop mumbling now that I'm off.",
            "Alright. Sorry if I was talking. I do that half asleep.",
            "Good night. Tell the next one I said thanks. Or don't."
        };

        static readonly string[] whispererSeated = {
            "Was I talking? I do that when I'm half gone.",
            "Heater's loud. Thought I heard my stop. Ignore me.",
            "I'll keep it down. People are trying to sleep.",
            "Long day. Mouth keeps moving. Nothing worth hearing."
        };

        static readonly string[] gazerGreetings = {
            "Evening. I'll face the window, if you don't mind.",
            "Lights in here are a lot. I'll keep my head down.",
            "Rather not do the chatting. Bad with faces up close.",
            "Sorry. Long day of people. A dark seat is plenty.",
            "I'll tell you when we're close. Don't want to miss it.",
            "Not far. A window seat is all I need."
        };

        static readonly string[] gazerFarewells = {
            "Thanks. I'll get out of the light.",
            "Alright. Don't look back, I'm already off.",
            "Good. Fresh air. The cabin was a bit much."
        };

        static readonly string[] gazerSeated = {
            "I'm alright. Don't love people standing over me.",
            "Window's fine. Nothing out there I need to see.",
            "You need something, or you just checking on folks?",
            "Sorry. Habit. I look at the floor when someone stops."
        };

        static readonly string[] guestGreetings = {
            "You stopping? Good. I'll hop on.",
            "Anywhere out of this wind is fine. I'll pay.",
            "Room for one more? I'll sit wherever's left.",
            "Evening. Don't need a speech. Just a ride.",
            "I'll get up when I see it. Don't want to miss it.",
            "Not far. I'll say when."
        };

        static readonly string[] guestRefused = {
            "Alright. Next one, then.",
            "Cold out here. Hope the next driver's got room.",
            "Fine. I'll walk. I've done worse nights.",
            "Okay. Shut the door. I won't make it a thing."
        };

        public static DialogueKind KindFor(string monsterId) {
            switch (monsterId) {
                case MonsterIds.Starer:
                    return DialogueKind.Starer;
                case MonsterIds.Whisperer:
                    return DialogueKind.Whisperer;
                case MonsterIds.Gazer:
                    return DialogueKind.Gazer;
                case MonsterIds.Guest:
                    return DialogueKind.Guest;
                default:
                    return DialogueKind.Human;
            }
        }

        // At the door, before the driver has decided. A blank destination name means the rider
        // never says where they are going. A named stop is only spoken about a third of the time:
        // saying it on every boarding was a tell, because a monster has no stop to give.
        public static string Greeting(DialogueKind kind, string destinationName, Random rng) {
            switch (kind) {
                case DialogueKind.Starer:
                    return Pick(starerGreetings, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererGreetings, rng);
                case DialogueKind.Gazer:
                    return Pick(gazerGreetings, rng);
                case DialogueKind.Guest:
                    return Pick(guestGreetings, rng);
                default:
                    if (string.IsNullOrEmpty(destinationName) || (rng != null && rng.Next(3) != 0)) {
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
                case DialogueKind.Gazer:
                    return Pick(gazerSeated, rng);
                case DialogueKind.Guest:
                    return Pick(guestGreetings, rng);
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
                case DialogueKind.Gazer:
                    return Pick(gazerFarewells, rng);
                case DialogueKind.Guest:
                    return Pick(guestRefused, rng);
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
                    return "I'll wait. I don't mind the cold if I can see the road.";
                case DialogueKind.Whisperer:
                    return "Alright. Sorry I asked. I'll catch the next one.";
                case DialogueKind.Gazer:
                    return "Yeah. Fair. I'll get out of the doorway.";
                case DialogueKind.Guest:
                    return Pick(guestRefused, rng);
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
