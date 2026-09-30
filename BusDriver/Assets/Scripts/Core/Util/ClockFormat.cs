using System;

namespace BusDriver.Core.Util {
    // The two displays of the one shift clock (§2.5)
    public enum ClockFormat : int { Dash = 0, Cctv = 1 }

    public static class ClockText {
        public const double SecondsPerDay = 86400.0;

        // Game-seconds since midnight → "12:34 AM" (Dash) or "12:34:56 AM" (Cctv).
        // Seconds are truncated, so the display never runs ahead of the clock.
        public static string Format(double gameSeconds, ClockFormat format) {
            double wrapped = gameSeconds % SecondsPerDay;
            if (wrapped < 0) {
                wrapped += SecondsPerDay;
            }
            int total = (int)Math.Floor(wrapped);
            int hours24 = total / 3600;
            int minutes = total / 60 % 60;
            int seconds = total % 60;
            int hours12 = hours24 % 12 == 0 ? 12 : hours24 % 12;
            string suffix = hours24 < 12 ? "AM" : "PM";
            if (format == ClockFormat.Cctv) {
                return $"{hours12:00}:{minutes:00}:{seconds:00} {suffix}";
            }
            return $"{hours12:00}:{minutes:00} {suffix}";
        }
    }
}
