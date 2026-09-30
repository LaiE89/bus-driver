using BusDriver.Gameplay.Bus;
using NUnit.Framework;
using UnityEditor;

namespace BusDriver.Tests.EditMode {
    // Proves the EditMode platform runs and can see runtime and project assets (T-M0-06)
    public class SampleEditModeTests {
        [Test]
        public void BusTuningAssetLoads() {
            BusTuning tuning = AssetDatabase.LoadAssetAtPath<BusTuning>("Assets/Settings/BusTuning.asset");
            Assert.IsNotNull(tuning, "Assets/Settings/BusTuning.asset is missing");
        }
    }
}
