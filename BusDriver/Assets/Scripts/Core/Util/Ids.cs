using System.Text.RegularExpressions;

namespace BusDriver.Core.Util {
    // Content ids (§4.1.11): lower_snake_case. Sounds, scares and hallucinations use dotted,
    // namespaced ids ("scare.starer.lens"), so each dot-separated segment is lower_snake_case (D53).
    public static class Ids {
        static readonly Regex Snake = new Regex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$");
        static readonly Regex Dotted = new Regex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*(?:\.[a-z0-9]+(?:_[a-z0-9]+)*)*$");

        public static bool IsValid(string id) {
            return !string.IsNullOrEmpty(id) && Dotted.IsMatch(id);
        }

        // A single segment: stop, route, look, monster and item ids
        public static bool IsSnakeCase(string id) {
            return !string.IsNullOrEmpty(id) && Snake.IsMatch(id);
        }
    }
}
