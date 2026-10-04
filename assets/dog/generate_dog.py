"""Build the small static modular trail dog. Blender 3.3, no external assets."""
import bpy
import json
import math
import os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

palette = {
    'fur': (.37,.245,.155,1), 'fur_light': (.66,.48,.30,1),
    'cream': (.79,.68,.48,1), 'dark': (.14,.12,.12,1),
    'collar': (.12,.39,.38,1), 'collar_light': (.30,.59,.52,1),
    'steel': (.34,.42,.46,1), 'steel_light': (.62,.68,.66,1),
    'armor': (.42,.27,.17,1), 'armor_light': (.60,.40,.25,1),
    'pack': (.62,.43,.22,1), 'brass': (.86,.66,.31,1)
}
materials = {}
for name, color in palette.items():
    mat=bpy.data.materials.new(name)
    mat.diffuse_color=color
    mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85
    materials[name]=mat

root=bpy.data.objects.new('Dog_Root',None)
bpy.context.collection.objects.link(root)
root['forward_blender']='-Y'
root['units']='metres'
root['notes']='Static modular dog; one Dog_Base body plus independent optional accessories.'
parts=[]

def finish(obj,name,mat,parent=root):
    obj.name=name
    obj.data.materials.append(materials[mat])
    obj.parent=parent
    obj['slot']=name.split('__')[0]
    parts.append(obj)
    return obj

def ellipsoid(name,at,scale,mat,segments=12,rings=8,parent=root):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=at)
    obj=bpy.context.object
    obj.scale=scale
    return finish(obj,name,mat,parent)

def box(name,at,size,mat,bevel=.0,parent=root):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at)
    obj=bpy.context.object
    obj.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('soft corners','BEVEL')
        mod.width=bevel;mod.segments=1
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj,name,mat,parent)

def cylinder(name,at,radius,depth,mat,vertices=10,rotation=(0,0,0),parent=root):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=at,rotation=rotation)
    return finish(bpy.context.object,name,mat,parent)

def cone(name,at,radius1,radius2,depth,mat,vertices=8,rotation=(0,0,0),parent=root):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius1,radius2=radius2,depth=depth,location=at,rotation=rotation)
    return finish(bpy.context.object,name,mat,parent)

ellipsoid('Dog_Base__Body',(0,.06,.48),(.31,.49,.29),'fur')
ellipsoid('Dog_Base__Chest',(0,-.29,.47),(.27,.24,.27),'cream')
ellipsoid('Dog_Base__Head',(0,-.49,.65),(.25,.25,.23),'fur_light')
ellipsoid('Dog_Base__Muzzle',(0,-.72,.54),(.16,.20,.12),'cream')
ellipsoid('Dog_Base__Nose',(0,-.90,.58),(.085,.065,.055),'dark')
for sign,side in [(-1,'L'),(1,'R')]:
    ellipsoid('Dog_Base__Cheek_'+side,(sign*.17,-.64,.56),(.09,.12,.09),'cream')
    ellipsoid('Dog_Base__Eye_'+side,(sign*.17,-.65,.72),(.043,.036,.047),'dark')
    ellipsoid('Dog_Base__Brow_'+side,(sign*.17,-.57,.79),(.08,.04,.035),'fur')
    ear=ellipsoid('Dog_Base__Ear_'+side,(sign*.22,-.43,.82),(.105,.14,.19),'fur')
    ear.rotation_euler[1]=sign*.27
    ellipsoid('Dog_Base__InnerEar_'+side,(sign*.22,-.57,.82),(.055,.026,.11),'fur_light')
    for fore,y in [(True,-.29),(False,.37)]:
        label='Front' if fore else 'Hind'
        x=sign*(.225 if fore else .23)
        ellipsoid('Dog_Base__Leg_'+label+'_'+side,(x,y,.24),(.10,.115,.24),'fur_light' if fore else 'fur')
        ellipsoid('Dog_Base__Paw_'+label+'_'+side,(x,y-.08,.075),(.115,.16,.075),'cream')
