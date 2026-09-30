# Bus Driver — Greybox Game Spec (v4, approved 2026-09-29)

> **SUPERSEDED (2026-09-29) by [`docs/ROADMAP.md`](ROADMAP.md).** This file is kept for history only. The roadmap holds every decision here (D1–D19), plus the architecture review's fixes, the full rules, and the ticketed implementation plan. Don't implement from this file.


> Living document. Update the decisions log when a call changes.
> **[DECIDE]** = needs a team call. **[PROPOSAL]** = my recommendation; it stands unless someone objects.
> Ideas that are explicitly *out of scope* live in **Appendix A**, not in the spec.

## Decisions log
| # | Decision | Date |
|---|---|---|
| D1 | Crashing has **no penalty**. No traffic, no civilian drivers | 2026-09-29 |
| D2 | Setting: a **rural, forested, possibly mountainous** night road | 2026-09-29 |
| D3 | **Roguelite**: death loses the whole run (money and items) | 2026-09-29 |
| D4 | **Sanity 0 = blackout → death** | 2026-09-29 |
| D5 | Fare is **credited on boarding**, and subtracted if the passenger is kicked or killed | 2026-09-29 |
| D6 | A run is **5 nights**. Only a **monster journal** persists across runs | 2026-09-29 |
| D7 | The evil-car "Follower" is an **idea only** (Appendix A) | 2026-09-29 |
| D8 | Greybox roster: **the Starer, the Whisperer, the Mimic** | 2026-09-29 |
| D9 | **Lateness = no tip only**. Being early earns tips | 2026-09-29 |
| D10 | **Jump scares are a first-class system**: each monster has unique scares, and low sanity triggers lower-impact startle hallucinations | 2026-09-29 |
| D11 | Each level's map is **linear**: one drivable road, no alternate routes. Side roads appear only as **blocked-off dressing** (fallen tree, fence, barricade, washed-out bridge) | 2026-09-29 |
| D12 | A **GPS/route map** on the dashboard shows progress along the route. **Glance only**, with no fullscreen map | 2026-09-29 |
| D13 | The Mimic is **always kickable**. The challenge is picking the right copy | 2026-09-29 |
| D14 | Guardrails everywhere **except one intentional cliff section**. Driving off it = **death, run over**, shown by a **third-person fall cinematic** (like Elden Ring fall deaths). The bus must never clip out of the map anywhere else. This is the single exception to D1 | 2026-09-29 |
| D15 | Greybox items: **Coffee, Rear-view mirror, Flashlight, Earplugs, Salt charm** | 2026-09-29 |
| D16 | Final art arrives via a **logic/view split**: artists own `View` children and `Assets/Art/`, and nothing there is ever generated. The rules are in `docs/ART_CONTRACT.md` | 2026-09-29 |
| D17 | The main menu is a **live 3D diorama built from game prefabs**: a night bus stop, a flickering lamp, a passenger waiting. The internet cover image is removed | 2026-09-29 |
| D18 | The menu has **subtle idle events** and never jump scares | 2026-09-29 |
| D19 | **New Run** plays a skippable "bus arrives" transition that hides the scene load | 2026-09-29 |

---

## 0. Context

The repo has a working driving + CCTV + passenger MVP (merged to `main`). It's a sandbox so far, not a game: there are no rules, no goal, and no way to lose. This spec defines everything needed for a **complete, playable greybox run**: route, clock, money, monsters, sanity, scares, death, items/shop, and the menu → night → summary → shop flow. It's architected so that more routes, monsters, items and final art plug in without rewrites.

**Goal of this phase:** someone can start a run from the main menu, drive a timed route for 5 nights, pick up and drop off passengers, spot and eject monsters via CCTV, get scared, die (and lose the run) or survive all 5 nights, see payouts, and buy items between nights.

**Non-goals:** final art, animation, VO, a second route's content, story/cutscenes, settings polish, and anything in Appendix A.

---

## 1. Design pillars

1. **Divided attention is the game.** Every system pushes the player to look away from the road or away from the cabin, and neither can safely be ignored for long.
2. **Readable dread, fair deaths.** Every monster has an observable tell and a telegraphed escalation. A death should feel like "I should have checked," never "that was random."
3. **Doubt has a cost.** Kicking out an innocent loses money, and keeping a monster risks your life. Decoys make the choice real.
4. **Driving is already hard.** There's no traffic and crashes aren't punished directly (D1). The road costs you **time and attention**, and monsters escalate while you're busy. The one exception is **the cliff** (D14): a single, clearly signposted stretch where the road itself can kill you.
5. **Every night could be your last.** Roguelite (D3): death ends the run.
6. **Scares are earned and paced.** Big scares mean something happened (a kill, a monster escalating), and small scares keep you jumpy. A director makes sure they never pile up into noise.

---

## 2. What exists today (audit, 2026-09-29)

