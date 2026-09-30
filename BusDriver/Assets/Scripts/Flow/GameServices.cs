using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Input;

namespace BusDriver.Gameplay.Flow {
    // Everything that lives for the whole application (§4.5). GameRoot builds it once, in the
    // order Saves → Settings → Meta → Audio → Input → Pause → Cursor → Scenes → Flow. Services
    // are created in that order by GameRoot.Build.
    public sealed class GameServices {
        public GameRootConfig Config;
        public BuildInfo Build;
        public ISaveStore Saves;
        public SettingsService Settings;
        public MetaService Meta;
        public IAudioService Audio;
        public InputService Input;
        public PauseService Pause;
        public CursorService Cursor;
        public SceneLoader Scenes;
        public RunFlow Flow;
    }
}
