using System;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;

namespace BusDriver.Gameplay.Items {
    // The run's three consumable slots and the one item they can hold: coffee (§2.18).
    // Slots live on RunState, so a thermos bought tonight is still there next night.
    public sealed class ItemService {
        public const string CoffeeId = "coffee";

        ShiftServices shift;

        public event Action OnChanged;

        public void Init(ShiftServices services) {
            shift = services;
            EnsureSlots();
        }

        public string[] Slots {
            get {
                EnsureSlots();
                return shift.Setup.Run.slots;
            }
        }

        public int CoffeePrice {
            get { return shift != null && shift.Balance != null ? shift.Balance.coffeeCents : 600; }
        }

        public float CoffeeSanity {
            get { return shift != null && shift.Balance != null ? shift.Balance.coffeeSanity : 30f; }
        }

        public int WalletCents {
            get { return shift != null && shift.Ledger != null ? shift.Ledger.WalletNowCents : 0; }
        }

        public bool CanAffordCoffee {
            get { return WalletCents >= CoffeePrice; }
        }

        public int EmptySlot {
            get {
                string[] slots = Slots;
                for (int i = 0; i < slots.Length; i++) {
                    if (string.IsNullOrEmpty(slots[i])) {
                        return i;
                    }
                }
                return -1;
            }
        }

        // Pays from the night's wallet and puts one coffee in the first free slot
        public bool TryBuyCoffee() {
            if (shift == null || shift.Ledger == null || !CanAffordCoffee || EmptySlot < 0) {
                return false;
            }
            if (!shift.Ledger.TrySpend(CoffeePrice)) {
                return false;
            }
            Slots[EmptySlot] = CoffeeId;
            Log.Info(LogCat.Economy, "bought coffee, wallet " + Money.Format(WalletCents));
            Raise();
            return true;
        }

        // Drinks the coffee in that slot. Sanity is clamped by SanitySystem.
        public bool Use(int index) {
            if (shift == null || shift.Director == null || shift.Director.State != ShiftState.Driving) {
                return false;
            }
            if (shift.Game != null && (shift.Game.Pause.IsPaused || shift.Game.Input.Context != InputContext.Driving)) {
                return false;
            }
            string[] slots = Slots;
            if (index < 0 || index >= slots.Length || slots[index] != CoffeeId) {
                return false;
            }
            slots[index] = null;
            if (shift.Sanity != null) {
                shift.Sanity.Apply(CoffeeSanity, SanityReason.Coffee);
            }
            Log.Info(LogCat.Sanity, "coffee, sanity " + (shift.Sanity != null ? shift.Sanity.Value.ToString("0.0") : "?"));
            Raise();
            return true;
        }

        public static string DisplayName(string id) {
            return id == CoffeeId ? "COFFEE" : "";
        }

        void EnsureSlots() {
            if (shift == null || shift.Setup == null || shift.Setup.Run == null) {
                return;
            }
            RunState run = shift.Setup.Run;
            if (run.slots != null && run.slots.Length == RunState.SlotCount) {
                return;
            }
            string[] next = new string[RunState.SlotCount];
            if (run.slots != null) {
                int count = run.slots.Length < next.Length ? run.slots.Length : next.Length;
                for (int i = 0; i < count; i++) {
                    next[i] = run.slots[i];
                }
            }
            run.slots = next;
        }

        void Raise() {
            if (OnChanged != null) {
                OnChanged();
            }
        }
    }
}
