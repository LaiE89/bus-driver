# Source of truth for ROADMAP.md §5 and §6. Edit here, then run:  python3 docs/.roadmap-src/tickets.py
# It rewrites the region between the ROADMAP-TICKETS markers, computing "Blocks", the blocker
# register, the critical path and the status tracker from the "Depends on" lists.
import re, sys, os

MILESTONES = []
# Ticket status for the §6 tracker: "Doing", "Blocked (<reason>)" or "Done (<date>)". Missing = Todo.
STATUS = {
    "T-M0-11": "Done (2026-09-29)",
    "T-M0-01": "Done (2026-09-29)",
    "T-M0-02": "Done (2026-09-29)",
    "T-M0-08": "Blocked ([HUMAN] build modules, itch page, butler; tools/itch.env is git-ignored)",
    "T-M0-09": "Blocked (needs T-M0-08)",
    "T-M0-03": "Done (2026-09-29)",
    "T-M0-04": "Done (2026-09-29)",
    "T-M0-05": "Done (2026-09-29)",
    "T-M0-06": "Done (2026-09-29)",
    "T-M0-07": "Done (2026-09-29; macOS Apple-silicon only until llvm-lipo is chmod +x, D52; Windows needs T-M0-08)",
    "T-M0-10": "Done (2026-09-29)",
    "T-M1-01": "Done (2026-09-29)",
    "T-M1-02": "Done (2026-09-29)",
    "T-M1-03": "Done (2026-09-29)",
    "T-M1-04": "Done (2026-09-30)",
    "T-M1-05": "Done (2026-09-30)",
    "T-M1-06": "Done (2026-09-30)",
    "T-M1-08": "Done (2026-09-30)",
    "T-M1-07": "Done (2026-09-30)",
    "T-M1-09": "Done (2026-09-30)",
    "T-M1-10": "Blocked ([HUMAN] mixer groups, snapshots and exposed parameters; AudioMixerValidatorTests reports Inconclusive)",
    "T-M1-12": "Done (2026-09-30)",
    "T-M1-13": "Done (2026-09-30)",
    "T-M1-11": "Done (2026-09-30; group routing to Master until T-M1-10)",
    "T-M1-14": "Done (2026-09-30)",
    "T-M1-15": "Done (2026-09-30)",
    "T-M1-16": "Done (2026-09-30)",
    "T-M1-17": "Done (2026-09-30)",
    "T-M1-18": "Done (2026-09-30)",
    "T-M1-19": "Done (2026-09-30)",
    "T-M1-20": "Done (2026-09-30)",
    "T-M1-21": "Done (2026-09-30)",
    "T-M2-01": "Done (2026-09-30)",
    "T-M2-02": "Done (2026-09-30)",
    "T-M2-03": "Done (2026-09-30)",
    "T-M2-04": "Done (2026-09-30)",
    "T-M2-05": "Done (2026-09-30)",
    "T-M2-06": "Done (2026-09-30)",
}
def M(mid, title, phase, goal, acceptance, note=""):
    MILESTONES.append(dict(id=mid, title=title, phase=phase, goal=goal, acceptance=acceptance, note=note, tickets=[]))
def T(tid, title, size, typ, deps, blockers, spec, do, acc):
    MILESTONES[-1]["tickets"].append(dict(id=tid, title=title, size=size, typ=typ, deps=deps, blockers=blockers, spec=spec, do=do, acc=acc))

# ------------------------------------------------------------------------------------------ M0
M("M0", "Project hygiene & safety net", "A",
  "The repo stays small and mergeable as binary assets arrive. Builds and tests run headless. Gameplay is unchanged.",
  ["`tools/verify.sh full` passes (the `content` mode still uses the legacy builder).",
   "The smoke test passes unchanged.",
   "A restricted itch.io page has Windows and macOS builds of the current MVP (once the [HUMAN] steps in T-M0-08 are done)."])
T("T-M0-11", "Reconcile PR #5 with the roadmap (crash death, seated kick, avatar layer)", "S", "code", [], "none", "D46, D1, D49, §4.16",
  ["Do this first: PR #5 was merged after the roadmap was written, and these three changes contradict it.",
   "**Crash death (D1):** in `SceneController`, delete `HandleCrash`, `WireCrashDetector`, `fatalCrashSpeedKmh` and the `crashDetector` subscription. Keep `CrashDetector.PreCollisionSpeedKmh`. `TriggerGameOver` stays; the `WeepingAngel` still uses it.",
   "**Seated kick:** in `PlayerInteractor`, only look for interactables in `PlayerMode.OnFoot`. Remove `seatedReach` and the driver-camera path.",
   "**Avatar layer (D49):** rename the layer `PlayerHead` to `PlayerAvatar` and move it from slot 8 to slot 19 in `TagManager.asset`. Update `PlayerAvatarVisuals.HeadLayerName` and the builder, then rebuild the scene (`BusDriverSceneBuilder.BuildScene`, then `OverlayMenusSceneBaker`).",
   "**Before the rebuild,** make sure `BusRoute.unity`, `Dash.mat` and `Controls Menu.prefab` have no uncommitted hand edits (commit or discard them first; the same check T-M0-10 automates)."],
  ["Driving into a wall at 80 km/h doesn't end the game.",
   "Seated, looking at a front-row passenger shows no prompt and RMB does nothing. On foot, the kick still works.",
   "Layer 8 is empty and layer 19 is `PlayerAvatar`. The avatar is visible on CCTV and not in the driver or on-foot view.",
   "The smoke test passes."])
T("T-M0-01", "Repo size rules and the file-size guard (no Git LFS)", "S", "tooling", [], "none", "§4.21, D41, Appendix A.0",
  ["Add `tools/hooks/pre-commit`. It rejects any staged file over **50 MB**, and any source-format file (`*.psd *.blend *.blend1 *.spp *.kra *.max *.ma *.mb *.ztl`) under `BusDriver/`. Enable it with `git config core.hooksPath tools/hooks`, documented in CONTRIBUTING (T-M0-02).",
   "Add EditMode `RepoRulesTests` to enforce the same two rules on everything under `Assets/`, so a machine without the hook is still caught at `verify.sh quick`.",
   "Re-encode the two large ambience WAVs (`Wind Ambience.wav` at 17 MB and `amb_driving_lp_01.wav` at 12 MB) to OGG Vorbis at quality 6. Keep the `.meta` files, so the GUIDs and references survive. The old versions stay in git history (about 30 MB, accepted).",
   "Leave the git history as it is: no rewrite, no force-push."],
  ["`RepoRulesTests` passes.",
   "A test file over 50 MB is refused by the hook, and so is a `.psd` under `Assets/`.",
   "The engine and wind loops still play (smoke test)."])
T("T-M0-02", "Git attributes, ignore rules and the contributing guide", "S", "tooling", ["T-M0-01"], "none", "§4.21, §4.15, §0.3",
  ["Create `.gitattributes` at the repo root: the `merge=unityyamlmerge` lines from §4.21, `-text` for binary asset types (so they're never line-ending converted), and `*.cs diff=csharp`.",
   "Extend `.gitignore` with `Builds/`, `BusDriver/Logs/`, `BusDriver/UserSettings/` and `*.tmp`.",
   "Write `docs/CONTRIBUTING.md`, covering:",
   "- the repo size rules (D41): exports only, sources in the shared drive, the 50 MB cap, OGG audio, and the hook setup",
   "- the UnityYAMLMerge merge-driver commands for macOS and Windows",
   "- generated vs owned folders (§4.15)",
   "- the conflict policy for `Generated/`, `Data/` and the dressing scene",
   "- the branch and PR flow",
   "- how to run `tools/verify.sh`."],
  ["`git check-attr -a` on a `.unity` file reports `merge: unityyamlmerge`, and on a `.png` reports `text: unset`.",
   "`CONTRIBUTING.md` covers every bullet above."])
T("T-M0-03", "Domain reload back on; remove dead assets", "S", "code", [], "none", "D24, §1.5",
  ["Turn **Enter Play Mode Options off** (`EditorSettings.enterPlayModeOptionsEnabled = false`; Unity 6.6 stores that as `m_EnterPlayModeOptionsEnabled: 1` with `m_EnterPlayModeOptions: 0`, D62).",
   "Delete:",
   "- `Assets/TutorialInfo/`, plus any asset only it uses (check by GUID search first)",
   "- `Assets/Scenes/SampleScene.unity`",
   "- `Assets/New Terrain.asset`",
   "- `Assets/Models/Images/CoverArt.jpg` and its `.meta`.",
   "In `Menu.unity`, replace the background image with a solid near-black `Image`. The diorama replaces it in T-M2-15.",
   "Leave `_backups/` at the repo root alone: it's human-owned."],
  ["The project compiles; the menu shows a dark background, and its buttons work.",
   "`grep -r CoverArt BusDriver/Assets` finds nothing.",
   "The smoke test passes."])
T("T-M0-04", "Assembly definitions and namespaces (transitional layout)", "M", "code", ["T-M0-03"], "none", "§4.2",
  ["Create `Assets/Scripts/Core/BusDriver.Core.asmdef`, for new code only.",
   "Create a transitional `Assets/Scripts/BusDriver.Runtime.asmdef` covering every existing runtime script. It references Core, Unity.InputSystem, the URP runtime, RP Core, TextMeshPro, UnityEngine.UI and Unity.Timeline. The legacy statics currently cross the future Gameplay/UI boundary in both directions, so the final split happens in T-M1-21.",
   "Create `Assets/Editor/BusDriver.Editor.asmdef` (Editor only).",
   "Create `Assets/Tests/EditMode/BusDriver.Tests.EditMode.asmdef` and `Assets/Tests/PlayMode/BusDriver.Tests.PlayMode.asmdef`.",
   "Add a namespace to every existing script, based on its **final** home (for example `BusDriver.Gameplay.Bus` for `BusController`, `BusDriver.UI.Screens` for `OptionsMenu`). Don't rename classes or files here.",
   "Scenes and prefabs reference scripts by GUID, so they keep working."],
  ["Zero compile errors.", "The smoke test passes.", "The player build in T-M0-07 succeeds, which proves the editor code is excluded."])
T("T-M0-05", "Logging wrapper", "S", "code", ["T-M0-04"], "none", "§4.18",
  ["Add `BusDriver.Core.Util.Log` and `LogCat`. `Log.Verbose` is `[Conditional(\"BUSDRIVER_VERBOSE\")]`.",
   "Log a session header at startup. For now this is a `[RuntimeInitializeOnLoadMethod]`; it moves into `GameRoot` in T-M1-04.",
   "Replace the runtime `Debug.Log` calls (`CrashDetector`, `SoundController`, `OptionsSaveSystem`, `MainMenu`) with `Log`."],
  ["`LogTests` pass: format and category filter.", "No `Debug.Log` remains in runtime code except inside `Log.cs`."])
T("T-M0-06", "Test infrastructure and the architecture-rules test (with allowlist)", "S", "test", ["T-M0-04", "T-M0-11"], "none", "§4.19, §4.1",
  ["Add one sample EditMode test and one sample PlayMode test.",
   "Add `ArchitectureRulesTests`. It scans `Assets/Scripts/**/*.cs` for the banned APIs (§4.1 rules 5, 8, 12) and for `UnityEngine.Input.`.",
   "It uses an **allowlist** of the violations that exist today, by file and pattern:",
   "- `GameObject.Find` / `FindAnyObjectByType` in `MainMenu`, `SceneController`, `BusEngineSound`, `SoundController`, `PlayerInteractor`, `PauseMenu`",
   "- the statics in `OptionsMenu`, `ControlsMenu`, `ingameMenus`, `GameKeys`, `MainMenu`, `SceneController.Instance` (read by `Passenger`, `WeepingAngel`, `PlayerInteractor`, `BusEngineSound`, the menus and `Dialogue/`)",
   "- legacy input reads.",
   "The allowlist may only shrink."],
  ["Both test platforms run with `-runTests` and pass.",
   "Adding a new `GameObject.Find` to a runtime file makes `ArchitectureRulesTests` fail."])
T("T-M0-07", "Build scripts, build label, `--selftest` mode", "M", "tooling", ["T-M0-04", "T-M0-05"],
  "Cross-platform builds need T-M0-08 ([HUMAN] build modules). Until then only the current-OS build is verified.", "§4.20",
  ["Add `BusDriver.Editor.Build.BuildScripts`:",
   "- `BuildWindows(dev)`, `BuildMac(dev)`, `BuildCurrent()`",
   "- output to `Builds/<platform>/<version>/`",
   "- the scene list from the build settings (switches to `SceneIds.BuildList` in T-M1-13).",
   "Write the label `<bundleVersion> (<git short hash>)` into a temporary `Resources/build_label.txt` (folded into `GameRootConfig` in T-M1-04). Show it bottom-right on the menu.",
   "Add a `--selftest` command-line flag:",
   "- once the first scene has loaded, wait 3 s, log `[SELFTEST] OK <label>` and quit with code 0",
   "- on any exception, log `[SELFTEST] FAIL <message>` and quit with code 1."],
  ["A macOS build is produced on the lead's Mac.",
   "Running its executable with `--selftest -logFile -` prints `[SELFTEST] OK` and exits 0.",
   "The menu shows the label."])
T("T-M0-08", "[HUMAN] Build modules, itch.io page, butler", "S", "[HUMAN]", ["T-M0-07"],
  "[HUMAN] In Unity Hub, add the *Windows Build Support (Mono)* and *Mac Build Support (Mono)* modules to 6000.6.0f1. Create a **restricted** itch.io project. Install butler and run `butler login`. Put `ITCH_TARGET=user/game` in `tools/itch.env` (git-ignored).",
  "§4.20",
  ["The human does the steps above.", "The agent adds `tools/itch.env` to `.gitignore`."],
  ["`BuildWindows` succeeds on the Mac.", "The itch page exists and is restricted."])
T("T-M0-09", "itch push script and first restricted upload", "S", "tooling", ["T-M0-08", "T-M0-07"], "none", "§4.20",
  ["Write `tools/push-itch.sh <channel> <buildDir> <version>`, which reads `tools/itch.env`.",
   "Build both platforms, self-test the macOS build, and push to the `windows` and `mac` channels as version `0.0.1`."],
  ["The itch dashboard shows both channels at 0.0.1.",
   "The Windows build, downloaded from itch on any Windows PC, reaches the menu."])
