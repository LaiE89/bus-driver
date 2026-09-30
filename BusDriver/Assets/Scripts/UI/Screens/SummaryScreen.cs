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
    // each stop's scheduled and actual time with its rating, the kick and delivery counts, and
    // Continue. It opens when the shift enters Summary and draws everything from the director's
    // NightResult. Esc does nothing here (no pause in the Summary, §4.11); Continue is focused, so
    // Submit alone moves on (§4.10 rule 4).
    public sealed class SummaryScreen : ScreenView, IGameBindable, IShiftBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text ledgerLabels;
        [SerializeField] TMP_Text ledgerAmounts;
        [SerializeField] TMP_Text walletText;
        [SerializeField] TMP_Text arrivalStops;
        [SerializeField] TMP_Text arrivalScheduled;
        [SerializeField] TMP_Text arrivalActual;
        [SerializeField] TMP_Text arrivalRatings;
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
            director.ConfirmSummary();
        }

        public void Show(NightResult result) {
            titleText.text = string.Format(UIText.SummaryTitle, result.nightIndex);
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
            walletText.text = string.Format(UIText.SummaryWallet, Money.Format(result.walletBeforeCents), Money.Format(result.WalletAfterCents));

            StringBuilder stops = new StringBuilder(UIText.ArrivalsStop).Append('\n');
            StringBuilder scheduled = new StringBuilder(UIText.ArrivalsScheduled).Append('\n');
            StringBuilder actual = new StringBuilder(UIText.ArrivalsActual).Append('\n');
            StringBuilder ratings = new StringBuilder(UIText.ArrivalsRating).Append('\n');
            for (int i = 0; i < result.arrivals.Count; i++) {
                NightArrival arrival = result.arrivals[i];
                stops.Append(arrival.displayName).Append('\n');
                scheduled.Append(ClockText.Format(arrival.scheduledGameSeconds, ClockFormat.Dash)).Append('\n');
                actual.Append(double.IsNaN(arrival.arrivalGameSeconds) ? UIText.ArrivalNone : ClockText.Format(arrival.arrivalGameSeconds, ClockFormat.Dash)).Append('\n');
                ratings.Append(RatingText(arrival.rating)).Append('\n');
            }
            arrivalStops.text = stops.ToString();
            arrivalScheduled.text = scheduled.ToString();
            arrivalActual.text = actual.ToString();
            arrivalRatings.text = ratings.ToString();
            countsText.text = string.Format(UIText.SummaryCounts, result.stats.monstersKicked, result.stats.innocentsKicked, result.stats.ridersDelivered);
        }

        static void Line(StringBuilder labels, StringBuilder amounts, string label, int cents) {
            labels.Append(label).Append('\n');
            amounts.Append(Money.FormatDelta(cents)).Append('\n');
        }

        public static string RatingText(ArrivalRating rating) {
            switch (rating) {
                case ArrivalRating.Early: return UIText.RatingEarly;
                case ArrivalRating.OnTime: return UIText.RatingOnTime;
                case ArrivalRating.Late: return UIText.RatingLate;
                case ArrivalRating.Missed: return UIText.RatingMissed;
                default: return UIText.ArrivalNone;
            }
        }
    }
}
