"""Check the portable villager using only Python's standard library."""
import json
import math
import struct
import sys
from pathlib import Path

path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).with_name('villager_base.glb')
raw = path.read_bytes()
magic, version, length = struct.unpack_from('<4sII', raw)
assert magic == b'glTF' and version == 2 and length == len(raw)
chunks = {}
offset = 12
while offset < len(raw):
    size, kind = struct.unpack_from('<I4s', raw, offset)
    assert size % 4 == 0
    offset += 8
    chunks[kind] = raw[offset:offset+size]
    offset += size
assert offset == length
doc = json.loads(chunks[b'JSON'])
blob = chunks[b'BIN\x00']
assert doc['asset']['version'] == '2.0'
assert len(doc['buffers']) == 1 and 'uri' not in doc['buffers'][0]
assert not doc.get('images') and not doc.get('textures')
assert not doc.get('skins') and not doc.get('animations')
assert not doc.get('cameras')
assert not doc.get('extensionsRequired')

def read_accessor(index):
    acc = doc['accessors'][index]
    view = doc['bufferViews'][acc['bufferView']]
    components = {'SCALAR':1, 'VEC2':2, 'VEC3':3, 'VEC4':4}[acc['type']]
    code = {5121:'B',5123:'H',5125:'I',5126:'f'}[acc['componentType']]
    fmt = '<' + code * components
    width = struct.calcsize(fmt)
    stride = view.get('byteStride', width)
    local = acc.get('byteOffset', 0)
    assert local + (acc['count']-1)*stride + width <= view['byteLength']
    start = view.get('byteOffset', 0) + local
    assert start + (acc['count']-1)*stride + width <= len(blob)
    return [struct.unpack_from(fmt, blob, start+i*stride) for i in range(acc['count'])]

triangles = vertices = 0
for mesh in doc['meshes']:
    for prim in mesh['primitives']:
        assert prim.get('mode',4) == 4
        positions = read_accessor(prim['attributes']['POSITION'])
        normals = read_accessor(prim['attributes']['NORMAL'])
        indices = [v[0] for v in read_accessor(prim['indices'])]
        assert positions and len(normals) == len(positions)
        assert len(indices) % 3 == 0
        assert all(0 <= i < len(positions) for i in indices)
        assert all(math.isfinite(x) for p in positions+normals for x in p)
        assert all(abs(sum(x*x for x in n)-1) < .002 for n in normals)
        assert all(len(set(indices[i:i+3]))==3 for i in range(0,len(indices),3))
        assert 0 <= prim['material'] < len(doc['materials'])
        triangles += len(indices)//3
        vertices += len(positions)

for node in doc['nodes']:
    assert not node.get('name','').startswith('Preview_')
    for child in node.get('children',[]):
        assert 0 <= child < len(doc['nodes'])
    for key in ('translation','rotation','scale','matrix'):
        assert all(math.isfinite(v) for v in node.get(key,[]))
for mat in doc['materials']:
    pbr=mat['pbrMetallicRoughness']
    assert len(pbr['baseColorFactor']) == 4
    assert all(0 <= c <= 1 for c in pbr['baseColorFactor'])

if path.name == 'villager_base.glb':
    info=json.loads(path.with_name('asset_info.json').read_text())
    assert triangles == info['triangles']
    assert len(doc['meshes']) == info['mesh_objects']
if path.name == 'villager_modular.glb':
    info=json.loads(path.with_name('modular_info.json').read_text())
    named={n['name']:n for n in doc['nodes']}
    assert all(part in named for part in info['parts'])
    assert len(info['parts']) == len(doc['meshes'])
    for socket,prefix in [('Socket_Hand_R','Equipment_Sword__'),('Socket_Back','Equipment_Backpack__')]:
        assert socket in named
        children=[doc['nodes'][i]['name'] for i in named[socket]['children']]
        assert children and all(name.startswith(prefix) for name in children)
    assert sum(n.get('name') == 'Shared__Body_Head' for n in doc['nodes']) == 1
report={'status':'PASS','file':str(path.resolve()),'bytes':len(raw),
        'meshes':len(doc['meshes']),'triangles':triangles,'exported_vertices':vertices,
        'materials':len(doc['materials']),
        'checks':['GLB container and buffer/accessor bounds','finite positions and unit normals',
                  'triangle indices and materials','no external dependencies',
                  'no preview stage, cameras, skinning or animations','matches source metadata']}
path.with_name('modular_validation_report.json' if path.name == 'villager_modular.glb' else 'validation_report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
