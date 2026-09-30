using System.Runtime.CompilerServices;

// Tests may reach internal hooks (ROADMAP §4.2)
[assembly: InternalsVisibleTo("BusDriver.Tests.EditMode")]
[assembly: InternalsVisibleTo("BusDriver.Tests.PlayMode")]
// The smoke test boots through RunFlow on a throwaway save root (GameRoot.RebootForTests)
[assembly: InternalsVisibleTo("BusDriver.Editor")]
