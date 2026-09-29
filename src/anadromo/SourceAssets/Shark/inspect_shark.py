import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path.cwd(); source=root/'SourceAssets/Shark'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(next(source.rglob('*.gltf'))))
for o in bpy.context.scene.objects:
    if o.type=='MESH': print('MESH',o.name,len(o.data.vertices),'bounds',[list(o.matrix_world@Vector(v)) for v in o.bound_box],'materials',[m.name for m in o.data.materials])
    if o.type=='ARMATURE': print('BONES',[(b.name,list(o.matrix_world@b.head_local)) for b in o.data.bones])
bpy.ops.wm.save_as_mainfile(filepath=str(source/'inspection.blend'))
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=16
meshes=[o for o in scene.objects if o.type=='MESH']; pts=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)]); center=(lo+hi)/2; size=max(hi-lo)
scene.world=bpy.data.worlds.new('Studio'); scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.1,.15,.19,1)
for loc,power in [((1,-2,3),700),((-2,1,1),500)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*size); l=bpy.context.object; l.data.energy=power*size*size; l.data.size=size*2; l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=center+Vector((1,-1,.5))*size*2); c=bpy.context.object; c.rotation_euler=(center-c.location).to_track_quat('-Z','Y').to_euler(); c.data.type='ORTHO'; c.data.ortho_scale=size*1.3; c.data.clip_end=size*20; scene.camera=c
scene.render.resolution_x=1000; scene.render.resolution_y=700; scene.render.resolution_percentage=100; scene.render.filepath=str(source/'inspection.png'); bpy.ops.render.render(write_still=True)
