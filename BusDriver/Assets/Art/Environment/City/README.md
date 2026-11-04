# City kit (final_expo_27THoct)

123 low-poly static meshes (shops, old offices, apartments, street props), delivered as OBJ.

- **Import:** `Tools ▸ Bus Driver ▸ Art ▸ Import City Kit` (`Assets/Editor/Art/CityKitImport.cs`). The OBJs are in hundredths of a metre, so they import ×100. The tool makes `Materials/M_City_*` and one prefab per mesh in `Prefabs/`, with the pivot at the ground centre and the front facing +Z. A shop's `_Glass` mesh goes into the shop's prefab.
- **Placement:** `Assets/Editor/Builders/RoadsideArtBuilder.cs` places them in Route01_World. It's part of the Route01_World builder, so edit the spot list there, not the scene.
- **Missing texture:** every mesh samples one palette atlas, `PolygonCity_Texture.png`, which wasn't in the delivery. Until it arrives, the materials are tinted per look over a generated greyscale stand-in (`Textures/T_PolygonCity_StandIn_BaseColor.png`). To use the real one, save it as `Textures/T_PolygonCity_BaseColor.png` and run the import again.
- **Delivery fixes:** six glass meshes (Shop_01/03/04/05, Shop_Corner_01, BusStop_01) were exported Z-up at 1/10000 scale. The copies here were converted to match the others. The `.mtl` files weren't kept, and `SM_Prop_Sign_Parking_01_001` (a duplicate) was dropped.
- **Known issue:** `SM_Prop_Billboard_01` is just the frame, with no face panel; the builder adds a blank panel behind it.
- **Lights:** RoadsideArtBuilder also adds lights: a front light and an interior glow on the open shops and the clinic, wall lights over the garage and depot doors, and a lamp on the billboard. Each light has its own LightFlicker.
