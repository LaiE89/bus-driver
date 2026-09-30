using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    public class SceneIdsTests {
        [Test]
        public void BuildListIsMenuThenNightThenRoute() {
            CollectionAssert.AreEqual(new[] {
                "Assets/Generated/Scenes/Menu.unity",
                "Assets/Generated/Scenes/Night_Systems.unity",
                "Assets/Generated/Scenes/Route01_World.unity",
            }, SceneIds.BuildList);
        }
    }
}
