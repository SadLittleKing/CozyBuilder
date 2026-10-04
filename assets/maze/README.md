# Maze rock asset

Edit `rock.json` to change the mineable rock's dimensions or flat colors. Run `python assets/maze/generate_rock.py` from the project root to export `rock.obj` and `rock.mtl` for Blender or another low-poly editor. The game's `MazeResources.Rock` draws the same box and four-face peak directly as vertex-color geometry, following the existing procedural terrain workflow. When changing the JSON dimensions, update that renderer to match before rebuilding GameClient. Ore rocks reuse the shape with a darker tint and a copper-colored seam.
