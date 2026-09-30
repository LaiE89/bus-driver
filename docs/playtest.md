# Bus Driver — Playtest checklists

Manual checks per milestone (ROADMAP §4.19). The automated tests cover the rules and the numbers; these cover what only a person can judge: whether things read, feel fair and land at the right moment. Play in the Editor from `Assets/Generated/Scenes/Menu.unity` (New Run), or press Play in `Night_Systems` for a debug run. F1 opens the debug overlay and its cheats.

Where the logs are, for bug reports:
- macOS: `~/Library/Logs/Bus Driver Team/Bus Driver/Player.log`
- Windows: `%USERPROFILE%\AppData\LocalLow\Bus Driver Team\Bus Driver\Player.log`

## M4 manual checks — the Starer, death and the cliff

Play night 1 at least three times: once ignoring the Starer, once watching it, once kicking it. Note anything that feels unfair or unreadable, with the time from the F1 "Run" section.

**The Starer's escalation (§2.10)**
- [ ] It boards at Hollow Creek Campground and sits at the back. Nothing about it stands out on the first CCTV look.
- [ ] Head tracking: on every CCTV camera and from the driver's seat, its head turns toward the camera you're looking through, slowly (about 25°/s), never snapping.
- [ ] Unwatched after the cliff, it is a few rows closer each time you check the CCTV. You never see it move.
- [ ] Its eyes visibly widen and its idle motion visibly stops as it escalates. At Unsettled you can tell something is off; at Aggressive you can tell it's dangerous.
- [ ] The lens scare: the first CCTV cycle after it turns Aggressive cuts to a camera with its face filling the lens. It lands as a jolt, not as a bug. If you don't cycle within 20 s, it never plays.
- [ ] Watching it on CCTV keeps it down: it never moves forward while on screen.

**The kill-sequence telegraph (§2.14, D31)**
- [ ] When it reaches Lethal it stands in the aisle beside the front row, the cabin lights flicker and the rumble plays. The telegraph is unmistakable even if you're watching the road.
- [ ] 4 s is enough to react: cycle the CCTV to it, hold it on screen for 1.5 s, and it sits back down in row 2. Try it three times; it should work every time you do it in time.
- [ ] When you don't react, the kill (its face at your shoulder, the sting, the blackout, Game Over) reads as "I should have checked", not as random.
- [ ] Game Over says "THE STARER GOT YOU" with the hint "It only moves when you aren't looking."

**Kicking it out (§2.13)**
- [ ] Stop, press Leave seat, walk down the aisle and look at it: the prompt says "Kick out" (right mouse button by default).
- [ ] It walks out through the door on its own. The fare box pops the bounty (+$5.00) and the Summary lists one monster kicked.
- [ ] Kicking an innocent instead refunds their fare (−$3.50 on the fare box).

**The cliff (§2.14 Fall, D14)**
- [ ] Dead Man's Bend is announced (sign, chevrons, rumble strip) and the missing guardrail on the left is visible at night.
- [ ] Drive off the left edge on purpose: the view cuts to the fall camera out over the valley, the bus is visible and lit as it drops, the first second is in slow motion, the engine cuts and the wind plays.
- [ ] The impact on the valley floor is audible and shakes the view.
- [ ] About 3 s after going over: fade to black with "YOU WENT OVER THE EDGE", then Game Over with "Keep your eyes on the road at Dead Man's Bend."
- [ ] Hitting the rock face on the right, or the guardrails elsewhere, never kills and never puts the bus through the world.

**Scare timing (§2.17)**
- [ ] No two big scares land within a few seconds of each other.
- [ ] Nothing but the kill itself plays during a telegraph.
- [ ] Options → Scare intensity Reduced: flashes are soft fades, there's no camera shake, and stings are quieter. Timing is unchanged.

**Death and the run (§2.14, D21)**
- [ ] After any death, Game Over offers New Run and Main Menu, and New Run starts night 1 from $0.
- [ ] Quitting the game on the Game Over screen and relaunching shows no Continue: the run is gone.
