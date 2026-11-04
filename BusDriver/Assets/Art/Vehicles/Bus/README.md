# Vehicles/Bus — Bus_Asset_001

The player's bus. Artist-owned (D16): nothing in this folder is generated.

| File | What it is |
|---|---|
| `SK_Bus_00.fbx` | The rigged bus, delivered as `SKM_Bus_00.fbx` (cm, Y up, Z forward) |
| `A_Bus_DoorOpen.fbx` | The door animation, delivered as `anim_bus_Dooropen.fbx`. **Empty** (see below) |
| `Textures/T_Bus_*_BaseColor/_Normal.png` | Delivered as `_BC` / `_N`; Trim and Window carry their opacity in alpha |
| `Textures/T_Bus_*_MaskMap.png` | Converted from the delivered `_ORM` maps |
| `Materials/M_*.mat` | URP Lit, one per material slot in the FBX |
| `../../_Source/Vehicles/Bus/` | The delivered `_ORM`, `_O` and the Trim/Window `_BC` originals the conversion reads |

## How it gets into the game

1. **Tools ▸ Bus Driver ▸ Art ▸ Import Bus** (`Editor/Art/BusArtImport.cs`) sets the import settings, converts the textures and builds the materials. Run it again after a re-delivery.
2. **Build All** (`BusArtViewBuilder`) puts the model into `Generated/Prefabs/Bus.prefab` as its View with an `AnimatedBusView`. The game drives the joints `FL_rot`, `FR_rot`, `RL_rot`, `RR_rot`, `steercolumn`, `door1` and `door2`. Everything bound only to `chassis` is baked into one static mesh per material.
3. The bus's physics, seats and anchors were fitted to this model (ROADMAP D108, `Editor/Builders/BusLayout.cs`). **A re-delivery must keep the same dimensions, seat positions and joint names.**

## Fix list for the next delivery

Nothing here blocks the game; each item has a workaround in place.

1. **The door animation has no keys.** `anim_bus_Dooropen.fbx` holds only the empty group `SKM_Bus_GRP` and a take named `Bus_DoorOpn` with no curves or joints. Re-export it with the skeleton and baked keys on `door1`/`door2`/`doorlever`. Until then the game turns each leaf 88° inward about its hinge.
2. **Duplicate mesh names.** Unity warns "Identifier uniqueness violation" for `SM_wheel_00` (×8), `SM_WheelNut_01…06`, `SM_SeatBottom_01`, `SM_SeatBabk_R`, `SM_Pedal_Base_00`, `SM_Pedal_Nut_05…08` and `SM_TopRail_Mount_00…02`. Give every mesh a unique name, or a re-import may re-link the wrong one. (`SM_SeatBabk_R` is also a typo.)
3. **Texture names and packing (Appendix A.0).** Please deliver `_BaseColor`, `_Normal` and `_MaskMap` (R metallic, G AO, A smoothness) instead of `_BC`, `_N` and `_ORM` (R AO, G roughness, B metallic). Put opacity in the BaseColor alpha rather than in a separate `_O` map. `T_Bus_Interior_*` is missing the `_00` variant number. The importer converts the current files, so this only matters for consistency.
4. **8-bit PNGs.** The textures are 16 bits per channel, which doubles the file size for no visible gain.
5. **No cabin light fixtures.** A.2 asks for emissive `Light_Cabin_Front/Mid/Rear` meshes, so the scares can flicker visible lamps, not just the light they cast. There are none on the ceiling now.
6. **No screen quads.** A.2 asks for `Mirror_Cabin` and flat UV 0–1 quads for the clock and fare box. The game currently puts the GPS on the dash monitor, the mirror view on the rear-view mirror, the clock on the switch panel right of the gauges and the fare box on the radio. Dedicated screens would look better.
7. **Maya lights and cameras in the FBX** (`Light_GRP`, `CCTV_GRP`, `DriverSeatAnchor_GRP`). They're ignored on import; leave them out next time, or turn them into `Anchor_` empties.
8. **The aisle is 0.48 m between seat cushions.** The on-foot player is 0.56 m wide, so their walking collider is 0.6 m and brushes the cushion edges. Widening the aisle by ~10 cm would fix it.
