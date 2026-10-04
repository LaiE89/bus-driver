#if UNITY_EDITOR || BUSDRIVER_DEV
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;

namespace BusDriver.Gameplay.Debug {
    // The monster and death cheats of the F1 overlay (§4.18, T-M4-10): spawn a monster seated, set
    // every monster's threat, force the most threatening one to Lethal, god mode, kill me by cause,
    // and win the night. The whole file only compiles in the Editor and development builds, so a
    // release build has none of it (BuildScriptsTests checks the gate).
    public static class DevCheats {
        const string Monsters = "Monsters";
        const string Death = "Death";
        const string Night = "Night";
        static readonly float[] ThreatSteps = { 0f, 25f, 50f, 75f, 99f };

        // ShiftContext, once every service is initialised
        public static void Register(ShiftServices shift) {
            DebugRegistry debug = shift.Debug;
            MonsterDefinition[] monsters = shift.Game.Config.monsters ?? new MonsterDefinition[0];
            for (int i = 0; i < monsters.Length; i++) {
                MonsterDefinition monster = monsters[i];
                if (monster != null) {
                    debug.AddCheat(new DebugCheat(Monsters, "Spawn " + monster.displayName + " seated", () => SpawnSeated(shift, monster)));
                }
            }
            for (int i = 0; i < ThreatSteps.Length; i++) {
                float threat = ThreatSteps[i];
                debug.AddCheat(new DebugCheat(Monsters, "Threat " + threat.ToString("0") + " (all)", () => SetThreat(shift, threat)));
            }
            debug.AddCheat(new DebugCheat(Monsters, "Force Lethal", () => ForceLethal(shift)));

            GodMode god = new GodMode();
            debug.AddCheat(new DebugCheat(Death, "God mode on/off", () => ToggleGodMode(shift, god)));
            debug.AddCheat(new DebugCheat(Death, "Kill me: monster", () => shift.Death.Die(DeathCause.MonsterKill, KillerId(shift))));
            debug.AddCheat(new DebugCheat(Death, "Kill me: sanity", () => shift.Death.Die(DeathCause.SanityZero)));
            debug.AddCheat(new DebugCheat(Death, "Kill me: fall", () => shift.Death.Die(DeathCause.Fall)));
            debug.AddCheat(new DebugCheat(Night, "Win the night", () => shift.Director.WinNightForDebug()));
        }

        // Stops every death but Abandoned; a monster whose telegraph runs out is let off, not expelled
        public sealed class GodMode : IDeathPreventer {
            public bool ExpelsMonster { get { return false; } }

            public bool TryPrevent(DeathReport report) {
                Log.Info(LogCat.Death, "god mode: " + report + " ignored");
                return true;
            }
        }

        public static bool ToggleGodMode(ShiftServices shift, GodMode god) {
            bool on = !shift.Death.HasPreventer(god);
            if (on) {
                shift.Death.AddPreventer(god);
            }else {
                shift.Death.RemovePreventer(god);
            }
            Log.Info(LogCat.Death, "god mode " + (on ? "on" : "off"));
            return on;
        }

        // In a free seat at the back, where a monster usually sits; anywhere if the back is full
        static void SpawnSeated(ShiftServices shift, MonsterDefinition monster) {
            BusSeat seat = shift.Cabin.FindFreeSeat(SeatZone.Rear, SeatZone.Mid);
            if (seat == null) {
                Log.Warn(LogCat.Threat, "no free seat for a cheat monster");
                return;
            }
            DebugRiders riders = shift.DebugRiders;
            RiderSpec spec = new RiderSpec {
                lookId = riders.NextLookId(),
                boardStopId = shift.Progress != null && shift.Progress.Next != null ? shift.Progress.Next.StopId : "",
                destinationStopId = "",
                monsterId = monster.id,
            };
            riders.SpawnSeated(spec, seat);
        }

        static void SetThreat(ShiftServices shift, float threat) {
            IReadOnlyList<MonsterBrain> active = shift.Monsters.Active;
            for (int i = 0; i < active.Count; i++) {
                if (active[i] != null && active[i] != shift.Monsters.KillOwner) {
                    active[i].Meter.Core.SetValue(threat);
                }
            }
        }

        static void ForceLethal(ShiftServices shift) {
            MonsterBrain target = Highest(shift);
            if (target != null) {
                target.Meter.Core.SetValue(100f);
            }
        }

        // The seated monster with the most threat, or null
        static MonsterBrain Highest(ShiftServices shift) {
            IReadOnlyList<MonsterBrain> active = shift.Monsters.Active;
            MonsterBrain best = null;
            for (int i = 0; i < active.Count; i++) {
                MonsterBrain brain = active[i];
                if (brain != null && brain.IsActive && (best == null || brain.Threat > best.Threat)) {
                    best = brain;
                }
            }
            return best;
        }

        static string KillerId(ShiftServices shift) {
            MonsterBrain killer = Highest(shift);
            return killer != null ? killer.MonsterId : "starer";
        }
    }
}
#endif
