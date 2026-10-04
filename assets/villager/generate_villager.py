"""Generate the villager with Blender 3.3+; no downloaded dependencies.

Run: blender --background --python generate_villager.py
Front is -Y in Blender. Feet are on Z=0. Units are metres.
"""
import bpy
import math
import json
import os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PALETTE = {
    'Skin': '#D8A078', 'Hair': '#573B30', 'HairHighlight': '#77513C',
    'Tunic': '#5C8072', 'TunicLight': '#789B85', 'TunicDark': '#436255',
    'Undershirt': '#EAD8B4', 'Trousers': '#57616A',
    'Leather': '#79523B', 'Sole': '#453B36', 'Brass': '#CDA765',
    'Eyes': '#302F2E', 'Cheek': '#C5826B',
}

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablock in list(bpy.data.collections):
    if datablock.name != 'Collection':
        bpy.data.collections.remove(datablock)
root = bpy.data.collections.get('Collection')
root.name = 'VILLAGER_ASSET'
groups = {}
for name in ['Body', 'Face', 'Hair', 'Outfit', 'Footwear']:
    collection = bpy.data.collections.new(name)
    root.children.link(collection)
    groups[name] = collection
stage = bpy.data.collections.new('PREVIEW_ONLY')
bpy.context.scene.collection.children.link(stage)

def linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4

def material(name, hex_color):
    mat = bpy.data.materials.new(name)
    rgb = tuple(linear(int(hex_color[i:i+2], 16) / 255) for i in (1, 3, 5))
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = .88
    bsdf.inputs['Specular'].default_value = .18
    return mat

mats = {key: material(key, value) for key, value in PALETTE.items()}

def assign(obj, name, mat, group):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    groups[group].objects.link(obj)
    obj.data.materials.append(mats[mat])
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return obj