T("T-M0-10", "`tools/verify.sh` and `tools/unity.sh`", "S", "tooling", ["T-M0-06", "T-M0-07"], "none", "§0.5",
  ["Implement the modes `quick`, `content`, `playmode`, `smoke`, `build` and `full`.",
   "Check for the lockfile: refuse batch mode while the Editor is open, and print the CLI alternative.",
   "Grep logs for `error CS|Exception|no serialized field` and parse the test XML for failures.",
   "Return a non-zero exit code with a one-line reason on any failure.",
   "`content` calls the legacy `BusDriverSceneBuilder.BuildScene`, then `OverlayMenusSceneBaker.BakeIntoBusRoute` and `ControlsMenuPrefabBuilder.Upgrade` (PR #5 put the pause, Game Over and controls UI there), until T-M1-13.",
   "**Before the first legacy rebuild,** diff the committed scene against what the builder would produce, per the MVP workflow (`grep propertyPath` in the PrefabInstance blocks, plus hand-added roots). Stop if anyone's hand edits would be lost."],
  ["`tools/verify.sh full` passes on a clean checkout.",
   "A deliberately broken test makes `playmode` exit non-zero with the test name."])

# ------------------------------------------------------------------------------------------ M1
M("M1", "Architecture skeleton", "A",
  "The MVP runs on the new architecture: GameRoot services, explicit wiring, the Input System, pause and audio services, and generated scenes and prefabs, with assemblies split into Core/Gameplay/UI. **Gameplay behaviour is unchanged.**",
  ["New Run from the generated menu loads `Night_Systems` + `Route01_World` (still the legacy loop). Driving, CCTV, doors, boarding and kicking all work.",
   "Esc pauses the game. Rebinding persists.",
   "`Flow_MenuNightMenuNight_NoErrors` passes.",
   "The `ArchitectureRulesTests` allowlist is **empty**.",
   "`tools/verify.sh full` passes."])
T("T-M1-01", "Core utilities: ids, RNG streams, money and clock formatting", "S", "code", ["T-M0-06"], "none", "§4.1 (8, 11), §2.5",
  ["`SceneIds`: `Menu`, `Night_Systems`, `Route01_World`, `Route01_Dressing`, `BuildList`.",
   "`RngStreams`: an FNV-1a hash of (seed, name) seeds a `System.Random`; `Get(name)` returns it.",
   "`Money.Format(cents)`: `$3.50`, and `−$3.50` with a true minus sign.",
   "`ClockFormat`: the dash format (`12:34 AM`) and the CCTV format (`12:34:56 AM`).",
   "`Ids.IsValid` (lower_snake_case)."],
  ["`RngStreamsTests`: same seed and name gives the same sequence; different names differ; extra draws on one stream don't change another.",
   "`MoneyTests` pass.",
   "`ClockFormatTests`: 1800 → `12:30 AM`; 5213.3 → `01:26 AM` / `01:26:53 AM`."])
T("T-M1-02", "Save store: envelopes, atomic writes, migrations", "M", "code", ["T-M1-01"], "none", "§4.9",
  ["Add `com.unity.nuget.newtonsoft-json` to `manifest.json` explicitly, at least at the version `packages-lock.json` already resolves.",
   "Add `ISaveStore`, `SaveService` (with an injectable root folder), `SaveSlot` {Settings=1, Meta=2, Run=3}, `SaveEnvelope`, `ISaveMigration` and the `SaveMigrations` registry.",
   "Use the JSON settings from §4.9."],
  ["`SaveStoreTests`:",
   "- round trip works, and no `.tmp` file is left behind",
   "- a simulated crash between write and replace leaves the old file intact",
   "- a corrupt file falls back to `.bak`",
   "- if both are corrupt, the default is returned and a `.corrupt-*` file exists",
   "- a newer `saveVersion` is refused and the file is untouched.",
   "`SaveMigrationTests` passes with a dummy v0 → v1 step."])
T("T-M1-03", "Save models and v1 fixtures", "S", "code", ["T-M1-02"], "none", "§4.9, §2.23",
  ["Add `SettingsData`, `RunState` (+ `RunStats`, `NightHistory`, `StopArrival`) and `MetaProgress` (+ `JournalEntryState`) in `BusDriver.Core.Save`. Defaults come from §2.23.",
   "Add the fixtures `Tests/Fixtures/v1/{run,meta,settings}.json`, matching §4.9."],
  ["`SaveFixtureTests` loads and round-trips every v1 fixture with no field lost.",
   "`SettingsData` defaults equal the §2.23 table."])
T("T-M1-04", "GameRoot, GameServices, SceneLoader, RunFlow skeleton", "L", "code", ["T-M1-03", "T-M0-05", "T-M0-07"], "none", "§4.3–§4.5",
  ["Add `GameRootConfig` in `Resources/`. It's created here and adopted by `DataSeeder` in T-M1-13. Move the build label into it.",
   "Add `GameRoot.Bootstrap` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`), `GameServices` and `ISceneRoot`.",
   "Add `SceneLoader`: single and additive loads, root discovery, `Initialize` calls, `Progress`, `AllowActivation`.",
   "Add `RunFlow` with the states Boot, Menu, LoadingNight and InNight, New Run, and the Editor debug run (EditorPrefs keys).",
   "Add a `MenuContext : ISceneRoot` to the Menu scene.",
   "Add a `LegacyNightRoot : ISceneRoot` to the legacy driving scene; the legacy builder adds it.",
   "`PlayGame` now calls `RunFlow.NewRun()`."],
  ["PlayMode `Boot_CreatesSingleGameRoot`: exactly one `GameRoot` after Menu → Night → Menu.",
   "PlayMode `Flow_NewRun_LoadsNight` passes.",
   "Pressing Play in the driving scene logs `debug run seed=…`."])
T("T-M1-05", "SettingsService; options screen bound to it", "M", "code", ["T-M1-04"], "none", "§2.23, §4.6, §4.9",
  ["Add `SettingsService`: load and save `settings.json`, then Apply. Apply sets:",
   "- resolution, fullscreen, quality and FPS/VSync",
   "- brightness → `RenderSettings.ambientLight` (this moves to `LightingPresetApplier` in T-M2-05)",
   "- master volume → the existing exposed `volume` parameter (until T-M1-10).",
   "`OptionsMenu` becomes `OptionsScreen`, which reads and writes `SettingsService`.",
   "`DriverLook` and `OnFootController` read sensitivity and invert-Y from `SettingsService`, handed over by the scene root (`LegacyNightRoot` now, `ShiftContext` later).",
   "Delete `OptionsSaveSystem`, `OptionsData`, the `OptionsMenu` statics and `SceneController.ApplySavedSettingsIfNeeded`."],
  ["PlayMode `Settings_PersistAcrossBoot` passes, using a temporary save root.",
   "The sensitivity set in Options changes the driving look after New Run.",
   "The `OptionsMenu` entries are gone from the allowlist."])
T("T-M1-06", "Input actions asset, InputService, contexts", "M", "code", ["T-M1-04"], "none", "§4.10",
  ["Author `Assets/Input/BusDriver.inputactions` (JSON) with the §4.10 maps and the D48 defaults (Interact = right mouse button, Doors = Q, Leave seat = E).",
   "Make it the project-wide actions asset. Delete the stock `InputSystem_Actions.inputactions`.",
   "Add the hand-written `BusDriverActions` wrapper.",
   "Add `InputService`: `SetContext`, the rebinding API, and loading/saving overrides through `SettingsService`."],
  ["`InputActionsTests`:",
   "- every §4.10 id exists",
   "- no two actions active in the same context share a default keyboard binding",
   "- `Pause` is live in every context except None."])
T("T-M1-07", "Migrate gameplay and menus off legacy input", "M", "code", ["T-M1-06", "T-M1-08"], "none", "§2.2, §4.10",
  ["Rewrite the input reads in `BusInput`, `DriverLook`, `OnFootController`, `PlayerInteractor`, `CCTVSystem`, `SceneController` (pause, leave seat, doors) and `DriverSeat`. Delete `GameKeys` and `ControlsMenuPrefabBuilder`.",
   "`ControlsMenu` becomes `ControlsScreen`, with Input System rebinding under the §4.10 rules.",
   "HUD prompts use `GetBindingDisplayString`.",
   "Keep the `ExternalControl` seams.",
   "Set `activeInputHandler` to Input System only. Every EventSystem uses `InputSystemUIInputModule`.",
   "Convert mouse-look scale as in §4.10.",
   "Delete `LegacyKeyBindings` and the temporary `SettingsData.legacyKeyBindings` field (D56); rebinds now live in `bindingOverridesJson`."],
  ["No `UnityEngine.Input.` or `Event.current` remains in runtime code (checked by `ArchitectureRulesTests`).",
   "Smoke-test drive numbers are within 5 % of the M0 baseline in `Logs/smoke.log`: 0–50 km/h time, stop time, turn angle.",
   "Rebinding CycleCamera to C works, persists, and shows in the HUD prompt. Rebinding Interact to a mouse button works; binding it to mouse movement is impossible.",
   "No UI text hard-codes a key name. Every prompt comes from a binding display string (controller-ready rule, §4.10)."])
T("T-M1-08", "PauseService and CursorService", "M", "code", ["T-M1-06"], "none", "§4.11",
  ["Add `PauseService`:",
   "- it stores and restores `timeScale`",
   "- sets `AudioListener.pause`",
   "- overrides the input context",
   "- uses a `CanPause` predicate supplied by the scene root",
   "- pauses on focus loss (except in batch mode).",
   "Add `CursorService`.",
   "Remove `ingameMenus.pausedGame` and every read of it: `CCTVSystem`, `DriverLook`, `BusInput`, `OnFootController`, `PlayerInteractor`, `BusEngineSound`, `DrivingHUD`, `SceneController`, `PauseMenu`, `GameOverMenu`.",
   "`ingameMenus` and `PauseMenu` become `PauseScreen` (Resume / Options / Quit to Menu / Quit Game). `SceneController.SetPaused`, its Esc handling and `ApplyCursor` are replaced by `PauseService` and `CursorService`.",
   "`GameOverMenu` and `SceneController.TriggerGameOver` read `PauseService` instead of `ingameMenus.pausedGame` (both survive until T-M4-06).",
   "UI audio sources set `ignoreListenerPause`."],
  ["PlayMode `Pause_FreezesTimeAndAudio`: `timeScale` is 0, the listener is paused, and the bus doesn't move.",
   "No static `pausedGame` remains."])
T("T-M1-09", "AudioService, sound definitions, mixer validator", "M", "code", ["T-M1-04"], "none", "§4.12, Appendix A.3",
  ["Add `SoundDefinition`, `SoundLibrary`, `AudioConfig`, `AudioGroup`, `AudioSnapshot` and `SoundIds` (every id in Appendix A.3).",
   "Add `IAudioService` / `AudioService`: a pool of 32, voice limits, cooldowns, handles, attached sources, `StopSceneSounds`, the `OnCaption` event, group volumes through exposed parameters, and snapshots.",
   "Move `Assets/SFX/MainMixer.mixer` to `Assets/Audio/`.",
   "Add `AudioMixerValidatorTests`. It reports **Inconclusive**, naming each missing group, snapshot or parameter, until T-M1-10 is done."],
  ["`AudioServiceTests` (EditMode, fake time source):",
   "- the voice limit steals the oldest instance",
   "- a repeat within the cooldown is ignored",
   "- a handle is invalid after Stop."])
T("T-M1-10", "[HUMAN] Create the mixer groups, snapshots and exposed parameters", "S", "[HUMAN]", ["T-M1-09"],
  "[HUMAN] Unity has no public API for creating AudioMixer groups or snapshots. A person opens `Assets/Audio/MainMixer.mixer` and creates exactly the §4.12 tree, the 4 snapshots and the exposed parameters. It takes about 10 minutes.",
  "§4.12",
  ["The human builds the mixer as specified.", "The agent re-runs the validator."],
  ["`AudioMixerValidatorTests` passes (no longer Inconclusive)."])
T("T-M1-11", "Migrate every sound to AudioService; placeholder audio", "M", "code", ["T-M1-09", "T-M1-13"],
  "Group routing needs T-M1-10. Until then every definition routes to Master.", "§4.12, Appendix A.3",
  ["`git mv` `Assets/SFX/*` to `Assets/Audio/Clips/*`, keeping the metas.",
   "`DataSeeder` creates a `SoundDefinition` for every entry on `Sound Controller.prefab` (read the prefab to list them) and for every Appendix A.3 id.",
   "Rewrite `BusEngineSound` (keeping the handbrake-stop cue), `CCTVSystem`, the menu UI clicks, `PauseScreen`, `GameOverMenu` and the scene ambience to use `IAudioService`. Remove `SceneController.soundController`.",
   "Delete `SoundController`, `Sound`, `Sound Controller.prefab` and its instances.",
   "Add `PlaceholderAudioBuilder`."],
  ["No reference to `SoundController` remains.",
   "PlayMode `Audio_EveryIdPlays`: every id produces a playing source.",
   "The engine pitch still follows speed (smoke log line).",
   "The night scene plays its ambience."])
T("T-M1-12", "UITheme, ThemedText, ScreenRouter, ConfirmDialog", "M", "code", ["T-M1-04"], "none", "§4.13",
  ["Add the `UITheme` type and a default `Data/UI/Theme.asset`, with LiberationSans SDF for every role as a placeholder.",
   "Add `ThemedText`, which applies its role on enable and in `OnValidate`.",
   "Add the `ScreenView` base, `ScreenRouter` (stack, focus, Cancel → pop or pause), `ConfirmDialog` and the `UIText` constants.",
   "Follow the controller-ready rules (§4.10): every screen is operable with arrows, Enter and Esc alone, and a `Selectable` always has focus."],
  ["PlayMode `ScreenRouterTests`:",
   "- focus is restored after a pop",
   "- Cancel pops the top screen",
   "- Cancel on an empty stack asks `PauseService` to pause.",
   "Editing a role's size in `Theme.asset` changes every themed text using that role."])
T("T-M1-13", "Builder framework: BuildAll, BuilderUtil, ProjectSettingsBuilder, DataSeeder, ContentValidator", "L", "tooling", ["T-M0-10", "T-M1-01", "T-M1-04"], "none", "§4.15, §4.16",
  ["Add `BuildAll.Run` with the 11 steps. Steps that aren't implemented yet log `skipped`.",
   "Add `BuilderUtil`, ported from the legacy builder: `Prim`, `Box`, `SetRef`, `SetRefArray`, `SetVector`, `SetStringArray`, plus `EnsureFolder`, `SaveOrOverwritePrefab` and `SaveScene`.",
   "Add `ProjectSettingsBuilder`:",
   "- layers 8–19 and the collision matrix (19 = `PlayerAvatar`, D49)",
   "- the `Containment` tag",
   "- Enter Play Mode Options off",
   "- player settings",
   "- the build list from `SceneIds`.",
   "Add `MaterialLibraryBuilder`: the greybox materials go to `Generated/Materials`.",
   "Add the `DataSeeder` framework (create-if-missing, plus a separate confirmed Reseed menu item). The UI theme's seed is `UIThemeSeed.Fill` (T-M1-12, D61).",
   "Add the `ContentValidator` framework: exit code 1 on failure in batch mode.",
   "`verify.sh content` now runs `BuildAll`."],
  ["`BuildAll` succeeds twice in a row starting from a deleted `Generated/`.",
   "`ProjectSettingsTests` passes: layers and matrix match §4.16.",
   "`DataSeederTests` passes: a hand-modified Data asset survives reseeding."])
