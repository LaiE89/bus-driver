using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
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
            CheckEnvironment,
            CheckLooks,
            CheckNights,
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

        // Every environment kind resolves to a view and has its logic prefab (T-M2-06, §4.14)
        static void CheckEnvironment(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null) {
                return;
            }
            EnvironmentViewSet set = config.environment;
            if (set == null) {
                problems.Add("GameRootConfig.environment is not assigned");
                return;
            }
            HashSet<string> seen = new HashSet<string>();
            foreach (EnvironmentViewEntry entry in set.entries) {
                if (entry == null) {
                    problems.Add("EnvironmentViewSet has an empty entry");
                    continue;
                }
                if (!seen.Add(entry.kind)) {
                    problems.Add($"environment kind '{entry.kind}' is listed twice");
                }
                foreach (GameObject view in new[] { entry.greyboxView, entry.artView }) {
                    if (view != null && (view.GetComponentInChildren<Collider>(true) != null || view.GetComponentInChildren<Rigidbody>(true) != null)) {
                        problems.Add($"the view '{view.name}' of '{entry.kind}' holds a collider or rigidbody (they belong on the logic prefab)");
                    }
                }
            }
            foreach (string kind in EnvironmentKinds.All()) {
                if (set.Resolve(kind) == null) {
                    problems.Add($"environment kind '{kind}' has no view");
                }
                GameObject logic = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabBuilder.LogicPath(kind));
                EnvironmentPiece piece = logic != null ? logic.GetComponent<EnvironmentPiece>() : null;
                if (piece == null || piece.Kind != kind || piece.ViewSlot == null) {
                    problems.Add($"environment kind '{kind}' has no logic prefab with an EnvironmentPiece and a view slot");
                }
            }
        }

        // At least 12 looks (D34), ids valid and unique, every look in the config, art views real
        // views, and the greybox view the factory falls back to (T-M3-01, §4.14)
        static void CheckLooks(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null) {
                return;
            }
            HashSet<string> seen = new HashSet<string>();
            PassengerLookDefinition[] looks = config.looks ?? new PassengerLookDefinition[0];
            for (int i = 0; i < looks.Length; i++) {
                PassengerLookDefinition look = looks[i];
                if (look == null) {
                    problems.Add($"GameRootConfig.looks entry {i} is empty");
                    continue;
                }
                if (!Ids.IsSnakeCase(look.id)) {
                    problems.Add($"look '{look.name}' has an invalid id '{look.id}'");
                }else if (!seen.Add(look.id)) {
                    problems.Add($"look id '{look.id}' is used twice");
                }
                if (look.greybox == null || look.greybox.heightScale < 0.95f || look.greybox.heightScale > 1.05f) {
                    problems.Add($"look '{look.id}' has no greybox parameters or a height scale outside 0.95–1.05");
                }
                if (look.artView != null && look.artView.GetComponent<PassengerViewBase>() == null) {
                    problems.Add($"look '{look.id}' has an art view without a PassengerViewBase at its root");
                }
                GameObject art = look.artView;
                if (art != null && (art.GetComponentInChildren<Collider>(true) != null || art.GetComponentInChildren<Rigidbody>(true) != null)) {
                    problems.Add($"the art view of look '{look.id}' holds a collider or rigidbody");
                }
            }
            if (seen.Count < LookSeed.Count) {
                problems.Add($"GameRootConfig.looks has {seen.Count} looks; at least {LookSeed.Count} are needed (D34)");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:PassengerLookDefinition", new[] { DataRootPath(LookSeed.Folder) })) {
                PassengerLookDefinition look = AssetDatabase.LoadAssetAtPath<PassengerLookDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (look != null && System.Array.IndexOf(looks, look) < 0) {
                    problems.Add($"look '{look.id}' isn't listed in GameRootConfig.looks");
                }
            }
            GameObject greybox = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.GreyboxPassengerViewPath);
            if (greybox == null || greybox.GetComponent<GreyboxPassengerView>() == null) {
                problems.Add("missing the greybox passenger view " + PrefabBuilder.GreyboxPassengerViewPath);
            }
        }

        // Nights 1–5 in order; end stops, boarding and destination stops on the route and in order;
        // looks and monster ids known (§2.19, T-M3-03)
        static void CheckNights(List<string> problems) {
            GameRootConfig config = Config;
            if (config == null) {
                return;
            }
            RouteDefinition route = config.Route(RouteSeed.RouteId);
            NightDefinition[] nights = config.nights ?? new NightDefinition[0];
            if (nights.Length != NightSeed.Count) {
                problems.Add($"GameRootConfig.nights has {nights.Length} entries; a run is {NightSeed.Count} nights (D6)");
            }
            string[] monsters = { NightSeed.Starer, NightSeed.Whisperer, NightSeed.Mimic, NightSeed.WeepingAngel };
            for (int i = 0; i < nights.Length; i++) {
                NightDefinition night = nights[i];
                if (night == null) {
                    problems.Add($"GameRootConfig.nights entry {i} is empty");
                    continue;
                }
                string n = "night " + night.nightIndex;
                if (night.nightIndex != i + 1) {
                    problems.Add($"GameRootConfig.nights entry {i} is {n}; the list is in night order");
                }
                if (route == null) {
                    continue;
                }
                int endIndex = route.IndexOfStop(night.endStopId);
                if (endIndex < 0) {
                    problems.Add($"{n} ends at '{night.endStopId}', which the route doesn't have");
                }
                foreach (RiderSpec rider in night.scripted ?? new RiderSpec[0]) {
                    if (rider == null) {
                        problems.Add($"{n} has an empty scripted rider");
                        continue;
                    }
                    int board = route.IndexOfStop(rider.boardStopId);
                    int destination = route.IndexOfStop(rider.destinationStopId);
                    if (board < 0 || destination < 0 || destination <= board) {
                        problems.Add($"{n}: rider {rider} needs a boarding stop before its destination, both on the route");
                    }else if (endIndex >= 0 && destination > endIndex) {
                        problems.Add($"{n}: rider {rider} rides past the night's end stop '{night.endStopId}'");
                    }
                    if (config.Look(rider.lookId) == null) {
                        problems.Add($"{n}: rider {rider} has an unknown look");
                    }
                    if (rider.IsMonster && System.Array.IndexOf(monsters, rider.monsterId) < 0) {
                        problems.Add($"{n}: rider {rider} is an unknown monster");
                    }
                }
                foreach (string monster in night.requiredMonsters ?? new string[0]) {
                    if (System.Array.IndexOf(monsters, monster) < 0) {
                        problems.Add($"{n} requires the unknown monster '{monster}'");
                    }
                }
                foreach (string monster in night.monsterPool ?? new string[0]) {
                    if (System.Array.IndexOf(monsters, monster) < 0) {
                        problems.Add($"{n} draws from the unknown monster '{monster}'");
                    }
                }
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
