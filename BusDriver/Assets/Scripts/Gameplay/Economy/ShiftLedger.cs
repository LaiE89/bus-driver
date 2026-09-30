using System;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;

namespace BusDriver.Gameplay.Economy {
    // The night's money (§2.7, §4.6): every entry, the totals, and the wallet as it stands now. The
    // run's wallet in RunState only takes the night's net when the night is completed (RunFlow), so
    // a death mid-night never half-applies it (D3).
    public sealed class ShiftLedger {
        readonly Ledger core = new Ledger();

        public IReadOnlyList<LedgerEntry> Entries { get { return core.Entries; } }
        public LedgerTotals Totals { get { return core.Totals; } }
        // RunState.walletCents when the night began
        public int WalletBeforeCents { get; }
        public int WalletNowCents { get { return WalletBeforeCents + core.Totals.NetCents; } }

        public event Action<LedgerEntry> OnEntry;

        public ShiftLedger(int walletBeforeCents) {
            WalletBeforeCents = walletBeforeCents;
        }

        public void Record(LedgerEntry entry) {
            core.Record(entry);
            Log.Info(LogCat.Economy, $"{entry.Kind} {Money.FormatDelta(entry.AmountCents)} rider {entry.RiderId} at {entry.StopId}");
            if (OnEntry != null) {
                OnEntry(entry);
            }
        }

        // The F1 "Economy" section
        public void WriteDebug(StringBuilder text) {
            LedgerTotals totals = core.Totals;
            text.Append("wallet ").Append(Money.Format(WalletBeforeCents)).Append(" → ").Append(Money.Format(WalletNowCents))
                .Append("  (night ").Append(Money.FormatDelta(totals.NetCents)).Append(")\n");
            for (int i = 0; i < LedgerTotals.KindCount; i++) {
                LedgerKind kind = (LedgerKind)i;
                if (totals.Count(kind) > 0) {
                    text.Append(kind).Append(" ×").Append(totals.Count(kind)).Append(' ')
                        .Append(Money.FormatDelta(totals.Cents(kind))).Append('\n');
                }
            }
        }
    }
}
