using System;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;

namespace BusDriver.Gameplay.Flow {
    // The journal and run counters, the only progress that survives a run (D6). Every change is
    // saved immediately (D21), so a crash or Alt-F4 can't lose it (§4.6, §4.9).
    public sealed class MetaService {
        readonly ISaveStore saves;

        public MetaProgress Current { get; private set; }

        public event Action<string> OnJournalChanged;

        public MetaService(ISaveStore saves) {
            this.saves = saves;
            Current = saves.LoadOrNew<MetaProgress>(SaveSlot.Meta);
        }

        public void RecordRunStarted() {
            Current.runsStarted++;
            Save();
        }

        public void RecordRunWon() {
            Current.runsWon++;
            Save();
        }

        public void RecordRunLost(DeathCause cause, string sourceId) {
            Current.runsLost++;
            int count;
            Current.deathsByCause.TryGetValue(cause, out count);
            Current.deathsByCause[cause] = count + 1;
            if (!string.IsNullOrEmpty(sourceId) && cause == DeathCause.MonsterKill) {
                Entry(sourceId).timesKilledBy++;
                Save();
                RaiseJournalChanged(sourceId);
                return;
            }
            Save();
        }

        public void RecordBestNight(int night) {
            if (night > Current.bestNight) {
                Current.bestNight = night;
                Save();
            }
        }

        JournalEntryState Entry(string monsterId) {
            JournalEntryState entry;
            if (!Current.journal.TryGetValue(monsterId, out entry)) {
                entry = new JournalEntryState();
                Current.journal.Add(monsterId, entry);
            }
            return entry;
        }

        void RaiseJournalChanged(string monsterId) {
            if (OnJournalChanged != null) {
                OnJournalChanged(monsterId);
            }
        }

        void Save() {
            saves.Save(SaveSlot.Meta, Current);
            Log.Verbose(LogCat.Save, "meta saved");
        }
    }
}
