using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;

namespace BusDriver.Gameplay.Flow {
    // Everything that lives for the whole application (§4.5). GameRoot builds it once, in the
    // order Saves → Settings → Meta → Audio → Input → Pause → Cursor → Scenes → Flow. Services
    // that don't exist yet are added by their tickets (Settings T-M1-05, Input T-M1-06,
    // Pause/Cursor T-M1-08, Audio T-M1-09).
    public sealed class GameServices {
        public GameRootConfig Config;
        public BuildInfo Build;
        public ISaveStore Saves;
        public MetaService Meta;
        public SceneLoader Scenes;
        public RunFlow Flow;
    }
}