T("T-M1-14", "Generated Bus, OnFootRig and FallCamera prefabs with greybox views", "L", "content", ["T-M1-13"], "none", "§4.14, §4.16, §4.8 (scare anchors), Appendix A.2",
  ["`PrefabBuilder` generates `Generated/Prefabs/Bus.prefab`. The **logic root** carries:",
   "- Rigidbody, `BusController`, `BusDoors`, `BusCabin`, `BusInput`, `CrashDetector` and the WheelColliders",
   "- the hull collider (layer Bus) and the interior colliders (layer BusInterior)",
   "- the 36 seats, `Anchor_Seat_R{row}_{L2,L1,R1,R2}`, where R1 is the front row",
   "- the cabin nodes `Anchor_DoorStep`, `Anchor_AisleAtDoor`, `DoorOutside` and `StandPoint`",
   "- `Anchor_DriverHead` with the look pivot, the driver camera and the ears (AudioListener)",
   "- 3 `CctvCamera`s at `Anchor_CCTV_Front/Mid/Rear` (the MVP poses, labels and ranges)",
   "- `Anchor_Headlight_L/R`, the cabin light group, `Anchor_Dash_Clock/FareBox/Gps/Mirror`, the scare anchors, and the Occluder trigger shell.",
   "Its **`View`** child is a `GreyboxBusView` (ported shell, interior, wheels and door panel). Wheel visuals, the door panel and steering go through `BusViewBase`.",
   "Also generate `OnFootRig.prefab` and `FallCamera.prefab`.",
   "Add the player avatar (D49): `PlayerAvatarViewBase` + `GreyboxPlayerAvatarView` (the PR #5 capsule body), seated under `Anchor_DriverHead`'s seat point on the bus and standing on `OnFootRig`, all on layer `PlayerAvatar`. The driver and on-foot cameras cull that layer.",
   "The bus starts with `DriveLock.Scripted`. `LegacyNightRoot` releases it until T-M1-17.",
   "The legacy scene builder instantiates the prefab."],
  ["`BusPrefabTests` asserts:",
   "- 36 seats at the legacy x/z positions",
   "- the CCTV and driver camera poses",
   "- axles at z = +3.3 / −2.7",
   "- the door opening at z 4.1–5.5",
   "- the avatar on layer 19, rendered by the CCTV cameras and culled by the driver and on-foot cameras.",
   "The smoke test passes."])
T("T-M1-15", "ShiftContext, ShiftServices, SceneController split", "L", "code", ["T-M1-14", "T-M1-07", "T-M1-08", "T-M1-05"], "none", "§4.5, §4.6",
  ["Add `ShiftContext : ISceneRoot` with `Initialize`, `AttachRoute` and `Begin`, and the fixed Init order. Services that don't exist yet are skipped.",
   "Add `ShiftServices` and `IShiftBindable`. `IShiftBindable` and the explicit Init order replace the interim `IGameBindable`/`SceneBinding` discovery (D56).",
   "Split `SceneController` (PR #5 grew it from `PlayerModeController`, D46):",
   "- pause and cursor are already gone (T-M1-08), and settings loading (T-M1-05)",
   "- NPC spawning (`PopulateBusStops`, `SpawnNpcAtStop`, `DespawnNpc`, the pool and the weights) moves to a `LegacyRiderSpawner` on the route root, which lives until T-M2-07",
   "- `TriggerGameOver`/`IsGameOver` move to a small `LegacyGameOver` on the scene root, which lives until T-M4-06",
   "- `PlayerPosition`, `DriverCamera`, `OnFootCamera` and `IsViewingCCTV` are handed to the classes that need them (`WeepingAngel`, `PlayerInteractor`, `Passenger`) through `Bind`",
   "- the rest is the mode switch. `git mv` it back to `Player/PlayerModeController.cs` (keeping the `.meta`, so references survive), with no `Instance`, services, pause or cursor.",
   "`DriverSeat` gets the controller through `Bind`.",
   "`BusCabin` drops its `cctv` field and receives its stops from the route root in `Init`.",
   "`ShiftContext` replaces `LegacyNightRoot`."],
  ["No `SceneController` remains, and there is no `PlayerModeController.Instance`.",
   "The allowlist loses its `SceneController`, `PlayerInteractor`, `DriverSeat` and `BusEngineSound` entries.",
   "The smoke test passes."])
T("T-M1-16", "Generated scenes (Night_Systems, Route01_World legacy loop, Menu v0) and HUD/Screens prefabs; smoke test ported", "L", "content", ["T-M1-15", "T-M1-12", "T-M1-11"], "none", "§4.3, §4.13, §4.15",
  ["`HUD.prefab`: `DrivingHUD` ported into `HudView` + `CctvOverlayView`. Every text uses `ThemedText`; the presenters are `IShiftBindable`.",
   "`Screens.prefab`: `ScreenRouter`, `PauseScreen`, `OptionsScreen`, `ControlsScreen`, `ConfirmDialog`.",
   "`NightSystemsBuilder` builds `Night_Systems`.",
   "`RouteBuilder` gets a **legacy mode** that ports the MVP loop, roadside, obstacles, 3 stops, lamps, waiting riders (through `LegacyRiderSpawner`) and the test monster (`WeepingAngel`) exactly into `Generated/Scenes/Route01_World.unity`, with a `RouteSceneRoot`.",
   "`MenuBuilder` v0: dark background, title, New Run / Options / Controls / Quit, build label, `MenuContext`.",
   "Port `BusSmokeTest` to `BusDriver.Editor.Smoke.SmokeTest`, loading through `RunFlow`.",
   "Delete the legacy `BusRoute.unity` and `Menu.unity`, `Prefabs/NPCs`, `Materials/Map`, `Materials/NPCs`, `Prefabs/Level Essentials` (including `In Canvas`), `BusDriverSceneBuilder.cs` and `OverlayMenusSceneBaker.cs`. `PauseScreen` replaces `PauseMenu`.",
   "The build list becomes Menu, Night_Systems, Route01_World."],
  ["`tools/verify.sh full` passes.",
   "Pressing Play in `Night_Systems` works (a debug run with the route loaded additively).",
   "The ported smoke test passes with the same checks as before."])
T("T-M1-17", "ShiftDirector skeleton; the Menu → Night → Menu loop", "M", "code", ["T-M1-16"], "none", "§2.1, §4.4",
  ["Add `ShiftDirector`: Intro (a 3 s card) → Driving, releasing `DriveLock.Scripted`.",
   "Pause → Quit to Menu is a plain quit for now; the D21 rule arrives in T-M7-08.",
   "Add `RunFlow.LoadMenu`."],
  ["PlayMode `Flow_MenuNightMenuNight_NoErrors`: menu → new run → drive 5 s → quit to menu → new run → drive 5 s. It asserts exactly one `GameRoot`, no errors, and one ambience voice."])
T("T-M1-18", "Debug overlay shell", "S", "code", ["T-M1-16"], "none", "§4.18",
  ["Add the `DebugOverlay` prefab: F1 toggles it, and it exists only in development builds and the Editor.",
   "Add the `IDebugSection` and `DebugCheat` registries. Services register their sections in `Init`.",
   "First sections: Run, Clock/Route (placeholders) and Attention mode."],
  ["F1 works in the Editor.", "A release build has no overlay (`BuildScriptsTests` checks the define)."])
T("T-M1-19", "Remove inherited leftovers (dialogue system, unused helpers)", "S", "code", ["T-M1-16"], "none", "Appendix B",
  ["Delete `Dialogue/` (`Dialogue`, `DialogueController`, `DialogueTrigger`) and the dialogue/objectives fields they used. They're unused by the design and recoverable from git. Dialogue and passenger ratings aren't part of the design: don't port them from any teammate branch.",
   "Delete the unused `ToolMethods` helpers (reference search first).",
   "Delete `Assets/Mesh` if it's empty."],
  ["It compiles.", "A reference search shows nothing depends on the removed files."])
T("T-M1-20", "Architecture rules enforced (allowlist empty)", "S", "test", ["T-M1-05", "T-M1-07", "T-M1-08", "T-M1-11", "T-M1-15", "T-M1-19"], "none", "§4.1",
  ["Delete the allowlist.",
   "Add the reflection check: no non-readonly static fields in `BusDriver.*` runtime types, except `GameRoot`'s bootstrap field and `Log`'s filter."],
  ["`ArchitectureRulesTests` passes with no allowlist."])
T("T-M1-21", "Split BusDriver.Runtime into BusDriver.Gameplay and BusDriver.UI", "M", "code", ["T-M1-20"], "none", "§4.2",
  ["Create `Scripts/Gameplay/BusDriver.Gameplay.asmdef` and `Scripts/UI/BusDriver.UI.asmdef`.",
   "`git mv` each file to match its namespace, then delete `BusDriver.Runtime.asmdef`.",
   "Fix any Gameplay → UI reference, using an event or a gameplay-side service.",
   "Add `InternalsVisibleTo` for the tests."],
  ["`AsmdefRulesTests`: Gameplay doesn't reference UI, and Core references none of our assemblies.",
   "`verify.sh full` passes."])

# ------------------------------------------------------------------------------------------ M2
M("M2", "Route, world & navigation", "A",
  "Route 1 exists as generated data-driven geometry, with containment, stops, tunnel, bridge and cliff. The bus is tracked along it, the clock and GPS work, and the menu becomes the diorama (D17 'early').",
  ["AutoPilot drives depot → lodge without leaving the corridor.",
   "`RouteContainmentTests` passes.",
   "A human can drive the whole route using the GPS, and stops show Served or Missed in the F1 overlay.",
   "The menu shows the lamp-lit stop diorama."],
  "Every ticket from M2 onwards implicitly depends on M1 being complete.")
T("T-M2-01", "RouteDefinition type and Route01 seed data", "M", "content", ["T-M1-13"], "none", "§3, §4.8",
  ["Add `RouteDefinition` and its nested types: segment, stop, stub, zone, sign, schedule and generation parameters.",
   "Add `SeedData` that creates `Data/Routes/Route01.asset` from the §3.1–§3.3 tables exactly."],
  ["`ContentValidationTests` passes for Route01: ids valid and unique, stops in order, every stop on a straight."])
T("T-M2-02", "RoutePath and ScheduleMath cores", "M", "code", ["T-M2-01"], "none", "§3, §2.4",
  ["Add `RoutePath`:",
   "- samples the segments every 1 m (position, tangent, right, elevation)",
   "- `Evaluate(d)`",
   "- `Project(worldPos, hintDistance)`, with a ±50 m window search and a full-search fallback",
   "- `TotalLength`.",
   "Add `ScheduleMath`: scheduled times and the arrival rating."],
  ["`RoutePathTests`: projection error < 0.05 m on the road; heading continuity.",
   "`RouteLayoutTests`: total length 3000 ± 0.1 m; minimum separation between parts more than 150 m apart along the road ≥ 100 m; no road within 300 m of the cliff's outer side; final heading −10° ± 0.5.",
   "`ScheduleTests`: the §3.2 table to the second."])
T("T-M2-03", "Road ribbon and cross-section profile meshes", "L", "content", ["T-M2-02"], "none", "§3.4",
  ["Add `RoadMeshBuilder`:",
   "- a ribbon mesh with shoulders, u across and v = distance / 4",
   "- centre-line dash quads (non-static, emissive-safe)",
   "- chunks of 100 m.",
   "Add `ProfileBuilder`: Forest ground, Rockface wall, Drop slope + guardrail + invisible wall, CliffDrop face + valley floor, Water deck + railing + creek plane, TunnelWall shell.",
   "Tag every containment collider `Containment`."],
  ["`RoadMeshTests`: width at sample points is 7.0 m (6.5 m in the cliff range); no degenerate triangles; normals point up."])
T("T-M2-04", "LightFlicker and emissive views", "S", "code", ["T-M1-21"], "none", "§4.14",
  ["Add `LightFlicker` with the Subtle, Unstable and Scripted modes. Scripted takes a curve plus a duration.",
   "Add `IEmissiveView`, and `EmissiveView` (a `MaterialPropertyBlock` on `_EmissionColor`).",
   "Groups work by shared reference."],
  ["`LightFlickerTests` (PlayMode): intensity stays within its bounds; a Scripted flicker ends exactly at its duration; emissive views follow the light."])
T("T-M2-05", "NightLightingPreset and LightingPresetApplier", "S", "code", ["T-M1-05", "T-M1-21"], "none", "§4.8, §2.23",
  ["Add the preset type and a seed asset holding the MVP values (ambient, fog, moon, post profile).",
   "The applier sets `RenderSettings` and the moon, and multiplies ambient by the brightness setting.",
   "`SettingsService` brightness now goes through the applier."],
  ["Changing brightness in Options changes the night scene's ambient light immediately.",
   "The menu and night scenes use the same preset asset."])
T("T-M2-06", "Environment logic prefabs, greybox views, EnvironmentViewSet", "L", "content", ["T-M1-14", "T-M2-04"], "none", "§3.3, §4.14, Appendix A.4",
  ["Generate a logic prefab and a greybox view for each of:",
   "- blockers: 5 kinds × variants A/B",
   "- stops: the 6 kinds plus Terminus and Depot. Each has a `BusStop` with `stopId`, a sign pole, a lamp (with `LightFlicker`) and an optional shelter",
   "- the street lamp",
   "- signs: `BridgeAhead`, `NoGuardrailAhead`, `SharpCurveRight`, `Chevron`, `TunnelAhead`, `StopSign`",
   "- guardrail segment (4 m) and end cap",
   "- trees: 2 conifer variants with a LODGroup",
   "- rock chunk, depot building, lodge building.",
   "Seed `Data/Views/Environment.asset` with an entry for every kind, pointing at the greybox views."],
  ["`ContentValidator`: every `EnvironmentViewSet` kind resolves.",
   "No view prefab contains a Collider or Rigidbody (`ViewRulesTests`)."])
