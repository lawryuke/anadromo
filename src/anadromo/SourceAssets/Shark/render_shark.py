import bpy
from pathlib import Path
from mathutils import Vector
root=Path.cwd(); source=root/'SourceAssets/Shark'
bpy.ops.wm.open_mainfile(filepath=str(source/'Shark_prepared.blend'))
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.world=bpy.data.worlds.new('Studio'); scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.12,.15,1)
for loc,power in [((2,-3,4),450),((-2,1,2),300)]:
    bpy.ops.object.light_add(type='AREA',location=loc); light=bpy.context.object; light.data.energy=power; light.data.size=3; light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2,-2,1)); camera=bpy.context.object; camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=2.4; scene.camera=camera
scene.render.resolution_x=1000; scene.render.resolution_y=700; scene.render.resolution_percentage=100; scene.render.filepath=str(source/'prepared.png'); bpy.ops.render.render(write_still=True)
