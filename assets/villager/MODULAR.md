# Static modular villager

The original `villager_base.blend`, `villager_base.glb`, generator, and previews are preserved. The new `villager_modular.glb` is a **module library**, not a single assembled character. A generic GLB viewer will display overlapping alternatives. CharacterEditor selects the requested pieces; GameClient loads the same saved configuration.

## Rebuild and edit

```powershell
& 'C:\Program Files\Blender Foundation\Blender 3.3\blender.exe' --background --python-exit-code 1 --python assets/villager/generate_modular.py
python assets/villager/validate_glb.py assets/villager/villager_modular.glb
```

`generate_modular.py` reuses the geometry-building section of `generate_villager.py`, then adds named options and socket nodes. It does not execute the original generator's exports or renders. Outputs are `villager_modular.blend`, `villager_modular.glb`, and `modular_info.json`. Regenerating replaces these modular outputs; save manual edits under another name first.

In `villager_modular.blend`, the original collections hold the shared pieces and sage/swept options. Additional collections are `Hair_Bun`, `Outfit_Rust`, `Equipment_Sword`, `Equipment_Backpack`, and two collections for each of the four inventory gear slots. The default combination is visible; alternate pieces are hidden individually in the viewport and for rendering. Use the Outliner eye/render toggles to inspect an alternative and hide the other option. To export the whole library manually, include all modules and the two socket empties, not only visible objects. The generator is the authoritative way to reproduce the tested full library.

## IDs and masks

`CharacterConfiguration` stores five stable IDs. All configurations refer to one `villager.base` body in one loaded `GlbScene`. `CharacterAssembly` references existing `MeshPart` instances; changing options neither duplicates full bodies nor reloads geometry.

| Slot | IDs | GLB part prefix |
| --- | --- | --- |
| Body | `villager.base` | `Shared__` |
| Hair | `swept`, `bun` | `Hair_Swept__`, `Hair_Bun__` |
| Outfit | `sage`, `rust` | `Outfit_Sage__`, `Outfit_Rust__` |
| Hand | `none`, `sword` | `Equipment_Sword__` |
| Back | `none`, `backpack` | `Equipment_Backpack__` |

The game inventory has four additional slots. Their selected item IDs live in `maze-save.json`, independently of the appearance choices in `character.json`:

| Inventory slot | Item IDs | GLB part prefixes |
| --- | --- | --- |
| Head | `padded_hood`, `iron_helm` | `Gear_Head_Hood__`, `Gear_Head_Helm__` |
| Body | `padded_vest`, `iron_cuirass` | `Gear_Body_Vest__`, `Gear_Body_Cuirass__` |
| Gloves | `leather_gloves`, `iron_gauntlets` | `Gear_Gloves_Leather__`, `Gear_Gloves_Iron__` |
| Feet | `trail_boots`, `iron_boots` | `Gear_Feet_Trail__`, `Gear_Feet_Iron__` |

The head meshes replace visible hair, gloves replace the shared hands and thumbs, and footwear replaces the shared boots; body gear sits over the selected outfit. CharacterEditor rows 5–8 cycle none and the two meshes for each slot as a visual preview. GameClient selects the same meshes from equipped inventory items. The backpack and sword appearance modules remain separate from these four gear slots.

All shared head, face, ear, hand, boot and uncovered arm pieces are reused. Both outfits hide `Shared__Body_Torso`. Rust additionally hides `Shared__Body_UpperArm_L/R` and `Shared__Body_Forearm_L/R`, replacing them with fitted long sleeves. Sage retains the bare arm geometry and short sleeve/cuff pieces. Each outfit includes trousers, collar, belt and pouch; only one complete outfit set is visible. A full unclothed lower body is not included.

## Rigid attachments and future rig

The scene root is `Villager_Root`. In Blender, +Z is up, -Y is forward, and units are metres. The GLB exporter converts to +Y up and +Z forward.

| Node | Blender position | Intended future bone |
| --- | --- | --- |
| `Socket_Hand_R` | `(0.535, -0.013, 0.811)` | `hand_r` |
| `Socket_Back` | `(0, 0.159, 1.08)` | `spine_02` |

Sword parts are authored around a local grip origin, blade along local +Z. The hand socket rotates that axis outward and down to clear the sleeve and read at the elevated camera. Backpack parts use a local body-facing panel origin, extending along Blender +Y. They are parented to the back socket. Exact socket rotations are recorded in `modular_info.json`; the Blender empties remain editable. `_R` currently means positive Blender X, matching the original prototype, and must be reconciled with anatomical rig naming when a skeleton is introduced.

The importer preserves every named world transform in `GlbScene.NodeTransforms` and bakes hierarchy transforms into static mesh vertices. **There is no skeleton, skinning or animation yet.** Equipment is rigid; torso, sleeves and trousers will need weights against one common skeleton for deformation. Future animation should evaluate socket bones at runtime and retain equipment in local coordinates instead of using the current baked world vertices. Mitten hands visually surround the grip but do not have articulated fingers.

## Verification

The full library contains 113 mesh primitives and 7,800 triangles. The default appearance with no inventory gear draws 47 parts; bun/rust/sword/backpack draws 56. `--verify-swaps` checks all 16 appearance combinations; `--verify-gear` checks all 81 head/body/gloves/feet combinations, masks, references and reversible cycles. Structural validation checks buffers, normals, materials, unique body head and equipment socket parents. [Light equipment](../../gear-light.png) and [iron equipment](../../gear-iron.png) were rendered in the MonoGame editor.

This remains a static flat-color prototype with overlapping construction geometry, not a production deformation mesh. There are no animations, skin weights, UV textures, physics, character scaling, or automatic fitting across body proportions. The short- and long-sleeve outfits and all eight gear meshes are deliberately fitted to this one body.
