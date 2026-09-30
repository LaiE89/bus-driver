namespace BusDriver.Gameplay.Flow {
    // A component that needs only the game services: the screens and the menu, which the Menu and
    // the night both use, and scene ambience. Scene roots bind the ones in their builder-filled
    // `bindables` lists in Initialize, before any Start (D65). Night-only UI is IShiftBindable.
    public interface IGameBindable {
        void Bind(GameServices game);
    }
}
