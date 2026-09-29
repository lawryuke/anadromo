"""Reproducible Blender preparation; source archives remain untouched."""
import bpy, bmesh, math, json, shutil
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path.cwd(); SOURCE=ROOT/'SourceAssets/Predators'; OUT=ROOT/'Assets/_Project/Art/Predators'
OUT.mkdir(parents=True,exist_ok=True)
def material(name, base, normal=None, rough=None, color=(1,1,1,1), roughness=.4):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=color; p.inputs['Roughness'].default_value=roughness
    def tex(path,socket,noncolor=False):
        if not path: return
        dest=OUT/'Textures'/Path(path).name; dest.parent.mkdir(exist_ok=True)
        shutil.copy2(path,dest)
        node=mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image=bpy.data.images.load(str(dest),check_existing=True)
        if noncolor: node.image.colorspace_settings.name='Non-Color'
        if socket=='Normal':
            n=mat.node_tree.nodes.new('ShaderNodeNormalMap'); n.inputs['Strength'].default_value=.65
            mat.node_tree.links.new(node.outputs['Color'],n.inputs['Color']); mat.node_tree.links.new(n.outputs[0],p.inputs[socket])
        else:
            mat.node_tree.links.new(node.outputs['Color'],p.inputs[socket])
            if name=='PiranhaSkin' and socket=='Base Color': mat.node_tree.links.new(node.outputs['Alpha'],p.inputs['Alpha'])
    tex(base,'Base Color'); tex(normal,'Normal',True); tex(rough,'Roughness',True)
    specs.append(dict(name=name,base=Path(base).name if base else '',normal=Path(normal).name if normal else '',roughness=roughness,color=list(color)))
    return mat
