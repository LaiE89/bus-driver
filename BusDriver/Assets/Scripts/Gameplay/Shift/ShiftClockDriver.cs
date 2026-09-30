using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Shift {
    // Advances the ShiftClock (§2.5, §4.6) with scaled time while the shift is in Driving, so the
    // intro card, the summary, death and the pause all hold it. The dash clock, the CCTV timestamp,
    // the GPS and the stop arrivals all read this one clock.
    public sealed class ShiftClockDriver : MonoBehaviour {
        ShiftDirector director;

        public ShiftClock Clock { get; private set; }
        public double NowGameSeconds { get { return Clock != null ? Clock.NowGameSeconds : double.NaN; } }

        // ShiftContext, step 2 of the Init order (§4.5)
        public void Init(ShiftServices shift) {
            director = shift.Director;
            RouteSchedule schedule = shift.Route.Route.schedule;
            Clock = new ShiftClock(schedule.shiftStartGameSeconds, schedule.gameSecondsPerRealSecond);
            shift.Debug.Register("Clock", WriteDebug);
        }

        public string Format(ClockFormat format) {
            return Clock != null ? Clock.Format(format) : "--:--";
        }

        void Update() {
            if (Clock == null) {
                return;
            }
            Clock.Tick(Time.deltaTime, director.State == ShiftState.Driving);
        }

        void WriteDebug(StringBuilder text) {
            text.Append(Clock.Format(ClockFormat.Cctv)).Append("  (").Append(Clock.NowGameSeconds.ToString("0.0"))
                .Append(" game-s, ").Append(Clock.ElapsedRealSeconds.ToString("0.0")).Append(" s driven)\n");
        }
    }
}
