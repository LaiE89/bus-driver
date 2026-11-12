using System;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Which table a rider draws from. Not serialized content: it only ever comes from a monster id.
    // An unknown id still draws the human lines.
    public enum DialogueKind { Human, Starer, Whisperer, Gazer, Guest, Mimic }

    // A human keeps one of these for the whole ride. The list is the six kinds a rider can be.
    public enum HumanVoice { Normal, Overworked, Drunk, Homeless, Teenager, Rude }

    // Short lines for the door, the seat and the drop-off. A human's lines stay in one voice.
    // A few of them notice something odd and do not explain it. Nobody names a stop. Monsters
    // talk like tired people who are slightly wrong. Every draw takes the caller's stream, so
    // the same seed replays the same chatter. Nobody is ever named.
    public static class PassengerDialogue {
        struct Voice {
            public string[] Plain;
            public string[] Seated;
            public string[] Early;
            public string[] OnTime;
            public string[] Late;
            public string[] Missed;
            public string[] Unrated;
            public string[] Kicked;
        }

        static readonly Voice[] voices = {
            new Voice {
                Plain = new[] { "Evening, driver.", "Hey.", "Quiet night.", "Heard the woods are loud tonight." },
                Seated = new[] { "I'm fine. Just tired.", "Wake me at my stop.", "Saw something on the shoulder. Probably a deer." },
                Early = new[] { "Thanks. That was quick.", "Didn't expect to be here already." },
                OnTime = new[] { "Good. We're here.", "Not bad." },
                Late = new[] { "That took a while.", "Cutting it close." },
                Missed = new[] { "You drove past my stop.", "That was my stop." },
                Unrated = new[] { "This is me. Thanks.", "I'll get off here." },
                Kicked = new[] { "You're throwing me off?", "What did I do?" }
            },
            new Voice {
                Plain = new[] { "Don't talk. I'm exhausted.", "Been on my feet all day.", "If they just stand there, leave them.", "If they won't name a stop, leave them." },
                Seated = new[] { "Please let me sleep.", "My feet are killing me.", "Clock's wrong. Or I am." },
                Early = new[] { "Early. I might actually rest.", "Thanks. I needed that." },
                OnTime = new[] { "On time. Rare.", "Good. I can still make it." },
                Late = new[] { "I'm going to be late for the next shift.", "That ate my break." },
                Missed = new[] { "That was my stop. I told you.", "Now I have to walk back to work." },
                Unrated = new[] { "Close enough. I need to sit.", "This'll do." },
                Kicked = new[] { "Out here? In the dark? I have work.", "You're throwing me off after that shift?" }
            },
            new Voice {
                Plain = new[] { "Hey. Sorry. Long night.", "I'll sit in the back.", "The one in the back wasn't blinking. Or I'm seeing things." },
                Seated = new[] { "I'm fine. Mostly.", "Don't let me miss it.", "Was I talking? Ignore that." },
                Early = new[] { "We're here already? Nice.", "Huh. Fast." },
                OnTime = new[] { "Huh. This is it.", "Alright. Thanks." },
                Late = new[] { "I sobered up. Wish I hadn't.", "That took forever." },
                Missed = new[] { "That was my stop. I think.", "You passed it. I was awake. Sort of." },
                Unrated = new[] { "Warm in here. That's enough.", "I'll get off. Probably." },
                Kicked = new[] { "Don't leave me. It's dark.", "You're throwing me off? Come on." }
            },
            new Voice {
                Plain = new[] { "Anywhere warm.", "I'll keep to myself.", "Some nights the trees answer. Don't answer back." },
                Seated = new[] { "Seat's warmer than outside.", "I won't be trouble.", "You ever just ride to the end?" },
                Early = new[] { "Didn't expect to be here this soon.", "Thanks. Really." },
                OnTime = new[] { "This'll do. Thanks.", "Good enough." },
                Late = new[] { "Long ride. Still better than walking.", "I'm not in a hurry." },
                Missed = new[] { "Now I've got to walk back.", "That was the stop. It's a long walk." },
                Unrated = new[] { "I'll get off here. It's fine.", "Warm enough. Thanks." },
                Kicked = new[] { "You can't leave me in the dark.", "Out here? In the dark?" }
            },
            new Voice {
                Plain = new[] { "Hey.", "Yo.", "My friend said don't stare at people on this bus." },
                Seated = new[] { "I'm on my phone. It's fine.", "This route is always dead.", "You drive this every night? That's wild." },
                Early = new[] { "Whoa. Already?", "Nice. Thanks." },
                OnTime = new[] { "Cool. Thanks.", "This is it." },
                Late = new[] { "This took forever.", "I'm gonna be so late." },
                Missed = new[] { "You passed my stop.", "That was my stop. Seriously." },
                Unrated = new[] { "I guess this is it.", "Yeah, I'll get off." },
                Kicked = new[] { "What did I do?", "You're throwing me off? For real?" }
            },
            new Voice {
                Plain = new[] { "Drive.", "Don't chat.", "If someone just stares, that's your problem." },
                Seated = new[] { "Why are you back here?", "I'm not talking.", "Bus smells. Just drive." },
                Early = new[] { "About time something went right.", "Fine. We're early." },
                OnTime = new[] { "Fine. We're here.", "Good. Door." },
                Late = new[] { "My grandma drives faster.", "That took forever." },
                Missed = new[] { "You drove past it. Unbelievable.", "That was my stop." },
                Unrated = new[] { "This better be it.", "I'm getting off." },
                Kicked = new[] { "Your manager is not going to be happy about this.", "You're throwing me off? Good luck surviving tonight." }
            }
        };

        static readonly string[] starerGreetings = {
            "Evening. Front's fine.",
            "I'll watch the road.",
            "Don't mind me.",
            "Don't talk. I'm looking."
        };

        static readonly string[] starerFarewells = {
            "I'll keep watching from out here.",
            "Thanks. My eyes need a second."
        };

        static readonly string[] starerSeated = {
            "Still looking. You can go.",
            "I don't blink. Bad habit.",
            "The road's empty. I checked."
        };

        static readonly string[] whispererGreetings = {
            "Sorry. Was I talking?",
            "I'll keep it down.",
            "Quiet night, isn't it?",
            "You hear that too, right?"
        };

        static readonly string[] whispererFarewells = {
            "They said thanks. I think.",
            "Alright. I'll stop now."
        };

        static readonly string[] whispererSeated = {
            "Wasn't talking to you.",
            "They said your name. Ignore it.",
            "I'll whisper. People are sleeping."
        };

        static readonly string[] gazerGreetings = {
            "Window's fine.",
            "Evening. Don't mind me.",
            "A bit bright in here.",
            "The lights are bright. That's all."
        };

        static readonly string[] gazerFarewells = {
            "Don't watch me leave.",
            "Thanks. I'm going."
        };

        static readonly string[] gazerSeated = {
            "Please don't stare.",
            "I'm fine. Look somewhere else.",
            "Count to three, then look away."
        };

        static readonly string[] guestGreetings = {
            "Evening.",
            "Anywhere's fine.",
            "Thanks for stopping.",
            "You opened the door. That's enough."
        };

        static readonly string[] guestSeated = {
            "I'm here now.",
            "Don't ask where I'm going.",
            "Keep driving. I'll stay."
        };

        static readonly string[] mimicGreetings = {
            "Evening.",
            "Hey. Been a while.",
            "I'll just sit.",
            "I'll sit. I already picked."
        };

        static readonly string[] mimicFarewells = {
            "I'll be back. Not like this.",
            "Thanks. That was me."
        };

        static readonly string[] mimicSeated = {
            "I said that already. Didn't I?",
            "This face is fine. Leave it.",
            "Don't look too long."
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
                case MonsterIds.Mimic:
                    return DialogueKind.Mimic;
                default:
                    return DialogueKind.Human;
            }
        }

        // One roll per human. Pass the result back into the line methods so the voice stays put.
        public static int RollVoice(Random rng) {
            return rng == null ? 0 : rng.Next(voices.Length);
        }
        // At the door. Nobody names a stop. destinationName is ignored so a human and a monster
        // board the same way. voice is the index from RollVoice. Below zero rolls one for this line only.
        public static string Greeting(DialogueKind kind, string destinationName, Random rng, int voice = -1) {
            switch (kind) {
                case DialogueKind.Starer:
                    return Pick(starerGreetings, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererGreetings, rng);
                case DialogueKind.Gazer:
                    return Pick(gazerGreetings, rng);
                case DialogueKind.Guest:
                    return Pick(guestGreetings, rng);
                case DialogueKind.Mimic:
                    return Pick(mimicGreetings, rng);
            }
            return Pick(voices[Resolve(rng, voice)].Plain, rng);
        }

        // The driver walked back and struck up a conversation with a seated rider.
        public static string Seated(DialogueKind kind, Random rng, int voice = -1) {
            switch (kind) {
                case DialogueKind.Starer:
                    return Pick(starerSeated, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererSeated, rng);
                case DialogueKind.Gazer:
                    return Pick(gazerSeated, rng);
                case DialogueKind.Guest:
                    return Pick(guestSeated, rng);
                case DialogueKind.Mimic:
                    return Pick(mimicSeated, rng);
                default:
                    return Pick(voices[Resolve(rng, voice)].Seated, rng);
            }
        }

        // Stepping off. Monsters never review the ride. A human's line comes from how their stop
        // was reached, with None standing in for a stop that was never rated.
        public static string Farewell(DialogueKind kind, ArrivalRating rating, bool kicked, Random rng, int voice = -1) {
            switch (kind) {
                case DialogueKind.Starer:
                    return Pick(starerFarewells, rng);
                case DialogueKind.Whisperer:
                    return Pick(whispererFarewells, rng);
                case DialogueKind.Gazer:
                    return Pick(gazerFarewells, rng);
                case DialogueKind.Guest:
                    return "";
                case DialogueKind.Mimic:
                    return Pick(mimicFarewells, rng);
            }
            Voice lines = voices[Resolve(rng, voice)];
            if (kicked) {
                return Pick(lines.Kicked, rng);
            }
            switch (rating) {
                case ArrivalRating.Early:
                    return Pick(lines.Early, rng);
                case ArrivalRating.OnTime:
                    return Pick(lines.OnTime, rng);
                case ArrivalRating.Late:
                    return Pick(lines.Late, rng);
                case ArrivalRating.Missed:
                    return Pick(lines.Missed, rng);
                default:
                    return Pick(lines.Unrated, rng);
            }
        }

        static int Resolve(Random rng, int voice) {
            if (voice >= 0 && voice < voices.Length) {
                return voice;
            }
            return RollVoice(rng);
        }

        // A missing stream still returns a line rather than throwing: a rider with nothing to say
        // is a worse bug than a predictable one
        static string Pick(string[] lines, Random rng) {
            return rng == null ? lines[0] : lines[rng.Next(lines.Length)];
        }
    }
}
