"""Reproducible modular prototype, preserving the original villager files.
Run with Blender 3.3: blender --background --python-exit-code 1 --python this_file
Uses the original procedural geometry helpers; no external assets.
"""
import os
import json
import math
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
source = open(os.path.join(HERE, 'generate_villager.py'), encoding='utf-8').read()
# Build only the geometry; the original exports and preview renders remain intact.
exec(compile(source.split('# Group the export')[0], 'generate_villager.py', 'exec'))
original = [o for g in groups.values() for o in g.objects]

def copy_part(obj, name, group, replacement=None):
    copy = obj.copy()
    copy.data = obj.data.copy()
    copy.name = name
    groups[group].objects.link(copy)
    if replacement:
        copy.data.materials.clear()
        copy.data.materials.append(mats[replacement])
    return copy

for name, color in {'Rust':'#AD664C', 'RustLight':'#D9A875', 'RustDark':'#804937',
                    'Pack':'#AB885A', 'Steel':'#BCC9CA', 'Grip':'#493D37',
                    'Padded':'#887B9A','PaddedLight':'#B6A8BE','IronDark':'#71838D',
                    'Trail':'#A2774C','TrailLight':'#C69A62'}.items():
    mats[name] = material(name, color)
for group in ['Hair_Bun', 'Outfit_Rust', 'Equipment_Sword', 'Equipment_Backpack',
              'Gear_Head_Hood','Gear_Head_Helm','Gear_Body_Vest','Gear_Body_Cuirass',
              'Gear_Gloves_Leather','Gear_Gloves_Iron','Gear_Feet_Trail','Gear_Feet_Iron']:
    collection = bpy.data.collections.new(group)
    root.children.link(collection)
    groups[group] = collection

# One shared head/body/face/boot set. Hair and outfit are selectable modules.
for obj in original:
    old = obj.name
    if old.startswith('Hair_'):
        obj.name = 'Hair_Swept__' + old[5:]
    elif old.startswith('Outfit_'):
        obj.name = 'Outfit_Sage__' + old[7:]
    else:
        obj.name = 'Shared__' + old

cap = next(o for o in original if o.name == 'Hair_Swept__Cap')
copy_part(cap, 'Hair_Bun__Cap', 'Hair_Bun')
ellipsoid('Hair_Bun__Bun', (0,.245,1.745), (.156,.145,.153), 'Hair', 'Hair_Bun')
ellipsoid('Hair_Bun__Tie', (0,.187,1.746), (.162,.035,.141), 'Leather', 'Hair_Bun')
for sign in [-1,1]:
    lock = bevel_box('Hair_Bun__Temple_'+str(sign),(sign*.227,-.10,1.65),(.057,.10,.15),'Hair','Hair_Bun',.023,2)
    lock.rotation_euler[1] = sign*.13

# Copy only clothing, never a second character. Long sleeves hide underlying arms.
recolors = {'Tunic':'Rust','TunicLight':'RustLight','TunicDark':'RustDark'}
for obj in original:
    if not obj.name.startswith('Outfit_Sage__') or 'Sleeve' in obj.name:
        continue
    old_mat = obj.data.materials[0].name
    copy_part(obj, obj.name.replace('Outfit_Sage__','Outfit_Rust__'), 'Outfit_Rust', recolors.get(old_mat))
for sign, side in [(-1,'L'),(1,'R')]:
    limb('Outfit_Rust__SleeveUpper_'+side,(sign*.227,0,1.225),(sign*.397,0,1.045),.108,.085,'Rust','Outfit_Rust')
    limb('Outfit_Rust__SleeveLower_'+side,(sign*.39,0,1.05),(sign*.510,-.009,.861),.089,.067,'Rust','Outfit_Rust')
    limb('Outfit_Rust__SleeveCuff_'+side,(sign*.481,-.007,.909),(sign*.515,-.01,.855),.074,.070,'RustLight','Outfit_Rust')

