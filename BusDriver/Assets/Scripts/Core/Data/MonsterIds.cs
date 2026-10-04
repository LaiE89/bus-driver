namespace BusDriver.Core.Data {
    // The four MonsterDefinition ids (§2.10-§2.12b). Content is data-driven, so nothing here
    // decides behaviour; these are for the few places that must name one monster in particular,
    // like the Whisperer's sanity death (§2.21) and the dialogue tables.
    public static class MonsterIds {
        public const string Starer = "starer";
        public const string Whisperer = "whisperer";
        public const string Mimic = "mimic";
        public const string WeepingAngel = "weeping_angel";
    }
}
