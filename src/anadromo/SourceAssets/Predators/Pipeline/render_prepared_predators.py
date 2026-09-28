import bpy, math
from pathlib import Path
from mathutils import Vector
root=Path.cwd(); out=root/'Docs/PredatorPreviews'; out.mkdir(parents=True,exist_ok=True)
for species in ['Piranha','Lamprey','Angler']:
    bpy.ops.wm.open_mainfile(filepath=str(root/'SourceAssets/Predators'/(species+'_prepared.blend')))
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=24
    scene.world=bpy.data.worlds.new('Underwater studio'); scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.06,.1,.12,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
    for loc,power,size in [((1,-1,2),90,2),((-1,0,.5),45,1.5)]:
        bpy.ops.object.light_add(type='AREA',location=loc); l=bpy.context.object; l.data.energy=power; l.data.shape='DISK'; l.data.size=size; l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(1.1,-1,.5)); cam=bpy.context.object
    cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=1.35; scene.camera=cam
    scene.render.resolution_x=1000; scene.render.resolution_y=700; scene.render.resolution_percentage=100
    scene.render.image_settings.media_type='IMAGE'; scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(out/(species+'_Blender.png'))
    bpy.ops.render.render(write_still=True)
