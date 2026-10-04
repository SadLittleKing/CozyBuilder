# Willowcross village terrain blockout

`settlement.json` is the village source layout. GameClient generates flat terrain, paths, plaza stones, pond, crop beds, low poly trees, labeled building blocks, and the south woodland-maze gate from it. `homestead.json` defines the separate residence grounds and farm plot positions. `interiors.json` defines reusable roofless room templates and furniture footprints. There are no hand-authored terrain or room meshes. These JSON files are copied beside GameClient's executable. Press **R** in GameClient to validate and reload all three; a failed reload keeps the previous maps.

Coordinates use **X east/west**, **Z north/south**, and metres. Negative Z is north on the overview; positive Z is toward the dungeon road. The 48 × 44 metre village is centred on `(0,0)`. Each generated ground tile is one square metre. `spawn` is the walking start. A route is a polyline of `[x,z]` points with a width in metres. The renderer paints tiles touched by routes as earth paths. `plaza`, `farm`, and `pond` regions are rectangles; the pond is drawn and blocked as an ellipse inside its rectangle. Buildings have a centre, width, depth, height, zone color, short label, and an `approach` point on a connecting path. The appearance of the crop rows and trees is procedural; `seed` changes the tree placement deterministically.

The village loop has a gathering square and well in the centre, hall to the north, shops and work buildings on the upper street, homes on the lower lanes, a pond to the southwest, farm beds and barn to the southeast, and a gate at the end of the south road. The hall, inn, shop, store, forge, four homes, and barn are colored **building footprint blocks**. Their labels are drawn above them by GameClient. Layout edits do not require Blender.

The `dungeon road` route's final point is the woodland-maze portal. Keep that point walkable and inside the village bounds. The maze itself is not JSON-authored: `WoodedMaze.cs` generates a 10 × 10 connected graph from the saved seed, then `WorldGraph` creates its cardinal portals. The biome method currently returns woodland for every node. The maze save records the seed and exploration state separately from these layout files.

Each village building has an `interior` template ID and an `approach` point on the path outside its footprint. GameClient creates a separate map instance for every building, even when multiple homes reuse the same `home` template. At an exterior approach, **E** enters at `(0,1.8)` inside its room. The exit is the south threshold at `(0,3.7)`; pressing E there returns to that exact building's exterior approach. The west village road endpoint at `homestead.villageApproach` leads to the homestead's `spawn`. Its `villageGate` returns to the village, and the homestead house's `approach` leads to the `residence` template. Portals are generated from these data fields and checked in both directions.

Interior templates specify a `name`, `floor` palette, room `width`/`depth`, and furniture boxes with labels and dimensions. The template IDs used by the ten buildings plus `residence` must exist. Furniture labeled `CHEST` is interactable when the player stands within about 2.1 metres and presses C. The residence and four home maps have independent, save-persistent chest contents even though the homes share one template. Furniture, walls and outer terrain bounds block movement. Rooms share a roofless cutaway presentation; these are role-specific blockouts, not finished architecture or NPC spaces.

The homestead's `field` rectangle contains eight editable `plots`. Stand within about 1.35 metres of a plot and press **F** to till, plant, water, then harvest. Planting spends one common seed from the bag. Harvest returns a common seed plus one crop, increments the count, and leaves tilled soil. If the bag lacks space for both items, harvesting waits without losing the crop. Plot stages, count, and items persist in `maze-save.json` across transitions, reload, and app restarts. There is no crop timer, watering resource, season or sale system.

To inspect it from the workspace root:

```powershell
dotnet run --project src/GameClient -- --verify-maps
dotnet run --project src/GameClient -- --map village --overview --screenshot map-village.png
dotnet run --project src/GameClient -- --map interior:inn --screenshot map-inn.png
dotnet run --project src/GameClient -- --map homestead --overview --screenshot map-homestead.png
dotnet run --project src/GameClient -- --map interior:residence --screenshot map-residence.png
dotnet run --project src/GameClient -- --verify-maze
dotnet run --project src/GameClient -- --verify-inventory
dotnet run --project src/GameClient -- --map maze:0 --maze-map --screenshot maze-entrance.png
dotnet run --project src/GameClient
```

`--verify-maps` checks village and homestead path connectivity, every building interior and residence round trip, exact return coordinates, collision, all generated meshes, and the farm cycle surviving map transitions. Keep approach points outside footprints and on paths. If a building moves, update its approach and nearby route. The visual tree clusters and village farm rows do not block travel in this blockout; buildings, furniture, water, well and map edges do.

The default follow camera prioritizes walking near the square. **M** toggles an overview of the current exterior map; WASD or arrows still move the character there. **Tab** toggles the maze automap. Character art remains static, and there are no finished buildings or combat in this prototype.
