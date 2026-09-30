using System;
using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // `--selftest` smoke check for built players (§4.20, T-M0-07): once the first scene is up,
    // wait 3 s, then quit with 0 and "[SELFTEST] OK <label>". Any exception logged before that
    // quits with 1 and "[SELFTEST] FAIL <message>". GameRoot adds it only when the flag is given.
    public sealed class SelfTestRunner : MonoBehaviour {
        public const string Flag = "--selftest";
        const float WaitSeconds = 3f;

        string label;
        float elapsed;
        bool finished;

        public static bool Requested(string[] commandLine) {
            return Array.IndexOf(commandLine, Flag) >= 0;
        }

        public void Begin(string buildLabel) {
            label = buildLabel;
        }

        void Awake() {
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string message, string stackTrace, LogType type) {
            if (type == LogType.Exception) {
                Finish(false, message);
            }
        }

        void Update() {
            // Unscaled, so a paused or slowed game can't stall the check
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= WaitSeconds) {
                Finish(true, label);
            }
        }

        void Finish(bool ok, string detail) {
            if (finished) {
                return;
            }
            finished = true;
            if (ok) {
                Log.Info(LogCat.Build, "[SELFTEST] OK " + detail);
            }else {
                Log.Error(LogCat.Build, "[SELFTEST] FAIL " + detail);
            }
            Application.Quit(ok ? 0 : 1);
        }
    }
}
