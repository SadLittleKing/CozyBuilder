# Game workspace

This solution has two MonoGame desktop apps. **CharacterEditor** assembles and previews a modular villager and trail dog. **GameClient** loads both into Willowcross Village, ten enterable building interiors, a separate homestead with a residence and farm plots, and a saved, generated woodland maze. They share the GLB loader in **Game.Assets**. The villager has one shared body, 16 hair/outfit/hand/back combinations, and four additional equipment slots with two visible item meshes each. The dog has 16 collar, helmet, armor, and backpack combinations.

## Projects and assets

```text
Game.sln
src/CharacterEditor/       Character assembly, preview camera, editor HUD and controls
src/GameClient/            Main game executable and generated village scene
src/Game.Assets/           Shared GLB loader, configuration and character assembly
assets/village/            Editable village, homestead and interior layout JSON
assets/villager/           Modular GLB, editable Blender sources, generators, validation
assets/dog/                Modular dog GLB, editable Blender source and generator
tools/                     Reserved for future standalone tool projects
```

The apps target .NET 10 and MonoGame DesktopGL 3.8.5.1. First build restores packages from nuget.org. A desktop session with OpenGL support is needed to display either app. The GLB is copied into both apps' build and publish output. Open `Game.sln` in an IDE and choose `GameClient` or `CharacterEditor` as the startup project.

## Run from the workspace root

```powershell
cd C:\waywardleaf\Research\Game
dotnet build Game.sln
dotnet run --project src/CharacterEditor
dotnet run --project src/GameClient
```

In the editor, click a row or press **1–4** to cycle hair, outfit, hand equipment, and backpack. Rows **5–8** preview head, body, gloves, and feet gear. Each gear row cycles through none and two distinct meshes. **F5** saves the appearance choices to `character.json`; gear rows are preview-only in this editor, while GameClient saves equipped items in the game save. **R** reloads the GLB while keeping the selection. Drag or use arrow keys to orbit, use the mouse wheel or plus/minus keys to zoom, **Space** for turntable, **Home** to reset the camera, **W** for wireframe, and **Esc** to close.

The client automatically loads `character.json` from the working directory when it exists; otherwise it uses swept hair, sage outfit, and no hand/back accessory. Use **WASD** or arrow keys to walk, **E** at a door, gate or open maze path to travel, **F** to farm or collect a seed or loose drop, **hold F** beside a maze tree or rock to harvest it, **I** for the bag and equipment panel, **C** near a chest to open it, **Tab** for the maze automap, **M** for the current map's overview, **R** to validate and reload edited village JSON and item GLBs, and **Esc** to close. The top panel shows the nearby action. The camera follows the character outdoors; indoors and in maze clearings it frames the whole space. Buildings, furniture, pond, well and map edges block movement.

To reach your homestead, follow the village's **west street** to the sign labeled **HOMESTEAD** and press E. The homestead's west gate returns to the village. Walk to the house threshold and press E to enter **YOUR RESIDENCE**; the south doorway returns to the same threshold. Every labeled village building likewise has its own interior and returns to its own exterior approach point. Houses share a furnishing template but remain separate map instances.

The homestead contains eight farm plots. Stand near one and press **F** to **till → plant → water → harvest**. Planting spends one `common_seed` from the bag; you start with four, and the maze's common-seed pickup adds three more. Harvesting returns one common seed and one `crop`, so the loop can continue indefinitely while your bag has room. A full bag leaves a watered crop ready to harvest later. Plot stages, harvest count, and crop inventory save across restarts. There is no crop timer, watering resource, season, or sale system yet.

The bag has **16 stack slots**; each chest has **20**. Seed and crop stacks hold multiple units, while equipment occupies one slot per piece. The starter bag has a padded hood, padded vest, leather gloves, trail boots, and four common seeds. Your **residence chest** holds iron alternatives for all four equipment slots; each of the four village homes has its own empty chest. The panel shows a baked icon for every item and a live, fixed-angle preview of the character with its current gear. **Right-click** gear in the bag to equip it, or an occupied equipment slot to unequip it; left-click selects a row. Keyboard controls remain: **Up/Down** selects an item, **Enter** or **E** equips a bag item, and **Tab** switches panes. Select an equipped slot and press Enter to unequip. Near a chest, press C, then use Tab to switch among chest, equipment, and bag. Enter transfers one selected item between bag and chest; **Shift+Enter** transfers its full stack. E equips a selected bag item even while the chest is open. I, C, or Esc closes the panel. When a bag or chest is full, a transfer or unequip fails without changing either side. Armor changes the actual villager meshes; dog gear remains in the separate `dog.json` configuration.

