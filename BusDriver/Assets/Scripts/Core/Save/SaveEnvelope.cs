using Newtonsoft.Json.Linq;

namespace BusDriver.Core.Save {
    // The wrapper around every save file (§4.9):
    // { "saveVersion": 1, "kind": "run", "writtenUtc": "...", "build": "0.3.0 (a1b2c3d)", "data": { ... } }
    public sealed class SaveEnvelope {
        public int saveVersion;
        public string kind;
        public string writtenUtc;
        public string build;
        // Kept as JSON until the version is known, so migrations can reshape it first
        public JObject data;
    }
}