def bevel_box(name, location, dimensions, mat, group, bevel=.03, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(obj, name, mat, group)
    mod = obj.modifiers.new('Soft corners', 'BEVEL')
    mod.width = bevel
    mod.segments = segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Keep large planes flat, small bevel facets legible.
    return obj

def ellipsoid(name, location, scale, mat, group, segments=12, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, name, mat, group)

def loft(name, rings, mat, group, sides=12):
    """Closed quad-ring mesh. Each ring: centre xyz and ellipse radii xy."""
    verts = []
    for x, y, z, rx, ry in rings:
        for i in range(sides):
            angle = 2 * math.pi * i / sides + math.pi / sides
            verts.append((x + rx * math.cos(angle), y + ry * math.sin(angle), z))
    faces = [tuple(reversed(range(sides)))]
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((r*sides+i, r*sides+j, (r+1)*sides+j, (r+1)*sides+i))
    faces.append(tuple((len(rings)-1)*sides+i for i in range(sides)))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    groups[group].objects.link(obj)
    obj.data.materials.append(mats[mat])
    return obj

def limb(name, start, end, r1, r2, mat, group):
    # Three loops provide a middle loop for later mesh/rig edits.
    start, end = Vector(start), Vector(end)
    length = (end-start).length
    obj = loft(name, [(0, 0, -length/2, r1, r1*.88),
                            (0, 0, 0, (r1+r2)/2, (r1+r2)*.44),
                            (0, 0, length/2, r2, r2*.88)], mat, group, 10)
    obj.location = (start+end)/2
    obj.rotation_euler = (end-start).to_track_quat('Z', 'Y').to_euler()
    return obj

# Body proportions: oversized head, short legs, a broad readable upper body.
loft('Body_Torso', [(0,0,.73,.17,.105), (0,0,1.04,.21,.12),
                    (0,0,1.23,.23,.11), (0,0,1.29,.13,.095)], 'Skin', 'Body')
loft('Body_Neck', [(0,0,1.23,.085,.075), (0,0,1.43,.087,.08)], 'Skin', 'Body')
head = bevel_box('Body_Head', (0,-.008,1.558), (.50,.425,.50), 'Skin', 'Body', .135, 3)
for sign, side in [(-1, 'L'), (1, 'R')]:
    ellipsoid('Body_Ear_'+side, (sign*.251,0,1.54), (.057,.047,.078), 'Skin', 'Body')
    # Simple solid mitten hands, with a separate thumb volume.
    limb('Body_UpperArm_'+side, (sign*.23,0,1.22), (sign*.397,0,1.045), .078,.066,'Skin','Body')
    limb('Body_Forearm_'+side, (sign*.39,0,1.05), (sign*.507,-.009,.868), .069,.053,'Skin','Body')
    hand = bevel_box('Body_Hand_'+side, (sign*.535,-.013,.811), (.118,.106,.154), 'Skin','Body',.038,2)
    hand.rotation_euler[1] = sign*.35
    ellipsoid('Body_Thumb_'+side, (sign*.486,-.048,.828), (.034,.038,.060), 'Skin','Body',10,6)

# Face features are solid meshes, so no texture setup is necessary in an engine.
for sign, side in [(-1,'L'), (1,'R')]:
    ellipsoid('Face_Eye_'+side, (sign*.091,-.223,1.578), (.022,.010,.034),'Eyes','Face')
    brow = bevel_box('Face_Brow_'+side,(sign*.09,-.222,1.64),(.054,.012,.013),'Hair','Face',.005,1)
    brow.rotation_euler[1] = sign*.05
    ellipsoid('Face_Cheek_'+side,(sign*.155,-.219,1.507),(.035,.006,.016),'Cheek','Face')
bevel_box('Face_Nose',(0,-.238,1.526),(.058,.048,.054),'Skin','Face',.018,2)
bevel_box('Face_Mouth',(0,-.223,1.457),(.044,.012,.009),'Hair','Face',.004,2)

# Hair cap: a closed faceted dome with the hairline higher at the front.
sides = 16
vertices = []
for r in range(4):
    for i in range(sides):
        a = 2*math.pi*i/sides
        front = max(0,-math.sin(a))
        if r == 0:
            rx,ry,z = .263,.237,1.575+.120*front
        elif r == 1:
            rx,ry,z = .268,.255,1.760+.007*math.cos(a)
        elif r == 2:
            rx,ry,z = .194,.185,1.825+.012*math.cos(a)
        else:
            rx,ry,z = .073,.075,1.853
        # A rounded-square lower ring follows the rounded-square head without
        # letting the temple corners poke through an elliptical hair cap.
        exponent = .50 if r <= 1 else 1.0
        cx = math.copysign(abs(math.cos(a))**exponent, math.cos(a))
        sy = math.copysign(abs(math.sin(a))**exponent, math.sin(a))
        vertices.append((rx*cx, ry*sy+.016, z))
faces = [tuple(reversed(range(sides)))]
for r in range(3):
    for i in range(sides):
        j=(i+1)%sides
        faces.append((r*sides+i,r*sides+j,(r+1)*sides+j,(r+1)*sides+i))
faces.append(tuple(3*sides+i for i in range(sides)))
mesh=bpy.data.meshes.new('Hair_Cap_Mesh')
mesh.from_pydata(vertices,[],faces)
mesh.update()
obj=bpy.data.objects.new('Hair_Cap',mesh)
groups['Hair'].objects.link(obj)
mesh.materials.append(mats['Hair'])
# Broad swept locks instead of fine strands: readable from the game camera.
for name, loc, dims, angle, mat in [
    ('Hair_Fringe_Centre',(-.041,-.220,1.722),(.265,.075,.119),-.23,'HairHighlight'),
    ('Hair_Fringe_Left',(-.175,-.193,1.676),(.090,.093,.181),-.25,'Hair'),
    ('Hair_Fringe_Right',(.155,-.209,1.708),(.106,.075,.114),-.30,'Hair'),
]:
    obj=bevel_box(name,loc,dims,mat,'Hair',.025,2)
    obj.rotation_euler[1]=angle

# Replaceable outfit pieces, assembled over a simple torso.
loft('Outfit_Tunic',[(0,0,.689,.255,.151),(0,0,.74,.253,.149),
                    (0,0,.926,.203,.13),(0,0,1.085,.231,.146),
                    (0,0,1.237,.264,.14),(0,0,1.286,.155,.105)],'Tunic','Outfit')
loft('Outfit_Hem',[(0,0,.685,.257,.153),(0,0,.73,.257,.153)],'TunicDark','Outfit')
loft('Outfit_Collar',[(0,0,1.275,.105,.095),(0,0,1.319,.107,.094)],'Undershirt','Outfit')
# Short folded collar tabs at the front.
for sign, side in [(-1,'L'),(1,'R')]:
    tab=bevel_box('Outfit_CollarTab_'+side,(sign*.062,-.113,1.263),(.098,.027,.091),'Undershirt','Outfit',.012,1)
    tab.rotation_euler[1]=sign*.40
    limb('Outfit_Sleeve_'+side,(sign*.227,0,1.225),(sign*.351,0,1.094),.105,.090,'Tunic','Outfit')
    limb('Outfit_SleeveCuff_'+side,(sign*.337,0,1.107),(sign*.368,0,1.073),.093,.088,'TunicLight','Outfit')
    # Trouser legs are intentionally separate, ready to edit into a skinned base.
    loft('Outfit_Trouser_'+side,[(sign*.124,0,.239,.089,.088),
         (sign*.124,0,.455,.103,.101),(sign*.118,0,.717,.111,.112),
         (sign*.105,0,.80,.115,.11)],'Trousers','Outfit',10)
    bevel_box('Footwear_Boot_'+side,(sign*.125,-.046,.114),(.209,.329,.206),'Leather','Footwear',.054,2)
    bevel_box('Footwear_Sole_'+side,(sign*.125,-.052,.036),(.219,.338,.072),'Sole','Footwear',.026,2)
    loft('Footwear_Cuff_'+side,[(sign*.125,0,.184,.108,.102),(sign*.125,0,.287,.104,.100)],'Leather','Footwear',10)
loft('Outfit_Belt',[(0,0,.886,.215,.143),(0,0,.944,.211,.142)],'Leather','Outfit')
bevel_box('Outfit_Buckle',(0,-.147,.916),(.081,.031,.074),'Brass','Outfit',.01,1)
bevel_box('Outfit_BuckleInset',(0,-.165,.916),(.040,.010,.037),'Leather','Outfit',.004,1)
bevel_box('Outfit_Pouch',(.199,-.119,.833),(.115,.101,.139),'Leather','Outfit',.022,2)
bevel_box('Outfit_PouchFlap',(.199,-.171,.865),(.12,.018,.049),'Leather','Outfit',.009,1)
ellipsoid('Outfit_PouchStud',(.199,-.184,.853),(.011,.005,.011),'Brass','Outfit',8,4)

# Group the export with an identity root at ground level.
anchor=bpy.data.objects.new('Villager_Root',None)
root.objects.link(anchor)
anchor.empty_display_type='PLAIN_AXES'
anchor.empty_display_size=.25
asset_objects=[]
for group in groups.values():
    for obj in group.objects:
        obj.parent=anchor
        obj['part_group']=group.name
        asset_objects.append(obj)
anchor['asset_notes']='Unrigged modular prototype. Front -Y; ground Z=0; metre units.'

scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
bpy.ops.object.select_all(action='DESELECT')
for obj in asset_objects+[anchor]:
    obj.select_set(True)
bpy.context.view_layer.objects.active=anchor
bpy.ops.export_scene.gltf(filepath=os.path.join(HERE,'villager_base.glb'),export_format='GLB',
    use_selection=True,export_yup=True,export_texcoords=False,export_normals=True,
    export_materials='EXPORT',export_cameras=False,export_lights=False,
    export_animations=False,export_extras=True)

# Preview stage is deliberately excluded from the portable GLB.
def move_to_stage(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    stage.objects.link(obj)

floor_mat=material('Preview_Ground','#DCDDD0')
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object
floor.name='Preview_Ground'
floor.data.materials.append(floor_mat)
move_to_stage(floor)
world=scene.world or bpy.data.worlds.new('World')
scene.world=world
world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.60,.56,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.8

def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

for name,location,energy,size in [('Key',(-3,-4,7),450,4),('Fill',(4,-1,4),180,5),('Rim',(1,4,6),300,3)]:
    data=bpy.data.lights.new(name,'AREA')
    data.energy=energy
    data.size=size
    obj=bpy.data.objects.new(name,data)
    stage.objects.link(obj)
    obj.location=location
    aim(obj,(0,0,1))

camera_data=bpy.data.cameras.new('Preview_Camera')
camera=bpy.data.objects.new('Preview_Camera',camera_data)
stage.objects.link(camera)
scene.camera=camera
camera_data.type='ORTHO'
camera_data.ortho_scale=2.60
scene.render.engine='BLENDER_EEVEE'
scene.eevee.use_gtao=True
scene.eevee.gtao_distance=3
scene.eevee.gtao_factor=1.13
scene.eevee.use_soft_shadows=True
scene.eevee.taa_render_samples=128
scene.render.resolution_x=1200
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='Standard'
scene.view_settings.look='Medium High Contrast'
scene.view_settings.exposure=0
scene.view_settings.gamma=1
scene.camera.data.lens=50

views=[('villager_preview.png',(3,-5,4.0),(0,0,.94)),
       ('villager_back.png',(-3,5,3.5),(0,0,.94)),
       ('villager_front.png',(0,-6,2.5),(0,0,.94))]
for filename,location,target in views:
    camera.location=location
    aim(camera,target)
    scene.render.filepath=os.path.join(HERE,filename)
    bpy.ops.render.render(write_still=True)

camera.location=views[0][1]
aim(camera,views[0][2])
scene.render.filepath=os.path.join(HERE,views[0][0])
# Make the source open directly on the intended camera and material colors.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
head.select_set(True)
bpy.context.view_layer.objects.active=head
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE,'villager_base.blend'))

mins=Vector((1e6,1e6,1e6)); maxs=-mins
triangles=0
for obj in asset_objects:
    obj.data.calc_loop_triangles()
    triangles+=len(obj.data.loop_triangles)
    for corner in obj.bound_box:
        point=obj.matrix_world @ Vector(corner)
        for i in range(3):
            mins[i]=min(mins[i],point[i]); maxs[i]=max(maxs[i],point[i])
report={'mesh_objects':len(asset_objects),'triangles':triangles,
        'dimensions_metres':list(maxs-mins),'bounds_blender':{'min':list(mins),'max':list(maxs)},
        'materials':len(mats),'rigged':False,'animations':0,
        'coordinates':'Blender Z up / front -Y; glTF Y up / front +Z',
        'palette':PALETTE}
with open(os.path.join(HERE,'asset_info.json'),'w') as f:
    json.dump(report,f,indent=2)
print('VILLAGER_BUILD_COMPLETE '+json.dumps(report))