# Local rigid equipment: sword grip centered on local origin, blade along +Z.
bevel_box('Equipment_Sword__Grip',(0,0,0),(.05,.05,.16),'Grip','Equipment_Sword',.01,1)
bevel_box('Equipment_Sword__Pommel',(0,0,-.095),(.075,.065,.045),'Brass','Equipment_Sword',.012,1)
bevel_box('Equipment_Sword__Guard',(0,0,.095),(.22,.065,.048),'Brass','Equipment_Sword',.012,1)
loft('Equipment_Sword__Blade',[(0,0,.12,.057,.023),(0,0,.52,.044,.018),(0,0,.65,.001,.001)],'Steel','Equipment_Sword',4)

# Backpack local origin is the centre of its body-facing panel; +Y points away.
bevel_box('Equipment_Backpack__Bag',(0,.107,0),(.32,.19,.34),'Pack','Equipment_Backpack',.055,2)
bevel_box('Equipment_Backpack__Flap',(0,.19,.09),(.34,.047,.17),'Leather','Equipment_Backpack',.025,2)
bevel_box('Equipment_Backpack__Clasp',(0,.222,.055),(.05,.02,.065),'Brass','Equipment_Backpack',.008,1)
for sign, side in [(-1,'L'),(1,'R')]:
    bevel_box('Equipment_Backpack__Strap_'+side,(sign*.11,-.032,.035),(.041,.06,.30),'Leather','Equipment_Backpack',.012,1)
roll = limb('Equipment_Backpack__Bedroll',(-.21,.09,.225),(.21,.09,.225),.075,.075,'TunicLight','Equipment_Backpack')

# Four independent inventory equipment slots. Every slot has two visible meshes.
ellipsoid('Gear_Head_Hood__Cap',(0,-.003,1.758),(.286,.252,.185),'Padded','Gear_Head_Hood')
bevel_box('Gear_Head_Hood__Brim',(0,-.166,1.685),(.48,.11,.09),'PaddedLight','Gear_Head_Hood',.025,1)
ellipsoid('Gear_Head_Helm__Shell',(0,-.003,1.776),(.288,.255,.172),'IronDark','Gear_Head_Helm')
bevel_box('Gear_Head_Helm__Brow',(0,-.211,1.690),(.51,.065,.09),'Steel','Gear_Head_Helm',.018,1)
for sign,side in [(-1,'L'),(1,'R')]:
    bevel_box('Gear_Head_Helm__Cheek_'+side,(sign*.25,-.035,1.59),(.055,.21,.21),'IronDark','Gear_Head_Helm',.015,1)

loft('Gear_Body_Vest__Shell',[(0,0,.78,.266,.158),(0,0,1.07,.257,.155),(0,0,1.24,.238,.135)],'Padded','Gear_Body_Vest')
bevel_box('Gear_Body_Vest__Front',(0,-.158,1.03),(.39,.038,.37),'PaddedLight','Gear_Body_Vest',.035,1)
loft('Gear_Body_Cuirass__Shell',[(0,0,.79,.278,.169),(0,0,1.09,.272,.163),(0,0,1.24,.236,.14)],'IronDark','Gear_Body_Cuirass')
bevel_box('Gear_Body_Cuirass__Plate',(0,-.171,1.05),(.43,.048,.33),'Steel','Gear_Body_Cuirass',.035,1)
bevel_box('Gear_Body_Cuirass__Badge',(0,-.204,1.06),(.08,.024,.09),'Brass','Gear_Body_Cuirass',.01,1)

