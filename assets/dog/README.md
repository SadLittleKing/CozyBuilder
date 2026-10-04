# Modular trail dog

`dog_modular.blend` is the editable Blender source, `dog_modular.glb` is the full module library used by MonoGame, and `generate_dog.py` reproduces both from built-in Blender primitives. No external textures or downloaded model files are needed. The GLB contains **45 named mesh parts**; a generic viewer will show all accessories at once because GameClient and the dog viewer choose visible slots at runtime.

Rebuild with Blender 3.3 from the workspace root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 3.3\blender.exe' --background --python-exit-code 1 --python assets/dog/generate_dog.py
dotnet run --project src/CharacterEditor -- --dog --verify-dog
```

The dog uses metres, about 1.1 m nose to tail and 0.9 m high including the raised tail. In Blender, +Z is up and **-Y is forward**. The glTF export converts this to MonoGame **+Y up, +Z forward**, matching the villager's ground and facing convention. The root is `Dog_Root`. Every base mesh begins `Dog_Base__`; optional meshes begin `Dog_Collar__`, `Dog_Helmet__`, `Dog_Armor__`, or `Dog_Backpack__`. The single base body is shared by all 16 combinations. Parts are static and rigid; there is no skeleton or animation yet.

`Socket_Dog_Backpack` is an empty child of `Dog_Root` at Blender `(0, 0.11, 0.81)` metres, on top of the spine. The exported MonoGame translation is approximately `(0, 0.81, -0.11)`. Its local +Z is up and -Y is forward in Blender. The simple backpack mesh is parented to that socket; future replacement packs can use the same local origin. `dog_info.json` lists every part and the socket convention. The socket is preserved in `GlbScene.NodeTransforms` for later attachment or animation work.

Inspect and toggle modules in the existing CharacterEditor executable:

```powershell
dotnet run --project src/CharacterEditor -- --dog
dotnet run --project src/CharacterEditor -- --dog --screenshot dog-default.png
dotnet run --project src/CharacterEditor -- --dog --helmet --armor --backpack --screenshot dog-armored.png
dotnet run --project src/CharacterEditor -- --dog --helmet --armor --backpack --back-view --screenshot dog-backpack.png
```

In the viewer, click an accessory row or press **F1–F4** for collar, helmet, armor, and backpack. Drag or use arrow keys to orbit, wheel or plus/minus to zoom, **Space** for turntable, **R** to reload the GLB, and **F5** to save `dog.json`. GameClient reads the same `dog.json` by default, or `--dog-config path`. Gear can also be toggled in GameClient with F1–F4; those choices save to `dog.json`. `--verify-dog` checks part groups, scale, the backpack socket, shared geometry, and all 16 toggle combinations.

The current low-poly model is a static prototype. The companion follows through the village, homestead, interiors and maze without collision or carrying capacity. A future rig can put the backpack socket on a spine bone while keeping the same slot names.