T("T-M2-07", "RouteBuilder: Route01_World from data", "L", "content", ["T-M2-03", "T-M2-05", "T-M2-06"], "none", "§3, §4.3, §4.15",
  ["Replace the legacy mode. `RouteBuilder` generates `Route01_World.unity` from `RouteDefinition` and `EnvironmentViewSet`:",
   "- road chunks and profiles",
   "- stubs with their walls and blockers",
   "- stops at their distances, facing the kerb (+x toward the kerb, as `BusStop` expects)",
   "- signs and lights",
   "- zone triggers: `TunnelZone`, `FallZone` segments, `RumbleZone` (components as stubs; behaviour comes later)",
   "- the depot pad and the bus spawn marker at 20 m in the right lane",
   "- the lodge and its end wall",
   "- the safety floor, the KillPlane, `FallCamAnchor` at the cliff",
   "- the `LightingPresetApplier`",
   "- `RouteSceneRoot` with lists of stops, zones, fall zones and the spawn point.",
   "Remove the legacy loop and its riders: delete `LegacyRiderSpawner`, `ObjectPooling` and `Passenger.PrepareForWaiting` (nights reload their scenes, so there's nothing to pool). Riders come back through the manifest in M3."],
  ["`BuildAll` produces the scene.",
   "The smoke test is updated to start on the new route and still passes: drive, stop, doors, boarding using a debug-spawned rider, kick.",
   "The route captures in `Logs/smoke/` show the road, a stop lamp, the cliff edge and the tunnel."])
T("T-M2-08", "Containment tests", "S", "test", ["T-M2-07"], "none", "§3.5",
  ["Implement `RouteContainmentTests` exactly as §3.5 describes, including the stub-mouth and cliff exceptions."],
  ["The test passes.",
   "Removing one guardrail segment in a temporary copy of the data makes it fail and report that distance."])
T("T-M2-09", "RouteTracker and KillPlane respawn", "M", "code", ["T-M2-07"], "none", "§2.3, §4.6",
  ["Add `RouteTracker`: projects the bus in FixedUpdate and exposes `DistanceAlong`, `Progress01`, `DistanceToNextStop`, `EtaGameSeconds` (from a 30 s moving-average speed) and `InZone(kind)`.",
   "Add `KillPlane` behaviour. Outside a `FallZone` it fades out for 1 s, respawns the bus upright on the nearest road point in the right lane facing the route direction, and logs **Error** `containment breach at d=…`."],
  ["PlayMode `Tracker_FollowsBus`: distance increases monotonically while driving forward.",
   "PlayMode `KillPlane_Respawns`: a bus teleported below the map outside the cliff is back on the road within 2 s, and the error is logged (the test expects it)."])
T("T-M2-10", "AutoPilot and the full-route drive test", "M", "test", ["T-M2-09"], "none", "§4.18",
  ["Add `AutoPilot` (development and test only): pure pursuit on `RoutePath`, the §4.18 speed profile, and `StopAt(stopId)` with door alignment. It has a debug cheat toggle.",
   "Add PlayMode `AutoPilot_DrivesFullRoute` at `timeScale` 3: no riders, drives depot → lodge."],
  ["It reaches `lodge` within 8 real minutes at `timeScale` 1 equivalent.",
   "Lateral offset never exceeds 2.5 m.",
   "No KillPlane respawn happens.",
   "`StopAt` leaves the door inside the zone at every stop."])
T("T-M2-11", "RouteProgress and stop integration", "M", "code", ["T-M2-09"], "none", "§2.4",
  ["Add `RouteProgress`: stop records, Served on the first fully-open doors in the zone, Missed at +30 m, `OnTerminus`.",
   "Adapt `BusStop` and `BusCabin` to route-provided stops, with alight-first ordering (for riders arriving in M3).",
   "Add the F1 section: stop states."],
  ["PlayMode `Stops_ServedAndMissed`: AutoPilot serves `farm_gate`, skips `gas_station` (Missed), serves `campground`, and the events fire in order."])
T("T-M2-12", "ShiftClock, dash clock, CCTV timestamp", "M", "code", ["T-M2-11", "T-M1-17"], "none", "§2.5, §4.13",
  ["Add the `ShiftClock` core (it advances only in Driving) and `ShiftClockDriver`.",
   "Add the world-space `DashClockView` at `Anchor_Dash_Clock`.",
   "The CCTV timestamp reads the same clock, which removes the MVP's cosmetic clock.",
   "Arrival times are recorded in game-seconds."],
  ["`ShiftClockTests` pass.",
   "PlayMode `Clock_OnlyAdvancesInDriving`: frozen in Intro and while paused, 6× in Driving."])
T("T-M2-13", "Dash GPS (RouteMapView)", "M", "code", ["T-M2-11", "T-M2-12"], "none", "§4.13, §2.23",
  ["Add a `UILineRenderer` graphic.",
   "Add `RouteMapView` on the world-space canvas at `Anchor_Dash_Gps`:",
   "- the whole route, north-up, with a 6 % margin",
   "- the travelled part dimmed, and the bus arrow",
   "- stop markers (✓ / ring / dot)",
   "- stubs as grey dead ends",
   "- the cliff red and dashed",
   "- a text block: next stop, distance, ETA with an EARLY/LATE label, and the clock.",
   "A `SetSignal(bool)` hook drives the tunnel's NO SIGNAL."],
  ["A capture test writes `Logs/smoke/gps.png`.",
   "`GpsLayoutTests`: every stop marker lies within 2 px of its projected route point."])
T("T-M2-14", "Tunnel and rumble zones", "S", "code", ["T-M2-07", "T-M2-04", "T-M1-11"], "none", "§3.3",
  ["`TunnelZone`: GPS NO SIGNAL, CCTV grain 1.0, the tunnel lights in Unstable mode, the `Tunnel` snapshot and `amb.tunnel`. It exposes `IsInside` for the sanity drain in M5.",
   "`RumbleZone`: `bus.rumble_strip` plus a 0.1-amplitude camera shake while any wheel is inside above 10 km/h."],
  ["PlayMode `Tunnel_TogglesEffects`: effects are on while inside and off after exiting."])
T("T-M2-15", "Menu diorama v1 (static)", "M", "content", ["T-M2-06", "T-M2-05", "T-M1-16"], "none", "§2.22, D17",
  ["`MenuBuilder` builds the diorama from generated prefabs:",
   "- the trailhead stop with its lamp",
   "- a 60 m road piece and a guardrail",
   "- forest trees",
   "- a waiting figure (a plain greybox capsule and sphere; swapped for `GreyboxPassengerView` in T-M3-01).",
   "Also: `NightLightingPreset`, `CameraDrift`, and the lamp buzz tied to flicker, `amb.forest_night` and `amb.wind`.",
   "The UI goes on the dark side of the frame, through `UITheme`."],
  ["The capture `Logs/smoke/menu.png` shows the lit stop and the figure.",
   "The menu buttons still work, and New Run loads the night."])

# ------------------------------------------------------------------------------------------ M3
M("M3", "Passengers, stops & economy", "A",
  "Riders from a night manifest wait at stops, board, ride to their destinations and pay fares. The logic/view split for passengers is in place. A night can be completed to a Summary.",
  ["Night 1 without monsters (debug flag) can be played start to finish: every rider is delivered and the Summary ledger is correct.",
   "`Night1_NoMonsters_AllDelivered_LedgerMatches` passes."])
T("T-M3-01", "Passenger logic/view split", "L", "code", ["T-M1-14", "T-M1-21"], "none", "§4.14, §2.6, Appendix A.1",
  ["Add `PassengerViewBase`, `GreyboxPassengerView` (poses, every greybox tell, decoy visuals, accessories), `RendererFlicker` and `ViewFactory`.",
   "`Passenger` loses its greybox `SetPose`. It gains `Anchor_Head` (logic) and a `PassengerViewBase` reference.",
   "Seed the `PassengerLookDefinition`s `look01`–`look12`, with distinct greybox colours and accessories.",
   "`PrefabBuilder` generates `Passenger.prefab` as a logic root plus a view created at spawn.",
   "The menu figure uses `GreyboxPassengerView`."],
  ["`ViewRulesTests`: views have no colliders or rigidbodies.",
   "The smoke test still boards and kicks.",
   "`PassengerViewTests` (PlayMode): every `TellId` changes something visible (a renderer bounds, scale or rotation delta) or is documented as a no-op."])
T("T-M3-02", "PassengerRegistry, riders, destinations, alight-then-board, seating zones", "M", "code", ["T-M3-01", "T-M2-11"], "none", "§2.4, §2.6",
  ["Add `RiderSpec`, `RiderRecord` and `PassengerRegistry`, plus `Passenger.Bind(ShiftServices)`.",
   "Stops run the alight-first sequence, and `Leave()` is wired.",
   "Seat choice uses the `seating` stream, with the zone preference API (`SeatZone` Front/Mid/Rear).",
   "Add the F1 section: riders."],
  ["PlayMode `Stop_AlightsThenBoards`: at a stop with 2 alighting and 2 waiting riders, both alight before the first boards."])
T("T-M3-03", "NightDefinition, scripted manifest, ManifestSpawner", "M", "content", ["T-M3-02"], "none", "§2.19",
  ["Add the `NightDefinition` type, including `endStopId`, and seed `Night1`–`Night5` (Night 1 scripted per §2.19 and ending at `church`, D43; Nights 2–5 parameters only).",
   "When `endStopId` isn't the route's last stop, spawn the `NightEndBarrier` 60 m past it, and tell the GPS to grey out the route beyond it.",
   "Add the `Manifest` type and `ManifestSpawner`, which spawns every rider at their stop at night start, in the Waiting state.",
   "Until T-M7-01, nights 2–5 reuse Night 1's scripted list.",
   "Add a debug flag `noMonsters` that removes monster riders."],
  ["PlayMode `Manifest_Night1Spawns`: 6 riders at the right stops, with the right looks and destinations.", "The barrier stands at 1,960 m on night 1 and is absent on night 2."])
T("T-M3-04", "Missed-stop consequences and terminus delivery", "S", "code", ["T-M3-02"], "none", "§2.4",
  ["On `OnMissed`, waiting riders walk away and despawn, and riders aboard who were going there are retargeted to the night's end stop.",
   "At the end stop, every non-monster rider aboard is delivered. The end stop can't be Missed."],
  ["PlayMode `MissedStop_RidersLostAndCarried` passes."])
T("T-M3-05", "Ledger core, EconomyRules, ShiftLedger", "M", "code", ["T-M3-02", "T-M2-12"], "none", "§2.7",
  ["Add the `Ledger` core (entries, totals by kind) and `EconomyMath` (tip).",
   "Add `ShiftLedger` and `EconomyRules`: board → fare; deliver at an Early stop → tip; kick → refund or bounty; death → refund.",
   "The wallet lives in `RunState`, in cents."],
  ["`LedgerTests` and `EconomyMathTests` pass.",
   "PlayMode `Economy_FareOnBoard_TipOnEarly` passes."])
T("T-M3-06", "ShiftDirector states, Intro card, Summary, NightResult", "M", "code", ["T-M3-05", "T-M1-17", "T-M1-12"], "none", "§2.1, §2.21, §4.4",
  ["`ShiftDirector` gets every state: Depot (a placeholder until T-M7-05), Intro, Driving, Summary, Dying, GameOver, RunWon.",
   "`NightResult` carries the ledger totals, the arrivals, sanity and stats.",
   "Add `IntroCardScreen` and `SummaryScreen` (§2.21).",
   "`RunFlow.CompleteNight` then loads the next night.",
   "Input contexts follow the states."],
  ["PlayMode `Night_CompletesToSummary`: after the terminus, Summary appears with correct totals; Continue loads night 2."])
T("T-M3-07", "Fare box display", "S", "code", ["T-M3-05"], "none", "§2.7, §4.13",
  ["Add a world-space `FareBoxView` at `Anchor_Dash_FareBox`. It shows the night total with ±delta pops (green/red plus the sign), and plays `bus.fare_tap` and `ui.money_up`/`ui.money_down`."],
  ["PlayMode: a boarding event produces a `+$3.50` pop within 0.2 s."])
T("T-M3-08", "Decoy behaviours", "S", "code", ["T-M3-01", "T-M3-03"], "none", "§2.6",
  ["Add a decoy driver component: it sets the decoy `TellId` on the view with the §2.6 timings, and plays `pax.mutter_loop` for `Mutter`.",
   "Riders whose manifest entry has a decoy get it."],
  ["PlayMode `Decoys_ShowTheirTell`: each `DecoyKind` has an observable view change within 20 s."])
T("T-M3-09", "Night 1 (no monsters) end-to-end tests", "S", "test", ["T-M3-06", "T-M3-04", "T-M2-10", "T-M3-03"], "none", "§2.19, §2.7",
  ["Add PlayMode `Night1_NoMonsters_AllDelivered_LedgerMatches`: AutoPilot serves every stop, and the expected fares are 5 × 350 plus the tips for the stops reached Early.", "Add `Night1_DurationReport`: AutoPilot at `timeScale` 1 plays night 1 (Intro to Summary) and logs `[NIGHT1] duration=…s` to the test output and the F1 overlay. **It is informational and never fails** (D43: ~5 min is a guideline, not a gate)."],
  ["The test passes. Its expected totals are computed from the manifest and the arrival ratings, not hard-coded."])

# ------------------------------------------------------------------------------------------ M4
M("M4", "Attention, threat, kicking, death, scares — the Starer and the cliff", "A",
  "The first monster can kill you fairly, and you can kick it out. The cliff kills. Death wipes the run.",
  ["Night 1 as designed: the Starer boards at `campground` and escalates after the cliff. Ignoring it kills you with a readable telegraph; watching it keeps it down; kicking it pays a bounty.",
   "Driving off the cliff plays the fall cam and ends the run.",
   "Every M4 PlayMode test passes."])
T("T-M4-01", "PlayerAttention", "M", "code", ["T-M3-02"], "none", "§2.8, D35",
  ["Add `PlayerAttention`:",
   "- mode and `AttentionOnRoad`",
   "- observers built from `CctvCamera`, the driver camera, the on-foot camera and the mirror (the mirror slot is filled in M7)",
   "- the viewport, range and Occluder-linecast test",
   "- per-rider `ObservedBy`, `TimeObserved`, `TimeSinceObserved`, in LateUpdate with no allocations.",
   "Add the F1 section: attention and observed riders."],
  ["PlayMode `Attention_ObservedByCctvWhenInFrame`: a rider in CAM2's view within 7 m is observed only while CAM2 is active.",
   "PlayMode `Attention_OccluderBlocks`.",
   "PlayMode `Attention_RoadYaw`: turning the head 40° clears `AttentionOnRoad`."])
