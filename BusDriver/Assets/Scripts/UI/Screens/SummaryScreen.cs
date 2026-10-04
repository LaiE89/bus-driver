using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Screens {
    // The night's Summary (§2.21): the headline, the grouped ledger, the wallet before and after,
    // each stop's scheduled and actual time, the kick and delivery counts, and Continue — or, when
    // the night quota was missed, a failed headline and Main Menu. Arrival ratings are not shown.
    // Esc does nothing here (no pause in the Summary, §4.11); the primary button is focused.
    public sealed class SummaryScreen : ScreenView, IGameBindable, IShiftBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text ledgerLabels;
        [SerializeField] TMP_Text ledgerAmounts;
        [SerializeField] TMP_Text walletText;
        [SerializeField] TMP_Text arrivalStops;
        [SerializeField] TMP_Text arrivalScheduled;
        [SerializeField] TMP_Text arrivalActual;
        [SerializeField] TMP_Text countsText;
        [SerializeField] Button continueButton;

        GameServices game;
        ShiftDirector director;
        int fareCents = 350;

        // Tests read what the screen shows
        public string LedgerAmountsText { get { return ledgerAmounts != null ? ledgerAmounts.text : ""; } }
        public string WalletText { get { return walletText != null ? walletText.text : ""; } }
        public Button ContinueButton { get { return continueButton; } }

        public override bool CancelPops { get { return false; } }

        protected override void Awake() {
            base.Awake();
            continueButton.onClick.AddListener(Continue);
        }

        public void Bind(GameServices services) {
            game = services;
        }

        public void Bind(ShiftServices shift) {
            director = shift.Director;
            if (shift.Balance != null) {
                fareCents = shift.Balance.fareCents;
            }
            director.OnStateChanged += HandleStateChanged;
        }

        void OnDestroy() {
            if (director != null) {
                director.OnStateChanged -= HandleStateChanged;
            }
        }

        void HandleStateChanged(ShiftState state) {
            if (state == ShiftState.Summary && director.Result != null) {
                Show(director.Result);
                router.Push(this);
            }
        }

        public void Continue() {
            if (director == null || director.State != ShiftState.Summary) {
                return;
            }
            if (game != null) {
                game.Audio.Play(SoundIds.UiClick);
            }
            if (router.Top == this) {
                router.Pop();
            }
            if (director.Result != null && !director.Result.quotaMet) {
                if (game != null) {
                    game.Flow.LoadMenu();
                }
                return;
            }
            director.ConfirmSummary();
        }

        public void Show(NightResult result) {
            bool failed = !result.quotaMet;
            titleText.text = string.Format(failed ? UIText.SummaryFailedTitle : UIText.SummaryTitle, result.nightIndex);
            LedgerTotals totals = result.totals;
            StringBuilder labels = new StringBuilder();
            StringBuilder amounts = new StringBuilder();
            int fares = totals.Count(LedgerKind.Fare);
            int each = fares > 0 ? totals.Cents(LedgerKind.Fare) / fares : fareCents;
            Line(labels, amounts, string.Format(UIText.LedgerFares, fares, Money.Format(each)), totals.Cents(LedgerKind.Fare));
            Line(labels, amounts, UIText.LedgerTips, totals.Cents(LedgerKind.Tip));
            Line(labels, amounts, UIText.LedgerRefunds, totals.Cents(LedgerKind.Refund));
            Line(labels, amounts, UIText.LedgerLost, totals.Cents(LedgerKind.Lost));
            Line(labels, amounts, UIText.LedgerBounties, totals.Cents(LedgerKind.Bounty));
            labels.Append('\n').Append(UIText.LedgerTotal);
            amounts.Append('\n').Append(Money.FormatDelta(totals.NetCents));
            ledgerLabels.text = labels.ToString();
            ledgerAmounts.text = amounts.ToString();
            if (failed) {
                walletText.text = string.Format(UIText.SummaryQuota, Money.Format(result.quotaCents), Money.Format(result.NetCents))
                    + "\n" + UIText.SummaryQuotaHint;
            }else {
                walletText.text = string.Format(UIText.SummaryWallet, Money.Format(result.walletBeforeCents), Money.Format(result.WalletAfterCents));
            }

            StringBuilder stops = new StringBuilder(UIText.ArrivalsStop).Append('\n');
            StringBuilder scheduled = new StringBuilder(UIText.ArrivalsScheduled).Append('\n');
            StringBuilder actual = new StringBuilder(UIText.ArrivalsActual).Append('\n');
            for (int i = 0; i < result.arrivals.Count; i++) {
                NightArrival arrival = result.arrivals[i];
                stops.Append(arrival.displayName).Append('\n');
                scheduled.Append(ClockText.Format(arrival.scheduledGameSeconds, ClockFormat.Dash)).Append('\n');
                actual.Append(double.IsNaN(arrival.arrivalGameSeconds) ? UIText.ArrivalNone : ClockText.Format(arrival.arrivalGameSeconds, ClockFormat.Dash)).Append('\n');
            }
            arrivalStops.text = stops.ToString();
            arrivalScheduled.text = scheduled.ToString();
            arrivalActual.text = actual.ToString();
            countsText.text = string.Format(UIText.SummaryCounts, result.stats.monstersKicked, result.stats.innocentsKicked, result.stats.ridersDelivered);
            TMP_Text buttonLabel = continueButton != null ? continueButton.GetComponentInChildren<TMP_Text>() : null;
            if (buttonLabel != null) {
                buttonLabel.text = failed ? UIText.MainMenu : UIText.Continue;
            }
        }

        static void Line(StringBuilder labels, StringBuilder amounts, string label, int cents) {
            labels.Append(label).Append('\n');
            amounts.Append(Money.FormatDelta(cents)).Append('\n');
        }
    }
}
