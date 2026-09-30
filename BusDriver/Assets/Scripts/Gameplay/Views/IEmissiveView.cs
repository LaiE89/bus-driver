namespace BusDriver.Gameplay.Views {
    // A view part that glows: lamp heads, tunnel fixtures, lit windows, signs (§4.14). LightFlicker
    // drives it together with its lights, so an art view only has to implement this to flicker.
    public interface IEmissiveView {
        // 0 = dark, 1 = the emission the view was authored with (A.0: authored at intensity 1)
        void SetEmission(float intensity01);
    }
}