Every catalog item has a reusable glTF 2.0 binary model (`.glb`) in `assets/items`. Gear models are exported from the same named meshes in `assets/villager/villager_modular.blend` that assemble onto the character; the nine resource models are authored by `assets/items/generate_models.py`. Run `blender --background assets/villager/villager_modular.blend --python-exit-code 1 --python assets/items/generate_models.py` from the workspace root to regenerate all 17 models. GameClient loads these GLBs once, renders each inventory icon to a transparent offscreen texture at startup, caches the textures for bag, chest, and equipment slots, and rebuilds the cache after a graphics-device reset. Ground drops draw the same GLB geometry at a smaller world scale. The game package includes the GLBs; generated icon PNGs are no longer required.

The trail dog follows the player on every map and wears a collar by default. Press **F1–F4** in GameClient to toggle its collar, helmet, armor, and backpack; this saves `dog.json` independently of the maze save. To inspect the asset up close, run `dotnet run --project src/CharacterEditor -- --dog`. The viewer offers the same F1–F4 toggles, clickable rows, orbit and zoom controls, and F5 to save `dog.json`. The [dog asset guide](assets/dog/README.md) documents its named parts, scale, Blender source and backpack socket.

Follow the village's **south dungeon road** to the gate marked **WOODLAND MAZE** and press **E**. The prototype contains 100 connected woodland clearings on a 10 × 10 grid. Each clearing has one to four actual exits, marked by tan paths and direction labels; press E near an exit to follow it. The entrance clearing's west path returns to the village gate. **Tab** opens an automap: green squares are visited clearings, dark squares are seen neighboring exits, and the gold square is your current position. Special-node letters appear only after visiting them. Exactly one clearing holds a rare seed and one holds three common seeds; stand beside either and press F to add the item to your bag once. If the bag is full, the pickup remains in the clearing. A third clearing contains a shrine: press E beside it to return to the village and unlock a shortcut. At the village dungeon gate, press **J** to jump back to the activated shrine, or E to restart at the maze entrance.

Four seeded resource nodes appear in each maze clearing. The first clearing always has harvestable wood and stone. Hold **F** near a tree or rock until the bar empties; releasing F or walking away resets progress. Trees take 1.35 seconds, stone 2.2, and ore rock 3.4. Harvested objects disappear and leave separate visible item-model drops. Press **F** near a drop to pick it up; if your 16-slot bag cannot fit the full stack, it stays on the ground. Red apple, blue berry, and purple plum trees show colored fruit before harvest and can drop their matching fruit with wood. Resource placement follows the maze seed and favors basic materials near the entrance. Depleted nodes and uncollected drops persist through travel and restarts. Intact trees and rocks remain composite world geometry distinct from their harvested item models. The low-poly rock node has an editable [source](assets/maze/rock.json), [OBJ export](assets/maze/rock.obj), and [generator](assets/maze/generate_rock.py).

GameClient automatically writes `maze-save.json` in the working directory. It stores the generation seed, current map and position, explored clearings, pickups, shrine unlock, bag, equipped items, each chest, farm state, depleted resources, and loose drops. Movement saves every two seconds; transitions, inventory actions, pickups, farming, and harvesting save immediately. Restarting resumes the same world. Older maze saves load with a starter bag and any seeds previously marked collected. `--seed 12345 --new-maze` begins a fresh maze and replaces that save on the next write. `--save other.json` selects a separate slot. Use `--map maze:0` for a direct debug start; screenshots do not modify the save. Maze generation uses a connected spanning tree plus a few loops, reciprocal cardinal exits, and deterministic special-node placement. The graph and biome field are structured for later scale and terrain additions; this prototype renders woodland only.

In the maze, press **Q** to ask the dog for a scent. Each press cycles among available modes: **Explore** guides one exit at a time toward the nearest unseen clearing, **Home** recalls the entrance, and **Shrine**, **Rare**, or **Common** recall those locations only after you have visited them. If an uncollected seed lies within three maze links, Explore may follow its scent first. The dog walks a short distance ahead toward the advised exit, while the top panel names its scent and one next direction; it never reveals the entire path. If every clearing is found or a requested trail is unavailable, it says so and Q lets you change mode. The scent mode is saved in `maze-save.json`; older saves without it load with the dog off. The dog does not yet pathfind around furniture or animate its walk.

## Configuration and verification

Both apps accept `--config path/to/character.json` and `--model path/to/file.glb` for the villager. The editor accepts `--head`, `--body-gear`, `--gloves`, and `--feet` item IDs for gear previews, plus `--verify-gear` to check every mesh combination. `--dog` opens its dog viewer, with `--helmet`, `--armor`, `--backpack`, `--no-collar`, `--back-view`, `--verify-dog`, and `--dog-config path`. The client accepts `--dog-model path`, `--dog-config path`, `--dog-scent explore|home|shrine|rare|common`, `--terrain path/to/settlement.json`, `--homestead path/to/homestead.json`, `--interiors path/to/interiors.json`, `--overview`, `--maze-map`, `--verify-village`, `--verify-maps`, `--verify-maze`, `--verify-dog`, `--verify-inventory`, `--inspect`, `--seed number`, `--new-maze`, `--save path`, and `--screenshot path`. `--map village|homestead|interior:<building-id>|interior:residence|maze:<0-99>` starts on a particular map for inspection or screenshots; normal play resumes its saved map, or starts in the village when no save exists. Input and output paths are relative to the working directory.

