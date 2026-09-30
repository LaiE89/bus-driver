using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Validation {
    // BuildAll's last step (§4.15): every required reference is assigned, ids are valid and
    // unique, and every id the code names has content behind it. Each check adds one line per
    // problem; BuildAll fails (exit code 1 in batch mode) on any. Tickets add their checks to
    // Checks as their content arrives.
    public static class ContentValidator {
        static readonly Action<List<string>>[] Checks = {
            CheckGameRootConfig,
            CheckSoundLibrary,
        };

        public static List<string> Validate() {
            List<string> problems = new List<string>();
            foreach (Action<List<string>> check in Checks) {
                check(problems);
            }
            return problems;
        }

        [MenuItem("Tools/Bus Driver/Validate Content")]
        public static bool ValidateAndLog() {
            List<string> problems = Validate();
            foreach (string problem in problems) {
                Debug.LogError("[VALIDATE] " + problem);
            }
            Debug.Log(problems.Count == 0 ? "[VALIDATE] OK" : $"[VALIDATE] FAIL: {problems.Count} problem(s)");
            return problems.Count == 0;
        }

        // -------------------------------------------------------------------- checks

        static GameRootConfig Config {
            get { return AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath); }
        }

        static void CheckGameRootConfig(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null) {
                problems.Add("missing " + GameRootConfig.AssetPath);
                return;
            }
            Require(problems, config.mixer, "GameRootConfig.mixer");
            Require(problems, config.inputActions, "GameRootConfig.inputActions");
            Require(problems, config.soundLibrary, "GameRootConfig.soundLibrary");
            Require(problems, config.audioConfig, "GameRootConfig.audioConfig");
            Require(problems, config.uiTheme, "GameRootConfig.uiTheme");
        }

        static void CheckSoundLibrary(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null || config.soundLibrary == null) {
                return;
            }
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < config.soundLibrary.sounds.Count; i++) {
                SoundDefinition sound = config.soundLibrary.sounds[i];
                if (sound == null) {
                    problems.Add($"SoundLibrary entry {i} is empty");
                    continue;
                }
                if (!Ids.IsValid(sound.id)) {
                    problems.Add($"sound '{sound.name}' has an invalid id '{sound.id}'");
                }else if (!seen.Add(sound.id)) {
                    problems.Add($"sound id '{sound.id}' is used twice");
                }
            }
        }

        public static void Require(List<string> problems, UnityEngine.Object value, string what) {
            if (value == null) {
                problems.Add(what + " is not assigned");
            }
        }
    }
}