| Area | State | Reuse? |
|---|---|---|
| `BusController` + `BusTuning` SO | WheelCollider bus, auto gears, `DriveLock` flags, `IsStopped`, `SetFrozen` | Keep as is |
| `BusInput`, `DriverLook`, `OnFootController`, `PlayerInteractor` | Work, but use legacy `Input` | Keep logic; migrate input |
| `BusDoors` | Open/close, drive lock, doorway token, hold | Keep |
| `BusCabin` / `BusSeat` | Seats, path nodes, events `OnPassengerBoarded/Seated/Kicked/Left` (no subscribers) | Keep; drop its CCTV dependency |
| `Passenger` base | Waiting→Boarding→Seated→Leaving→Gone, virtual hooks, `IsSeenBy(cam)` (unused). `Leave()` is never called, so nobody gets off voluntarily | Keep; extend |
| `Monster` / `StaringMonster` | Empty marker class / cosmetic head-tracking test | Replace with the framework in §5 |
| `BusStop` | Zone test + boarding loop. No id, order, schedule or destination | Extend |
| `CCTVSystem` | 3 fixed cams + home view, `ActiveCamera`, `OnViewChanged`, grade volume | Keep; feed the Attention service |
| `CrashDetector` | Raises `OnCrash(deltaV, isMajor)`, no subscribers | Keep; feedback and scare hooks only |
| `PlayerModeController` | Singleton "god-lite": mode switch, pause, cursor, settings bootstrap, service locator | Split (§9.7) |
| `DrivingHUD` | Speed/gear/prompt, cosmetic CCTV clock | Extend |
| Menus/Options/Sound/Dialogue | Inherited shell with static-global coupling (`ingameMenus.pausedGame`, `OptionsMenu.*`) | Keep and clean up gradually |
| `BusDriverSceneBuilder` (1121 lines) | Regenerates **the whole scene**, so hand edits are lost. PR #3 already drifted from it (rebuilding would re-add 4 riders) | Restructure (§9.6) |
| `BusSmokeTest` | End-to-end play-mode test | Keep; extend per milestone |
| Packages | Timeline is installed (useful for scare sequences); Input System installed but unused; test-framework unused | — |
| Missing entirely | route order, clock, destinations, fares, tips, money, sanity, monster threat, scares, death, night/run flow, shop, items, saves, asmdefs, namespaces, unit tests | This spec |

Road today: one flat ~777 m loop with 3 stops, about 70 s per lap. It will be replaced (§3.3).

---

## 3. Structure

### 3.1 Run (D3, D6)
```
Main Menu → New Run → Depot/Shop → NIGHT 1 → Summary → Depot/Shop → … → NIGHT 5 → Run Won screen
                                      └─ death (monster kill / sanity 0) → Game Over (run stats) → run wiped → Main Menu
```
- Money and items persist within a run and are wiped on death.
- **Monster journal** (the only cross-run progress): the first time you *see* a monster's tell, be killed by it, or kick it, its entry is unlocked, with the tells described, a greybox sketch, and lore text. You can read it from the main menu and the depot.

### 3.2 Night (one shift)
- You start parked at the town depot, clocked in (e.g. 00:40).
- The route is an **ordered list of stops** ending at the terminus. At each stop: pull into the zone, open the doors (F), riders for this stop get off, then new riders board. The fare is credited on boarding.
- Between stops: drive, and cycle CCTV to watch the cabin. If something is wrong, stop, leave the seat, walk up to the passenger and kick them out.
- **Night won:** reach the terminus and open the doors → Summary.
- **Run lost:** a monster KillSequence completes, or sanity reaches 0.

### 3.3 Route 1 (greybox)
- **Setting (D2):** a winding two-lane road through forest and hills. It has no street lights between stops, so the headlights are the main light. There are switchbacks, a bridge, a guardrailed stretch over a drop, and a **tunnel** (lights flicker, CCTV noise: a scare hot-spot).
- About 3–4 km, point-to-point from the **town depot** to the **terminus** (a mountain lodge). It's built by the builder from a `RouteLayout` spline description. Greybox trees are cylinders and cones, and the hills are ramps or Terrain.
- **Linear (D11):** exactly one drivable road. Side-road stubs branch off for 10–30 m and end in a **blocker**. Blockers come in several kinds: a fallen tree, a chain-link fence with a "ROAD CLOSED" sign, concrete barriers, a collapsed bridge, or a gate. They make the world feel bigger than the route, and they're natural places for scares and hallucinations (a figure standing behind the fence). They're solid colliders, so the player can't leave the route.
  - `RouteLayout` describes the main spline plus `SideStub { distanceAlong, side, angle, length, blockerType }`. The builder places a stub road piece and a `Blocker` prefab (greybox: a log cylinder / fence boxes / barrier blocks), which art can replace per blocker type.
  - Dense tree lines on both sides (plus invisible walls where needed) keep the corridor readable.

### 3.3b Containment and the cliff (D14)
- **Everywhere except the cliff:** guardrails on every drop, tree lines and invisible walls on every edge, and a continuous ground collider under the whole map. The rule is that **the bus can never leave the playable corridor**.
  - The builder emits containment automatically from `RouteLayout`: guardrails wherever the terrain drops beside the road, and invisible walls along the corridor.
  - An automated test drives a spline-following bot along the route with random steering to try to escape, and asserts containment.
  - As a last-resort safety net, falling below a kill-height anywhere *outside* the cliff zone fades to black, logs an error, and respawns the bus on the nearest road point. This should never trigger in play; if it does, the test suite should catch it.
- **The cliff ("Dead Man's Bend"):** one stretch of mountain road, about 150–250 m, with no guardrail on the outer edge and a long drop.
  - **Fairness (pillar 2):** the player gets clear warning.
    - A "NO GUARDRAIL / SHARP CURVE" warning sign and chevrons before it.
    - It's marked in red on the GPS.
    - The road narrows slightly and the rumble strip sounds.
    - Night 1 routes you through it early, before any monster pressure.
  - **Design intent:** it's the one place where looking at the CCTV is truly dangerous. Place it between stops where monsters are likely to be escalating, so the player must choose between checking the cabin and surviving the bend.
  - **Implementation:** the cliff is modelled as a `FallZone` trigger volume below the edge. When the bus enters it, `DeathCause.Fall` fires and starts a `FallDeathSequence`:
    1. Input locks, and the camera detaches from the driver's head to a **third-person `FallCamera`**, positioned on the cliff edge and framing the bus as it drops. The physics keeps simulating, with an optional brief slow-mo.
    2. The engine audio cuts to wind, and there's an impact sound.
    3. About 3 s later: fade and death text → Game Over (run over).
  - It uses the same death pipeline as monster kills (§5.4). Only the presentation step differs.
