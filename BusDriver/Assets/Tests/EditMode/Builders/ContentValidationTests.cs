using System.Collections.Generic;
using BusDriver.Editor.Validation;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Builders {
    // The committed content passes the same validation BuildAll runs (§4.15, §4.19)
    public class ContentValidationTests {
        [Test]
        public void ContentIsValid() {
            List<string> problems = ContentValidator.Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
