# Contributing to Bus Driver

`docs/ROADMAP.md` is the authoritative spec. Read its §0 before you start: it covers how tickets work, the Definition of Done and the verification commands. This file covers the day-to-day rules for working in the repo.

## 1. Repo size rules (D41: no Git LFS)

The repo uses plain git. These rules keep it small enough to clone quickly:

- **Commit exports only.** That means FBX, PNG/TGA, OGG (or short WAVs) and TTF/OTF. Source files (`.psd`, `.blend`, `.spp`, `.kra`, `.max`, `.ma`, `.mb`, `.ztl`, audio masters…) live in the team's shared drive, **never** under `BusDriver/`.
- **No file over 50 MB.** Textures are 2K at most.
- **Audio is OGG Vorbis** (quality 6 is the default). A short one-shot may stay WAV.
- **Commit art when it's done or nearly done**, not on every iteration. Every committed version stays in history forever.
- **History is never rewritten.** No filter-branch and no force-push.

### The pre-commit hook

`tools/hooks/pre-commit` refuses a commit that stages a file over 50 MB, or a source-format file under `BusDriver/`. Enable it once per clone:

```sh
git config core.hooksPath tools/hooks
```

The EditMode test `RepoRulesTests` checks the same two rules on everything under `Assets/`. A clone without the hook is still caught by `tools/verify.sh quick`.

## 2. Merging Unity YAML (UnityYAMLMerge)

`.gitattributes` routes `*.unity *.prefab *.asset *.mat *.anim *.controller *.overrideController *.mixer` to a merge driver called `unityyamlmerge`, and marks binary types `binary` (no line-ending conversion, no text diff). Register the driver on every machine that merges. The attributes do nothing until you do.

**macOS:**

```sh
git config --global merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config --global merge.unityyamlmerge.driver \
  '"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Helpers/UnityYAMLMerge" merge -p %O %B %A %A'
git config --global merge.unityyamlmerge.recursive binary
```

**Windows** (Git Bash or PowerShell; adjust the Hub path if you installed elsewhere):

```sh
git config --global merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config --global merge.unityyamlmerge.driver \
  '"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
git config --global merge.unityyamlmerge.recursive binary
```

If the driver can't merge a file, it leaves conflict markers like any other merge. Resolve them by hand, or take one side (see §4).

## 3. Generated vs owned folders (ROADMAP §4.15)

| Folder | Owner | Rule |
|---|---|---|
| `Assets/Generated/` | the builders | **Never hand-edit.** Change a builder or a `Data/` asset, then run **Tools ▸ Bus Driver ▸ Build All** (`tools/verify.sh content`) |
| `Assets/Data/` | designers | Seeded once by `DataSeeder`, then edited directly. Never silently reseed |
| `Assets/Art/` | artists | Never written by code (Appendix A) |
| `Assets/Audio/Clips/` | audio engineers | Clips referenced by `SoundDefinition`s |
| `Assets/Scenes/Route01_Dressing.unity` | artists | The only hand-authored scene: static art only |
| `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/` | programmers | Code |

Every scene in the build (`Menu`, `Night_Systems`, `Route01_World`) is generated under `Assets/Generated/Scenes/` by Build All. Hand edits to them are lost on the next rebuild; to see the game, press Play in `Menu`, or in `Night_Systems` for a debug run that loads the route beside it.

## 4. Conflict policy

- **`Generated/`:** take either side, then run Build All. Never hand-merge generated YAML, because fileIDs change on every build.
- **`Data/`:** one editor per asset at a time. Say in the team chat which asset you're editing.
- **`Route01_Dressing`:** one editor at a time.
- **Code:** a normal merge.

## 5. Branches and pull requests

- Use short-lived feature branches, one per ticket or group of tickets (for example `feature/T-M1-06-input-actions`), with a PR into `main`.
- The human lead owns branches. Agents don't create, switch, merge or reset branches unless asked.
- Put the ticket ID in commit subjects: `T-M1-06: Input actions asset, InputService, contexts`.
- A PR is ready when `tools/verify.sh quick` passes (plus `playmode`/`smoke` if it touches play) and ROADMAP §6 is updated.

## 6. Verifying: `tools/verify.sh`

Run it from the repo root. Batch mode needs the Editor to be **closed** for this project; the script refuses otherwise and prints the Unity CLI alternative. `UNITY_PATH` overrides the Editor binary (default: `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity`).

| Command | Runs |
|---|---|
| `tools/verify.sh quick` | compile, then the EditMode tests |
| `tools/verify.sh content` | `BuildAll` (every content builder, §4.15), then the EditMode tests |
| `tools/verify.sh playmode` | the PlayMode tests |
| `tools/verify.sh smoke` | the smoke test: plays the generated Menu, starts a New Run through `RunFlow` on a throwaway save root and drives the night (captures go to `BusDriver/Logs/smoke/*.png`) |
| `tools/verify.sh build` | a standalone build for the current OS, plus its `--selftest` |
| `tools/verify.sh full` | content + playmode + smoke + build |

It exits non-zero with a one-line reason on any failure. Logs are in `BusDriver/Logs/`. `tools/unity.sh <args>` runs the Editor in batch mode with any other arguments.

A few details:

- **`content` refuses to run while generated files have hand edits**, because a rebuild overwrites them. Those files are everything under `Assets/Generated/`. A file that is dirty only because the previous `content` run rebuilt it is fine: the script records its hash in `BusDriver/Logs/content.stamp`. Commit or discard hand edits first, or set `VERIFY_ALLOW_DIRTY=1`.
- **`build` runs the player's self-test headless** (`-batchmode -nographics --selftest`). To check a build by hand, run the executable with `--selftest -logFile -`. It prints `[SELFTEST] OK <label>` and exits 0.
- **Log scanning ignores two things:** the Editor's own `UnityEditor.Search` indexer exception at startup, which is an Editor bug and not ours, and exceptions that a test expects.
- **macOS builds are Apple silicon only** until someone makes the Editor's `llvm-lipo` executable (D52). The build log prints the exact `chmod +x` command.
- **The smoke test fixes `UnityEngine.Random`'s seed** before boarding, so seating is the same on every run.

### Smoke-test baseline

The legacy smoke test's drive numbers on 2026-09-29 (M0), used by T-M1-07 to check that the Input System migration keeps the feel (±5 %):

| Measure | Value |
|---|---|
| 0–50 km/h | 12.4 s |
| Stop from about 55 km/h | 4.2 s |
| Turn in 3 s of full right lock at about 46 km/h | 66.2° |