- **6 rural stops**, each a pool of lamp light (safe-feeling): farm gate, gas station, campground, church, clinic, trailhead.
- A night takes about 12–15 real minutes.

### 3.4 Night difficulty curve **[PROPOSAL]**
Each night draws its manifest from `NightDefinition` (monster pool, threat budget, decoy ratio, threat-rate multiplier, scripted riders). Night 1 is mostly scripted, to teach.

| Night | Monsters | Notes |
|---|---|---|
| 1 | 1 Starer (scripted, boards at stop 2) | teaches CCTV and kicking; 1 decoy |
| 2 | Starer + Whisperer | introduces sanity |
| 3 | Mimic + 1 random | introduces "don't stare" |
| 4 | 2–3 random | faster threat rates |
| 5 | 3–4 random, includes all types | finale; possibly two of one type |

---

## 4. Route, clock, passengers, money

### 4.1 Route & schedule
- `RouteDefinition` (SO): an ordered list of `StopEntry { stopId, displayName, scheduledTime }`. Riders come from the night's manifest, not the route.
- In the scene, `BusStop` gains `stopId`. `RouteProgress` (runtime) tracks `NextStopIndex` and the arrival log.
- **[PROPOSAL]** Stops are served in order. Passing a stop without opening the doors = **missed stop**: its waiting riders are lost (no fare), and riders aboard who were headed there are carried on to the terminus without a tip.
- The HUD shows the next stop's name and scheduled time.
- `RouteTracker` projects the bus onto the route spline every frame and exposes `DistanceAlong`, `Progress01`, `DistanceToNextStop`, and an ETA estimated from the average speed. The GPS, the arrival rating and the debug cheats all use it.

### 4.1b GPS / route map (D12)
- A **diegetic screen on the dashboard**, to the right of the steering wheel. You check it by glancing down with mouse look, which fits pillar 1 because it's one more place for your eyes to go.
- **Contents:**
  - The route drawn as a line from depot to terminus, with the travelled part dimmed.
  - A bus arrow, and the stop markers (served ✓ / next highlighted / upcoming).
  - The next stop's name, distance, ETA vs scheduled time (early/late colour), and the clock.
  - The blocked side-stubs drawn as short greyed dead ends, which sells the "roads used to go there" feel.
- **Implementation:** a UI polyline generated from the `RouteLayout` spline (top-down 2D projection), rendered to a RenderTexture on the dash screen. It isn't a live top-down camera, so it's cheap, stylised and fully data-driven. A world-to-map transform is computed once per route.
- Glance only (D12). The cliff section is drawn in red.
- **Horror hooks:** the GPS is a hallucination target at low sanity. It can show a stop that doesn't exist, the bus arrow drifting off the road into the forest, or static with "RECALCULATING". It goes dead in the tunnel.
- **Reusable:** `RouteMapView` works for any `RouteLayout`, and the same component can render the route overview on the Night intro card and the Summary screen.

### 4.2 Clock
- `ShiftClock` runs at **1 real s = 4 game s** (tunable per night): a ~15-minute night is about 1 game hour.
- A diegetic digital clock on the dashboard. The CCTV timestamp reads from the same clock.
- Arrival rating: **Early** (≥ 1 game-min early) → tips for alighting passengers. **On time / late** → no tip (D9).

### 4.3 Passengers
- `PassengerProfile`: `displayName`, `fare`, `destinationStopId`, `archetype` (Normal / Decoy / Monster type), and `viewPrefab` (the art slot).
- Normal passengers **get off automatically** at their destination (this wires the currently unused `Passenger.Leave()`).
- Passengers can **die** (the Mimic kills them, §5.2). They're removed with a cue, and their fare is lost.
- **Decoys:** normal passengers with odd but harmless behaviour, such as nodding off with the head dropping, a phone glow on the face, muttering, sitting facing backwards, or a hood always up. They're visible on CCTV and costly to kick.

### 4.4 Money
- `Wallet` (run-scoped) and `ShiftLedger` (per night, itemised, drives the Summary screen).

  | Event | Effect |
  |---|---|
  | Passenger boards | `+fare` (counter ticks up) |
  | Delivered to destination | fare kept. `+tip` if the stop was reached early |
  | Innocent kicked out | `−fare` |
  | Passenger killed by a monster | `−fare` |
  | Monster kicked out | **[PROPOSAL]** fare kept + small bounty |
  | Missed stop | riders not picked up; nothing extra |
  | Crash | nothing (D1) |
- The money HUD shows animated deltas (+$3.50 / −$3.50).

---

## 5. Monsters