T("T-M4-02", "Threat cores", "S", "code", ["T-M1-01"], "none", "§2.9",
  ["Add `ThreatMeterCore` (value, stage, events, grace, freeze) and `ThreatRules` (first match wins; multipliers on positive rates only)."],
  ["`ThreatMeterTests` and `ThreatRulesTests` pass, including the stage boundaries (25/50/100), the sanity factor at 60/30/0, and the grace period."])
T("T-M4-03", "Monster framework", "L", "code", ["T-M4-01", "T-M4-02", "T-M3-03"], "none", "§2.9, §4.8",
  ["Add `MonsterDefinition` and seed Starer, Whisperer, Mimic and WeepingAngel (rules, observer kinds, escape, bounty, journal text).",
   "Add `MonsterBrain` (Bind, rules context from attention/sanity/night), the `ThreatMeter` component, the `MonsterSystem` registry, and the `IKickHandler` hook on `Passenger`.",
   "`PrefabBuilder` generates a monster prefab variant for each definition.",
   "Delete `Monster.cs`, `StaringMonster.cs`, `StaringMonster.prefab` and the MVP test monster. Keep `WeepingAngel.cs` and its prefab as a reference until T-M6-05 ports them.",
   "Add the F1 section: monsters."],
  ["PlayMode `Monster_ThreatFollowsRules`: a spawned Starer's threat rises while unobserved and falls while observed, at the defined rates ± 5 %."])
T("T-M4-04", "ScareArbiter core", "S", "code", ["T-M1-01"], "none", "§2.17",
  ["Add the pure `ScareArbiter`: the global gap, per-tier cooldowns, Kill preemption, the Monster queue (size 1, 5 s), Startle drops, Ambient telegraph suppression, and the no-scare states."],
  ["`ScareArbiterTests` covers every rule in §2.17 with a fake clock."])
T("T-M4-05", "ScareDefinition, ScarePlayer, ScareDirector", "L", "code", ["T-M4-04", "T-M1-14", "T-M1-11"], "none", "§2.17, §4.8",
  ["Add the `ScareDefinition` type.",
   "Add `ScarePlayer`, implementing every `ScareStepKind`: sounds, overlays (a full-screen `RawImage`), shake, cabin-light flicker/off, CCTV static/cut, input lock, forced home view, scare head at an anchor (on layer ScareFx), all-passengers-react, hands over camera, blackout, wait. It respects **Scare intensity Reduced**.",
   "Add `ScareDirector`, which wraps the arbiter and applies the sanity cost (−5 / −2).",
   "Generate the greybox scare heads (a sphere with dark eye sockets) and overlay textures.",
   "Add the F1 cheat: trigger scare by id."],
  ["PlayMode `Scares_StepsExecute`: each step kind runs without error and restores state (camera, lights, input) afterwards.",
   "PlayMode `Scares_ReducedIntensity`: no overlay alpha above 0.5 and no shake."])
T("T-M4-06", "DeathDirector, presenters, Game Over, run wipe", "M", "code", ["T-M3-06", "T-M4-05"], "none", "§2.14, §2.21, §4.4",
  ["Add `DeathDirector` with `Die(cause, sourceId)`, preventers, `OnDeathStarted`/`OnPresented`, and the Dying input context.",
   "Add the presenters `MonsterKillPresenter` and `BlackoutPresenter` (generic). The Fall presenter comes in T-M4-09.",
   "Add `GameOverScreen`. Delete `GameOverMenu` and `LegacyGameOver`.",
   "`RunFlow` deletes the run save at `OnDeathStarted`.",
   "Add `DeathCause.Abandoned` without a presenter."],
  ["PlayMode `Death_DeletesRunSave`: `run.json` is gone before the presenter finishes.",
   "Game Over shows the cause and the hint.",
   "New Run from Game Over works."])
T("T-M4-07", "KillSequence", "M", "code", ["T-M4-03", "T-M4-05", "T-M4-06"], "none", "§2.14, D31",
  ["Add `KillSequence`:",
   "- the telegraph (4 s, lights, rumble, telegraph tell, `ScareDirector.TelegraphActive`)",
   "- escape conditions (`ObserveFor`, `UnobservedFor`)",
   "- kick escape",
   "- a single-sequence lock through `MonsterSystem`, with the other monster holding at 99.9",
   "- the Salt preventer hook (a checked interface; the item arrives in M7)",
   "- the kill scare, then `Die(MonsterKill)`."],
  ["PlayMode `KillSequence_OnlyOneAtATime` passes.",
   "PlayMode `KillSequence_EscapeResetsThreat` passes."])
T("T-M4-08", "The Starer", "M", "content", ["T-M4-07"], "none", "§2.10",
  ["Add `StarerAdvanceConfig` and the ability: target row, teleport while unobserved ≥ 1 s, a full-row fallback.",
   "Tells: head-track (the existing math), Stillness, EyesWide.",
   "Add `scare.starer.lens` (triggered on the next CCTV cycle within 20 s of first entering Aggressive) and `scare.starer.kill`.",
   "Night 1's scripted Starer boards at `campground`."],
  ["PlayMode `Starer_AdvancesOnlyUnobserved` passes.",
   "PlayMode `Starer_Ignored_Kills`: with the bus parked, `Die` is called 78–88 s after it sits down (10 s grace + 66.7 s at 1.5/s × night 1's 1.0 + the 4 s telegraph + the kill scare).",
   "PlayMode `Starer_Watched_NeverLethal`: CAM on it for 120 s, threat < 25.",
   "PlayMode `Starer_TelegraphEscape_ByWatching` passes."])
T("T-M4-09", "The cliff: fall death", "M", "code", ["T-M4-06", "T-M2-07"], "none", "§2.14 (Fall), §3.3",
  ["Add the `FallZone` behaviour (hull enters → `Die(Fall)`) and `FallDeathPresenter`: input lock, `FallCamera` at `FallCamAnchor` tracking the bus, slow-mo 0.5 for 1 s, `death.fall_wind`, the valley impact sound and shake, then fade with the text at 3 s.",
   "The KillPlane ignores the cliff area."],
  ["PlayMode `Cliff_DriveOff_FallDeath`: AutoPilot with a lateral offset command drives off at 1500 m → cause Fall → Game Over.",
   "The capture `Logs/smoke/fall.png` shows the bus mid-fall."])
T("T-M4-10", "Monster and death cheats", "S", "code", ["T-M4-07", "T-M1-18"], "none", "§4.18",
  ["Add the cheats: spawn a monster seated, set threat, force Lethal, god mode, kill me (by cause), win the night."],
  ["Each cheat works from F1 in a Development build, and none compiles into a release build."])
T("T-M4-11", "Night 1 with the Starer: end-to-end tests", "S", "test", ["T-M4-08", "T-M4-09", "T-M4-10"], "none", "§2.10, §2.13",
  ["Add PlayMode `Starer_Kicked_BountyAndSanity`: stop, leave the seat, walk up (scripted `OnFootController.ExternalControl`), kick → +500 ¢, sanity +10.",
   "Add PlayMode `Night1_DemoRun_WithKick`: AutoPilot drives night 1, stops once to kick the Starer, and reaches the Summary with no errors. It logs the duration (informational, not asserted)."],
  ["Every M4 PlayMode test passes in one `playmode` run."])


# ------------------------------------------------------------------------------------------ M4b
M("M4b", "Controller support & the Alpha build", "A",
  "Full gamepad play arrives early, together with the first external build: the **Alpha**. That is night 1 end to end (the Starer, the cliff, death, the Summary), playable on keyboard and mouse **or** a controller.",
  ["**Gate A (Alpha)** holds:",
   "- A new player can install, start from the menu, play night 1 to the Summary or to death, change settings and quit **using only a controller**, on an Xbox-layout and a PlayStation-layout pad, on Windows and macOS.",
   "- The same works with keyboard and mouse.",
   "- `0.5.0-alpha` is on the restricted itch page.",
   "Every M4b PlayMode test passes."],
  "M4b needs M4 (night 1 with the Starer) and runs alongside M5. From then on, `UI_EveryScreenPadNavigable` discovers every `ScreenView` automatically, so each screen added in M5–M8 (Depot, Journal, Credits, the new settings) must be pad-navigable to pass its own milestone (D45).")
T("T-M4b-01", "Gamepad driving and on-foot feel", "M", "code", ["T-M1-07", "T-M4-11"], "none", "§4.10, D45",
  ["Tune the gamepad bindings for Driving and OnFoot, which exist since T-M1-06:",
   "- stick deadzones: Input System processors, 0.15 inner and 0.95 outer",
   "- analogue throttle and brake on the triggers: `BusController.SetInput` already takes −1..1",
   "- steering response curve",
   "- right-stick look with acceleration: a separate `gamepadLookSensitivity` (default 180°/s at full deflection)."],
  ["PlayMode `Gamepad_DrivesBus`, using `InputTestFixture` with a virtual gamepad:",
   "- full RT reaches 50 km/h in the same time as W, within 10 %",
   "- a half trigger holds a lower steady speed",
   "- left-stick steering turns the bus",
   "- Y cycles CCTV.",
   "PlayMode `Gamepad_OnFootKick`: move, look and kick with the pad."])
T("T-M4b-02", "Active-device tracking and prompt glyphs", "M", "code", ["T-M4b-01", "T-M1-12"], "none", "§4.10, §4.13",
  ["`InputService` tracks the last-used device family (Keyboard&Mouse, Xbox, PlayStation, generic Gamepad) and raises `OnDeviceFamilyChanged`.",
   "Every prompt (HUD, hints, screens) is built from `GetBindingDisplayString` for the active control scheme, and rendered with a TMP sprite asset of glyphs: generated placeholder glyphs now, with art in `UITheme` later.",
   "The cursor hides while a pad is active."],
  ["PlayMode `Prompts_SwitchWithDevice`: pressing a pad button switches the leave-seat prompt to the pad glyph within 1 frame, and a key press switches it back."])
T("T-M4b-03", "Gamepad UI navigation, with an auto-discovering test", "M", "code", ["T-M4b-02", "T-M1-16", "T-M3-06", "T-M4-06"], "none", "§4.13",
  ["Every existing screen is fully navigable with the D-pad or left stick plus A/B:",
   "- menu, pause, options, controls, intro, summary, game over",
   "- explicit `Navigation` where the automatic order is wrong",
   "- sliders and dropdowns operable with the D-pad",
   "- focus is never lost (if the selection becomes null, the first `Selectable` is re-selected)",
   "- B = Cancel everywhere.",
   "Add PlayMode `UI_EveryScreenPadNavigable`. It finds **every** `ScreenView` prefab through `ContentValidator`'s registry, so screens added later are covered automatically."],
  ["The test passes: a virtual pad reaches and activates every interactive element on every screen, and B backs out."])
T("T-M4b-04", "Gamepad rebinding and pad settings", "S", "code", ["T-M4b-03", "T-M1-05"], "none", "§2.23, §4.10",
  ["The Controls screen gets a Gamepad tab, with the same rules as the keyboard tab.",
   "New settings: stick look sensitivity, pad invert Y, vibration on/off, and trigger deadzone.",
   "They're added to `SettingsData` **v1**, because this is before G1 and so needs no migration (§4.9); update the v1 fixture.",
   "T-M8-03 keeps these rows when it extends the Options screen."],
  ["Pad rebinds and pad settings persist across a restart.", "`SaveFixtureTests` passes with the updated fixture."])
T("T-M4b-05", "Controller disconnect handling", "S", "code", ["T-M4b-02", "T-M1-08"], "none", "§4.11",
  ["If the active pad disconnects (`InputSystem.onDeviceChange`) in Driving or OnFoot, pause, and show \"Controller disconnected — reconnect or press any key\". Reconnecting restores the focus; the player unpauses."],
  ["PlayMode `Gamepad_DisconnectPauses` passes."])
T("T-M4b-06", "Rumble", "S", "code", ["T-M4b-04", "T-M4-09"], "none", "§2.17",
  ["Add haptics through `Gamepad.SetMotorSpeeds`, driven by a small `HapticsService`:",
   "- crash (by deltaV)",
   "- rumble strip",
   "- kill-sequence telegraph (a pulse)",
   "- kill scare (a burst)",
   "- fall impact.",
   "It respects the vibration setting and Scare intensity Reduced (half strength), and stops on pause and on scene change.",
   "Priority: **COULD**, so it may slip past the Alpha."],
  ["Motors are zero when paused, after a scene change, and with vibration off."])
T("T-M4b-07", "[HUMAN] Controller QA pass", "S", "[HUMAN]", ["T-M4b-03", "T-M4b-04", "T-M4b-05"],
  "[HUMAN] A person with an Xbox-layout and a PlayStation-layout controller, on Windows and on macOS.", "M4b acceptance",
  ["Play the Gate A script (M4b acceptance) with each pad on each OS, and log issues as tickets."],
  ["The script passes on 2 pads × 2 OSes, or every failure has a ticket."])
T("T-M4b-08", "Alpha build (Gate A)", "S", "tooling", ["T-M4b-07", "T-M4-11", "T-M2-15", "T-M0-09"], "none", "§1.4, §4.20",
  ["Add an Alpha section to `docs/playtest.md`: the night 1 script, a controller section, and a short questionnaire.",
   "Build and push `0.5.0-alpha` to both channels.",
   "Tick Gate A in §6."],
  ["`--selftest` passes on the macOS build.",
   "Both channels show 0.5.0-alpha.",
   "Every Gate A criterion is checked off."])

# ------------------------------------------------------------------------------------------ M5
M("M5", "Sanity, hallucinations & the Whisperer", "A",
  "Low sanity visibly and audibly changes play and can kill. The Whisperer pulls your eyes off the road.",
  ["Night 2 composition (Starer + Whisperer, via the debug manifest) is playable.",
   "Sanity drains and restores as in §2.15, hallucinations appear by tier, and sanity 0 kills.",
   "Every M5 PlayMode test passes."])
T("T-M5-01", "SanityCore", "S", "code", ["T-M1-01"], "none", "§2.15",
  ["Add the pure `SanityCore`: value, clamping, tiers, named drains per second, one-off deltas, carry-over, and the threat factor."],
  ["`SanityCoreTests`: tier boundaries at 75/50/25/10; carry-over (end 20 → 60, end 50 → 80, end 90 → 100); drains stack and remove cleanly."])
