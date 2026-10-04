# Villager base

For the newer interactive swap library, see [MODULAR.md](MODULAR.md). The original base files described here remain preserved.

A warm, faceted 3D prototype for a cozy combat RPG, designed to remain readable from an elevated orthographic camera. Original geometry generated locally; no external textures, fonts, models, or paid dependencies.

## Files

- `villager_base.glb` — engine-neutral glTF 2.0 binary. Only the character is exported.
- `villager_base.blend` — editable Blender scene, with named component collections and a separate preview stage.
- `generate_villager.py` — complete procedural source. Requires Blender 3.3 or a compatible version of its Python API.
- `villager_preview.png` — elevated front three-quarter view, approximately 28 degrees downward.
- `villager_back.png` — elevated rear three-quarter view.
- `villager_front.png` — near-front inspection view.
- `asset_info.json` — dimensions, polygon count, color palette, coordinate conventions.
- `validate_glb.py` / `validation_report.json` — standard-library structural validation and its results.

## What is included

An oversized rounded head, solid mesh eyes and face details, swept blocky hair, a sage tunic, cream collar, belt and pouch, trousers, boots, and simple mitten hands. An A-like relaxed assembly pose keeps the limbs visible and accessible for editing.

48 separately named mesh objects, 3,964 triangles, and 13 flat-color PBR materials. Flat normals intentionally split vertices along facets; the exported vertex count is higher than the Blender editable vertex count. Approximate size: 1.85 m tall, 1.23 m across the hands, 0.52 m deep. Origin at the ground between the feet.

Blender coordinates: Z up, front -Y. GLB coordinates: Y up, front +Z. The exporter converts the axes. Preserve node translations/rotations and parent transforms when loading. Material `baseColorFactor` values are **linear RGB**; authored hex colors in `PALETTE` are sRGB. Preview lighting and ambient occlusion are not baked into the GLB, so engine lighting affects its appearance.

## Editing in Blender

Open `villager_base.blend`. The `VILLAGER_ASSET` collection contains `Body`, `Face`, `Hair`, `Outfit`, and `Footwear`. Expand a collection in the Outliner to choose a piece. Use Material Properties to change its color, or Tab into Edit Mode to reshape it. `PREVIEW_ONLY` contains the floor, lights, and camera; hide that collection when convenient for modeling.

The shared material colors can also be changed in `PALETTE` near the top of the generator, then regenerated. Body proportions and clothing outlines are controlled by the dimensions and ring coordinates in the source. Regenerating overwrites the `.blend`, `.glb`, and previews, so save manual variations under new filenames first.

To export a manually edited character: select `Villager_Root` and its character mesh descendants, choose File > Export > glTF 2.0, select GLB, enable Selected Objects and +Y Up, and exclude cameras, lights, and animations. Do not select `PREVIEW_ONLY` objects.

## Regenerating

From the project root in PowerShell:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 3.3\blender.exe' --background --python-exit-code 1 --python assets/villager/generate_villager.py
python assets/villager/validate_glb.py
```

## Prototype limitations

This is **not rigged or animated**. Parts overlap by design; it is not a continuous welded body or production deformation mesh. There are no skin weights, facial blend shapes, UVs, textures, collision shapes, LODs, or equipment attachment bones. The legs are represented by the trousers; there is no complete unclothed body underneath. Left/right suffixes refer to Blender X sides rather than an anatomical rig convention.

For shared combat animations, next build a common skeleton and weight the body/clothes to it, add elbow/knee deformation geometry or use deliberate rigid-piece articulation, and place hand/back attachment bones for equipment. Hair, outfit colors, and rigid accessories can already be varied independently. Material/node counts prioritize editability; merge compatible parts and reduce materials/draw calls when optimizing a crowded village.
