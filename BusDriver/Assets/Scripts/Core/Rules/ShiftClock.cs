using BusDriver.Core.Util;

namespace BusDriver.Core.Rules {
    // The shift's clock (§2.5): game-seconds since midnight, starting at the route's shift start and
    // running gameSecondsPerRealSecond times faster than real time, but only while the shift is in
    // Driving (which includes on foot). ShiftClockDriver ticks it with scaled time, so pausing stops it.
    public sealed class ShiftClock {
        public double StartGameSeconds { get; private set; }
        public double GameSecondsPerRealSecond { get; private set; }
        public double NowGameSeconds { get; private set; }
        // Real (scaled) seconds the clock has run for
        public double ElapsedRealSeconds { get { return (NowGameSeconds - StartGameSeconds) / GameSecondsPerRealSecond; } }

        public ShiftClock(double startGameSeconds, double gameSecondsPerRealSecond) {
            StartGameSeconds = startGameSeconds;
            GameSecondsPerRealSecond = gameSecondsPerRealSecond > 0.0 ? gameSecondsPerRealSecond : 1.0;
            NowGameSeconds = startGameSeconds;
        }

        // One frame: the clock only moves in Driving, and never backwards
        public void Tick(double realSeconds, bool driving) {
            if (!driving || realSeconds <= 0.0) {
                return;
            }
            NowGameSeconds += realSeconds * GameSecondsPerRealSecond;
        }

        public string Format(ClockFormat format) {
            return ClockText.Format(NowGameSeconds, format);
        }
    }
}
