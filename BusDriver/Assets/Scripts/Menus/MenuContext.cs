using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.UI.Menu {
    // The Menu scene's root (§4.3): hands the game services to the menu UI
    public sealed class MenuContext : MonoBehaviour, ISceneRoot {
        [SerializeField] BuildLabelView buildLabel;

        public GameServices Game { get; private set; }

        public void Initialize(GameServices game) {
            Game = game;
            game.Input.SetContext(InputContext.Menu);
            // MainMenu and the options screen (D56)
            SceneBinding.BindAll(gameObject.scene, game);
            if (buildLabel != null) {
                buildLabel.Show(game.Build.Label);
            }
        }
    }
}
