# Architecture Review: Bus Driver — Game Spec v4

**Document reviewed:** `docs/GAME_SPEC.md` v4 (approved 2026-09-29), with `docs/ART_CONTRACT.md` as supporting context. I also spot-checked the project itself (`ProjectSettings/`, `Packages/`, git).
**Review date:** 2026-09-29
**Scope of review:** The whole spec, read as a **roadmap to a complete itch.io release in 3 months**. ART_CONTRACT is reviewed only where the spec depends on it. Game design is reviewed only where a rule is ambiguous or contradicts another; balance and fun are for playtests.

## Verdict

**Needs rework, but the plan, not the architecture.**

The architecture is good and mostly right-sized. The logic/view split, `PlayerAttention`, the pure-C# rules with tests, the Scare and Death directors, and the decisions log are exactly what I'd want to see. What needs rework is the roadmap around it:
- nine milestones with no dates, owners or cut line
- a refactor-only M0 and a tooling-heavy M8
- no production plan for the four artists and audio engineers
- a 12-week deadline for a *finished* game, not a greybox.

Fix C1 first, then the week-1 items under Recommended next steps, and this becomes "Ready with changes".

## Project profile

| | |
|---|---|
| Genre | First-person horror: driving and CCTV surveillance, roguelite (a 5-night run) |
| Scope tier | Student / small indie. Structure is held to small-indie expectations; tooling is held to near-jam tolerance |
| Team | 4 developers, 2 artists, 2 audio engineers. 3 months. A course project |
| Target platforms and input | **Not stated.** Assumed: a downloadable desktop build on itch.io (Windows x64, possibly macOS), keyboard and mouse first |
| Unity version and render pipeline | **Not stated in the spec.** The project is on `6000.6.0f1`, not an LTS release, with URP 17.6 |
| Multiplayer | None (implied; not stated as a non-goal) |
| Post-launch plans | None stated. Assumed: a few itch updates, no live operations |

**Assumptions made in this review:**
- The release is free, on itch.io only, with no store or platform services.
- "3 months" is about 12 weeks, and the last 2 are needed for bug fixing and submission.
- The team is part-time (other courses), so a "week" is well under 40 hours per person.
- The game is in English only.

## Scorecard

| Area | Rating | Headline |
|---|---|---|
| Document foundations | Weak | Excellent decisions log and pillars. No platform, performance target, schedule, owners or risk list |
| Project structure & assemblies | Strong | Five asmdefs with the dependency direction written down. Right-sized |
| Object model & composition | Strong | Logic/view split, component-built monsters, anchors on the logic roots |
| Dependencies & communication | Adequate | `ShiftContext` plus C# events is right. Wiring for spawned objects and static state (with domain reload off) is unaddressed |
| Game flow, state & scenes | Weak | Night states are defined. Run-level flow, scene lifetimes, teardown and scene ownership aren't |
| Data architecture | Adequate | SO definitions vs plain runtime state is right. No ID scheme, and some balance rules are hard-coded |
| Save & persistence | Weak | Clear run/meta split. No version, stable IDs, safe write or anti-save-scum rule |
| Asset & content pipeline | Adequate | Import rules and view swaps are good. The generated route blocks hand-dressing, and the art tooling is over-scoped |
| Gameplay systems & extensibility | Strong | Attention, Threat and the directors make a new monster mostly data. A few rule ambiguities |
| Input | Adequate | Input System maps per mode. The rebind UI and gamepad scope aren't stated |
| UI architecture | Adequate | `UITheme` and an event-driven HUD. The framework and menu navigation aren't stated |
| Audio | Missing | Named clips only. No mixer plan, snapshots, spatial rules or ownership |
| Performance & memory | Missing | No budgets, in a game with several RenderTexture cameras and a forest |
| Async, timing & lifecycle | Adequate | The Timeline-gated async load is good. No pause contract or focus handling |
| Diagnostics & debug | Adequate | The F1 overlay is strong. No build label or bug-report path |
| Testing & automation | Strong | EditMode cores plus a smoke test. The containment bot is over-scoped |
| Version control, build & release | Weak | Force Text and visible meta files are already set. LFS is deferred, and there are no builds until the end |
| Accessibility | Adequate | Scare-intensity hook. No photosensitivity warning, and some GPS cues are colour-only |
| Team workflow & tooling | Weak | Tooling built for generality, instead of lanes, ownership and art/audio schedules |
| Platform integration, localization, networking | N/A | itch.io only, English only, single-player. State all three as non-goals in §0 |

## Critical and major findings

### [C1] The roadmap has no timeline, owners or cut line, and its scope doesn't fit 12 weeks

**Severity:** Critical · **Area:** Document foundations / team workflow · **Type:** missing decision · **Cost to change later:** High. Every week spent on work that later gets cut is lost, and by week 8 there's no room left to re-plan.

