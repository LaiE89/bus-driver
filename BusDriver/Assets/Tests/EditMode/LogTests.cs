using BusDriver.Core.Util;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.EditMode {
    public class LogTests {
        [TearDown]
        public void ResetFilter() {
            Log.EnableAll();
        }

        [Test]
        public void FormatPrefixesTheCategory() {
            Assert.AreEqual("[Economy] fare +$3.50", Log.Format(LogCat.Economy, "fare +$3.50"));
        }

        [Test]
        public void InfoIsWrittenWhenItsCategoryIsEnabled() {
            LogAssert.Expect(LogType.Log, "[Route] stop served");
            Log.Info(LogCat.Route, "stop served");
        }

        [Test]
        public void InfoIsDroppedWhenItsCategoryIsFiltered() {
            Log.SetEnabled(LogCat.Route, false);
            Assert.IsFalse(Log.IsEnabled(LogCat.Route));
            Assert.IsTrue(Log.IsEnabled(LogCat.Flow), "filtering one category must not touch another");
            Log.Info(LogCat.Route, "hidden");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WarningsAndErrorsIgnoreTheFilter() {
            Log.SetEnabled(LogCat.Save, false);
            LogAssert.Expect(LogType.Warning, "[Save] disk nearly full");
            LogAssert.Expect(LogType.Error, "[Save] write failed");
            Log.Warn(LogCat.Save, "disk nearly full");
            Log.Error(LogCat.Save, "write failed");
        }

        [Test]
        public void ReenablingACategoryRestoresIt() {
            Log.SetEnabled(LogCat.Audio, false);
            Log.SetEnabled(LogCat.Audio, true);
            Assert.IsTrue(Log.IsEnabled(LogCat.Audio));
        }

        [Test]
        public void SessionHeaderNamesTheUnityVersion() {
            StringAssert.Contains(Application.unityVersion, Log.SessionHeader());
        }
    }
}
