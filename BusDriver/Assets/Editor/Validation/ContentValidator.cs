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
            CheckRoutes,
            CheckLighting,
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
                if (sound.clips == null || sound.clips.Length == 0 || System.Array.IndexOf(sound.clips, null) >= 0) {
                    problems.Add($"sound '{sound.id}' has no clip (or an empty slot)");
                }
            }
            // Every id the code can name has a definition behind it (§4.12)
            foreach (string id in SoundIds.All) {
                if (!seen.Contains(id)) {
                    problems.Add($"SoundIds.{id} has no SoundDefinition in the SoundLibrary");
                }
            }
        }

        // §3, §4.8: ids valid and unique, stops in order and each on a straight, everything inside the route
        static void CheckRoutes(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null) {
                return;
            }
            if (config.routes == null || config.routes.Length == 0) {
                problems.Add("GameRootConfig.routes is empty");
                return;
            }
            HashSet<string> routeIds = new HashSet<string>();
            for (int i = 0; i < config.routes.Length; i++) {
                RouteDefinition route = config.routes[i];
                if (route == null) {
                    problems.Add($"GameRootConfig.routes entry {i} is empty");
                    continue;
                }
                if (!Ids.IsSnakeCase(route.id)) {
                    problems.Add($"route '{route.name}' has an invalid id '{route.id}'");
                }else if (!routeIds.Add(route.id)) {
                    problems.Add($"route id '{route.id}' is used twice");
                }
                CheckRoute(route, problems);
            }
        }

        // The one night preset every generated scene uses (T-M2-05)
        static void CheckLighting(List<string> problems) {
            string path = DataRootPath("Lighting/Night.asset");
            NightLightingPreset preset = AssetDatabase.LoadAssetAtPath<NightLightingPreset>(path);
            if (preset == null) {
                problems.Add("missing " + path);
                return;
            }
            Require(problems, preset.postProfile, "NightLightingPreset.postProfile");
            if (preset.referenceBrightness <= 0f) {
                problems.Add("NightLightingPreset.referenceBrightness must be positive");
            }
        }

        static string DataRootPath(string relative) {
            return "Assets/Data/" + relative;
        }

        // Half the kerb-clear length: a stop needs a straight for ±12 m (§3.2)
        const float StopClearHalfLength = 12f;

        public static void CheckRoute(RouteDefinition route, List<string> problems) {
            string r = "route '" + route.id + "'";
            if (route.segments == null || route.segments.Length == 0) {
                problems.Add(r + " has no segments");
                return;
            }
            float[] starts = new float[route.segments.Length + 1];
            for (int i = 0; i < route.segments.Length; i++) {
                RouteSegment segment = route.segments[i];
                if (segment == null) {
                    problems.Add($"{r} segment {i + 1} is empty");
                    return;
                }
                if (segment.kind == SegmentKind.Straight && segment.length <= 0f) {
                    problems.Add($"{r} segment {i + 1}: a straight needs a positive length");
                }
                if (segment.kind == SegmentKind.Arc && (segment.radius <= 0f || segment.angleDeg == 0f || Mathf.Abs(segment.angleDeg) >= 360f)) {
                    problems.Add($"{r} segment {i + 1}: an arc needs a positive radius and a non-zero angle under 360°");
                }
                starts[i + 1] = starts[i] + segment.Length;
            }
            float total = starts[route.segments.Length];
            if (route.roadWidth <= 0f || route.cliffRoadWidth <= 0f || route.shoulderWidth < 0f) {
                problems.Add(r + " has an invalid road or shoulder width");
            }

            HashSet<string> stopIds = new HashSet<string>();
            float previous = float.NegativeInfinity;
            for (int i = 0; i < route.stops.Length; i++) {
                RouteStop stop = route.stops[i];
                if (stop == null) {
                    problems.Add($"{r} stop {i} is empty");
                    continue;
                }
                string s = $"{r} stop '{stop.stopId}'";
                if (!Ids.IsSnakeCase(stop.stopId)) {
                    problems.Add($"{r} stop {i} has an invalid id '{stop.stopId}'");
                }else if (!stopIds.Add(stop.stopId)) {
                    problems.Add($"{s} is listed twice");
                }
                if (string.IsNullOrEmpty(stop.displayName)) {
                    problems.Add(s + " has no display name");
                }
                if (stop.distance <= previous) {
                    problems.Add(s + " is out of route order");
                }
                previous = stop.distance;
                if (stop.distance <= 0f || stop.distance >= total) {
                    problems.Add($"{s} at {stop.distance} m is outside the route (0–{total:0.##} m)");
                }else if (!OnOneStraight(route, starts, stop.distance - StopClearHalfLength, stop.distance + StopClearHalfLength)) {
                    problems.Add($"{s} at {stop.distance} m isn't on a straight for ±{StopClearHalfLength} m");
                }
            }
            if (route.stops.Length == 0) {
                problems.Add(r + " has no stops");
            }else {
                RouteStop last = route.stops[route.stops.Length - 1];
                if (last == null || last.stopId != route.terminusStopId) {
                    problems.Add($"{r} terminusStopId '{route.terminusStopId}' isn't its last stop");
                }
            }

            for (int i = 0; i < route.stubs.Length; i++) {
                RouteStub stub = route.stubs[i];
                if (stub == null || stub.distance <= 0f || stub.distance >= total || stub.length <= 0f || stub.width <= 0f
                    || stub.angleDeg <= 0f || stub.angleDeg >= 180f) {
                    problems.Add($"{r} stub {i} is invalid or outside the route");
                }
            }
            for (int i = 0; i < route.zones.Length; i++) {
                RouteZone zone = route.zones[i];
                if (zone == null || zone.start >= zone.end || zone.start < 0f || zone.end > total) {
                    problems.Add($"{r} zone {i} has an invalid range");
                }else if (zone.span != ZoneSpan.Across && zone.edgeWidth <= 0f) {
                    problems.Add($"{r} zone {i} ({zone.kind}) is an edge strip with no width");
                }
            }
            for (int i = 0; i < route.signs.Length; i++) {
                RouteSign sign = route.signs[i];
                if (sign == null || sign.distance < 0f || sign.distance > total) {
                    problems.Add($"{r} sign {i} is outside the route");
                }
            }
            RouteSchedule schedule = route.schedule;
            if (schedule == null || schedule.scheduleSpeed <= 0f || schedule.gameSecondsPerRealSecond <= 0f
                || schedule.dwellAllowanceSeconds < 0f || schedule.missedStopMargin <= 0f) {
                problems.Add(r + " has an invalid schedule");
            }
            if (route.depotSpawnDistance <= 0f || route.depotSpawnDistance >= total) {
                problems.Add(r + " has its depot spawn outside the route");
            }
        }

        static bool OnOneStraight(RouteDefinition route, float[] starts, float from, float to) {
            for (int i = 0; i < route.segments.Length; i++) {
                if (from >= starts[i] && to <= starts[i + 1]) {
                    return route.segments[i].kind == SegmentKind.Straight;
                }
            }
            return false;
        }

        public static void Require(List<string> problems, UnityEngine.Object value, string what) {
            if (value == null) {
                problems.Add(what + " is not assigned");
            }
        }
    }
}
