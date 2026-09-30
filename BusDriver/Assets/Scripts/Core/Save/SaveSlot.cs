namespace BusDriver.Core.Save {
    // One file per slot under the save root (§4.9). Explicit values, append-only.
    public enum SaveSlot : int { Settings = 1, Meta = 2, Run = 3 }

    public static class SaveSlots {
        public static string FileName(SaveSlot slot) {
            return Kind(slot) + ".json";
        }

        // The envelope's "kind", which is also the file's base name
        public static string Kind(SaveSlot slot) {
            switch (slot) {
                case SaveSlot.Settings: return "settings";
                case SaveSlot.Meta: return "meta";
                case SaveSlot.Run: return "run";
                default: throw new System.ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }
    }
}
