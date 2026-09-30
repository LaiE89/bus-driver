using System;
using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.Flow {
    // Owns every scene load and wires each loaded scene's ISceneRoot (§4.5, §4.6).
    // Wiring happens in SceneManager.sceneLoaded, i.e. after the scene's Awake/OnEnable and before
    // any Start, so roots can rely on the services from Start on. That handler also wires scenes
    // that legacy code still loads directly (the MVP pause and Game Over menus, D55).
    public sealed class SceneLoader {
        readonly MonoBehaviour host;
        readonly Dictionary<Scene, ISceneRoot> wired = new Dictionary<Scene, ISceneRoot>();
        readonly List<GameObject> rootObjects = new List<GameObject>();
        GameServices game;

        public float Progress { get; private set; } = 1f;
        // Cleared while a New Run sequence wants to hold the loaded night back (D19, T-M8-06)
        public bool AllowActivation { get; set; } = true;
        public bool IsLoading { get; private set; }

        // After a scene's root (null when it has none) has been initialized
        public event Action<Scene, ISceneRoot> OnSceneReady;

        public SceneLoader(MonoBehaviour host) {
            this.host = host;
        }

        public void Attach(GameServices services) {
            game = services;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        public void Detach() {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        public IEnumerator LoadMenu() {
            return Load(SceneIds.Menu);
        }

        // Until T-M1-16 a night is the single legacy scene; it then becomes Night_Systems plus
        // Route01_World loaded additively
        public IEnumerator LoadNight(NightSetup setup) {
            return Load(SceneIds.LegacyNight);
        }

        // Wires scenes that were loaded before GameRoot could listen (the first scene, if Unity
        // raised sceneLoaded before the subscription, and tests that reboot the root)
        public void WireLoadedScenes() {
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) {
                    Wire(scene);
                }
            }
        }

        IEnumerator Load(string sceneName) {
            if (IsLoading) {
                Log.Warn(LogCat.Flow, $"load of {sceneName} ignored: another load is running");
                yield break;
            }
            IsLoading = true;
            Progress = 0f;
            // Everything but UI stops on a scene change (§4.3, §4.12)
            game.Audio.StopSceneSounds();
            Log.Info(LogCat.Flow, "loading " + sceneName);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) {
                IsLoading = false;
                Progress = 1f;
                Log.Error(LogCat.Flow, $"scene {sceneName} is not in the build list");
                yield break;
            }
            operation.allowSceneActivation = false;
            while (!operation.isDone) {
                // Unity parks async loads at 0.9 until activation is allowed
                Progress = Mathf.Clamp01(operation.progress / 0.9f);
                if (operation.progress >= 0.9f && AllowActivation) {
                    operation.allowSceneActivation = true;
                }
                yield return null;
            }
            Progress = 1f;
            IsLoading = false;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (mode == LoadSceneMode.Single) {
                game.Pause.ResetForSceneChange();
            }
            Wire(scene);
        }

        void HandleSceneUnloaded(Scene scene) {
            wired.Remove(scene);
        }

        void Wire(Scene scene) {
            if (wired.ContainsKey(scene)) {
                return;
            }
            ISceneRoot root = FindRoot(scene);
            wired.Add(scene, root);
            if (root != null) {
                root.Initialize(game);
            }
            Log.Info(LogCat.Flow, $"scene {scene.name} ready ({(root != null ? root.GetType().Name : "no scene root")})");
            if (OnSceneReady != null) {
                OnSceneReady(scene, root);
            }
        }

        ISceneRoot FindRoot(Scene scene) {
            ISceneRoot found = null;
            rootObjects.Clear();
            scene.GetRootGameObjects(rootObjects);
            for (int i = 0; i < rootObjects.Count; i++) {
                ISceneRoot candidate = rootObjects[i].GetComponent<ISceneRoot>();
                if (candidate == null) {
                    continue;
                }
                if (found != null) {
                    Log.Error(LogCat.Flow, $"scene {scene.name} has more than one ISceneRoot; using {found.GetType().Name}");
                    continue;
                }
                found = candidate;
            }
            return found;
        }

        public Coroutine Run(IEnumerator routine) {
            return host.StartCoroutine(routine);
        }
    }
}