**Evidence:**
- §12 lists M0–M8 with deliverables and "done when", but no durations, dates or owners.
- §0 defines the goal of "this phase" as a playable greybox, and lists "final art, animation, VO, … settings polish" as non-goals. The real deliverable is a complete itch.io release in 3 months.
- M0 bundles about 11 deliverables before anything new is playable: asmdefs, the builder restructure, view interfaces, a new Night scene, `ShiftContext`, the pause split, the full Input System migration, tests, `UITheme`, `SceneIds` and the `MainMenu` clean-up. Its done-criterion is "no behaviour change".
- M1–M6 are written as a chain, each building on the last. There's no lane plan that would let 4 developers work in parallel.
- Several pieces of tooling serve generality more than this game:
  - the `RouteLayout` spline builder with automatic containment (§3.3, §3.3b)
  - the containment bot test (§3.3b)
  - Make View from FBX, Validate Art, reference FBX export, `UCX_` extraction and the F2 greybox/art toggle (§11.3, §11.6, M8)
  - four idle events plus a Timeline transition for the menu in M1 (§10b.6).

**Why it matters:** A student team rarely fails because of bad architecture. It fails by reaching week 10 with a beautiful foundation and no second monster, no shop and no art in the game.
- Nothing in the plan tells you in week 4 whether you're on track.
- Nothing says which features go when you slip, and you will slip.
- Because the spec targets a greybox, nobody has scheduled the work that turns a greybox into a release: art and audio integration, lighting, the mix, credits, the settings pass and the itch page.

**Recommendation:**
1. **Rewrite §12 as a 12-week plan with dated checkpoints.** At each checkpoint you *judge scope*; you don't slip the date. Suggested plan:

   | Week | Checkpoint | Contents |
   |---|---|---|
   | W1 | Setup | LFS and merge driver, pinned Unity version, first build to a restricted itch page, lanes assigned, the audio and route decisions, art style exploration |
   | W2–3 | Foundations + blockout | M0-lite (below), route blockout, clock and ordered stops, ledger core, `ScareDirector` core. **Art style locked at the end of W3** |
   | W4–5 | **Vertical slice: Night 1** | Drive the route with GPS and clock, fares, Starer and kick, cliff and fall death, Game Over, Summary. First real passenger and ambience in. **Playtest 1** |
   | W6–7 | **Feature-complete greybox** | Whisperer, sanity and startles; Mimic and decoys; shop and 5 items; run flow, saves and journal. **Playtest 2** |
   | W8–9 | Content and integration | 5 nights tuned, the Night 1 teaching script, art and audio integration waves, lighting, menu diorama polish |
   | W10 | **Content lock** | Settings, accessibility, credits, itch page. No new features after this |
   | W11–12 | Release | Bug fixing, performance on the minimum-spec machine, release-candidate builds, submission. W12 is buffer |

2. **Give each developer a lane that owns systems end to end,** so that after M0 the M1–M6 work runs in parallel against its interfaces:

   | Lane | Owns |
   |---|---|
   | 1. Road & bus | Bus, route and splines, containment, cliff and fall cam, GPS, clock, environment integration |
   | 2. Passengers & monsters | Passenger states, views and anchors, `PlayerAttention`, `ThreatMeter`, the 3 monsters, kicking, decoys, the manifest |
   | 3. Horror layer | `ScareDirector`, sanity, hallucinations, the `DeathDirector` presenters, Timelines, lighting and post-processing, audio integration with the audio engineers |
   | 4. Flow, economy & UI | `GameSession` and run flow, ledger, shop and items, saves, journal, menus and HUD, the Input System migration, builds and itch releases |

3. **Trim M0 to what unblocks the lanes.** Keep:
   - asmdefs
   - the `ShiftContext` and `GameSession` skeleton
   - `IPassengerView`, for passengers only
   - the scene split (M1)
   - `PauseController`
   - `SceneIds`.

   Move the rest out:
   - The Input System migration goes to Lane 4's first two weeks; it blocks nobody.
   - `IBusView` and `IEnvironmentView` wait until bus and environment art is close.

4. **Add a MoSCoW column to §12.** My suggested "won't have unless ahead of schedule" list:
   - the containment bot test (keep the kill-plane fallback and add a manual edge-drive pass instead)
   - the Make View tool
   - the reference FBX export (the ART_CONTRACT dimensions are enough)
   - `UCX_` extraction (colliders are placed by hand on the logic prefab)
   - the F2 toggle
   - GPS hallucinations
   - 2 of the 4 menu idle events
   - the §8 "later candidates" items.

5. **Rewrite the §0 goal:** a feature-complete greybox by W7, followed by an explicit integration phase.

**Trade-off:** you get less reusable tooling, and some jobs get done by hand. For one route, three monsters and about eight passengers, doing them by hand is cheaper than building the tool.

### [M1] Route authoring: a generated route fights hand-dressing and playtest iteration

**Severity:** Major · **Area:** Content pipeline / scene management · **Type:** problematic decision · **Cost to change later:** High. This decides where every environment asset and scare prop lives, and changing it after dressing starts means placing them all again.

**Evidence:**
- §3.3 says the route "is built by the builder from a `RouteLayout` spline description", and §3.3b says "The builder emits containment automatically".
- §9.6 has the builder generate `Route01_Environment.prefab`.
- §11.4 says "Anything an artist touches is never generated", and §11.1's `EnvironmentViewSet` maps each *kind* of object to a view.
- §9.6 says "The Night scene is hand-authored once, so teammates can edit it safely."
- The existing builder is 1121 lines and has already drifted from the scene (§2).

