using System.Globalization;

namespace BusDriver.Core.Util {
    // Money is integer cents everywhere (§0.2). Negative amounts use a true minus sign (U+2212),
    // and deltas always carry a sign so colour is never the only cue (§2.23).
    public static class Money {
        public const char MinusSign = '−';

        // 350 → "$3.50", -350 → "−$3.50"
        public static string Format(long cents) {
            string body = Body(cents);
            return cents < 0 ? MinusSign + body : body;
        }

        // 350 → "+$3.50", -350 → "−$3.50", 0 → "$0.00"
        public static string FormatDelta(long cents) {
            if (cents > 0) {
                return "+" + Body(cents);
            }
            return Format(cents);
        }

        static string Body(long cents) {
            // Written this way so long.MinValue has an absolute value too
            ulong abs = cents < 0 ? (ulong)(-(cents + 1)) + 1 : (ulong)cents;
            return "$" + (abs / 100).ToString("#,0", CultureInfo.InvariantCulture) + "."
                + (abs % 100).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
