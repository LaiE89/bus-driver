namespace BusDriver.Core.Util {
    // The physics and render layers of §4.16. ProjectSettingsBuilder writes the names and the
    // collision matrix; code uses these indices instead of LayerMask.NameToLayer lookups.
    public static class Layers {
        public const int Default = 0;
        public const int Bus = 8;
        public const int BusInterior = 9;
        public const int Passenger = 10;
        public const int World = 11;
        public const int Occluder = 12;
        public const int Zone = 13;
        public const int Player = 14;
        public const int ScareFx = 15;
        // CCTV camera k (1..3) excludes layer MimicHideCamBase + k (§2.12, D39)
        public const int MimicHideCamBase = 15;
        public const int MimicHideCam1 = 16;
        public const int MimicHideCam2 = 17;
        public const int MimicHideCam3 = 18;
        public const int PlayerAvatar = 19;

        public const int First = Bus;
        public const int Last = PlayerAvatar;

        // Index − First → name
        static readonly string[] names = {
            "Bus", "BusInterior", "Passenger", "World", "Occluder", "Zone", "Player", "ScareFx",
            "MimicHideCam1", "MimicHideCam2", "MimicHideCam3", "PlayerAvatar",
        };

        public static string NameOf(int layer) {
            return layer >= First && layer <= Last ? names[layer - First] : "";
        }

        public static int Mask(int layer) {
            return 1 << layer;
        }
    }

    public static class Tags {
        // Every collider the bus can never cross (§3.5, §4.16)
        public const string Containment = "Containment";
    }
}