tail=cylinder('Dog_Base__Tail',(0,.58,.65),.075,.45,'fur',rotation=(math.radians(-53),0,0))
ellipsoid('Dog_Base__TailTip',(0,.76,.79),(.095,.11,.09),'cream')

# Accessories are independent mesh groups, sharing the base body's coordinates.
collar=cylinder('Dog_Collar__Band',(0,-.335,.58),.225,.115,'collar',vertices=12,rotation=(math.pi/2,0,0))
ellipsoid('Dog_Collar__Tag',(0,-.46,.43),(.085,.025,.085),'brass')
box('Dog_Collar__Buckle',(.18,-.35,.60),(.055,.045,.09),'collar_light',.01)

helmet=ellipsoid('Dog_Helmet__Cap',(0,-.49,.86),(.255,.25,.135),'steel')
box('Dog_Helmet__Brow',(0,-.68,.83),(.40,.11,.07),'steel_light',.02)
for sign,side in [(-1,'L'),(1,'R')]:
    box('Dog_Helmet__EarGuard_'+side,(sign*.22,-.47,.75),(.085,.18,.16),'steel',.02)
    box('Dog_Helmet__Strap_'+side,(sign*.205,-.56,.59),(.04,.07,.24),'armor',.01)

box('Dog_Armor__BackPlate',(0,.05,.75),(.54,.70,.09),'armor',.06)
box('Dog_Armor__ChestPlate',(0,-.30,.56),(.45,.12,.25),'armor_light',.04)
for sign,side in [(-1,'L'),(1,'R')]:
    box('Dog_Armor__Side_'+side,(sign*.285,.07,.52),(.055,.44,.26),'armor',.025)
    box('Dog_Armor__Rivet_'+side,(sign*.30,-.17,.60),(.035,.035,.035),'brass',.0)

socket=bpy.data.objects.new('Socket_Dog_Backpack',None)
bpy.context.collection.objects.link(socket)
socket.parent=root
socket.location=(0,.11,.81)
socket.empty_display_type='ARROWS'
socket.empty_display_size=.13
socket['attachment']='backpack local origin; local +Z up and -Y forward'
socket['future_bone']='spine'
box('Dog_Backpack__Bag',(0,.05,.13),(.38,.39,.28),'pack',.055,parent=socket)
box('Dog_Backpack__Flap',(0,-.16,.20),(.38,.09,.16),'armor_light',.02,parent=socket)
box('Dog_Backpack__Clasp',(0,-.215,.19),(.07,.035,.08),'brass',.01,parent=socket)
for sign,side in [(-1,'L'),(1,'R')]:
    box('Dog_Backpack__Strap_'+side,(sign*.17,.045,-.045),(.055,.40,.06),'armor',.015,parent=socket)

bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for obj in parts+[root,socket]:obj.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.gltf(filepath=os.path.join(HERE,'dog_modular.glb'),export_format='GLB',
    use_selection=True,export_yup=True,export_texcoords=False,export_normals=True,
    export_materials='EXPORT',export_cameras=False,export_lights=False,
    export_animations=False,export_extras=True)

# The editable file opens in the default collar-only presentation.
for obj in parts:
    hidden=obj.name.startswith(('Dog_Helmet__','Dog_Armor__','Dog_Backpack__'))
    obj.hide_set(hidden);obj.hide_render=hidden
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE,'dog_modular.blend'))
with open(os.path.join(HERE,'dog_info.json'),'w',encoding='utf8') as file:
    json.dump({'root':'Dog_Root','units':'metres','forward_blender':'-Y','export_forward':'+Z',
               'socket':{'name':'Socket_Dog_Backpack','blender_position':list(socket.location),'future_bone':'spine'},
               'slots':['Dog_Base','Dog_Collar','Dog_Helmet','Dog_Armor','Dog_Backpack'],
               'parts':[obj.name for obj in parts],'rigged':False},file,indent=2)
print('DOG_BUILD_COMPLETE',len(parts),'mesh parts')
