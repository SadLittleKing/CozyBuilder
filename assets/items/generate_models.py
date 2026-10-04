"""Export one reusable GLB model for each GameClient item.

Run from the workspace root:
  blender --background assets/villager/villager_modular.blend --python-exit-code 1 --python assets/items/generate_models.py
Gear comes from the actual modular villager source meshes. Resource props are built
here once and exported as models for both world drops and runtime icon rendering.
"""
import bpy
import json
import math
import os

HERE=os.path.dirname(os.path.abspath(__file__))
GEAR={
    'padded_hood':'Gear_Head_Hood__','iron_helm':'Gear_Head_Helm__',
    'padded_vest':'Gear_Body_Vest__','iron_cuirass':'Gear_Body_Cuirass__',
    'leather_gloves':'Gear_Gloves_Leather__','iron_gauntlets':'Gear_Gloves_Iron__',
    'trail_boots':'Gear_Feet_Trail__','iron_boots':'Gear_Feet_Iron__',
}
RESOURCES=['common_seed','rare_seed','crop','wood','stone','ore','apple','berry','plum']

scene=bpy.context.scene
for obj in bpy.data.objects:
    if obj.type=='MESH':obj.hide_render=True

def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def material(name,hexcode):
    mat=bpy.data.materials.get('Icon_'+name)
    if mat:return mat
    mat=bpy.data.materials.new('Icon_'+name);mat.use_nodes=True
    rgb=tuple(linear(int(hexcode[i:i+2],16)/255) for i in (1,3,5))
    mat.diffuse_color=(*rgb,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*rgb,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8
    return mat

mats={
    name:material(name,color) for name,color in {
        'tan':'#C5A46D','gold':'#EDC35B','rare':'#C6B1DC','leaf':'#6B9A62',
        'crop':'#BC6F65','wood':'#9A6640','ring':'#D0A875','stone':'#9BA49F',
        'stone_dark':'#65736F','ore':'#D9974A','apple':'#D95A4E',
        'berry':'#5269CA','plum':'#A45DAD','stem':'#755138'
    }.items()
}

def finish(obj,mat):
    obj.name='Icon_'+obj.name
    obj.data.materials.clear();obj.data.materials.append(mats[mat])
    obj.hide_render=False
    return obj
def sphere(at,scale,mat,segments=12,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=at)
    obj=bpy.context.object;obj.scale=scale
    return finish(obj,mat)
def ico(at,scale,mat,subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=at)
    obj=bpy.context.object;obj.scale=scale
    return finish(obj,mat)
def box(at,scale,mat):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at)
    obj=bpy.context.object;obj.scale=scale
    return finish(obj,mat)
def cylinder(at,radius,depth,mat,rotation=(0,0,0),vertices=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=at,rotation=rotation)
    return finish(bpy.context.object,mat)
def cone(at,radius1,radius2,depth,mat,vertices=8):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius1,radius2=radius2,depth=depth,location=at)
    return finish(bpy.context.object,mat)

def make_prop(item):
    before=set(bpy.data.objects)
    if item=='common_seed':
        for x,y in [(-.20,-.06),(0,.10),(.21,-.05)]:
            seed=sphere((x,y,.11),(.16,.11,.10),'tan',10,6)
            seed.rotation_euler[2]=x*1.7
        sphere((0,.03,.025),(.36,.23,.035),'leaf')
    elif item=='rare_seed':
        ico((0,0,.24),(.25,.25,.34),'gold')
        ico((0,0,.04),(.38,.33,.08),'rare')
        for x in [-.27,.27]:ico((x,.07,.12),(.09,.08,.15),'rare')
    elif item=='crop':
        sphere((0,0,.18),(.29,.28,.24),'crop')
        for x,y in [(-.13,0),(.12,.04),(0,-.1)]:
            leaf=sphere((x,y,.49),(.09,.20,.055),'leaf');leaf.rotation_euler[1]=x*2
    elif item=='wood':
        for x,z in [(-.16,.14),(.16,.25)]:
            cylinder((x,0,z),.14,.63,'wood',(math.pi/2,0,0),10)
            cylinder((x,-.32,z),.12,.025,'ring',(math.pi/2,0,0),10)
    elif item in {'stone','ore'}:
        rock=ico((0,0,.25),(.39,.34,.28),'stone_dark' if item=='ore' else 'stone',1)
        rock.rotation_euler[2]=.16
        if item=='ore':
            for x,y,z in [(-.17,-.22,.35),(.14,-.19,.28),(.03,.16,.46)]:
                shard=ico((x,y,z),(.10,.09,.14),'ore',1);shard.rotation_euler[1]=x
    elif item=='apple':
        sphere((0,0,.22),(.28,.28,.27),'apple')
        cylinder((0,0,.51),.025,.17,'stem')
        leaf=sphere((.13,0,.56),(.13,.06,.04),'leaf');leaf.rotation_euler[1]=-.25
    elif item=='berry':
        for x,y,z in [(-.15,-.08,.24),(.16,-.08,.25),(0,.13,.20),(.02,-.03,.39)]:
            sphere((x,y,z),(.16,.15,.15),'berry',10,6)
        leaf=sphere((0,.09,.54),(.22,.11,.035),'leaf');leaf.rotation_euler[1]=.22
    elif item=='plum':
        sphere((0,0,.25),(.25,.23,.34),'plum')
        cylinder((0,0,.58),.02,.12,'stem')
        leaf=sphere((.14,.02,.62),(.14,.07,.04),'leaf');leaf.rotation_euler[1]=-.3
    return [obj for obj in bpy.data.objects if obj not in before]

def export(item,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.hide_render=False
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.gltf(filepath=os.path.join(HERE,item+'.glb'),export_format='GLB',use_selection=True,
                              export_materials='EXPORT')
    print('ITEM_EXPORTED',item,len(objects))
    for obj in objects:obj.hide_render=True

for item,prefix in GEAR.items():
    objects=[obj for obj in bpy.data.objects if obj.type=='MESH' and obj.name.startswith(prefix)]
    if item.endswith('gloves') or item=='iron_gauntlets':
        objects=[obj for obj in objects if obj.name.endswith('_R')]
    if not objects:raise RuntimeError('Missing gear source '+prefix)
    export(item,objects)

for item in RESOURCES:
    objects=make_prop(item)
    export(item,objects)
    for obj in objects:bpy.data.objects.remove(obj,do_unlink=True)

with open(os.path.join(HERE,'manifest.json'),'w',encoding='utf8') as output:
    json.dump({'format':'glTF 2.0 GLB','source':'villager_modular.blend and procedural low-poly resource props',
               'item_ids':list(GEAR)+RESOURCES},output,indent=2)
