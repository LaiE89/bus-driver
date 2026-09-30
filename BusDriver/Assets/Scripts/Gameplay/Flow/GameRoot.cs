using System;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.Flow {
    // The one persistent object (D28). It builds GameServices before the first scene loads and
    // is the only DontDestroyOnLoad in the game; everything else reaches the services through
    // ISceneRoot.Initialize and the Init/Bind calls that follow from it.
    public sealed class GameRoot : MonoBehaviour {
        // The bootstrap field: one of the two pieces of static mutable state §4.1.5 allows.
        // Domain reload is on (D24), so it starts empty on every Play.
        static GameRoot bootstrapped;

        public GameServices Services { get; private set; }
        // The concrete service, for its per-frame tick; everything else sees IAudioService
        AudioService audio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() {
            if (bootstrapped != null) {
                return;
            }
            GameRoot root = Create(Application.persistentDataPath);
            root.Services.Flow.Boot(SceneManager.GetActiveScene());
        }

        static GameRoot Create(string saveRoot) {
            GameRootConfig config = Resources.Load<GameRootConfig>(GameRootConfig.ResourceName);
            if (config == null) {
                Log.Error(LogCat.Flow, "Resources/" + GameRootConfig.ResourceName + " is missing; running on defaults");
                config = ScriptableObject.CreateInstance<GameRootConfig>();
            }
            GameObject host = new GameObject("GameRoot");
            DontDestroyOnLoad(host);
            GameRoot root = host.AddComponent<GameRoot>();
            root.Build(config, saveRoot);
            bootstrapped = root;
            return root;
        }

        void Build(GameRootConfig config, string saveRoot) {
            BuildInfo build = new BuildInfo(config.buildLabel, Application.version);
            Log.Info(LogCat.Build, Log.SessionHeader(build.Label));
            GameServices services = new GameServices {
                Config = config,
                Build = build,
            };
            services.Saves = new SaveService(saveRoot, SaveMigrations.CreateDefault(), build.Label);
            services.Settings = new SettingsService(services.Saves, config.mixer);
            services.Meta = new MetaService(services.Saves);
            // Scaled time: a paused listener doesn't advance clips, so voices mustn't time out
            audio = new AudioService(transform, config.soundLibrary, config.audioConfig, config.mixer, () => Time.time);
            services.Audio = audio;
            services.Input = new InputService(ResolveActions(config), services.Settings);
            services.Pause = new PauseService(services.Input);
            services.Cursor = new CursorService();
            services.Input.OnContextChanged += services.Cursor.Apply;
            services.Cursor.Apply(services.Input.EffectiveContext);
            services.Scenes = new SceneLoader(this);
            services.Flow = new RunFlow(services);
            services.Scenes.Attach(services);
            Services = services;
            services.Settings.Apply();
            if (SelfTestRunner.Requested(Environment.GetCommandLineArgs())) {
                gameObject.AddComponent<SelfTestRunner>().Begin(build.Label);
            }
        }

        // The config's asset is also the project-wide one; the fallback only covers a config that
        // lost its reference
        static InputActionAsset ResolveActions(GameRootConfig config) {
            InputActionAsset asset = config.inputActions as InputActionAsset;
            if (asset == null) {
                Log.Error(LogCat.Input, "GameRootConfig.inputActions is not set; using the project-wide actions");
                asset = InputSystem.actions;
            }
            return asset;
        }

        void Update() {
            Services.Input.Tick();
        }

        // After gameplay has moved things, so attached sounds follow this frame's positions
        void LateUpdate() {
            audio.Tick();
        }

        void OnApplicationFocus(bool hasFocus) {
            if (Services == null) {
                return;
            }
            Services.Pause.HandleFocus(hasFocus);
            // The OS frees the cursor while the window is in the background
            if (hasFocus) {
                Services.Cursor.Apply(Services.Input.EffectiveContext);
            }
        }

        void Start() {
            // The first scene is normally wired from sceneLoaded; this catches it if Unity raised
            // that before Bootstrap subscribed
            Services.Scenes.WireLoadedScenes();
            // The mixer ignored the volume set during Bootstrap
            Services.Settings.ApplyAudio();
        }

        void OnDestroy() {
            if (Services != null) {
                Services.Scenes.Detach();
            }
            if (bootstrapped == this) {
                bootstrapped = null;
            }
        }

        internal static GameRoot Current {
            get { return bootstrapped; }
        }

        // Tests: swap the running root for a fresh one that saves under saveRoot, so no test ever
        // writes to the real persistentDataPath. The scenes already loaded are wired to it.
        internal static GameRoot RebootForTests(string saveRoot) {
            if (bootstrapped != null) {
                DestroyImmediate(bootstrapped.gameObject);
            }
            GameRoot root = Create(saveRoot);
            root.Services.Flow.Boot(SceneManager.GetActiveScene());
            root.Services.Scenes.WireLoadedScenes();
            return root;
        }
    }
}
