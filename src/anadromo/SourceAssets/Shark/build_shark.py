"""Prepare the supplied shark without modifying the previous predator assets."""
import bpy, math, json
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path.cwd(); SOURCE=ROOT/'SourceAssets/Shark'; OUT=ROOT/'Assets/_Project/Art/Shark'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(next(SOURCE.rglob('*.gltf'))))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.data.vertices)>100]
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
for o in bpy.context.scene.objects: o.animation_data_clear()
for arm in arms:
    for p in arm.pose.bones:
        p.matrix_basis=Matrix.Identity(4)
        for c in list(p.constraints): p.constraints.remove(c)
bpy.context.view_layer.update()
pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)])
# Match the 1.8 m placeholder, including its tail, rather than rescaling the passage.
length=1.9; scale=length/(hi.y-lo.y); center=(hi+lo)/2
t=Matrix.Scale(scale,4)@Matrix.Translation(-center)
world={o:o.matrix_world.copy() for o in meshes+arms}
for o in meshes+arms:
    o.parent=None; o.matrix_world=Matrix.Identity(4); o.data.transform(t@world[o])
for o in list(bpy.context.scene.objects):
    if o not in meshes+arms: bpy.data.objects.remove(o,do_unlink=True)
arm=arms[0]; arm.name='SharkRig'
for o in meshes:
    bpy.context.view_layer.objects.active=o
    for p in o.data.polygons: p.use_smooth=True
    mod=o.modifiers.new('Silhouette refinement','SUBSURF'); mod.levels=1
    bpy.ops.object.modifier_move_up(modifier=mod.name); bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.context.view_layer.update()
# Bake subdued dorsal pigmentation into the original UVs, preserving gills, eyes and teeth.
mat=meshes[0].data.materials[0]
base=next(n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
mat.name='SharkSkin'; nodes=mat.node_tree.nodes; nodes.clear(); links=mat.node_tree.links
image=nodes.new('ShaderNodeTexImage'); image.image=base
geometry=nodes.new('ShaderNodeNewGeometry'); xyz=nodes.new('ShaderNodeSeparateXYZ'); links.new(geometry.outputs['Position'],xyz.inputs[0])
remap=nodes.new('ShaderNodeMapRange'); remap.inputs['From Min'].default_value=-.10; remap.inputs['From Max'].default_value=.12
links.new(xyz.outputs['Z'],remap.inputs['Value'])
ramp=nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color=(.9,.93,.94,1); ramp.color_ramp.elements[1].color=(.28,.37,.41,1)
links.new(remap.outputs[0],ramp.inputs[0])
mix=nodes.new('ShaderNodeMixRGB'); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=1; links.new(image.outputs['Color'],mix.inputs[1]); links.new(ramp.outputs[0],mix.inputs[2])
emit=nodes.new('ShaderNodeEmission'); links.new(mix.outputs[0],emit.inputs[0]); output=nodes.new('ShaderNodeOutputMaterial'); links.new(emit.outputs[0],output.inputs['Surface'])
baked=bpy.data.images.new('Shark_Albedo',2048,2048,alpha=False)
target=nodes.new('ShaderNodeTexImage'); target.image=baked; nodes.active=target
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=8; scene.render.bake.margin=12
bpy.ops.object.bake(type='EMIT')
baked.filepath_raw=str(OUT/'Shark_Albedo.png'); baked.file_format='PNG'; baked.save()
nodes.clear(); p=nodes.new('ShaderNodeBsdfPrincipled'); p.inputs['Roughness'].default_value=.46; p.inputs['Metallic'].default_value=0
tex=nodes.new('ShaderNodeTexImage'); tex.image=baked; links.new(tex.outputs['Color'],p.inputs['Base Color'])
output=nodes.new('ShaderNodeOutputMaterial'); links.new(p.outputs[0],output.inputs['Surface'])
for im in bpy.data.images:
    if im.has_data: im.pack()
bpy.ops.object.select_all(action='DESELECT')
for o in meshes+arms: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Shark_prepared.blend'))
# Refresh imported glTF pose caches before FBX export.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Shark_prepared.blend'))
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(OUT/'Shark.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_mesh_modifiers=False,apply_scale_options='FBX_SCALE_UNITS')
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
(SOURCE/'rig.json').write_text(json.dumps([{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local)} for b in arm.data.bones],indent=2))