T("T-M5-02", "SanitySystem wiring and carry-over", "M", "code", ["T-M5-01", "T-M4-05", "T-M3-05", "T-M2-14"], "none", "§2.15",
  ["`SanitySystem` hooks up every §2.15 source: baseline, `TunnelZone.IsInside`, doors open at a stop, the kick outcomes, rider death, scares (through `OnScareStarted`) and Coffee (M7).",
   "It freezes outside Driving.",
   "`RunFlow` applies the carry-over.",
   "Add the F1 cheats: set sanity; show drains."],
  ["PlayMode `Sanity_TunnelDrains`: about −0.55/s inside, −0.05/s outside.",
   "PlayMode `Sanity_CarryOverBetweenNights` passes."])
T("T-M5-03", "Sanity presentation and SanityZero death", "M", "code", ["T-M5-02", "T-M4-06"], "none", "§2.15, §2.14",
  ["Add `SanityFx`: the vignette/saturation Volume weights by formula, and the `san.heartbeat_loop` volume.",
   "`OnDepleted` → `Die(SanityZero)`. The presenter is `scare.blackout.generic`, or the Whisperer variant (T-M5-06)."],
  ["PlayMode `Sanity_ZeroKills` passes.",
   "Vignette intensity at sanity 50 is 0.183 ± 0.01."])
T("T-M5-04", "Hallucination picker, definitions, director", "M", "code", ["T-M5-02", "T-M4-05"], "none", "§2.16",
  ["Add the pure `HallucinationPicker` (tier eligibility, weights, cooldowns, the `hallucination` stream) and the `HallucinationDefinition` type.",
   "Seed the 14 entries in §2.16.",
   "Add `HallucinationDirector` (tier intervals, requests through `ScareDirector`).",
   "Add the F1 cheat: trigger hallucination."],
  ["`HallucinationPickerTests` pass: determinism, eligibility, cooldowns.",
   "PlayMode `Hallucinations_SuppressedDuringTelegraph` passes."])
T("T-M5-05", "Hallucination effects", "M", "code", ["T-M5-04", "T-M2-13", "T-M3-07"], "none", "§2.16",
  ["Implement the effects `PhantomPassengerEffect` (a CCTV-only phantom on the active camera's render layer), `FigureInHeadlightsEffect` (straights only, never in the cliff range), `SteeringDriftEffect` (never within the cliff range ±50 m), `CameraCycleEffect`, `GpsGlitchEffect`, `FakeMoneyEffect` and `HornEffect`.",
   "The step-only entries use `ScareDefinition`s.",
   "Voiced entries set captions."],
  ["PlayMode `Hallucinations_EachRunsClean`: each id plays and cleans up.",
   "The phantom isn't in `PassengerRegistry` and isn't observed.",
   "Steering drift never fires within the cliff range ±50 m (forced test)."])
T("T-M5-06", "The Whisperer", "M", "content", ["T-M5-03", "T-M4-07"], "none", "§2.11",
  ["Add `WhispererDrainConfig` and the ability: the drain per stage through `SanitySystem.AddDrain`.",
   "Add the whisper audio: the 3D loop plus the 2D feed while ObservedByCctv, with volume by stage.",
   "Tells: WhisperLean, MouthWhisper, JawStretch.",
   "Add `scare.whisperer.driver` (the monster scare) and `scare.whisperer.kill` (the SanityZero presenter variant).",
   "A kick stops the audio and the drain instantly."],
  ["PlayMode `Whisperer_Ignored_SanityDeath`: road-only driving (no tunnel) from sanity 100 kills 120–200 s after it sits down. The model estimate is about 145 s: Lethal at about 93 s, then 1.25/s total drain.",
   "PlayMode `Whisperer_Watched_ThreatFalls` passes.",
   "PlayMode `Whisperer_Kicked_DrainStops` passes."])
T("T-M5-07", "M5 end-to-end run", "S", "test", ["T-M5-05", "T-M5-06"], "none", "§2.15–§2.16",
  ["Add PlayMode `Night2_Composition_Playable`: the debug manifest with a Starer and a Whisperer. AutoPilot and a scripted CCTV-checking policy complete the night."],
  ["The night completes with no errors, and at least one hallucination fires below 75 sanity."])

# ------------------------------------------------------------------------------------------ M6
M("M6", "The Mimic, the Weeping Angel & passenger death", "A",
  "All four monsters are in and beatable. Innocents can die, the Mimic's copy mechanic works per D22, and the Weeping Angel stalks per D47.",
  ["Night 3 composition (debug manifest) is playable, both with the Mimic + Starer and with the Mimic + Weeping Angel.",
   "Every M6 PlayMode test passes."])
T("T-M6-01", "Passenger death pipeline", "S", "code", ["T-M3-05", "T-M5-02"], "none", "§2.6",
  ["Add `Passenger.Die()`: `PlayDeath`, a 0.6 s cabin flicker, removal after 2 s, `BusCabin.OnPassengerDied`, the registry outcome, the refund (EconomyRules) and sanity −10."],
  ["PlayMode `PassengerDeath_RefundsAndDrains` passes."])
T("T-M6-02", "The Mimic", "L", "content", ["T-M6-01", "T-M4-07"], "none", "§2.12, D22, D39",
  ["Add `MimicCopyConfig` and the ability:",
   "- template choice at boarding (or the next rider to sit)",
   "- `ViewFactory.Recreate` to change look",
   "- the replace trigger (entering Aggressive, or the 120 s hunger timer)",
   "- the replace sequence: flicker, template death, teleport, threat set to 20, retarget",
   "- the hide camera K on layer `MimicHideCamK`, re-picked after each replace",
   "- shadows off, and the `MimicFlicker` tell = threat / 100.",
   "Its observer kinds exclude OnFoot.",
   "Add `scare.mimic.turn` (monster scare) and `scare.mimic.kill`."],
  ["PlayMode `Mimic_Boards_PairVisible`: two riders with the same look id aboard.",
   "PlayMode `Mimic_HiddenFromOneCamera`: renderers aren't drawn by camera K (culling mask check).",
   "PlayMode `Mimic_OnFootLookDoesNotRaiseThreat` passes."])
T("T-M6-03", "Flashlight reveal component", "S", "code", ["T-M6-02"], "none", "§2.12, §2.18",
  ["Add `FlashlightReveal` on `OnFootRig`: a head spot light, and after 0.75 s of interactor focus on the Mimic it sets `MimicReveal` for 2 s plus `mon.mimic.reveal`.",
   "It's disabled until the item enables it (M7). A debug toggle is available now."],
  ["PlayMode `Flashlight_RevealsOnlyMimic` passes."])
T("T-M6-04", "Mimic end-to-end tests", "S", "test", ["T-M6-02", "T-M6-03"], "none", "§2.12, §2.13",
  ["Add PlayMode `Mimic_KickTemplate_InnocentPenalty`, `Mimic_KickMimic_Bounty` and `Mimic_Hunger_ReplacesVictim` (never observed; a replace happens at 120 s ± 1)."],
  ["Every M6 test passes."])
T("T-M6-05", "The Weeping Angel", "M", "content", ["T-M4-07", "T-M4-08"], "none", "§2.12b, D47",
  ["Add `AngelStalkConfig` and the ability:",
   "- stand at Unsettled; the target point from threat (rounded to a row), ending at `StandPoint` at Lethal",
   "- walk seat → aisle → along the aisle at 0.55 m/s only while unobserved; freeze while observed; teleport after 3 s without progress; never sit back down",
   "- after a kill-sequence escape (threat 60), jump back to the matching row on the next unobserved frame.",
   "Port the path from `WeepingAngel.NextWaypointLocal` and the head-or-root check from `WeepingAngel.IsBeingWatched` into the observation test for this monster.",
   "Tells: `Stillness` = 1, `AngelWeep`, `AngelReach`, and the looping `mon.angel.scrape` while it moves.",
   "Add `scare.angel.closer` (monster scare) and `scare.angel.kill`.",
   "Delete `WeepingAngel.cs` and `WeepingAngel.prefab`."],
  ["PlayMode `Angel_FreezesWhenObserved`: observed mid-walk, it moves less than 1 cm in 2 s and its threat doesn't change.",
   "PlayMode `Angel_WatchingNeverLowersThreat`: observed for 60 s from threat 50, it's still at 50.",
   "PlayMode `Angel_Ignored_Kills`: with the bus parked and multiplier 1.0, `Die` is called 95–106 s after it sits down (10 s grace + 83.3 s at 1.2/s + the 4 s telegraph + the kill scare).",
   "PlayMode `Angel_TelegraphEscape_ByWatching`: 2 s of observation during the telegraph sets threat to 60.",
   "PlayMode `Angel_Kicked_Bounty`: walk up while looking at it, kick → +500 ¢.",
   "No `WeepingAngel` reference remains."])

# ------------------------------------------------------------------------------------------ M7
M("M7", "Run loop: nights, manifests, items, shop, saves, journal", "A",
  "A full 5-night run can be won or lost, with items, the shop, Continue and the journal.",
  ["A human can play New Run → 5 nights → Run Won, using cheats to shorten nights.",
   "A death wipes the run while the journal persists; quitting mid-night counts as death.",
   "Every M7 PlayMode test passes."])
T("T-M7-01", "ManifestGenerator", "M", "code", ["T-M3-03", "T-M4-03"], "none", "§2.19",
  ["Add the pure `ManifestGenerator` implementing rules 1–8 of §2.19.",
   "`ManifestSpawner` uses it for nights 2–5."],
  ["`ManifestGeneratorTests`: 1000 seeds × nights 2–5 meet every constraint (aboard ≤ 10, the Mimic's precondition, no shared looks, monster windows and type rules, decoy counts); the same (seed, night) gives an identical manifest."])
T("T-M7-02", "Run persistence, Continue, abandoned detection", "M", "code", ["T-M4-06", "T-M3-06"], "none", "§4.4, §4.9, D21",
  ["Complete `RunFlow`: `nightInProgress` on entering Driving, the save after the Summary, deletion on death and on a win, abandoned detection at Boot (with the meta record and menu message), and Continue.",
   "Debug runs never write saves."],
  ["PlayMode `Abandon_MidNight_NextBootAbandoned` (simulated restart through `RunFlow.Boot` with a temporary save root) passes.",
   "PlayMode `Continue_FromDepot` passes."])
T("T-M7-03", "Inventory, item definitions, ItemSystem, slots HUD", "M", "code", ["T-M1-06", "T-M5-02"], "none", "§2.18",
  ["Add the pure `Inventory` (slots, permanents, the buy rules), `ItemDefinition` and the `ItemEffect` base.",
   "Seed the 5 items.",
   "Add `ItemSystem` (keys 1–3, the passive Salt).",
   "Add `ItemSlotsView` on the HUD."],
  ["`InventoryTests` pass: every buy rule and the slot behaviour."])
T("T-M7-04", "Item effects", "M", "code", ["T-M7-03", "T-M6-03", "T-M4-07", "T-M4-01"],
  "T-M1-10 ([HUMAN] mixer) for the Earplugs snapshot. Without it the effect still zeroes the drain and logs a warning.", "§2.18",
  ["Implement the effects:",
   "- `RestoreSanityEffect`",
   "- `EarplugsEffect`: 30 s, the drain ×0, the snapshot, a HUD timer",
   "- `SaltCharmEffect`: an `IDeathPreventer` for MonsterKill only, which expels the monster",
   "- `FlashlightEffect`: enables `FlashlightReveal`",
   "- `MirrorEffect`: the mirror camera and RT on `Anchor_Dash_Mirror`, and registration of the Mirror observer."],
  ["PlayMode `Items_Coffee`, `Items_Earplugs`, `Items_SaltExpels`, `Items_SaltDoesNotStopFall` and `Items_MirrorObserves` pass."])
T("T-M7-05", "Depot shop", "M", "code", ["T-M7-03", "T-M3-06"], "none", "§2.18, D33",
  ["Add the `DepotShop` service (`TryBuy` with a `BuyFailure` reason) and `DepotScreen` (list, prices, disabled reasons, wallet, Journal button, Start shift).",
   "The Depot state runs on nights 2–5."],
  ["PlayMode `Shop_BuyRules`: can't afford; slots full; a permanent bought once; the wallet decreases by the price."])
T("T-M7-06", "Journal", "M", "code", ["T-M4-08", "T-M5-06", "T-M6-02", "T-M6-05"], "none", "§2.20",
  ["Add `JournalRules`: seen (3 s cumulative observation while Unsettled or above), kicked, killedBy → `MetaService` (saved immediately).",
   "Add `JournalScreen`, opened from the menu and the depot, with its sections and ??? placeholders.",
   "Game Over shows the cause's hint."],
  ["PlayMode `Journal_UnlocksPersistAfterDeath` passes.",
   "`JournalProgressTests` pass."])
T("T-M7-07", "Run Won and run statistics", "S", "code", ["T-M7-02"], "none", "§2.21",
  ["Add `RunWonScreen`.",
   "Game Over and Run Won show the run statistics from `RunState`.",
   "The meta counters update."],
  ["PlayMode `Run_FiveNights_WinCheat_RunWon`: 5 nights with the win-night cheat → Run Won, and the save is deleted."])
T("T-M7-08", "Pause menu: the D21 quit rule", "S", "code", ["T-M4-06", "T-M1-08"], "none", "§2.21, D21",
  ["Quit to Menu and Quit Game from Driving or Dying show the confirmation and then `Die(Abandoned)`.",
   "From the Depot they quit normally."],
  ["PlayMode `Pause_QuitDuringDriving_IsDeath` passes.",
   "PlayMode `Pause_QuitInDepot_KeepsRun` passes."])
T("T-M7-09", "Full-run tests", "S", "test", ["T-M7-01", "T-M7-02", "T-M7-04", "T-M7-05", "T-M7-06", "T-M7-07", "T-M7-08"], "none", "§2.1",
  ["Add PlayMode `Run_DeathNight3_JournalPersists` and `Run_ContinueAfterRestart_Night3`."],
  ["Every M7 test passes in one run."])

# ------------------------------------------------------------------------------------------ M8
M("M8", "Menu, onboarding, settings & accessibility", "A",
  "Everything outside the core loop that a finished game needs: the full menu, first-launch warning, settings, hints, captions, credits, and the menu's idle events and arrival sequence.",
  ["A first-time player sees the warning and brightness screen, learns night 1 from the hints, and can find every setting.",
   "The menu diorama has its idle events and the New Run arrival sequence."])
