using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // One money event of the night (§2.7): every change to the money creates one, and the Summary
    // is built from them alone. Refunds and killed riders carry negative amounts.
    public readonly struct LedgerEntry {
        public readonly LedgerKind Kind;
        public readonly int AmountCents;
        public readonly string RiderId;
        public readonly string StopId;
        public readonly double GameTime;

        public LedgerEntry(LedgerKind kind, int amountCents, string riderId, string stopId, double gameTime) {
            Kind = kind;
            AmountCents = amountCents;
            RiderId = riderId ?? "";
            StopId = stopId ?? "";
            GameTime = gameTime;
        }

        public override string ToString() {
            return $"{Kind} {AmountCents} {RiderId} {StopId}";
        }
    }

    // The night's totals by kind, and the net change to the wallet
    public sealed class LedgerTotals {
        public const int KindCount = 5;

        readonly int[] cents = new int[KindCount];
        readonly int[] counts = new int[KindCount];

        public int Cents(LedgerKind kind) { return cents[(int)kind]; }
        public int Count(LedgerKind kind) { return counts[(int)kind]; }

        public int NetCents {
            get {
                int net = 0;
                for (int i = 0; i < KindCount; i++) {
                    net += cents[i];
                }
                return net;
            }
        }

        internal void Add(LedgerEntry entry) {
            cents[(int)entry.Kind] += entry.AmountCents;
            counts[(int)entry.Kind]++;
        }

        public LedgerTotals Clone() {
            LedgerTotals copy = new LedgerTotals();
            System.Array.Copy(cents, copy.cents, KindCount);
            System.Array.Copy(counts, copy.counts, KindCount);
            return copy;
        }
    }

    // The night's ledger (§2.7, §4.6 ShiftLedger's core): entries in order, and running totals
    public sealed class Ledger {
        readonly List<LedgerEntry> entries = new List<LedgerEntry>();

        public IReadOnlyList<LedgerEntry> Entries { get { return entries; } }
        public LedgerTotals Totals { get; } = new LedgerTotals();

        public void Record(LedgerEntry entry) {
            entries.Add(entry);
            Totals.Add(entry);
        }
    }
}
