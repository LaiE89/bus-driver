using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.Flow {
    // A component in a legacy scene that needs the game services (the options screen, the look
    // controllers). The scene root binds every one of them in Initialize, before any Start (D56).
    // ShiftContext's explicit Init/Bind order and IShiftBindable replace this in T-M1-15.
    public interface IGameBindable {
        void Bind(GameServices game);
    }

    public static class SceneBinding {
        // Every IGameBindable under the scene's roots, inactive objects included
        public static void BindAll(Scene scene, GameServices game) {
            List<GameObject> roots = new List<GameObject>();
            scene.GetRootGameObjects(roots);
            for (int i = 0; i < roots.Count; i++) {
                IGameBindable[] bindables = roots[i].GetComponentsInChildren<IGameBindable>(true);
                for (int j = 0; j < bindables.Length; j++) {
                    bindables[j].Bind(game);
                }
            }
        }
    }
}