### 5.1 Framework (reusable)
A monster is a `Passenger` plus composable components, so a new monster is mostly data plus one ability script.
- **`PlayerAttention`** (a service): one source of truth for what the player is looking at: `Road` (driver view facing forward), `Mirror` (future), `Cctv(camIndex)`, or `OnFoot`. It also answers `IsObserved(target)` (frustum + occlusion ray) and `TimeSinceObserved(target)`. Most monster rules are built on this.
- **`ThreatMeter`**: 0 → 100 across the stages *Dormant → Unsettled → Aggressive → Lethal*. Each monster defines the rates, e.g. "+x/s while unobserved", "−y/s while observed", or "×k when sanity is low".
- **`MonsterTell`**: the observable behaviour for each stage, expressed through the view interface (`IPassengerView.SetTell(tellId, intensity)`) so art replaces greybox without logic changes.
- **`MonsterAbility`**: active effects on stage transitions or ticks (move seat, drain sanity, kill a passenger).
- **`KillSequence`**: at Lethal, a **telegraph** window (about 3–5 s: a stage sting, lights flicker; still escapable by stopping, or by the kick if the player is already walking) → the monster's **kill scare** (§7) → Game Over.
- `MonsterDefinition` (SO): the threat curve and rates, tells per stage, ability parameters, kick resistance, scare set (§7), and journal entry.
- The kill/threat interfaces are generic (`IThreat`), so a non-passenger threat (e.g. Appendix A's Follower) can be added later without refactoring.

### 5.2 Greybox roster (D8)
The three monsters are designed to pull attention in **different directions**:

| | **The Starer** | **The Whisperer** | **The Mimic** |
|---|---|---|---|
| Punishes | *not* watching the cabin | watching the road too long | watching one passenger *too long* |
| Tell (on CCTV) | Its head tracks whichever camera you're on. Its seat is closer than you remember | Leans toward neighbours, mouth moving. Faint whispers that get louder when its camera is active | Two passengers are identical. One copy sometimes doesn't appear on one of the cameras |
| Threat rises | while unobserved | while the player's attention is on the road | while it is observed on CCTV or looked at directly |
| Threat falls | while observed on CCTV | while its camera is active | when looked away from |
| Ability per stage | moves one row forward (visible progress toward you) | drains sanity; the rate grows per stage | copies a passenger: the original **dies** (fare lost) and the copy takes its seat |
| Lethal | reaches the front row → kill scare → death | drains sanity to 0 → blackout (D4) | turns on the driver → kill scare → death |
| Kick | normal | normal. The whispers stop instantly, which relieves sanity drain | **always kickable (D13)**; you must kick the *right copy*. Kicking the wrong one = an innocent kicked. Up close, the true copy can be told apart (Flashlight item) |
| Journal hint | "It only moves when you aren't looking." | "Don't listen to it for too long." | "Count the faces. Then look away." |

### 5.3 Kicking out
- The current flow stays: stop the bus → leave the seat (E) → walk up → E "Kick out" → it walks out the door.
- **[PROPOSAL]** Kicking works wherever the bus is stopped, as it does today. There's no penalty for stopping mid-route (D1 spirit), but the clock keeps running (tips) and the Starer/Whisperer keep escalating while you walk.
- `OnKickRequested` lets a monster refuse (a hook for Mimic rules and items).

### 5.4 Death
- No health or lives. There are exactly three causes: a **monster KillSequence**, **sanity 0** (D4), and **falling off the cliff** (D14).
- Unified pipeline: `DeathDirector.Die(DeathCause, presenter)`. It freezes the rules, locks input, and plays the cause's presenter (a kill scare Timeline, the blackout, or the fall cam), then goes to Game Over. New death types only add a presenter.
- Presenter → Game Over screen (the cause of death and its journal hint, nights survived, money earned, monsters caught) → the run save is deleted → Main Menu / New Run.

---

## 6. Sanity
- `Sanity` 0–100. **[PROPOSAL]** It carries over between nights, partly: each night starts at `max(current + 30, 60)`.
- **Drains:** the Whisperer, witnessing a passenger death (the Mimic), the tunnel/dark stretches, a slow baseline drain over the night, and kicking out an innocent (guilt). Crashes don't drain it (D1).
- **Restores:** items (§8), kicking out a monster (moderate), and the lit stops (a small regen while parked at a stop).
- It amplifies monsters: threat rates are multiplied by a sanity factor.
- **Tiers** (drive the `HallucinationDirector`, §7):

  | Sanity | Effects |
  |---|---|
  | 75–100 | none |
  | 50–75 | ambient audio hallucinations: footsteps behind the seat, a fake door chime, a knock on the window. **Occasional low-impact startles** |
  | 25–50 | visual: a phantom passenger on a CCTV feed, static bursts, lights flicker, fake money deltas, a figure in the headlights. **Startles more often** |
  | 10–25 | control and perception: steering drift, the camera cycles by itself, heavy vignette, heartbeat |
  | ≤10 | final warning: tunnel vision, whispers saying "driver…" |
  | 0 | **blackout → death** (D4), shown as the Whisperer's kill scare if it's aboard, otherwise a generic blackout scare |
- There's no number on the HUD. Sanity shows as screen-edge vignette plus heartbeat. The debug overlay shows the value.

---

## 7. Scares (D10)

### 7.1 Tiers
| Tier | Source | Impact | Examples | Gameplay effect |
|---|---|---|---|---|
| **Kill scare** | a monster's KillSequence, sanity 0 | full jump scare: camera takeover, loud sting, face-in-lens | unique per monster (below) | ends the run |
| **Monster scare** | a monster's stage transition (optional, per `MonsterDefinition`) | medium: a sting, a brief visual; no camera takeover | the Starer is suddenly in the next camera you cycle to, staring into the lens | a warning that escalation happened; readable |
| **Startle** | `HallucinationDirector` at low sanity | low: a short sound and/or a flash of an image, under 1 s | a bang on the window, a face flash on a CCTV feed, a passenger standing behind the driver in the mirror view then gone, the horn going off by itself, radio static | makes you jumpy and costs attention; never lethal, never lies about lethal state |

### 7.2 Per-monster scares (greybox versions use placeholder stings, primitive "faces" and camera cuts)
- **The Starer**
  - *Monster scare:* you cycle to a camera and its face fills the lens.
  - *Kill:* you switch back to the driver view, and it's leaning over your shoulder into the driver camera.
- **The Whisperer**
  - *Monster scare:* a whisper pans hard into one ear and says "driver".
  - *Kill (sanity 0):* the whispers crescendo, hands close over the camera from behind, then blackout.
- **The Mimic**
  - *Monster scare:* on the CCTV feed, every passenger turns to the camera at once, then snaps back.
  - *Kill:* the lights cut, and when they come back, all the passengers have its face. It's at the driver's window.

### 7.3 Architecture
- **`ScareDefinition`** (SO): tier, a Timeline asset or a list of step components (camera override, audio sting, screen flash, image overlay, light flicker, input lock duration), priority, and cooldown.
- **`ScareDirector`** (service): the single gate for all scares.
  - Enforces spacing: a global minimum gap and a per-tier cooldown.
  - Handles priority: a kill preempts everything, and startles are dropped rather than queued.
  - Suppresses startles during KillSequence telegraphs, so the telegraph stays readable (pillar 2).
- **Timeline** is used for kill scares (the package is already installed). Artists and designers can then retime scares or swap placeholder faces for final models without code.
- A settings toggle for scare intensity (e.g. a reduced flash) is a cheap accessibility win. **[PROPOSAL]** Plan the hook now and build the UI later.

---

## 8. Items & shop
- `ItemDefinition` (SO): id, name, price, `consumable | permanent | upgrade`, icon, and an `ItemEffect` script.
- The **Depot shop** appears between nights (a greybox UI list). Up to 3 consumable slots in the cab (keys 1–3). Upgrades apply automatically. Items only last the run (D3).

**Greybox set (D15):** one counter per monster, plus sanity and a safety net.
| Item | Type | Effect | Counters |
|---|---|---|---|
| Thermos of coffee | consumable | +30 sanity | the Whisperer, general |
| Rear-view cabin mirror | upgrade | an always-on small cabin view on the dash (a low-res RenderTexture of one cabin camera), so you can glance without leaving the road view | the Starer |
| Flashlight | permanent | on foot: pointed at a passenger, it reveals whether it's the Mimic (the copy has no shadow / flickers) | the Mimic |
| Earplugs | consumable | 30 s of immunity to the Whisperer's drain, but audio tells and cues are muffled too | the Whisperer (a trade-off) |
| Salt charm | consumable | survives one monster kill attempt; the monster is expelled instead. **It doesn't** prevent sanity-0 or cliff deaths | a safety net |

Later candidates (not greybox): an extra CCTV camera, an IR CCTV filter, cruise control, a ticket punch log, a radio.

---

## 9. Architecture

### 9.1 Principles
- **Data-driven content**: routes, nights, passengers, monsters, scares, items and tuning are ScriptableObjects.
- **Composition over inheritance** for monster behaviour.
- **Logic/view split**: gameplay talks to `IPassengerView` / `IBusView`. The greybox views are separate prefab children, and art drops in as prefab variants.
- **No new singletons or `GameObject.Find`.** A scene composition root (`ShiftContext`) wires services through serialized references. Cross-scene state lives in one `GameSession`.
- **Events**: C# events on the owning service. The rules, HUD and scare layers subscribe; nothing polls.
- **Pure-C# cores** (ledger, schedule rating, threat curve, sanity tiers, scare arbitration, manifest generation), each with EditMode unit tests. MonoBehaviours are thin adapters.
- **Seeded randomness**: the run seed feeds manifest generation and the hallucination picks, for reproducible bugs.

### 9.2 Assemblies & namespaces
```
BusDriver.Core       events, data types, GameSession/RunState, save, seeded RNG
BusDriver.Gameplay   Bus, Passengers, Monsters, Route, Clock, Economy, Sanity, Scares, Items, Attention  → Core
BusDriver.UI         HUD, menus, shop, summary, journal                                                → Core, Gameplay
BusDriver.Editor     builders, tools, smoke test                                                         → all
BusDriver.Tests.EditMode / .PlayMode
```

### 9.3 Runtime object map (night scene)
```
ShiftContext (composition root)
 ├─ ShiftDirector        Intro → Driving → Summary | GameOver
 ├─ RouteProgress        ← RouteDefinition, BusStop[]
 ├─ RouteTracker         ← RouteLayout spline, Bus   → RouteMapView (dash GPS)
 ├─ ShiftClock
 ├─ ManifestSpawner      ← NightDefinition, RunState.seed
 ├─ ShiftLedger          → RunState.Wallet
 ├─ PlayerAttention      ← CCTVSystem, PlayerModeController
 ├─ SanitySystem         → HallucinationDirector → ScareDirector
 ├─ ScareDirector        ← monster KillSequences / stage scares
 ├─ Rules                FareRules, DeathRules, JournalRules
 └─ Bus prefab           BusController, BusCabin, BusDoors, CCTVSystem, seats…
```

### 9.4 Persistent state
- `GameSession` holds the settings and the optional `RunState` (wallet, inventory, night index, sanity, seed). It's created by a bootstrap, so Play works from any scene: pressing Play in the night scene creates a debug run.
- `MetaProgress` holds the monster journal.
- JSON at `persistentDataPath/run.json` (written between nights, **deleted on death**, no mid-night save) and `meta.json`. Settings stay in `settings.dat`.

### 9.5 Input
- Migrate to the Input System actions asset (`Driving`, `OnFoot`, `CCTV`, `UI` maps), switched with the mode. This fixes the rebind conflicts and gives gamepad support.
- `ExternalControl` seams stay for tests.

### 9.6 Scene authoring (fixes the builder-vs-hand-edit drift)
- The builder generates **prefabs and environment chunks** only: `Bus.prefab`, `BusStop.prefab`, `Passenger.prefab`, the monster prefabs, and `Route01_Environment.prefab` (from `RouteLayout`).
- The `Night` scene is **hand-authored once** (prefabs + `ShiftContext`), so teammates can edit it safely. All 5 nights use one scene with a different `NightDefinition`.

### 9.7 Clean-ups bundled into M0
- Split `PlayerModeController` → mode switch / `PauseController` (the single pause owner, replacing the `ingameMenus.pausedGame` reads) / `CursorController`.
- `BusCabin` stops referencing `CCTVSystem` and uses `PlayerAttention` instead.
- `CCTVSystem` parallel arrays → a `CctvCamera` component per camera.
- Sound names → constants (`SoundIds.*`) to prevent the duplicate/typo bugs.
- Scene names → `SceneIds` constants. No more build-index loading.
- **`UITheme`** (SO: fonts, palette, sizes per role: Title / Button / Body / HUD / Screen) + a `ThemedText` component. All new UI uses it, so the final fonts and palette are a one-asset swap (§10b.3, ART_CONTRACT §7.1).

---

## 10. UI / HUD (greybox)
- **Driver view:** speed, gear, dashboard clock, **dash GPS screen** (§4.1b), next stop + scheduled time, money with delta pops, item slots, sanity vignette. **[PROPOSAL]** The clock, GPS and money counter are diegetic dash elements (a fare box display for the money). Only prompts and the vignette are screen-space overlays.
- **CCTV view:** camera label, REC, timestamp, scanlines, hallucination overlays.
- **Screens:** Main menu (+ Journal), Night intro card, Summary (itemised ledger, arrival ratings, monsters caught, innocents kicked), Depot Shop, Game Over, Run Won.
- **Debug overlay (F1):** sanity, threat per monster, attention target, scare director log, ledger, seed, and cheats (skip to stop, spawn monster, set sanity).

---

## 10b. Main Menu (D17–D19)
The menu should look like the game, because it *is* the game: the same prefabs, lighting and views. It replaces the current flat Canvas, which has a stock internet image (`Assets/Models/Images/CoverArt.jpg`, a licensing risk) and the default LiberationSans text.

### 10b.1 Scene
- `Menu.unity` is rebuilt by hand, composed of the **same prefabs the game uses**. Final art therefore shows up in the menu automatically through the view system (§11), with no menu-specific art.
  - A `BusStop.prefab` variant: one of the 6 stop kinds, trailhead or farm gate suggested.
  - A road segment and guardrail, and trees from the `EnvironmentViewSet`.
  - One `Passenger.prefab` in the Waiting state. Its logic is off and only its view is driven.
- **Shared look:** the night lighting (ambient, fog, moon light, URP post volume, CCTV-free) lives in a **`NightLightingPreset`** asset. `LightingPresetApplier` applies it in both the menu and the Night scene, so the two can't drift apart.
- **Lamp flicker:** a reusable `LightFlicker` component drives both the Light and the emission of the lamp's view. The same component is used in-game (street lamps, cabin lights, tunnel), so there's no menu-only code.
- **Camera:** a fixed composition across the road looking at the stop, with slow handheld drift (`CameraDrift`: Perlin position and rotation, small amplitude). The UI sits on the dark side of the frame.
- **Audio:** night forest ambience, lamp buzz tied to flicker, distant wind. These are Sound Controller entries; there's no menu music for now.

### 10b.2 Idle events (D18)
- A `MenuEventDirector` fires a random event every 20–45 s, with seeded weights. Every event is **masked by a flicker or a camera drift** so the change is only noticed afterwards:
  - The lamp flickers hard. When it steadies, the passenger has shifted pose or is standing slightly closer.
  - The passenger slowly turns its head toward the camera over about 10 s, and turns back the next time the lamp flickers.
  - The lamp goes fully dark for about 1 s, and the passenger is **gone**. The next flicker brings them back.
  - Distant headlights sweep through the trees with the sound of an engine, and nothing arrives.
- **Rules:** no loud stings, no face-in-camera, no events while a submenu (Options/Controls/Journal) is open, and a respect for the scare-intensity setting (§7.3).
- It reuses `IPassengerView` (`SetPose`, head look via `LookAtTargetIK`) and `LightFlicker`, so it's all generic components.

### 10b.3 UI
- **Placeholder style.** Every text uses a **`UITheme`** role (Title, Button, Body, HUD, Screen). Swapping the fonts and palette later is a one-asset change (see the ART_CONTRACT UI theme section).
- **Buttons:**
  - **New Run**.
  - **Continue Night N** (only if `run.json` exists, M6).
  - **Journal** (M6).
  - **Options**, **Controls**: the existing prefabs, restyled through `UITheme`.
  - **Quit**.
- **Layout:** left-aligned over the dark side of the frame, with no background panel so the scene reads through. Navigable by keyboard and gamepad (Input System UI module).
- The title text is placeholder ("BUS DRIVER"). A logo slot takes an image later.

### 10b.4 New Run transition (D19)
- A short Timeline of about 6–8 s, skippable with any key:
  1. Headlights sweep in, and the **real `Bus.prefab` view** pulls up to the stop.
  2. The doors open and the waiting passenger boards. Any idle-event state resets first.
  3. Fade to black, showing the night card ("NIGHT 1 · 00:40").
- **The Night scene loads async during the Timeline** (`allowSceneActivation` is gated on the Timeline ending), so there's no separate loading bar. The existing Loading Screen prefab stays only as a fallback for slow loads.
- The bus in the menu is **visual only**: its view prefab moves along a short spline, with no WheelCollider physics.

### 10b.5 Code clean-up folded in
- `MainMenu` loses `GameObject.Find("Sound Controller")` and the static `soundController`: the controller comes through a serialized reference.
- Scene loading uses `SceneIds` constants, not build indices.
- `CanvasMenu`'s fade is reused for the title fade-in.
- `CoverArt.jpg` is removed from the project, or moved outside `Assets/` if it's still wanted as a mood reference. It must not ship.

### 10b.6 Milestone placement
| Milestone | Menu work |
|---|---|
| M0 | `UITheme` + `ThemedText`; `SceneIds`; `MainMenu` de-singleton |
| **M1** | `NightLightingPreset`, `LightFlicker`, `CameraDrift`, the diorama scene, `MenuEventDirector` with the 4 events, the New Run bus-arrives Timeline, removal of `CoverArt.jpg` |
| M6 | Continue / Journal buttons |

---

## 11. Art pipeline: greybox → final art (D16)

The full rules for artists are in **[`docs/ART_CONTRACT.md`](ART_CONTRACT.md)**. This section covers the engineering side.

### 11.1 Rule: logic never touches visuals
Every object that will get art is split into a **logic root** (we own it) and a **`View` child** (the artists own it). Replacing a grey box means swapping the view child, with no code changes and no scene edits.
```
Passenger_Starer (logic root)
 ├─ Passenger / Monster components, ThreatMeter, colliders, interact trigger
 ├─ Anchors: Head, SeatPivot, …        (empty transforms read by logic)
 └─ View                               ← the only part that changes
     ├─ GreyboxView (capsule + sphere)  ← now
     └─ StarerView (FBX + materials + Animator) ← later, prefab variant
```
- **Colliders, triggers, rigidbodies and gameplay anchors live on the logic root only.** Hitboxes, seat positions and door paths therefore don't move when a mesh changes.
- The bus follows the same pattern. `Bus.prefab` keeps the WheelColliders, hull collider, seats, door path, CCTV mounts and driver-head anchor. A `BusView` child holds the model, and the visual wheels bind to the WheelColliders by name.
- The environment follows it too: blockers, trees, stops, guardrails and signs are each a logic prefab plus a view. An **`EnvironmentViewSet`** asset maps each kind (e.g. `Blocker.FallenTree`) to its view prefab. Swapping that one asset re-dresses the whole route.

### 11.2 View interfaces (the only contract with gameplay)
```csharp
public interface IPassengerView {
    void SetLocomotion(float speed);            // walk blend
    void SetPose(PassengerPose pose);           // Stand, Sit
    void SetTell(TellId tell, float intensity); // 0..1, monster tells
    void PlayReaction(ReactionId id);           // flinch, look-around
    void PlayDeath();
    Transform Head { get; }
}
```
- `GreyboxPassengerView`: transforms and colour tweaks (replaces the hard-coded `Passenger.SetPose`).
- `AnimatedPassengerView`: written once and reused by every art passenger. It forwards calls to an Animator using the **standard parameter names** from the art contract. Monster-specific clips use an `AnimatorOverrideController`.
- Tells that need procedural behaviour (the Starer's head tracking, the Mimic's flicker) are small reusable view components (`LookAtTargetIK`, `FlickerRenderer`). Gameplay only ever calls `SetTell`.
- Likewise `IBusView` (doors, wheel visuals, interior light states, dash screens) and `IEnvironmentView` (blocker/sign/lamp states such as flicker).

### 11.3 Tooling (M8)
- **`ArtImportPostprocessor`** on `Assets/Art/**` enforces import settings by folder and suffix: scale, Humanoid rig for characters, material extraction, texture compression and max size, and the normal-map flag.
- **Tools ▸ Bus Driver ▸ Make View from FBX** creates the view prefab, adds `AnimatedPassengerView`/`BusView`, assigns the shared controller or override, and auto-binds anchors by name.
- **Tools ▸ Bus Driver ▸ Validate Art** checks every view against the contract:
  - required anchors and clips are present
  - the pivot and height are within tolerance of the greybox
  - there are no colliders or rigidbodies inside views
  - triangle and texture budgets are met
  - there are no missing materials or scripts

  It also runs in the smoke test, so broken art fails the test, not the playtest.
- **Reference FBX export:** the greybox bus, passenger and blockers are exported to `Assets/Art/_Reference/` with the exact dimensions, so artists can model to scale.

### 11.4 Builder rule
After M0 the builder generates only **logic prefabs and `Greybox*View` prefabs**. It never writes into `Assets/Art/`, into prefab variants, or into `EnvironmentViewSet` entries that point at art. **Anything an artist touches is never generated.**

### 11.5 Scares, audio, VFX
- Kill-scare Timelines bind to view anchors (`Head`, `Anchor_Face`) and a placeholder face. Designers retarget the bindings and retime the scare without code.
- Audio keeps the named Sound Controller entries: the sound designer swaps the clip on the entry and the name stays the same.
- Lights and particles attach to anchors, never to mesh bones that may be renamed.

### 11.6 Incremental and reversible
- Art lands one asset at a time. Mixed scenes (one monster with art, one still greybox) are normal.
- **F2 debug toggle: Greybox ↔ Art** swaps every view that has art back to greybox at runtime, so you can quickly tell whether a bug comes from gameplay or from the art.

---

## 12. Milestones to a playable greybox
Each milestone ends playable, with the smoke test extended.

| # | Milestone | Deliverables | Done when |
|---|---|---|---|
| **M0** | Foundations | asmdefs and namespaces; builder → logic prefabs + `Greybox*View` children (§11); `IPassengerView` / `IBusView` with greybox implementations; hand-authored `Night` scene; `ShiftContext`; pause/mode split; Input System; EditMode test assembly; `UITheme` + `ThemedText`; `SceneIds`; `MainMenu` de-singleton | the existing smoke test passes on the new scene; no behaviour change |
| **M1** | Route, Clock & GPS | `RouteLayout` spline road (first-pass linear forest/hill road + blocked side-stubs + auto guardrails/walls), `RouteDefinition`, ordered stops, `RouteTracker`, dash GPS (`RouteMapView`), `ShiftClock` + dash clock, destinations/alighting, terminus → Summary stub, containment bot test; **main menu diorama v1** (§10b: `NightLightingPreset`, `LightFlicker`, `CameraDrift`, `MenuEventDirector`, bus-arrives Timeline, `CoverArt.jpg` removed) | you can drive the full route using the GPS, the containment test passes, you drop everyone off, and New Run from the diorama menu lands in the Night scene |
| **M2** | Economy | `Wallet`, `ShiftLedger`, fares/tips, money HUD deltas, Summary screen | the ledger is unit-tested; the totals are correct |
| **M3** | Threat + scares + death | `PlayerAttention`, `ThreatMeter`, `MonsterDefinition`, `KillSequence`, `ScareDirector`, `DeathDirector`, the Starer (with its scares), **the cliff + `FallDeathSequence`**, Game Over, debug overlay | the Starer can kill you fairly and you can kick it; driving off the cliff plays the fall cam and ends the run |
| **M4** | Sanity + hallucinations | `SanitySystem`, `HallucinationDirector`, startles, the Whisperer, sanity-0 death | low sanity visibly and audibly changes play and can kill |
| **M5** | Mimic + passenger death + decoys | the Mimic, the copy mechanic, passenger death, 4–5 decoy behaviours | all 3 monsters are in and beatable; decoys fool playtesters |
| **M6** | Run flow & items | `RunState`/`MetaProgress` saves, `NightDefinition` + manifest generator, Depot shop, ~5 items, journal, Run Won, menu Continue/Journal buttons | a full 5-night run can be won or lost |
| **M7** | Content & tuning | forest dressing, tunnel/drop set-pieces, night 1 teaching script, difficulty curve, balance pass, playtest checklist | a new player finishes or dies fairly; a night lasts about 12–15 minutes |
| **M8** | Art pipeline (parallel with M3–M7, starts when the artists do) | Git LFS for art binaries, `Assets/Art/` folder skeleton + `_Reference` FBX exports, `ArtImportPostprocessor`, Make View tool, Validate Art tool + smoke-test hook, `AnimatedPassengerView`, shared animator controller, `EnvironmentViewSet`, `UCX_` collider extraction, F2 greybox/art toggle | one artist-made passenger and one blocker are swapped in with zero code/scene changes and pass validation |

---

## 13. Verification
- **EditMode unit tests:** ledger, schedule rating, threat curves, sanity tiers, scare arbitration, manifest generation (seeded).
- **PlayMode smoke test** (`BusSmokeTest`, extended): drive the route, board and alight, kick, a monster reaches Lethal and kills, sanity 0 kills, a night completes, the run advances.
- The batch-mode workflow as today (build prefabs → tests → `[SMOKE]` + console check).
- A manual playtest checklist per milestone in `docs/playtest.md`.
- The final spec is committed as `docs/GAME_SPEC.md` at the start of M0.

---

## 14. Open items
No blocking questions remain. These **[PROPOSAL]**s stand unless someone objects:
- Missed stop = waiting riders lost, no extra penalty (§4.1). Monster kicked = fare kept + bounty (§4.4).
- Sanity carry-over `max(current + 30, 60)` (§6).
- The night difficulty curve (§3.4). All numbers are first-pass and get tuned in M7.
- Diegetic dash for the clock, GPS and money (§10).
- A scare-intensity accessibility hook (§7.3).

---

## Appendix A — Ideas (NOT part of the spec)
- **The Follower (evil car):** headlights behind the bus that close in while you're slow or stopped and fall back at speed, and kill if they reach you. It never collides (D1). It would punish stopping to kick. The `IThreat` interface keeps the door open.
- **The Passenger Who Stays:** doesn't get off at its stop and has no idle motion; it kills other passengers each stage.
- On-foot escort (dragging a monster out) instead of it walking out itself.
- Multiple routes/levels (each still linear, D11), a route choice per night.
- A reactive menu: the waiting figure at the menu stop becomes the monster that killed you last run (uses journal data).
- Random road events (a figure in the road, a tree falling across the *main* road) that force a stop.
