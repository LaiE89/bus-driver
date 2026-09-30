using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.UI.Menu {
    // The Menu scene's root (§4.3): hands the game services to the menu UI through an explicit,
    // builder-filled list (§4.1 rule 5)
    public sealed class MenuContext : MonoBehaviour, ISceneRoot {
        [SerializeField] BuildLabelView buildLabel;
        [Tooltip("Components of this scene that need the game services (IGameBindable), bound in order")]
        [SerializeField] MonoBehaviour[] bindables = new MonoBehaviour[0];

        public GameServices Game { get; private set; }

        public void Initialize(GameServices game) {
            Game = game;
            game.Input.SetContext(InputContext.Menu);
            for (int i = 0; i < bindables.Length; i++) {
                IGameBindable bindable = bindables[i] as IGameBindable;
                if (bindable != null) {
                    bindable.Bind(game);
                }
            }
            if (buildLabel != null) {
                buildLabel.Show(game.Build.Label);
            }
        }
    }
}