T("T-M8-01", "Main menu, complete", "M", "code", ["T-M7-02", "T-M7-06", "T-M2-15"], "none", "§2.22",
  ["Menu buttons: New Run (confirming if a run exists), Continue — Night N (visibility rule), Journal, Options, Controls, Credits, Quit.",
   "Add the abandoned-run message line and the build label."],
  ["PlayMode `Menu_ContinueVisibility` passes.",
   "PlayMode `Menu_NewRunConfirmsWhenRunExists` passes."])
T("T-M8-02", "First launch: warning and brightness calibration", "S", "code", ["T-M8-01"], "none", "§2.23, D37",
  ["Add `FirstLaunchScreen` (the content and photosensitivity warning) and `BrightnessScreen` (a calibration symbol plus a slider).",
   "They set `warningAcknowledged`."],
  ["PlayMode `FirstLaunch_ShownOnce` passes."])
T("T-M8-03", "Settings: groups, scare intensity, hints, captions, invert Y, defaults", "M", "code", ["T-M1-05", "T-M1-07"],
  "The per-group volume sliders need T-M1-10 ([HUMAN] mixer).", "§2.23",
  ["Extend `OptionsScreen` with every §2.23 row and Restore defaults.",
   "Polish `ControlsScreen` (conflict messages, per-action reset)."],
  ["PlayMode `Settings_AllRowsApply`: each setting changes its target (mixer parameter, `ScarePlayer` mode, hint visibility, look sign)."])
T("T-M8-04", "HintDirector (night 1)", "S", "code", ["T-M4-08", "T-M3-06"], "none", "§2.23",
  ["Add `HintDirector` with the §2.23 triggers, shown once per run on Night 1 only if Hints is on, and a HUD `HintView`."],
  ["PlayMode `Hints_Night1Sequence`: the hints appear in order at their triggers and never on night 2."])
T("T-M8-05", "Captions", "S", "code", ["T-M1-11"], "none", "§2.23",
  ["Add `CaptionView`, subscribed to `AudioService.OnCaption` (a 2.5 s line, 3 lines maximum).",
   "Fill in the caption strings on the voiced SoundDefinitions."],
  ["PlayMode: playing `mon.whisper.driver` shows `[whisper] driver…` when Captions is on, and nothing when it's off."])
T("T-M8-06", "MenuEventDirector (idle events)", "M", "code", ["T-M8-01", "T-M3-01"], "none", "§2.22, D18",
  ["Add the four idle events. They use the `menu` stream, fire every 20–45 s, pause while a submenu is open, and are masked by a flicker."],
  ["PlayMode `MenuEvents_AllFourRun`: each is forced, completes and restores the diorama; none fires while Options is open."])
T("T-M8-07", "New Run arrival sequence", "M", "code", ["T-M8-01", "T-M1-14"], "none", "§2.22, D19",
  ["Add `MenuArrivalSequence`: the bus view (no physics) on a 40 m path, the doors and boarding, then fade.",
   "The Night load happens during it, with `AllowActivation` at the end; the loading bar is the fallback.",
   "Any key skips it."],
  ["PlayMode `Menu_ArrivalHidesLoad`: the night becomes active at most 1 s after the sequence ends; skipping works."])
T("T-M8-08", "Credits", "S", "content", ["T-M8-01"], "none", "§2.22, §1.3",
  ["Add `CreditsDefinition`, seeded with credit roles (placeholder names, filled in later) and the licences of every third-party item in use (TextMesh Pro / LiberationSans, Unity packages, and any fonts or sounds added later).",
   "Add `CreditsScreen`."],
  ["`ContentValidationTests`: every third-party item has a licence line."])

# ------------------------------------------------------------------------------------------ M9
M("M9", "G1 — Playable Greybox", "A",
  "Tuned, performant, playtested greybox build. **Route geometry freezes here.**",
  ["The G1 criteria in §1.4 hold.",
   "The minimum-spec budgets (§4.17) are met or have tickets.",
   "External playtesters finish or die fairly (by the playtest questionnaire).",
   "`0.9.0-greybox` is on the restricted itch page."])
T("T-M9-01", "Balance audit and TuningRecorder", "S", "code", ["T-M7-09", "T-M8-04"], "none", "§0.1, §4.18",
  ["Confirm every **[TUNE]** number in §2 lives in a data asset: grep runtime code for numeric literals in rule code and move any stragglers into `BalanceConfig` or definitions.",
   "Add `TuningRecorder` (development and `--playtest` builds)."],
  ["`ContentValidationTests` also asserts that `BalanceConfig` equals the §2 seed values (only until T-M9-06 changes them deliberately)."])
T("T-M9-02", "Quality levels and performance techniques", "M", "code", ["T-M2-07"], "none", "§4.17",
  ["Add the Low, Medium and High URP assets and quality levels.",
   "Tree GPU instancing and LOD culling, static batching rules, far clip, the shadow policy, and `PerfProbe`."],
  ["An Editor-profiled Medium run on the lead's Mac is at or under the budget table (captured to `Logs/perf.txt`)."])
T("T-M9-03", "[HUMAN] Minimum-spec profiling session", "S", "[HUMAN]", ["T-M9-02", "T-M0-08"],
  "[HUMAN] Needs the minimum-spec machine (the weakest PC available) and a person to run a Development build with the profiler.", "§4.17",
  ["Run the Development build on the minimum-spec machine; record the §4.17 metrics in `docs/playtest.md`.",
   "File a ticket for every metric over budget."],
  ["The metrics table is filled in, and every overage has a ticket."])
T("T-M9-04", "Playtest kit and the 0.9.0-greybox build", "S", "tooling", ["T-M9-01", "T-M0-09", "T-M8-02", "T-M8-03"], "none", "§4.20, §4.18",
  ["Write the G1 script and questionnaire in `docs/playtest.md`, with the `Player.log` and tuning CSV locations.",
   "Add a `--playtest` flag that enables `TuningRecorder`.",
   "Build and push `0.9.0-greybox`."],
  ["Both channels are on itch; `--selftest` passes on the macOS build."])
T("T-M9-05", "[HUMAN] Playtest round", "S", "[HUMAN]", ["T-M9-04"],
  "[HUMAN] At least 5 players who haven't played before. One person can run every session. Collect the CSVs, logs and questionnaire answers.", "docs/playtest.md",
  ["Run the sessions and put the raw results in `docs/playtest-results/`."],
  ["There are results from at least 5 players."])
T("T-M9-06", "Tuning pass (data only)", "M", "content", ["T-M9-05"], "none", "§2 [TUNE]",
  ["Adjust the data assets from the playtest evidence: monster rates, sanity numbers, prices, schedule speed, hallucination intervals.",
   "Log every change in `docs/playtest.md` (the Tuning log) and update the §2 seed notes where the meaning changed.",
   "**No code changes** unless a bug is found."],
  ["The follow-up playtest checklist passes.",
   "Record the median night 1 time for first-time playtesters. **Guideline:** about 5 minutes (D43). If it runs well over, trim night 1 content first (riders, stops, hints), then schedule speed and dwell, and leave the route alone. Not a pass/fail gate.",
   "`BalanceConfig`-equality assertions are updated to the new values."])
T("T-M9-07", "G1 gate review and route geometry freeze", "S", "tooling", ["T-M9-06", "T-M9-03", "T-M4b-08"], "none", "§1.4, D26",
  ["Walk through the G1 criteria.",
   "Tag the repo `g1-greybox`.",
   "From now on, any change to `RouteDefinition` geometry needs a D-row. The dressing (T-M11-11) depends on this."],
  ["Every G1 criterion is checked off in §6, with notes."])

# ------------------------------------------------------------------------------------------ M10
M("M10", "Art pipeline tooling", "B",
  "Artists can drop an asset in, validate it and see it in game with zero code or scene changes.",
  ["One artist-made passenger and one blocker are swapped in with zero code/scene changes and pass validation (T-M10-12)."],
  "M10 can start once T-M3-01 and T-M2-06 are done, and runs in parallel with M4–M9.")
T("T-M10-01", "Art folder skeleton and READMEs", "S", "tooling", ["T-M0-02"], "none", "Appendix A",
  ["Create `Assets/Art/` with the Appendix A folder tree and a `README.md` in each folder explaining what goes there.",
   "Update `docs/ART_CONTRACT.md` to match Appendix A (add the header pointing here as authoritative)."],
  ["The folders exist; the ART_CONTRACT diff matches Appendix A item by item."])
T("T-M10-02", "ArtImportPostprocessor", "M", "tooling", ["T-M10-01"], "none", "Appendix A.1",
  ["Add an `AssetPostprocessor` scoped to `Assets/Art/**`. By folder and suffix it sets:",
   "- scale factor 1 and Humanoid for `Characters/`",
   "- material import mode, and texture type / sRGB / max size from the texture suffix",
   "- audio load type by category (streaming for loops over 10 s, decompress-on-load for short sounds)."],
  ["`ImportRulesTests` imports a test FBX, PNGs and a WAV from `Tests/ArtFixtures/` into a temp folder and asserts the settings."])
T("T-M10-03", "Reference FBX export", "S", "tooling", ["T-M10-01", "T-M1-14", "T-M2-06", "T-M3-01"], "none", "Appendix A.2",
  ["Add `com.unity.formats.fbx`.",
   "Export the greybox bus (with every anchor), a passenger (with the anchors) and each blocker/stop to `Assets/Art/_Reference/` (read-only by convention).",
   "Priority: **SHOULD**."],
  ["The exported FBXs re-import at 1:1 with the anchors present."])
T("T-M10-04", "AnimatedPassengerView, shared AnimatorController, TellBindingSet", "M", "code", ["T-M3-01"], "none", "§4.14, Appendix A.1",
  ["Add `AnimatedPassengerView` implementing `PassengerViewBase`: the Animator parameters from §4.14, Humanoid IK look-at, and the `TellBindingSet` mapping.",
   "A builder generates `Generated/Animation/Passenger.controller` with the states and parameters.",
   "Add the per-monster `AnimatorOverrideController` convention."],
  ["PlayMode `AnimatedView_DrivesAnimator`: with a test Humanoid (Unity's default test rig, or a fixture FBX), every view call sets the expected parameter or blendshape."])
T("T-M10-05", "AnimatedBusView", "S", "code", ["T-M1-14"], "none", "§4.14, Appendix A.2",
  ["Add `AnimatedBusView`: it binds `Wheel_FL/FR/RL/RR`, `Door_Panel` (or A/B), `SteeringWheel`, the `Light_Cabin_*` emissives and the dash anchors, by name."],
  ["`BusViewTests` pass with a fixture FBX hierarchy of the right names."])
T("T-M10-06", "Make View from FBX tool", "M", "tooling", ["T-M10-02", "T-M10-04", "T-M10-05"], "none", "Appendix A",
  ["Add **Tools ▸ Bus Driver ▸ Make View from FBX**. It creates `View_<Name>.prefab` next to the FBX, adds the right view component, assigns the controller or override, auto-binds anchors by name, and registers the result in the matching `PassengerLookDefinition.artView` or `EnvironmentViewSet` entry (after asking to confirm the target).",
   "It's also callable in batch mode with a path argument."],
  ["Running it on the fixture FBX produces a view that the `ArtValidator` passes."])
T("T-M10-07", "ArtValidator and ArtValidationTests", "M", "tooling", ["T-M10-06"], "none", "Appendix A",
  ["Add **Tools ▸ Bus Driver ▸ Validate Art**. It checks:",
   "- the required anchors, clips and tell bindings",
   "- no Collider, Rigidbody or MonoBehaviour except the allowed view components",
   "- pivot and height within ±5 cm / ±5 % of the greybox",
   "- the triangle and texture budgets",
   "- no missing materials or scripts.",
   "`ArtValidationTests` runs it over everything in `Assets/Art/`."],
  ["Deliberately broken fixtures report each problem with the asset path and the reason."])
T("T-M10-08", "F2 greybox ↔ art toggle", "S", "code", ["T-M10-04", "T-M10-05"], "none", "§4.14",
  ["F2 (development builds) flips `ViewFactory.UseArt` and recreates every passenger and bus view, keeping pose and tells."],
  ["PlayMode `ArtToggle_PreservesState`: pose and tell intensities are identical after two toggles."])
T("T-M10-09", "Route01_Dressing scene support", "S", "tooling", ["T-M2-07"], "none", "D26, §4.3",
  ["Create an empty `Assets/Scenes/Route01_Dressing.unity` once. It is never regenerated.",
   "`SceneLoader` loads it additively when it's in the build list.",
   "Write `docs/DRESSING.md`: the rules (static art only; the Containment and Zone layers are forbidden) and how to view it together with `Route01_World` in the Editor."],
  ["`DressingRulesTests`: the dressing scene contains no colliders on World/Zone layers and no scripts except LODGroup, Light and ReflectionProbe."])
T("T-M10-10", "Timeline scare override", "M", "code", ["T-M4-05"], "none", "§2.17, D36",
  ["Add `TimelineScarePlayer`: if `ScareDefinition.timelineOverride` is set, it plays the Timeline instead of the steps, binding the tracks named `Camera`, `Monster`, `Audio`, `Overlay`, `Lights`.",
   "It runs on `DirectorUpdateMode.GameTime`.",
   "Priority: **SHOULD**."],
  ["PlayMode: a fixture Timeline plays and releases the camera/input exactly as the step version does."])
T("T-M10-11", "UCX_ collider extraction for environment art", "S", "tooling", ["T-M10-06"], "none", "Appendix A.4",
  ["When `RouteBuilder` instantiates an art environment view containing `UCX_*` meshes, it creates MeshColliders on the **logic root** from them and disables their renderers.",
   "Priority: **COULD**."],
  ["A fixture blocker with a UCX mesh collides in play, and the validator still passes."])
T("T-M10-12", "[ART] Pipeline dry run: one passenger, one blocker", "S", "[ART]", ["T-M10-07", "T-M10-08"],
  "[ART] One passenger and one blocker delivered to Appendix A.", "§1.4 (G2), Appendix A",
  ["Import, Make View, Validate, then play."],
  ["They show in game with **zero code or scene changes**, and the validator passes. This is the key acceptance test for D16."])

# ------------------------------------------------------------------------------------------ M11
M("M11", "Art & audio integration", "B",
  "Every greybox view has its final art, and the final audio set and mix are in.",
  ["The G2 criteria in §1.4 hold.",
   "`ArtValidationTests` and every PlayMode test still pass with art on.",
   "F2 still toggles."],
  "Each asset ticket is done when the asset passes Validate Art and appears in game with no code changes. If it needs a code change, file a separate code ticket and record why.")
