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

        // Night_Systems (single, so the previous night or the menu goes), then Route01_World
        // additively (§4.3). RunFlow begins the night from the route scene's sceneLoaded.
        public IEnumerator LoadNight(NightSetup setup) {
            if (IsLoading) {
                Log.Warn(LogCat.Flow, "night load ignored: another load is running");
                yield break;
            }
            yield return Load(SceneIds.NightSystems, 0f, 0.6f);
            if (!SceneManager.GetSceneByName(SceneIds.NightSystems).isLoaded) {
                yield break;
            }
            yield return Load(SceneIds.Route01World, 0.6f, 1f, LoadSceneMode.Additive);
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
            return Load(sceneName, 0f, 1f);
        }

        // Progress runs from `from` to `to` over this load; single loads stop the scene sounds
        IEnumerator Load(string sceneName, float from, float to, LoadSceneMode mode = LoadSceneMode.Single) {
            if (IsLoading) {
                Log.Warn(LogCat.Flow, $"load of {sceneName} ignored: another load is running");
                yield break;
            }
            IsLoading = true;
            Progress = from;
            if (mode == LoadSceneMode.Single) {
                // Everything but UI stops on a scene change (§4.3, §4.12)
                game.Audio.StopSceneSounds();
            }
            Log.Info(LogCat.Flow, "loading " + sceneName + (mode == LoadSceneMode.Additive ? " (additive)" : ""));
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null) {
                IsLoading = false;
                Progress = 1f;
                Log.Error(LogCat.Flow, $"scene {sceneName} is not in the build list");
                yield break;
            }
            operation.allowSceneActivation = false;
            while (!operation.isDone) {
                // Unity parks async loads at 0.9 until activation is allowed
                Progress = Mathf.Lerp(from, to, Mathf.Clamp01(operation.progress / 0.9f));
                if (operation.progress >= 0.9f && AllowActivation) {
                    operation.allowSceneActivation = true;
                }
                yield return null;
            }
            Progress = to;
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

        // Route01_World beside an already loaded Night_Systems (the editor debug run, §4.4)
        public IEnumerator LoadRouteAdditive() {
            return Load(SceneIds.Route01World, 0f, 1f, LoadSceneMode.Additive);
        }

        // A component on one of the scene's root objects (roots only, like ISceneRoot)
        public static T FindInScene<T>(Scene scene) where T : Component {
            if (!scene.IsValid() || !scene.isLoaded) {
                return null;
            }
            List<GameObject> roots = new List<GameObject>();
            scene.GetRootGameObjects(roots);
            for (int i = 0; i < roots.Count; i++) {
                T found = roots[i].GetComponent<T>();
                if (found != null) {
                    return found;
                }
            }
            return null;
        }

        // The first such root component in any loaded scene
        public static T FindLoaded<T>() where T : Component {
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                T found = FindInScene<T>(SceneManager.GetSceneAt(i));
                if (found != null) {
                    return found;
                }
            }
            return null;
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
