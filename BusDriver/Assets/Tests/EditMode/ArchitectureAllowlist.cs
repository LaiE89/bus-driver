namespace BusDriver.Tests.EditMode {
    // Violations of ROADMAP §4.1 that exist today, in legacy code (T-M0-06). This list may only
    // SHRINK: ArchitectureRulesTests fails on any violation not listed here, and on any entry that
    // no longer matches a violation (delete it). T-M1-20 deletes the whole list.
    static class ArchitectureAllowlist {
        // "<path under Assets/Scripts>|<rule>"
        public static readonly string[] Patterns = {
            "Bus/BusCabin.cs|UnityRandom",
            "Passengers/WeepingAngel.cs|UnityRandom",
        };

        // "<type full name>.<field>" for static mutable fields (auto-property backing fields show
        // as <Name>k__BackingField)
        public static readonly string[] Statics = {
        };
    }
}
