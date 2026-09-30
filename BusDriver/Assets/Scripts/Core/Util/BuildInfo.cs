namespace BusDriver.Core.Util {
    // What produced this build (§4.18, §4.20). The label comes from GameRootConfig.buildLabel,
    // written by BuildScripts; editor sessions and hand-made builds show "<version> (dev)".
    public sealed class BuildInfo {
        public string Label { get; }
        public string Version { get; }

        public BuildInfo(string configuredLabel, string version) {
            Version = version;
            Label = string.IsNullOrWhiteSpace(configuredLabel) ? version + " (dev)" : configuredLabel.Trim();
        }
    }
}
