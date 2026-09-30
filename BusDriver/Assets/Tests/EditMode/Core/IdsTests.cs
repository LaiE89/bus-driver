using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    public class IdsTests {
        [TestCase("farm_gate")]
        [TestCase("look01")]
        [TestCase("route01")]
        [TestCase("weeping_angel")]
        [TestCase("scare.starer.lens")]
        [TestCase("mon.whisper_feed_loop")]
        public void Valid(string id) {
            Assert.IsTrue(Ids.IsValid(id), id);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Farm_gate")]
        [TestCase("farm-gate")]
        [TestCase("farm__gate")]
        [TestCase("_farm")]
        [TestCase("farm_")]
        [TestCase("1farm")]
        [TestCase("scare..lens")]
        [TestCase("scare.")]
        [TestCase("farm gate")]
        public void Invalid(string id) {
            Assert.IsFalse(Ids.IsValid(id), id ?? "null");
        }

        [Test]
        public void SnakeCaseRejectsDots() {
            Assert.IsTrue(Ids.IsSnakeCase("gas_station"));
            Assert.IsFalse(Ids.IsSnakeCase("scare.starer"));
        }
    }
}