specs=[]
for species in ['Piranha','Lamprey','Angler']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if species=='Lamprey': bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Lamprey/source/Sketchfab_2022_05_15_21_45_23.blend'))
    else: bpy.ops.import_scene.fbx(filepath=str(SOURCE/('Piranha2/source/piex.fbx' if species=='Piranha' else 'Angler/source/model/model/Zorag.fbx')))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    for o in bpy.context.scene.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    for arm in arms:
        for p in arm.pose.bones:
            p.matrix_basis=Matrix.Identity(4)
            for c in list(p.constraints): p.constraints.remove(c)
    bpy.context.view_layer.update()
    # Normalize meshes and skeleton into meters, centered and facing Blender -Y / Unity +Z.
    rot=Matrix.Rotation(math.pi/2,4,'Z') if species=='Lamprey' else Matrix.Identity(4)
    pts=[rot@o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)])
    length={'Piranha':.75,'Lamprey':1.1,'Angler':1.05}[species]
    scale=length/(hi.y-lo.y); center=(lo+hi)/2
    t=Matrix.Scale(scale,4)@Matrix.Translation(-center)@rot
    world={o:o.matrix_world.copy() for o in meshes+arms}
    for o in meshes+arms:
        o.parent=None; o.matrix_world=Matrix.Identity(4); o.data.transform(t@world[o])
    for o in list(bpy.context.scene.objects):
        if o not in meshes+arms: bpy.data.objects.remove(o,do_unlink=True)
    if species=='Piranha':
        mats=[material('PiranhaSkin',SOURCE/'Piranha2/textures/fish01_UV_CC.png',roughness=.36)]
    elif species=='Lamprey':
        d=SOURCE/'Lamprey/textures'
        mats=[material('LampreySkin',d/'Lamprea.png',color=(.74,.79,.72,1),roughness=.32),material('LampreyTeeth',d/'Hortzak.png',roughness=.3),material('LampreyEye',d/'Begia_1_Base_Color.png',roughness=.17),material('LampreyEye2',d/'Begia_1_Base_Color.png',roughness=.17),material('LampreyMouth',d/'Papilla_Base_Color.png',roughness=.4)]
    else:
        d=SOURCE/'Angler/source/model/model/textures'; mats={}
        for part in ['Body','Teeth','Fins','Eyes']:
            mats[part]=material('Angler'+part,d/(part+'_1001_albedo.jpg'),d/(part+'_1001_normal.png'),d/(part+'_1001_roughness.jpg'),roughness=.3 if part=='Eyes' else .46)
    for o in meshes:
        if species=='Angler':
            for slot in o.material_slots: slot.material=mats[slot.material.name.split('.')[0]]
        else:
            for i in range(len(o.data.materials)): o.data.materials[i]=mats[i]
        for poly in o.data.polygons: poly.use_smooth=True
        bpy.context.view_layer.objects.active=o; o.select_set(True)
        if species=='Lamprey':
            # Retain oral detail while lowering cost from 111k to approximately 25k vertices.
            mod=o.modifiers.new('Game topology','DECIMATE'); mod.ratio=.23
            bpy.ops.object.modifier_apply(modifier=mod.name)
        else:
            if species=='Piranha':
                bm=bmesh.new(); bm.from_mesh(o.data)
                crease=bm.edges.layers.float.new('crease_edge')
                for edge in bm.edges:
                    if edge.is_boundary: edge[crease]=1
                bm.to_mesh(o.data); bm.free()
            mod=o.modifiers.new('Silhouette refinement','SUBSURF'); mod.levels=2 if species=='Piranha' else 1
            # Refine rest mesh before the armature, retaining original skin weights and UVs.
            bpy.ops.object.modifier_move_up(modifier=mod.name)
            bpy.ops.object.modifier_apply(modifier=mod.name)
        o.select_set(False)
    if species=='Lamprey':
        arm_data=bpy.data.armatures.new('LampreySkeleton'); arm=bpy.data.objects.new('LampreyRig',arm_data); bpy.context.collection.objects.link(arm)
        bpy.context.view_layer.objects.active=arm; arm.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
        count=12; step=length/count
        for i in range(count):
            bone=arm_data.edit_bones.new('Swim_%02d'%i); bone.head=(0,-length*.5+i*step,0); bone.tail=(0,-length*.5+(i+1)*step,0)
            if i: bone.parent=arm_data.edit_bones['Swim_%02d'%(i-1)]; bone.use_connect=True
        bpy.ops.object.mode_set(mode='OBJECT')
        for o in meshes:
            groups=[o.vertex_groups.new(name='Swim_%02d'%i) for i in range(count)]
            for v in o.data.vertices:
                f=max(0,min(count-1,(v.co.y+length*.5)/step-.5)); a=int(f); b=min(a+1,count-1); w=f-a
                groups[a].add([v.index],1-w,'REPLACE')
                if b!=a and w>0: groups[b].add([v.index],w,'REPLACE')
            m=o.modifiers.new('Continuous swimming skin','ARMATURE'); m.object=arm
            o.shape_key_add(name='Basis'); key=o.shape_key_add(name='OralPulse')
            for v in key.data:
                influence=max(0,1-(v.co.y+length*.5)/.065)
                v.co.x*=1+influence*.12; v.co.z*=1+influence*.12
        arms=[arm]
    # Keep the asset editable in Blender; Unity receives FBX to avoid a Blender dependency.
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes+arms: o.select_set(True)
    bpy.context.view_layer.objects.active=arms[0]
    for im in bpy.data.images:
        if im.source=='FILE' and im.has_data: im.pack()
    bpy.context.scene.render.image_settings.media_type='IMAGE'
    bpy.context.scene.render.image_settings.file_format='PNG'
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(species+'_prepared.blend')))
    # Reload the saved rest pose: FBX otherwise retains cached pose matrices from the source rig.
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/(species+'_prepared.blend')))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']; arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUT/(species+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_mesh_modifiers=False,apply_scale_options='FBX_SCALE_UNITS')
    report={'bones':[{ 'name':b.name,'head':list(arms[0].matrix_world@b.head_local),'tail':list(arms[0].matrix_world@b.tail_local)} for b in arms[0].data.bones],'vertices':sum(len(o.data.vertices) for o in meshes)}
    (SOURCE/'Inspection'/(species+'_prepared.json')).write_text(json.dumps(report,indent=2))
(OUT/'materials.json').write_text(json.dumps({'materials':specs},indent=2))
