using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Screens {
    // Game Over (§2.21): the cause, its hint, the run's totals, and New Run / Main Menu. It opens
    // when the death presenter hands the shift to GameOver. Esc does nothing (Game Over can't be
    // paused, §4.11); New Run is focused, so Submit alone starts over (§4.10 rule 4).
    public sealed class GameOverScreen : ScreenView, IGameBindable, IShiftBindable {
        [SerializeField] ScreenRouter router;
        [SerializeField] TMP_Text causeText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] TMP_Text statsText;
        [SerializeField] Button newRunButton;
        [SerializeField] Button mainMenuButton;

        GameServices game;
        ShiftServices shift;

        public string CauseText { get { return causeText != null ? causeText.text : ""; } }
        public string HintText { get { return hintText != null ? hintText.text : ""; } }
        public string StatsText { get { return statsText != null ? statsText.text : ""; } }
        public Button NewRunButton { get { return newRunButton; } }
        public Button MainMenuButton { get { return mainMenuButton; } }

        public override bool CancelPops { get { return false; } }

        protected override void Awake() {
            base.Awake();
            newRunButton.onClick.AddListener(NewRun);
            mainMenuButton.onClick.AddListener(MainMenu);
        }

        public void Bind(GameServices services) {
            game = services;
        }

        public void Bind(ShiftServices services) {
            shift = services;
            shift.Director.OnStateChanged += HandleStateChanged;
        }

        void OnDestroy() {
            if (shift != null) {
                shift.Director.OnStateChanged -= HandleStateChanged;
            }
        }

        void HandleStateChanged(ShiftState state) {
            if (state != ShiftState.GameOver || shift.Death == null || shift.Death.Report == null) {
                return;
            }
            Show(shift.Death.Report);
            router.Push(this);
            if (game != null) {
                game.Audio.Play(SoundIds.MusGameoverSting);
            }
        }

        public void NewRun() {
            Click();
            if (game != null) {
                game.Flow.NewRun();
            }
        }

        public void MainMenu() {
            Click();
            if (game != null) {
                game.Flow.LoadMenu();
            }
        }

        void Click() {
            if (game != null) {
                game.Audio.Play(SoundIds.UiClick);
            }
        }

        public void Show(DeathReport report) {
            GameRootConfig config = game != null ? game.Config : null;
            MonsterDefinition monster = config != null && !string.IsNullOrEmpty(report.SourceId) ? config.Monster(report.SourceId) : null;
            causeText.text = CauseLine(report.Cause, monster);
            hintText.text = HintLine(report.Cause, monster);
            statsText.text = StatsLine();
        }

        // §2.21: THE STARER GOT YOU / YOUR MIND WENT DARK / YOU WENT OVER THE EDGE
        public static string CauseLine(DeathCause cause, MonsterDefinition monster) {
            switch (cause) {
                case DeathCause.MonsterKill:
                    string name = monster != null && !string.IsNullOrEmpty(monster.displayName) ? monster.displayName : UIText.SomethingName;
                    return string.Format(UIText.GameOverMonster, name.ToUpperInvariant());
                case DeathCause.SanityZero: return UIText.GameOverSanity;
                case DeathCause.Fall: return UIText.GameOverFall;
                case DeathCause.Abandoned: return UIText.GameOverAbandoned;
                default: return UIText.GameOverTitle;
            }
        }

        // The killer's journal hint; the fall and the sanity blackout have their own (§2.21)
        public static string HintLine(DeathCause cause, MonsterDefinition monster) {
            if (monster != null && monster.journal != null && !string.IsNullOrEmpty(monster.journal.hint)
                && (cause == DeathCause.MonsterKill || cause == DeathCause.SanityZero)) {
                return monster.journal.hint;
            }
            switch (cause) {
                case DeathCause.Fall: return UIText.HintFall;
                case DeathCause.SanityZero: return UIText.HintSanity;
                default: return "";
            }
        }

        // The run so far plus this night, which ended before its Summary could add it
        string StatsLine() {
            RunState run = game != null ? game.Flow.Run : null;
            int nightsSurvived = run != null ? Mathf.Max(0, run.nightIndex - 1) : 0;
            int fares = run != null ? run.stats.faresCents : 0;
            int monsters = run != null ? run.stats.monstersKicked : 0;
            int innocents = run != null ? run.stats.innocentsKicked : 0;
            if (shift != null) {
                if (shift.Ledger != null) {
                    fares += shift.Ledger.Totals.Cents(LedgerKind.Fare);
                }
                if (shift.Riders != null) {
                    IReadOnlyList<RiderRecord> riders = shift.Riders.All;
                    for (int i = 0; i < riders.Count; i++) {
                        if (riders[i].Status != RiderStatus.Kicked) {
                            continue;
                        }
                        if (riders[i].IsMonster) {
                            monsters++;
                        }else {
                            innocents++;
                        }
                    }
                }
            }
            return string.Format(UIText.GameOverStats, nightsSurvived, Money.Format(fares), monsters, innocents);
        }
    }
}