```powershell
dotnet run --project src/CharacterEditor -- --hair bun --outfit rust --sword --backpack --save-config character.json --verify-swaps
dotnet run --project src/CharacterEditor -- --config character.json --screenshot character-editor-preview.png
dotnet run --project src/GameClient -- --verify-maps
dotnet run --project src/GameClient -- --map village --overview --screenshot map-village.png
dotnet run --project src/GameClient -- --map interior:inn --screenshot map-inn.png
dotnet run --project src/GameClient -- --map homestead --overview --screenshot map-homestead.png
dotnet run --project src/GameClient -- --map interior:residence --screenshot map-residence.png
dotnet run --project src/GameClient -- --verify-maps --verify-maze
dotnet run --project src/GameClient -- --map maze:0 --maze-map --screenshot maze-entrance.png
dotnet run --project src/GameClient -- --map maze:13 --screenshot maze-shrine.png
dotnet run --project src/GameClient -- --verify-dog
dotnet run --project src/CharacterEditor -- --dog --verify-dog
dotnet run --project src/CharacterEditor -- --dog --screenshot dog-default.png
dotnet run --project src/CharacterEditor -- --dog --helmet --armor --backpack --screenshot dog-armored.png
dotnet run --project src/GameClient -- --map maze:0 --dog-scent explore --maze-map --screenshot-after 90 --screenshot maze-dog-leading.png
dotnet run --project src/GameClient -- --verify-inventory --verify-maps --verify-maze
dotnet run --project src/GameClient -- --verify-resources
dotnet run --project src/GameClient -- --new-maze --map maze:0 --preview-harvest --screenshot resource-progress.png
dotnet run --project src/GameClient -- --new-maze --map maze:0 --preview-drops --screenshot resource-drops.png
dotnet run --project src/GameClient -- --new-maze --preview-fruit berry --screenshot resource-berry.png
dotnet run --project src/GameClient -- --new-maze --map interior:residence --preview-chest --screenshot inventory-chest.png
dotnet run --project src/GameClient -- --new-maze --map interior:residence --preview-equipped --preview-inventory --screenshot inventory-equipped.png
dotnet run --project src/GameClient -- --new-maze --map interior:residence --preview-iron-gear --preview-icon-items --preview-inventory --screenshot inventory-icons.png
dotnet run --project src/GameClient -- --new-maze --map maze:0 --preview-drops --screenshot resource-model-drops.png
dotnet run --project src/CharacterEditor -- --head padded_hood --body-gear padded_vest --gloves leather_gloves --feet trail_boots --screenshot gear-light.png
dotnet run --project src/CharacterEditor -- --head iron_helm --body-gear iron_cuirass --gloves iron_gauntlets --feet iron_boots --screenshot gear-iron.png
```

`--verify-swaps` checks all 16 character appearance configurations; `--verify-gear` checks all 81 equipment mesh combinations. `--verify-inventory` checks bag/chest transfer, equip and unequip, full-bag failures, seed pickups, farming inventory, save/reload, and older save migration. `--verify-resources` checks seed determinism, early basics, all drop types, hold durations, release reset, bag-full pickups, transitions, and disk reload. `--verify-dog` checks the exported dog parts, socket, guidance and save compatibility. `--verify-maps` checks all 113 maps and existing portals; `--verify-maze` checks deterministic generation, travel, one-time pickups and shrine travel. `--inspect` loads data without opening a window. `--screenshot-after N` waits N rendered frames before capturing. The modular villager GLB now has 113 mesh primitives and 7,800 triangles. Previews include [residence chest](inventory-chest.png), [equipped bag](inventory-equipped.png), [light gear](gear-light.png), [iron gear](gear-iron.png), and [iron gear in GameClient](game-iron-gear.png).

Village maps are authored in [settlement.json](assets/village/settlement.json), [homestead.json](assets/village/homestead.json), and [interiors.json](assets/village/interiors.json). Terrain and simple room furniture meshes are generated from those definitions. [Map editing guide](assets/village/README.md) explains coordinates, room templates, portals and farm plots. The wooded maze is generated in code from the saved seed and connected to the dungeon-road endpoint in settlement data. Village building exteriors and interiors remain labeled blockouts. There is no maze combat yet. Character art remains static and unrigged.

For part IDs, editable sources, Blender regeneration, attachment nodes and rigging limits, see [assets/villager/MODULAR.md](assets/villager/MODULAR.md). Both applications use static, opaque meshes and flat colors. The asset has no skeleton, skin weights, or animation yet. The GLB importer supports the static subset described in the module documentation; it does not handle textured materials, skins, morph targets, or required extensions.
