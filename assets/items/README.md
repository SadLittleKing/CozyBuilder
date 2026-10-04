# Item model pack

Each of the 17 `ItemCatalog` IDs has one static, flat-color glTF 2.0 binary model (`<id>.glb`). The files use embedded geometry and materials; there are no external textures or runtime Blender dependencies. `manifest.json` lists the IDs. This directory can be packaged independently with a future content pack.

Regenerate from the workspace root with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 3.3\blender.exe' --background assets/villager/villager_modular.blend --python-exit-code 1 --python assets/items/generate_models.py
```

The generator exports all eight equipment items from the named meshes in the modular villager Blender source. It creates the nine resources as low-poly Blender objects, then exports those objects. The source script and villager `.blend` are editable; the GLBs are runtime assets. Gloves use one representative right-hand mesh for a legible item silhouette, while the equipped character still uses both hands from the same source.

GameClient loads all GLBs through `GlbScene` once. It uses their triangles directly for ground drops and renders the same triangles to 128×128 transparent textures for inventory, chest, and equipment icons. Textures are cached until an **R** reload or graphics-device reset. Intact trees and rock nodes are separate composite world objects; these models represent harvested items.
