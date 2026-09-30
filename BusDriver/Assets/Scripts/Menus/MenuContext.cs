using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.UI.Menu {
    // The Menu scene's root (§4.3): hands the game services to the menu UI
    public sealed class MenuContext : MonoBehaviour, ISceneRoot {
        [SerializeField] MainMenu mainMenu;
        [SerializeField] BuildLabelView buildLabel;

        public GameServices Game { get; private set; }

        public void Initialize(GameServices game) {
            Game = game;
            if (mainMenu != null) {
                mainMenu.Bind(game);
            }
            if (buildLabel != null) {
                buildLabel.Show(game.Build.Label);
            }
        }
    }
}