T("T-M11-01", "[ART] Style lock: fonts, palette, logo → UITheme", "S", "[ART]", ["T-M1-12"],
  "[ART] Font files with their licences, a palette, a logo PNG.", "Appendix A.5",
  ["Generate the TMP font assets (a builder step), fill in `Theme.asset`, set the logo slot, and add the licences to `CreditsDefinition`."],
  ["Every screen updates with no per-screen edits."])
T("T-M11-02", "[ART] Bus exterior and interior", "L", "[ART]", ["T-M10-12"], "[ART] Bus FBX(es) and textures per Appendix A.2.", "Appendix A.2",
  ["Make View → assign it to the bus prefab's art view slot (`Bus.prefab` gets an `artView` reference the builder respects)."],
  ["The dimensions validate, wheels spin, the door animates, and the dash displays sit on their anchors."])
T("T-M11-03", "[ART] Passenger kit → 12 looks", "L", "[ART]", ["T-M10-12"], "[ART] A modular passenger kit plus the shared animation set (Appendix A.1).", "Appendix A.1",
  ["Make a view for each look and assign `look01`–`look12`.artView."],
  ["The 12 looks are distinct on a greyscale CCTV capture (a human check of `Logs/smoke/cctv_*.png`).", "Every look validates."])
T("T-M11-04", "[ART] The Starer", "M", "[ART]", ["T-M11-03", "T-M4-08"], "[ART] Starer per Appendix A.1.", "Appendix A.1",
  ["Make View, set the tell bindings and the scare head, and add the kill/lens clips (via the Timeline override if the animators want it)."],
  ["The Starer tests pass with art on."])
T("T-M11-05", "[ART] The Whisperer", "M", "[ART]", ["T-M11-03", "T-M5-06"], "[ART] Whisperer per Appendix A.1.", "Appendix A.1",
  ["As T-M11-04."], ["The Whisperer tests pass with art on."])
T("T-M11-06", "[ART] The Mimic's true form", "M", "[ART]", ["T-M11-03", "T-M6-02"], "[ART] Mimic per Appendix A.1.", "Appendix A.1",
  ["As T-M11-04. The disguise uses the other looks automatically."], ["The Mimic tests pass with art on."])
T("T-M11-14", "[ART] The Weeping Angel", "M", "[ART]", ["T-M11-03", "T-M6-05"], "[ART] Weeping Angel per Appendix A.1 (no walk cycle needed).", "Appendix A.1",
  ["As T-M11-04."], ["The Weeping Angel tests pass with art on."])
T("T-M11-07", "[ART] Environment kit", "L", "[ART]", ["T-M10-12"], "[ART] Road/shoulder materials, guardrail, blockers ×5 (A/B), stops ×6 + terminus + depot, lamps, signs, tunnel, bridge, cliff dressing, rocks, trees ×4 (Appendix A.4).", "Appendix A.4",
  ["Make View for each kind and assign the `EnvironmentViewSet` entries, then run Build All."],
  ["`RouteContainmentTests` still passes (art never changes collision).", "The route captures show the art."])
T("T-M11-08", "[ART] Scare overlays and VFX", "S", "[ART]", ["T-M4-05"], "[ART] `T_Scare_*` overlays and flicker/static sheets.", "Appendix A.5",
  ["Assign them in the `ScareDefinition` steps and the CCTV overlay."], ["The scares use the art; Reduced intensity still caps the overlay alpha."])
T("T-M11-09", "[AUDIO] Final sound set", "L", "[AUDIO]", ["T-M1-11"], "[AUDIO] Clips for every Appendix A.3 id.", "Appendix A.3",
  ["Assign the clips to each `SoundDefinition` and untick `placeholder`."],
  ["`ContentValidator`: zero placeholders left.", "`Audio_EveryIdPlays` passes."])
T("T-M11-10", "[AUDIO] Mix pass", "M", "[AUDIO]", ["T-M11-09", "T-M1-10"], "[AUDIO] + [HUMAN] listening sessions.", "§4.12",
  ["Set the mixer levels, the snapshot values and the per-definition volumes against the loudness targets in Appendix A.3."],
  ["Signed off after a full night's listen on speakers and headphones."])
T("T-M11-11", "[ART] Route dressing", "L", "[ART]", ["T-M10-09", "T-M9-07", "T-M11-07"], "[ART] Hand dressing in `Route01_Dressing` after the G1 route freeze.", "D26",
  ["Dress the route: scare props, the church, the tunnel mouths, the cliff view, set pieces."],
  ["`DressingRulesTests` passes; the performance budgets still hold (re-run T-M9-02's capture)."])
T("T-M11-12", "Lighting pass with final art", "M", "content", ["T-M11-07", "T-M11-02"], "none", "§4.17",
  ["Tune `NightLightingPreset`, the lamp and headlight intensities, and the CCTV grade with the final art. Changes are data only."],
  ["The captures are signed off.", "The CCTV feed is readable (the looks are distinguishable)."])
T("T-M11-13", "G2 gate review", "S", "tooling", ["T-M11-01", "T-M11-02", "T-M11-03", "T-M11-04", "T-M11-05", "T-M11-06", "T-M11-14", "T-M11-07", "T-M11-08", "T-M11-10", "T-M11-11", "T-M11-12"], "none", "§1.4",
  ["Walk through the G2 criteria. Tag `g2-content-complete`."], ["Every G2 criterion is checked off in §6."])

# ------------------------------------------------------------------------------------------ M12
M("M12", "Release", "C",
  "Ship 1.0 publicly on itch.io.",
  ["The G3 criteria in §1.4 hold: a public page, 1.0.0 builds for both platforms, and save fixtures captured."])
T("T-M12-01", "Content lock and bug triage", "S", "tooling", ["T-M11-13"], "none", "§1.4",
  ["Declare content lock (only bug-fix tickets from here) and triage the open bugs into must-fix and won't-fix.",
   "Confirm controller support still passes (`UI_EveryScreenPadNavigable` and a quick pad play-through)."],
  ["The triage list is in `docs/playtest.md`."])
T("T-M12-02", "Final performance pass", "M", "code", ["T-M12-01"], "[HUMAN] The minimum-spec machine, for measurement.", "§4.17",
  ["Fix the budget overages. Re-measure on the minimum-spec machine."], ["Every §4.17 budget is met."])
T("T-M12-03", "[HUMAN] itch.io page", "S", "[HUMAN]", ["T-M12-01"],
  "[HUMAN] Page copy, screenshots and GIFs; content warnings (horror, flashing lights); the controls; the macOS Gatekeeper note; a credits link.", "§4.20",
  ["The human writes the page. The agent can draft the text and capture screenshots from builds."], ["The page is proofread."])
T("T-M12-04", "Release 1.0.0", "S", "tooling", ["T-M12-02", "T-M12-03"], "none", "§4.20, §4.9",
  ["Build the 1.0.0 release (not Development) for both platforms, run `--selftest`, push to itch, and make the page public.",
   "Capture save fixtures from the release build into `Tests/Fixtures/v1-release/`."],
  ["Both downloads reach the menu on a clean machine.", "`SaveFixtureTests` includes the release fixtures."])
T("T-M12-05", "Post-release rules", "S", "tooling", ["T-M12-04"], "none", "§4.9, §4.21",
  ["Document in `CONTRIBUTING.md`:",
   "- every save-model change needs a migration and a fixture",
   "- patch versioning (1.0.x)",
   "- the hotfix flow."],
  ["The rules are present, and a sample migration test template exists."])


# ------------------------------------------------------------------------------------------ render
SIZE_W = {"S": 1, "M": 2, "L": 4}
all_t = {t["id"]: (m, t) for m in MILESTONES for t in m["tickets"]}
errors = []
for tid, (m, t) in all_t.items():
    for d in t["deps"]:
        if d not in all_t: errors.append(f"{tid} depends on unknown {d}")
# cycle check + topo
state = {}
order = []
def visit(n, stack):
    if state.get(n) == 1: errors.append("cycle: " + " -> ".join(stack + [n])); return
    if state.get(n) == 2: return
    state[n] = 1
    for d in all_t[n][1]["deps"]:
        if d in all_t: visit(d, stack + [n])
    state[n] = 2; order.append(n)
for n in all_t: visit(n, [])
if errors:
    print("\n".join(errors)); sys.exit(1)
blocks = {n: [] for n in all_t}
for n, (m, t) in all_t.items():
    for d in t["deps"]: blocks[d].append(n)
# critical path to G1 (M0..M9), weighted by size
g1 = {n for n, (m, t) in all_t.items() if m["phase"] == "A"}
dist, prev = {}, {}
for n in order:
    if n not in g1: continue
    best, bp = 0, None
    for d in all_t[n][1]["deps"]:
        if d in g1 and dist[d] > best: best, bp = dist[d], d
    dist[n] = best + SIZE_W[all_t[n][1]["size"]]; prev[n] = bp
end = max((n for n in dist), key=lambda n: dist[n])
path = []
while end: path.append(end); end = prev[end]
path.reverse()

out = []
w = out.append
w("## 5. Roadmap — milestones and tickets\n")
w("> Generated from `docs/.roadmap-src/tickets.py`. **Edit the script, not this section,** then run `python3 docs/.roadmap-src/tickets.py`. The script checks every dependency exists, rejects cycles, and computes **Blocks**, the blocker register, the critical path and the §6 tracker.\n")
w("### 5.0 Overview\n")
w("| Milestone | Phase | Goal | Tickets | Gate |")
w("|---|---|---|---|---|")
for m in MILESTONES:
    gate = {"M9": "**G1**", "M11": "**G2**", "M12": "**G3**", "M4b": "**A (Alpha)**"}.get(m["id"], "")
    w(f"| {m['id']} — {m['title']} | {m['phase']} | {m['goal']} | {len(m['tickets'])} | {gate} |")
w("")
w("```mermaid")
w("flowchart LR")
w("  M0[M0 Hygiene] --> M1[M1 Architecture]")
w("  M1 --> M2[M2 Route & world]")
w("  M2 --> M3[M3 Passengers & economy]")
w("  M3 --> M4[M4 Threat, death, Starer, cliff]")
w("  M4 --> M4b{{M4b Controller + Alpha = Gate A}}")
w("  M4 --> M5[M5 Sanity & Whisperer]")
w("  M4b --> M9")
w("  M5 --> M6[M6 Mimic & Angel]")
w("  M6 --> M7[M7 Run loop, items, saves]")
w("  M7 --> M8[M8 Menu, settings, a11y]")
w("  M8 --> M9{{M9 = G1 Playable Greybox}}")
w("  M3 -.-> M10[M10 Art pipeline tooling]")
w("  M2 -.-> M10")
w("  M10 --> M11[M11 Art & audio integration]")
w("  M9 --> M11")
w("  M11 --> G2{{G2 Content complete}}")
w("  G2 --> M12{{M12 = G3 Release}}")
w("```\n")
w("**How to read the dependencies:**\n")
w("- Ticket dependencies are exact, and many tickets in different milestones can run in parallel. Two examples: the pure cores (T-M4-02, T-M4-04, T-M5-01) only need T-M1-01; M10 starts as soon as T-M3-01 and T-M2-06 are done.\n")
w("- A milestone is *complete* when all its tickets are done; its acceptance criteria are then checked.\n")
w(f"**Critical path to G1** (weights S=1, M=2, L=4; {dist[path[-1]]} units):")
w(" → ".join(f"`{p}`" for p in path) + "\n")
w("### 5.1 Blocker register\n")
w("Every ticket that needs something outside the codebase, in dependency order. Plan these early: they're the only things an implementing agent can't do alone.\n")
w("| Ticket | Kind | What's needed |")
w("|---|---|---|")
for n in order:
    m, t = all_t[n]
    b = t["blockers"]
    if b and b != "none":
        kinds = sorted(set(re.findall(r"\[(HUMAN|ART|AUDIO|DECISION)\]", b + " " + t["typ"]))) or ["partial"]
        w(f"| `{n}` {t['title']} | {', '.join(kinds)} | {b} |")
w("")
for m in MILESTONES:
    w(f"### {m['id']} — {m['title']}  *(Phase {m['phase']})*\n")
    w(f"**Goal:** {m['goal']}\n")
    if m["note"]: w(f"> {m['note']}\n")
    w("**Milestone acceptance:**")
    for a in m["acceptance"]: w(f"- {a}")
    w("")
    for t in m["tickets"]:
        w(f"#### {t['id']} · {t['title']}")
        deps = ", ".join(f"`{d}`" for d in t["deps"]) or "—"
        bl = ", ".join(f"`{b}`" for b in blocks[t["id"]]) or "—"
        w(f"- **Size:** {t['size']} · **Type:** {t['typ']}")
        w(f"- **Depends on:** {deps}")
        w(f"- **Blocks:** {bl}")
        w(f"- **Blockers:** {t['blockers']}")
        w(f"- **Spec:** {t['spec']}")
        w("- **Do:**")
        for d in t["do"]:
            if d.startswith("- "): w(f"    {d}")
            else: w(f"  - {d}")
        w("- **Acceptance:**")
        for a in t["acc"]:
            if a.startswith("- "): w(f"    {a}")
            else: w(f"  - [ ] {a}")
        w("")
w("---\n")
w("## 6. Status tracker\n")
w("Update the status as tickets move: `Todo`, `Doing`, `Blocked (<reason>)` or `Done (<date>)`. The G1/G2/G3 rows are ticked at their gate-review tickets.\n")
w("| Ticket | Title | Size | Status |")
w("|---|---|---|---|")
for m in MILESTONES:
    for t in m["tickets"]:
        w(f"| `{t['id']}` | {t['title']} | {t['size']} | {STATUS.get(t['id'], 'Todo')} |")
w("| **A** | Alpha (§1.4) | — | Todo |")
w("| **G1** | Playable Greybox (§1.4) | — | Todo |")
w("| **G2** | Content complete (§1.4) | — | Todo |")
w("| **G3** | Release 1.0 (§1.4) | — | Todo |")
w("")
text = "\n".join(out)

here = os.path.dirname(os.path.abspath(__file__))
road = os.path.join(here, "..", "ROADMAP.md")
src = open(road, encoding="utf-8").read()
start, stop = "<!-- ROADMAP-TICKETS:BEGIN -->", "<!-- ROADMAP-TICKETS:END -->"
block = f"{start}\n{text}\n{stop}"
if start in src:
    src = re.sub(re.escape(start) + r".*?" + re.escape(stop), lambda _: block, src, flags=re.S)
else:
    src = src.rstrip() + "\n\n---\n\n" + block + "\n"
open(road, "w", encoding="utf-8").write(src)
print(f"ok: {len(all_t)} tickets, critical path {dist[path[-1]]} units ({len(path)} tickets)")