**Why it matters:**
- **Dressing:** What makes a horror route good is one-off hand placement: the figure behind the fence, the church, the tunnel mouth, the cliff view, sightlines that hide a scare. None of that is a "kind → view" swap. With a generated environment prefab, that dressing is either overwritten on the next regeneration, or it lives in a second layer that silently misaligns whenever `RouteLayout` changes.
- **Iteration:** Playtests *will* move stops and the cliff to hit the target night length. That's exactly the kind of regeneration you can't afford once the route is dressed.
- **Merges:** Separately, one `Night.unity` edited by 4 developers and 2 artists is a monolithic-scene merge problem. Force Text doesn't make scene YAML merge well.

**Recommendation:**
- **Make a hand-authored spline the single source of truth.**
  - Install `com.unity.splines` (it isn't in the project yet).
  - The road is a `SplineContainer` in the route scene, and its mesh comes from `SplineExtrude` or from road-kit pieces placed along it.
  - `RouteTracker`, the GPS and the containment check all read that spline, so §4.1b's data-driven GPS still holds.
- **Replace the environment generator with small editor helpers** that place objects once and then hand them over to the scene: "place guardrails along the selected spline range" and "place a side stub and blocker here".
- **Split the night into additive scenes by owner:**
  - `Night_Systems` (developers): `ShiftContext`, bus spawn, UI
  - `Route01_World` (whoever builds the level): road, containment, stops, dressing
  - optionally `Route01_Lighting`.

  Load them with `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)`.
- **Add a one-line ownership rule:** one editor per scene at a time, announced in team chat, or enforced with `git lfs lock`.

**Trade-off:** You lose "containment is guaranteed by construction". To make up for it:
- keep the kill-plane fallback from §3.3b
- add an EditMode test that opens the route scene, samples the spline every 5 m, and asserts there's a guardrail or wall collider on both sides except inside the `FallZone` range
- do a manual edge-drive pass at each checkpoint.

### [M2] Run-level flow, scene lifetimes and teardown are undefined

**Severity:** Major · **Area:** Game flow, state and scenes · **Type:** missing / ambiguous decision · **Cost to change later:** High. Every system's init and teardown depend on these rules, and a late answer means touching all of them.

**Evidence:** §3.1 draws the run flow. §9.3 defines `ShiftDirector` (Intro → Driving → Summary | GameOver) inside a single night. §9.4 says `GameSession` is "created by a bootstrap". The spec doesn't say:
- what owns the run-level transitions (Depot → Night → Summary → Depot → Run Won / Game Over → Menu)
- which scene the Depot/Shop, Summary, Game Over and Run Won screens live in
- whether the Night scene reloads for each night
- how `GameSession` and the Sound Controller survive scene loads. Today the Menu scene instantiates a Sound Controller prefab and `MainMenu` finds it by name (§10b.5).
- what happens on Quit to Menu mid-night.

**Why it matters:** Every playtester takes the path "Menu → New Run → die → Menu → New Run". That path is where these problems show up:
- duplicate managers
- double event subscriptions
- a stale `RunState`
- a second Sound Controller that doubles every sound.

These bugs arrive late and touch every system.

**Recommendation:** Add a §3.5 "Flow and lifetimes" with a state diagram and one rule per state:
- **Scenes:** `Menu` and `Night` (the latter split additively per M1).
- **One persistent root:** a single `GameRoot`, created by `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` and marked `DontDestroyOnLoad`. It holds the plain-C# `GameSession`, the audio service (M5) and `RunFlow`, the run-level state machine. **Nothing else persists.**
- **Reload the Night scene for every night** (`LoadSceneAsync` in single mode). Night state is then reset by construction, and teardown comes for free.
- **Screens:** Summary and Game Over are UI screens in the Night scene. The Depot is either in the Night scene before `Intro`, or its own small scene; pick one.
- **Quitting:** Quit to Menu mid-night abandons the night. M3 covers what that means for the save.
- **Test:** add "menu → new run → die → menu → new run → finish night 1" to the smoke test. That one path catches most teardown bugs.

**Trade-off:** Reloading the scene each night adds a few seconds of loading, which the night card hides.

### [M3] Saves have no version, no stable IDs, no safe write, and a save-scum hole

**Severity:** Major. Calibrated for a free game with a small save: the fix is cheap now and costly after the first public build. · **Area:** Save and persistence · **Type:** missing decision · **Cost to change later:** Medium–high. Once an itch build is out, every player's `meta.json` is a compatibility constraint, and renaming a journal key wipes their journal.

**Evidence:**
- §9.4 says: "JSON at `persistentDataPath/run.json` (written between nights, **deleted on death**, no mid-night save) and `meta.json`". There's no version field, no serializer choice, no write strategy, and no ID scheme for journal entries or inventory items.
- §8 gives `ItemDefinition` an `id`, but not its type.
- §11.2 introduces the enums `TellId`, `ReactionId` and `PassengerPose`. `MonsterDefinition` assets will serialize them ("tells per stage", §5.1).

**Why it matters:**
- The journal is the only thing that survives a run (D6), so it's the thing players will be upset to lose.
- Unity serializes enums as integers. Inserting a new `TellId` in the middle silently remaps every monster's tells in every asset.
- `run.json` is written between nights and is only deleted after the death sequence. A player who is about to die, or is watching the fall cinematic, can Alt-F4 and Continue from the start of the night, which undercuts D3.

**Recommendation:**
- **Version:** give both files a root of `{ int saveVersion; … }`, plus a list of migration steps that run on load. Keep one fixture file per public build in the tests, with an EditMode test that loads each one.
- **Serializer:** use Newtonsoft Json.NET, so the journal can be a `Dictionary<string, JournalEntry>`. `JsonUtility` silently drops dictionaries. `com.unity.nuget.newtonsoft-json` 3.0.2 is already in `packages-lock.json` as a dependency; add it to `manifest.json` explicitly.
- **IDs:**
  - Every definition a save references gets a string ID (`"starer"`, `"coffee"`), and an EditMode test checks they're unique.
  - Serialized enums get explicit values (`HeadTrack = 10`) and are never reordered.
- **Safe write:** write `run.json.tmp`, then `File.Replace` it over `run.json` and keep a `.bak`. If a load fails, fall back to `.bak`, then to "no run".
- **Save-scum:** write `run.json` with `inNight = true` when a night starts, and mark or delete the run at the *start* of `DeathDirector.Die`, not after Game Over. On Continue with `inNight` set, apply the rule the team picks in open question 4.

**Trade-off:** About half a day of Lane 4's time in M6. The alternative is a journal you can't patch later.

### [M4] Domain reload is off, but the architecture has no rule for static state

**Severity:** Major · **Area:** Dependencies / iteration speed · **Type:** missing decision · **Cost to change later:** Medium, and it grows with every static field and event.

**Evidence:**
- **Settings:** `ProjectSettings/EditorSettings.asset` has `m_EnterPlayModeOptionsEnabled: 1` and `m_EnterPlayModeOptions: 3`, meaning both domain reload and scene reload are disabled.
- **Existing statics:** the code already holds static state in `PlayerModeController.Instance`, `ingameMenus.pausedGame`, `OptionsMenu.sens/volume/…`, `MainMenu.soundController`, `ControlsMenu.switchCameraKey` and `GameKeys.*`.
- **No resets:** there's no `[RuntimeInitializeOnLoadMethod]` anywhere in `Assets/`.
- **The spec:** it doesn't mention any of this, and the bootstrap-created `GameSession` in §9.4 is exactly the kind of object that ends up static.

**Why it matters:** With domain reload off, static fields and static event subscribers survive from one Play session to the next in the Editor. The result is second-Play bugs:
- a stale `RunState` from the last test
- `pausedGame` stuck at `true`
- handlers firing twice.

None of these happen in a build, so four developers will lose hours chasing issues that "only happen on the second Play".

**Recommendation:** Pick one option, write it into §9.1, and make it an M0 item:
- **(a)** Turn domain reload back on, under Project Settings ▸ Editor ▸ Enter Play Mode Settings. The project is small, so a few extra seconds per Play is cheap.
- **(b)** Keep it off, with the rule that every static field and static event is reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method, plus a smoke-test step that enters Play twice.

I'd pick (a) for this team. **Trade-off:** entering Play Mode is slower.

### [M5] Audio has no architecture, though the game's scares depend on it

**Severity:** Major · **Area:** Audio · **Type:** missing decision · **Cost to change later:** Medium–high. Every sound call site and every audio asset gets authored against whatever the system turns out to be.

**Evidence:**
- §9.7 and §11.5 cover audio only as "Sound names → constants" and "keeps the named Sound Controller entries".
- The project's `Assets/SFX/MainMixer.mixer` has a single `Master` group and one exposed `volume` parameter.
- The design needs behaviours that setup can't express:
  - whispers that "get louder when its camera is active" (§5.2)
  - a whisper that "pans hard into one ear" (§7.2)
  - Earplugs that muffle "audio tells and cues" (§8)
  - the tunnel, the blackout and the heartbeat (§6)
  - stings that never pile up (§7.3)
  - pause (§9.7).
- The spec doesn't mention middleware, or who owns which audio assets.

**Why it matters:**
- In this game, audio carries tells (the Whisperer) as well as scares, so it's gameplay, not polish.
- One string-keyed `SoundController` prefab edited by two audio engineers is a merge hotspot.
- If mixer routing arrives late, the volume sliders, Earplugs, pause and the tunnel each get their own hack.

**Recommendation:** Add a §9.8 Audio section, decided in W1 together with the audio engineers:
- **Tooling:** Unity's mixer, or FMOD Studio (it has a free indie tier; check the current terms). Choose FMOD only if the audio engineers already know it. Otherwise stay with Unity on this schedule.
- **Mixer groups** (if you use Unity): Master ▸ Music, Ambience, SFX (Bus, Cabin), Voice/Whisper, Scares, UI. Expose one volume parameter per settings slider.
- **Snapshots:**
  - `Default`
  - `Paused` (gameplay groups ducked or low-passed, UI unaffected)
  - `Earplugs` (low-pass on Voice/Whisper and Cabin)
  - `Tunnel`
  - `Blackout`.
- **Sound definitions:** one small asset per sound or bank, holding the clips, volume and pitch ranges, mixer group, 2D/3D setting and a max-voices limit. The audio engineers then add assets instead of editing one prefab. Keep `SoundIds`, pointing at these assets.
- **Service:** a pooled `AudioService` (`UnityEngine.Pool.ObjectPool<AudioSource>`) owned by the persistent root from M2. Passengers such as the Whisperer get 3D sources; stings and UI are 2D.
- **Rules:**
  - `ScareDirector` owns sting playback, so audio spacing follows scare spacing.
  - Pause uses the `Paused` snapshot, plus `AudioListener.pause` with `ignoreListenerPause` on UI sources.
  - The listener position during CCTV is decided in open question 5.

**Trade-off:** 2–3 days of Lane 3's time early, instead of retrofitting every sound late.

### [M6] Art and audio production isn't on the roadmap

**Severity:** Major · **Area:** Team workflow / content pipeline · **Type:** missing decision · **Cost to change later:** High. Assets that arrive late can't be corrected at scale.

**Evidence:**
- §12's milestones cover developer work only. M8 "starts when the artists do" and is mostly tooling.
- There's no asset list, no priorities, no art-style deadline and no audio milestones.
- ART_CONTRACT sets the budgets and minimums, but not *when* each is due: at least 8 passenger looks, 8 animation clips, 3 monsters with tells, the bus, environment kits, 6 stop kinds and 5 blocker types.
- The fonts and the art style are still undecided (§10b.3, ART_CONTRACT §7.1).

**Why it matters:**
- **Capacity:** Half the team has no dates. Two artists have about 8 production weeks for that whole list, plus a 3–4 km forest route, a tunnel, a bridge, a depot and a lodge.
- **Style:** An undecided style blocks all final art.
- **Integration:** The first real asset through the pipeline is what reveals scale, rig and material problems. If that happens in week 9, there's no time to redo 8 passengers.

**Recommendation:**
- **Add an art/audio track to §12:**
  - **Style lock by the end of W3:** fonts and art style, as a one-page style sheet plus one test asset lit with the night preset.
  - **One passenger and one blocker through the pipeline by W4.** This is M8's excellent done-criterion, moved earlier and done without waiting for the tools.
  - **A prioritized asset list,** with an owner and a week for each asset.
- **Prioritize by screen time.** The player spends most of each night looking at the **bus interior and the CCTV feeds**, so the cabin, the dashboard, the passengers and the monsters are the hero assets.
  - Trees and distant scenery can come from free or Asset Store kits. List them in the credits and check their licences.
  - Shorten the route if needed (see the night-length note under Minor findings).
- **Use the Humanoid rig to borrow animation.** Take locomotion and sitting clips from an existing library (Mixamo, for example; check its terms), so the artists spend their time on the monster tells.
- **Audio track:**
  - an ambience and bus pass for the W5 slice
  - monster tells and stings by W7
  - the mix in W9–10.

### [M7] Version control isn't ready for eight people and binary assets

**Severity:** Major · **Area:** Version control · **Type:** problematic decision (deferred) · **Cost to change later:** Grows with every binary commit. Moving files to LFS later means rewriting the history that eight clones share.

**Evidence:**
- M8 schedules "Git LFS for art binaries" for after the artists start.
- The repo has no `.gitattributes`.
- WAVs are already in plain git history: `Wind Ambience.wav` (17 MB) and `amb_driving_lp_01.wav` (12 MB). The packed repo is about 20 MB.
- The spec doesn't mention a merge driver or scene ownership.

Credit where it's due: the project already uses Force Text serialization and Visible Meta Files.

**Why it matters:** Two artists and two audio engineers will commit FBX, PSD, texture and WAV files every week for 12 weeks. Without LFS, every clone and fetch downloads every version of every one of them, forever.

**Recommendation:** Do this in W1, before any artist commits:
- **Add `.gitattributes`:**
  - LFS for `*.fbx *.blend *.psd *.png *.tga *.jpg *.wav *.ogg *.mp3 *.ttf *.otf`
  - `*.unity *.prefab *.asset merge=unityyamlmerge`, with UnityYAMLMerge configured as the merge driver in each developer's git config (Unity Manual: "Smart merge").
- **Existing WAVs:** either move them with `git lfs migrate import --include="*.wav" --everything` (this rewrites history, so everyone re-clones afterwards), or accept the ~30 MB already in history and only track new files.
- **Quota:** check the LFS storage and bandwidth quota on your GitHub plan. Every clone counts toward bandwidth.
- **Write the rules down:** short-lived branches with PRs into `main` (which the team already does), and the scene ownership rule from M1.

**Trade-off:** Everyone installs git-lfs, and possibly re-clones once.

### [M8] No target platform, performance budget or build/release plan

**Severity:** Major · **Area:** Document foundations / build and release · **Type:** missing decision · **Cost to change later:** Medium. Problems found in the first real build late in the project turn into crunch.

**Evidence:**
- The spec doesn't name a platform, a minimum spec, a frame-rate target or a Unity version. The project is on `6000.6.0f1`, which isn't an LTS release.
- There are no build steps, no build number, no itch upload plan and no plan for crash or bug reports.
- §13 verifies only in the Editor and in batch mode.
- Several decisions have performance costs but no budget:
  - the always-on mirror camera (§8)
  - the GPS RenderTexture (§4.1b)
  - dense tree lines along 3–4 km of road (§3.3)
  - URP post-processing and fog (§10b.1).

**Why it matters:**
- **Hardware:** itch players run games on laptops, and the course will probably grade on a lab machine.
- **First builds find problems:** Editor-only code in runtime scripts, scenes missing from the build list, stripped URP features, save paths.
- **WebGL:** a browser build would change the input, audio, save and RenderTexture decisions (open question 1).

**Recommendation:**
- **Add a §0.1 Targets section:**
  - the platforms (assumed: Windows x64, plus macOS if teammates develop on Mac)
  - a minimum spec: the weakest machine on the team or in the lab
  - 60 fps at 1080p on that machine.
- **Pin `6000.6.0f1` for the whole course.** Everyone installs exactly that version. No mid-project upgrades, except a patch release if a blocker needs one. Moving back to an LTS release would be a downgrade, which Unity doesn't support cleanly.
- **Build tooling:**
  - a `Tools ▸ Bus Driver ▸ Build` menu item plus a batch-mode entry point (`BuildPipeline.BuildPlayer`, or Unity 6 Build Profiles), which fits the existing batch workflow
  - `PlayerSettings.bundleVersion` plus the git short hash stamped into a label on the main menu
  - pushes with itch.io's `butler` to channels (`windows`, `mac`) on a restricted page from W1, with a build at every checkpoint.
- **Bug reports:** tell playtesters where `Player.log` is, and ask for it together with the build label. That's enough crash reporting for this scope.
- **Performance:** profile one build on the minimum-spec machine at each checkpoint.
  - Use GPU instancing or static batching for the tree lines, with fog plus camera far-clip culling.
  - Render the mirror item at low resolution and a reduced frame rate.

## Completeness gaps

| Category | Gap | Where it should live |
|---|---|---|
| Boot & flow | **Credits screen:** 8 names plus third-party licences | §10b.3 buttons |
| Boot & flow | **Pause menu contents:** Resume / Options / Abandon run / Quit to menu, with a confirm on destructive actions | §3.5 (M2) |
| Boot & flow | **First-launch screen:** photosensitivity/content warning and a **brightness calibration**. It's a night game lit by headlights, and the options menu already has a brightness setting | §3.5 (M2) |
| Save | Version, safe writes, the abandon/save-scum rule, a "reset journal" option | M3 |
| Settings | Per-group volume (the mixer exposes only `volume`) | M5 |
| Settings | The scare-intensity setting (the hook exists in §7.3) | §7.3 |
| Settings | Input System rebinding. The Controls menu rebinds a static `KeyCode switchCameraKey` today, and §9.5's migration doesn't mention it | §9.5 |
| Input | Say whether gamepad is a must. Keyboard and mouse is enough for itch; "gives gamepad support" (§9.5) implies QA that nobody has budgeted | §9.5 |
| UI/UX | Name the framework (presumably uGUI + TMP) | §10 |
| UI/UX | Back/cancel navigation across the inherited menus | §10 |
| UI/UX | Aspect ratio and ultrawide handling for the screen-space overlays | §10 |
| Audio | Everything in M5 | §9.8 |
| Localization | State "English only" as a non-goal | §0 |
| Accessibility | A **photosensitivity warning**, in the game and on the itch page. It's a must for a game with flashes and flicker | first-launch screen |
| Accessibility | Don't signal early/late and the cliff by colour alone; add text or icons | §4.1b |
| Accessibility | Optional captions for spoken lines ("driver…") | §7.3 |
| Performance, build & release | Everything in M8 | §0.1, §13 |
| QA | "Start at night N" and "give item" cheats. A full run takes 60–75 minutes, too long to reach Night 5 by playing | §10 debug overlay |
| Legal | Credits and a third-party licence list (fonts, audio, Asset Store, animation libraries). Check the course's rules on public release | docs, credits screen |

## Change-scenario walkthroughs

**Scenario: add a 4th monster, "The Passenger Who Stays" (Appendix A)**
- **What changes:**
  - a new `MonsterDefinition`
  - one `MonsterAbility` script (kills a neighbour each stage)
  - `ScareDefinition`s and a journal entry
  - the monster added to `NightDefinition` pools
  - a view that reuses `AnimatedPassengerView` with an override controller.
- **Code or data:** one ability script. Everything else is data.
- **Risk:** its core rule, "doesn't get off at its stop", is a route event, not attention. §5.1's rate model ("+x/s while unobserved") needs to accept any rate source, for example `IThreatRate` strategies, not a fixed set of observed/unobserved rates.
- **Assessment:** well supported, given explicit enum values and pluggable rate sources.

**Scenario: add an item with a novel effect (the IR CCTV filter)**
- **What changes:** an `ItemDefinition` and an `ItemEffect` that needs to reach `CCTVSystem`'s grade volume.
- **Code or data:** code, as you'd expect. But the spec doesn't define what an `ItemEffect` can reach, and each of the five greybox items touches a different system:
  - sanity (Coffee)
  - a camera (Mirror)
  - passenger views (Flashlight)
  - the audio mixer (Earplugs)
  - the death pipeline (Salt charm).
- **Risk:** each item gets wired ad hoc. The Salt charm has to intercept inside `KillSequence` or `DeathDirector`, and that hook isn't specified.
- **Assessment:** workable. Define `ItemEffect.Apply(ShiftServices)` / `Remove()` and a `DeathDirector.TryPrevent(cause)` hook; the Salt charm is its first user.

**Scenario: rebalance after playtest 2**
- **What changes:** threat rates, sanity drains, fares and tips, the clock rate, the night curve. All of these are SO data (§9.1).
- **Code or data:** data, except for three rules that are written into the spec as fixed values. Move them into a `BalanceConfig` SO:
  - the sanity carry-over `max(current + 30, 60)` (§6)
  - the "≥ 1 game-min early" tip threshold (§4.2)
  - the startle frequency for each sanity tier.
- **Risk:** low. SO edits made in Play Mode persist, which is good for tuning. That's safe here because runtime state lives in plain objects (§9.4).
- **Assessment:** well supported.

**Scenario: playtests show the cliff should be 400 m later, and stops 3 and 4 are too close together**
- **What changes (as written):** edit `RouteLayout` and regenerate `Route01_Environment.prefab`.
  - The GPS and tracker follow automatically.
  - Hand-placed dressing near the change is lost or misaligned.
  - The `RouteDefinition` stops have to be matched up with the scene's `BusStop`s again.
- **What changes (with M1):** move the spline knots and the road re-extrudes; move the stop prefabs and the `FallZone` by hand; the tracker and GPS follow.
- **Risk:** the hand-typed `scheduledTime` values go stale. Derive them from each stop's distance along the spline and an assumed speed.
- **Assessment:** painful as written; workable with M1.

**Scenario: artists replace the Starer's greybox, and an audio engineer replaces its sting**
- **What changes:**
  - a prefab variant with a `StarerView` (FBX and Animator), with anchors named per ART_CONTRACT
  - the kill Timeline rebound to `Anchor_Face`
  - the clip on the named audio entry.
- **Code or data:** data. That's the whole point of D16.
- **Risk:**
  - `LookAtTargetIK` needs a Humanoid rig with a sensible head bone.
  - Timeline bindings to the greybox face break silently; add a smoke-test check for unbound tracks.
  - Both audio engineers editing the single SoundController prefab will conflict (M5).
- **Assessment:** well supported for visuals; workable for audio.

**Scenario: the journal gains a "times killed by" counter between playtest builds**
- **What changes:** the `MetaProgress` model.
- **Code or data:** code plus a save change.
- **Risk:** adding a field is harmless with either serializer. Renaming a field or changing its type either resets it silently (`JsonUtility`) or throws on load.
- **Assessment:** workable today. Add M3's version and migrations before the first public build.

## What the design gets right

- **The decisions log (D1–D19).** Decisions are dated, numbered and referenced from the sections they shape. Someone joining in week 5 can find out *why* the Mimic is always kickable without asking anyone. Keep it that way: when a decision changes, add a row rather than editing the old one.
- **The logic/view split, with colliders and anchors on the logic root (§11.1).** This is what lets two artists work alongside four developers without blocking them.
  - Hitboxes, seat positions and door paths never move when a mesh changes.
  - Scenes that mix greybox and final art are normal.
  - M8's done-criterion ("one passenger and one blocker swapped in with zero code/scene changes") is a great acceptance test. Move it earlier (M6).
- **`PlayerAttention` as the single source of truth for what the player is looking at (§5.1).** All three monsters are rules about attention.
  - One service answering `IsObserved` and `TimeSinceObserved` keeps those rules consistent and testable.
  - The Mirror item and the on-foot view each just add an attention source.
- **Pure-C# cores with EditMode tests, aimed at the right things (§9.1, §13).** The ledger, schedule rating, threat curves, scare arbitration and the manifest are where this game's silent, expensive bugs would live. Feel-driven code such as driving and the camera is rightly left to playtests.
- **Two directors that protect the pillars.**
  - `ScareDirector` puts pillar 6 into code: spacing, priority, dropped startles, and suppression during telegraphs.
  - `DeathDirector.Die(cause, presenter)` sends three very different deaths through one pipeline, so the run wipe, stats and journal hints can't drift apart.
- **Technology sized to the project.** Five assemblies, a hand-written composition root instead of a DI container, direct references instead of Addressables, no ECS, and Timeline for authored scares. Each is the right call at this scope.
- **Pressing Play in any scene creates a debug run (§9.4)**, and the F1 overlay shows the seed, the scare log and cheats (§10). This will save the team more hours than any other single item.
- **The builder rule (§11.4).** It correctly diagnoses the PR #3 drift and forbids generating anything an artist touches. M1 extends that thinking to the route.
- **Fairness is designed in, not tuned in afterwards.** D14's cliff warnings, the telegraph windows (§5.1), and the rule that startles "never lie about lethal state" (§7.1).
- **Cheap wins already spotted.** `UITheme` for the undecided fonts, removing `CoverArt.jpg`, `SceneIds` instead of build indices, and Input System actions instead of legacy `Input`.

## Minor findings and suggestions

**Composition and wiring**
- **Spawned objects:** passengers and monsters spawned at runtime can't hold serialized scene references. State that `ManifestSpawner` calls an explicit `Bind(ShiftServices)` after `Instantiate`, so nobody reaches for `FindAnyObjectByType`.
- **View references:** Unity doesn't serialize interface-typed fields; `[SerializeField] IPassengerView view` won't show up in the Inspector. Interface references also skip Unity's fake-null check, so after the F2 toggle destroys a view, `view != null` is still true. Store the view as a `Component` and cast it, or check `(view as Object) == null`.
- **Events:** "nothing polls" (§9.1) is too absolute, since the speedometer should read speed every frame. Reword it to "state changes are events; continuous values are read". Also add the rule: subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **Seeded randomness:** derive a separate `System.Random` for each system (manifest, hallucinations, menu events) from the run seed. Otherwise one extra hallucination roll changes the whole manifest, and seeds stop reproducing bugs.

**Pause, time and lifecycle**
- **Write the pause contract:**
  - `timeScale = 0`, with `ShiftClock`, threat and sanity all on scaled time
  - the `Paused` audio snapshot
  - the gameplay action map off and the UI map on
  - Timelines on `DirectorUpdateMode.GameTime`, so kill scares and the fall cam pause too
  - the menu diorama on unscaled time.
- **Pause during a death presenter:** decide whether it's allowed. My suggestion: allowed, but quitting from there counts as a death.
- **Focus loss:** auto-pause on `OnApplicationFocus(false)`. It's cheap, and expected in a game where a missed second kills you.

**Scope and tuning**
- **Night length arithmetic:** 3.5 km at the current loop's ~40 km/h average is about 5 minutes of driving. Add 6 stops at about 30 s each and a night is roughly 8 minutes, not the 12–15 in §3.3. Either shorten the route (which also helps the artists) or accept shorter nights. Settle it in the W4–5 slice.
- **One authoring style per scare tier:** `ScareDefinition` allows "a Timeline asset or a list of step components" (§7.3). Pick one per tier, e.g. Timeline for kill scares and steps for startles.
- **Menu (D17–D19):** keep "early" by shipping a *static* diorama in M1: the scene, `NightLightingPreset` and `LightFlicker`, which the game needs anyway. Build `MenuEventDirector` and the bus-arrives Timeline in W8–9.
- **Seating rules for the manifest generator:** if the Starer and the Whisperer share a CCTV camera, watching one camera handles both, and Night 2's lesson disappears.

**Data**
- Make `stopId` a string. Add an EditMode test that checks every `RouteDefinition` stop exists as a `BusStop` in the route scene. The same test can scan every definition for missing references and duplicate IDs.

**UI**
- Name the framework: uGUI + TMP, to match the inherited menus. The diegetic dash elements should be world-space canvases, except the GPS, which is a RenderTexture.
- Add a Credits button to §10b.3.

## Open questions for the author

1. **Platform:** a downloadable Windows (and Mac?) build, or WebGL playable in the browser on itch? WebGL changes saves, audio start-up, RenderTexture costs and input, so decide before W2.
2. **Course calendar:** are there fixed demo, playtest or grading dates? The checkpoints should land on them, and the real end date sets the cut line.
3. **The Mimic's sequence (§5.2):**
   - The tell is "two passengers are identical", but the ability has the original *die* and the copy take its seat, which leaves only one. Does the "pick the right copy" window exist only between the copy appearing and the original dying? What's the tell after that?
   - Its threat rises "while … looked at directly". Does walking up to kick it, or using the Flashlight on it, push it toward Lethal?
4. **Abandoning a night:** if the player quits mid-night, or Alt-F4s during a death, does Continue restart the night or count it as a death? (M3)
5. **CCTV audio:** when a cabin camera is active, does the listener move to that camera or stay with the driver? This decides how "whispers get louder when its camera is active" gets built.
6. **Escaping a KillSequence (§5.1):** "still escapable by stopping". Does stopping alone cancel Lethal, or only make a kick possible? The telegraph lasts 3–5 s, which is less time than it takes to walk to the back row.
7. **Gamepad:** a must-have or a nice-to-have? It changes Lane 4's QA and menu work.
8. **Level design:** who builds and dresses the route: a developer (Lane 1) or an artist? The answer decides the M1 scene split.

## Recommended next steps

1. **W1:** Turn §12 into the week plan, with lanes, checkpoints and a MoSCoW cut line (C1). Answer open questions 1, 2 and 8.
2. **W1:** Add `.gitattributes` with LFS rules and the YAML merge driver, decide what to do with the existing WAVs, and pin the Unity version (M7, M8).
3. **W1:** Make the first standalone build and push it to a restricted itch page, with the version label on the menu (M8).
4. **W1:** Decide the route pipeline and the additive scene split. I recommend the hand-authored spline (M1).
5. **W1–2:** Write §3.5 "Flow and lifetimes", the domain-reload rule and the pause contract (M2, M4, minor findings).
6. **W1–2:** Hold the audio architecture session with the audio engineers: tooling, mixer groups, snapshots, sound assets (M5).
7. **W2–3:** Put the art/audio track into §12. Lock the style by W3, and get the first passenger and blocker in by W4 (M6).
8. **Before any M6 code:** add the save version, string IDs, explicit enum values and safe writes (M3).

## Further reading

- **Unity's e-book on project organization and version control:** LFS, Smart Merge and scene ownership (M1, M7).
- **Unity Manual, "Enter Play Mode Options" and "Domain Reloading",** for your Unity version (M4).
- **Robert Nystrom, *Game Programming Patterns*:** the State chapter for run flow, and the Service Locator chapter as a contrast to your composition root.
- **Game Accessibility Guidelines (gameaccessibilityguidelines.com):** photosensitivity and captions.
- **Kazman, Klein and Clements, *ATAM*:** the method behind the change scenarios. Worth repeating at the W7 checkpoint.