for sign,side in [(-1,'L'),(1,'R')]:
    x=sign*.535
    bevel_box('Gear_Gloves_Leather__Hand_'+side,(x,-.013,.811),(.14,.13,.18),'Leather','Gear_Gloves_Leather',.033,1)
    bevel_box('Gear_Gloves_Leather__Cuff_'+side,(sign*.513,-.012,.901),(.17,.14,.08),'Trail','Gear_Gloves_Leather',.025,1)
    bevel_box('Gear_Gloves_Iron__Hand_'+side,(x,-.013,.811),(.15,.14,.19),'IronDark','Gear_Gloves_Iron',.025,1)
    bevel_box('Gear_Gloves_Iron__Plate_'+side,(x,-.092,.83),(.13,.025,.10),'Steel','Gear_Gloves_Iron',.018,1)
    bevel_box('Gear_Gloves_Iron__Cuff_'+side,(sign*.512,-.012,.908),(.18,.15,.08),'IronDark','Gear_Gloves_Iron',.02,1)
    x=sign*.125
    bevel_box('Gear_Feet_Trail__Boot_'+side,(x,-.046,.12),(.22,.34,.22),'Trail','Gear_Feet_Trail',.046,1)
    bevel_box('Gear_Feet_Trail__Sole_'+side,(x,-.052,.035),(.23,.35,.07),'Sole','Gear_Feet_Trail',.022,1)
    bevel_box('Gear_Feet_Iron__Boot_'+side,(x,-.046,.13),(.23,.35,.24),'IronDark','Gear_Feet_Iron',.033,1)
    bevel_box('Gear_Feet_Iron__Toe_'+side,(x,-.205,.088),(.22,.072,.11),'Steel','Gear_Feet_Iron',.017,1)
    bevel_box('Gear_Feet_Iron__Sole_'+side,(x,-.052,.032),(.24,.36,.065),'Sole','Gear_Feet_Iron',.018,1)

anchor = bpy.data.objects.new('Villager_Root',None)
root.objects.link(anchor)
def socket(name, location, rotation=(0,0,0)):
    obj = bpy.data.objects.new(name,None)
    root.objects.link(obj)
    obj.parent = anchor
    obj.location = location
    obj.rotation_euler = rotation
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = .15
    obj['future_bone'] = 'hand_r' if 'Hand' in name else 'spine_02'
    return obj
# R means positive Blender X in this prototype (not yet anatomical rig naming).
# Blade points outwards and forwards, away from sleeve/head, grip inside mitten.
hand = socket('Socket_Hand_R',(.535,-.013,.811),(math.radians(65),math.radians(25),0))
hand.rotation_euler = Vector((.65,-.18,-.74)).to_track_quat('Z','Y').to_euler()
back = socket('Socket_Back',(0,.159,1.08))
objects = []
for group in groups.values():
    for obj in group.objects:
        obj.parent = hand if obj.name.startswith('Equipment_Sword__') else back if obj.name.startswith('Equipment_Backpack__') else anchor
        obj['part_id'] = obj.name
        obj['slot'] = obj.name.split('__')[0]
        objects.append(obj)
anchor['notes'] = 'Static modular library. Select one hair/outfit. Shared body once. No skinning.'
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for obj in objects+[anchor,hand,back]: obj.select_set(True)
bpy.context.view_layer.objects.active = anchor
bpy.ops.export_scene.gltf(filepath=os.path.join(HERE,'villager_modular.glb'),export_format='GLB',
    use_selection=True,export_yup=True,export_texcoords=False,export_normals=True,
    export_materials='EXPORT',export_cameras=False,export_lights=False,
    export_animations=False,export_extras=True)
# Save an editable library with only the default combination visible initially.
for obj in objects:
    hidden = obj.name.startswith(('Hair_Bun__','Outfit_Rust__','Equipment_','Gear_')) or obj.name == 'Shared__Body_Torso'
    obj.hide_set(hidden)
    obj.hide_render = hidden
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE,'villager_modular.blend'))
report = {'shared_body':'villager.base', 'hair':['swept','bun'], 'outfit':['sage','rust'],
          'hand':['none','sword'], 'back':['none','backpack'],
          'equipment':{'head':['none','padded_hood','iron_helm'],
                       'body':['none','padded_vest','iron_cuirass'],
                       'gloves':['none','leather_gloves','iron_gauntlets'],
                       'feet':['none','trail_boots','iron_boots']},
          'sockets':{'Socket_Hand_R':{'future_bone':'hand_r','blender_position':list(hand.location),'blender_rotation_radians':list(hand.rotation_euler)},
                     'Socket_Back':{'future_bone':'spine_02','blender_position':list(back.location)}},
          'parts':[o.name for o in objects], 'rigged':False}
with open(os.path.join(HERE,'modular_info.json'),'w') as f: json.dump(report,f,indent=2)
print('MODULAR_BUILD_COMPLETE',len(objects),'mesh parts')
