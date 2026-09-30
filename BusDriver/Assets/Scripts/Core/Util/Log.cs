using System;
using System.Diagnostics;
using UnityEngine;

namespace BusDriver.Core.Util {
    // Explicit values: the filter is a bit mask over these, and they may be stored (append-only, §4.1).
    public enum LogCat : int {
        Flow = 0, Save = 1, Route = 2, Economy = 3, Attention = 4, Threat = 5, Sanity = 6, Scare = 7,
        Death = 8, Audio = 9, Input = 10, Items = 11, Journal = 12, Build = 13, Content = 14
    }

    // The only way runtime code logs (§4.18). Info and Verbose obey the category filter; warnings
    // and errors always get through, because hiding a problem is never what the filter is for.
    public static class Log {
        const int AllCategories = ~0;

        // The one allowed piece of static mutable state besides GameRoot's bootstrap field (§4.1.5).
        // Domain reload is on (D24), so it resets on every Play.
        static int enabledMask = AllCategories;

        public static bool IsEnabled(LogCat cat) {
            return (enabledMask & (1 << (int)cat)) != 0;
        }

        public static void SetEnabled(LogCat cat, bool enabled) {
            if (enabled) {
                enabledMask |= 1 << (int)cat;
            }else {
                enabledMask &= ~(1 << (int)cat);
            }
        }

        public static void EnableAll() {
            enabledMask = AllCategories;
        }

        public static string Format(LogCat cat, string message) {
            return "[" + cat + "] " + message;
        }

        public static void Info(LogCat cat, string message, UnityEngine.Object context = null) {
            if (IsEnabled(cat)) {
                UnityEngine.Debug.Log(Format(cat, message), context);
            }
        }

        public static void Warn(LogCat cat, string message, UnityEngine.Object context = null) {
            UnityEngine.Debug.LogWarning(Format(cat, message), context);
        }

        public static void Error(LogCat cat, string message, UnityEngine.Object context = null) {
            UnityEngine.Debug.LogError(Format(cat, message), context);
        }

        public static void Exception(LogCat cat, Exception exception, UnityEngine.Object context = null) {
            UnityEngine.Debug.LogError(Format(cat, exception.ToString()), context);
        }

        [Conditional("BUSDRIVER_VERBOSE")]
        public static void Verbose(LogCat cat, string message, UnityEngine.Object context = null) {
            Info(cat, message, context);
        }

        // Session header so every Player.log starts with what produced it (§4.18).
        // Moves into GameRoot in T-M1-04.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LogSessionHeader() {
            Info(LogCat.Build, SessionHeader());
        }

        public static string SessionHeader() {
            return $"session: {Application.productName} {BuildLabel.Current} | Unity {Application.unityVersion} | "
                + $"{SystemInfo.operatingSystem} | {Application.platform}{(UnityEngine.Debug.isDebugBuild ? " (development)" : "")}";
        }
    }
}
