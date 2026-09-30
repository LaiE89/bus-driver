using System.Runtime.CompilerServices;

// Tests may reach internal hooks (ROADMAP §4.2)
[assembly: InternalsVisibleTo("BusDriver.Tests.EditMode")]
[assembly: InternalsVisibleTo("BusDriver.Tests.PlayMode")]
[assembly: InternalsVisibleTo("BusDriver.Editor")]
