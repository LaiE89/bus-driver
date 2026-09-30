using System;
using System.Collections.Generic;
using System.Text;

namespace BusDriver.Gameplay.Debug {
    // One readout block of the F1 overlay (§4.18). Services register theirs in Init; the overlay
    // calls Write a few times a second while it is open, so Write must not do anything expensive.
    public interface IDebugSection {
        string Title { get; }
        void Write(StringBuilder text);
    }

    // One button on the F1 overlay. Cheats only do anything in development builds and the Editor:
    // the overlay itself doesn't exist in a release build (DevBuild.Enabled).
    public sealed class DebugCheat {
        public readonly string Group;
        public readonly string Label;
        public readonly Action Run;

        public DebugCheat(string group, string label, Action run) {
            Group = group;
            Label = label;
            Run = run;
        }
    }

    // The night's sections and cheats (§4.18), owned by ShiftServices so nothing here is static:
    // a new night starts with an empty registry
    public sealed class DebugRegistry {
        readonly List<IDebugSection> sections = new List<IDebugSection>();
        readonly List<DebugCheat> cheats = new List<DebugCheat>();

        public IReadOnlyList<IDebugSection> Sections { get { return sections; } }
        public IReadOnlyList<DebugCheat> Cheats { get { return cheats; } }
        public event Action OnChanged;

        public void Register(IDebugSection section) {
            if (section == null || sections.Contains(section)) {
                return;
            }
            sections.Add(section);
            Changed();
        }

        public void AddCheat(DebugCheat cheat) {
            if (cheat == null) {
                return;
            }
            cheats.Add(cheat);
            Changed();
        }

        // A section written by a lambda, for services whose readout is a line or two
        public void Register(string title, Action<StringBuilder> write) {
            Register(new DelegateSection(title, write));
        }

        void Changed() {
            if (OnChanged != null) {
                OnChanged();
            }
        }

        sealed class DelegateSection : IDebugSection {
            readonly Action<StringBuilder> write;
            public string Title { get; }

            public DelegateSection(string title, Action<StringBuilder> write) {
                Title = title;
                this.write = write;
            }

            public void Write(StringBuilder text) {
                write(text);
            }
        }
    }

    // Development builds and the Editor (§4.18). A release build compiles this to false, so the
    // overlay removes itself and no cheat can run.
    public static class DevBuild {
#if UNITY_EDITOR || BUSDRIVER_DEV
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

        // The same answer as a call, so a branch on it doesn't warn about unreachable code in
        // whichever kind of build makes it constant
        public static bool IsEnabled() {
            return Enabled;
        }
    }
}
