using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode {
    // Proves the PlayMode platform runs: frames advance and physics simulates (T-M0-06)
    public class SamplePlayModeTests {
        [UnityTest]
        public IEnumerator FallingBodyMovesDown() {
            GameObject body = new GameObject("SampleBody");
            Rigidbody rb = body.AddComponent<Rigidbody>();
            float startY = body.transform.position.y;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less(rb.position.y, startY, "gravity did not move the body");
            Object.Destroy(body);
        }
    }
}
