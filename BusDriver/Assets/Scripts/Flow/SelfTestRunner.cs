using System;
using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // `--selftest` smoke check for built players (§4.20, T-M0-07): once the first scene is up,
    // wait 3 s, then quit with 0 and "[SELFTEST] OK <label>". Any exception logged before that
    // quits with 1 and "[SELFTEST] FAIL <message>". Does nothing without the flag.
    public sealed class SelfTestRunner : MonoBehaviour {
        public const string Flag = "--selftest";
        const float WaitSeconds = 3f;

        float elapsed;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested() {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) {
                return;
            }
            new GameObject("SelfTestRunner").AddComponent<SelfTestRunner>();
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
                Finish(true, BuildLabel.Current);
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
