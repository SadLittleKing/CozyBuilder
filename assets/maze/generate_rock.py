"""Export the editable low-poly rock description to a portable OBJ/MTL pair."""
import json
from pathlib import Path

root = Path(__file__).parent
data = json.loads((root / "rock.json").read_text())
width, depth = data["footprint"]
height = data["base_height"]
peak = data["peak_height"]
x, z = width / 2, depth / 2
vertices = [(-x, 0, -z), (x, 0, -z), (x, 0, z), (-x, 0, z),
            (-x, height, -z), (x, height, -z), (x, height, z), (-x, height, z),
            (0, peak, 0)]
faces = [(1, 2, 3, 4), (1, 2, 6, 5), (2, 3, 7, 6),
         (3, 4, 8, 7), (4, 1, 5, 8),
         (5, 6, 9), (6, 7, 9), (7, 8, 9), (8, 5, 9)]
lines = ["# Exported from rock.json; metres, Y up", "mtllib rock.mtl", "o woodland_rock"]
lines += [f"v {vx:.4f} {vy:.4f} {vz:.4f}" for vx, vy, vz in vertices]
for index, face in enumerate(faces):
    lines += ["usemtl " + ("top" if index >= 5 else "side"),
              "f " + " ".join(map(str, face))]
(root / "rock.obj").write_text("\n".join(lines) + "\n")
materials = []
for name, key in (("top", "base_rgb"), ("side", "side_rgb")):
    r, g, b = (v / 255 for v in data[key])
    materials += [f"newmtl {name}", f"Kd {r:.5f} {g:.5f} {b:.5f}", ""]
(root / "rock.mtl").write_text("\n".join(materials))
print(root / "rock.obj")
