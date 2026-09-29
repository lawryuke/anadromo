import bpy, json, sys, os, math
from pathlib import Path
from mathutils import Vector
root=Path.cwd()
out=root/'SourceAssets/Predators/Inspection'; out.mkdir(exist_ok=True)
models={'Piranha1':'Piranha1/fish pir.glb','Piranha2':'Piranha2/source/piex.fbx','Lamprey':'Lamprey/source/Sketchfab_2022_05_15_21_45_23.blend','Angler':'Angler/source/model/model/Zorag.fbx'}
for name,rel in models.items():
    if (out/(name+'.png')).exists(): continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=root/'SourceAssets/Predators'/rel
    if path.suffix=='.blend': bpy.ops.wm.open_mainfile(filepath=str(path))
    elif path.suffix=='.glb': bpy.ops.import_scene.gltf(filepath=str(path))
    else: bpy.ops.import_scene.fbx(filepath=str(path))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    info=[]
    for o in meshes:
        info.append(dict(name=o.name,verts=len(o.data.vertices),bounds=[list(o.matrix_world@Vector(v)) for v in o.bound_box],materials=[m.name if m else '' for m in o.data.materials],groups=[g.name for g in o.vertex_groups]))
    (out/(name+'.json')).write_text(json.dumps(dict(meshes=info,actions=[a.name for a in bpy.data.actions],bones={o.name:[b.name for b in o.data.bones] for o in bpy.context.scene.objects if o.type=='ARMATURE'}),indent=2))
    for im in bpy.data.images:
        if im.source=='FILE':
            matches=list((root/'SourceAssets/Predators'/name if name!='Lamprey' else root/'SourceAssets/Predators/Lamprey').rglob(Path(im.filepath.replace('\\','/')).name))
            if matches: im.filepath=str(matches[0]); im.reload()
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    lo=Vector([min(p[i] for p in points) for i in range(3)]); hi=Vector([max(p[i] for p in points) for i in range(3)])
    center=(lo+hi)/2; size=max(hi-lo)
    for o in list(bpy.context.scene.objects):
        if o.type in ('LIGHT','CAMERA'): bpy.data.objects.remove(o,do_unlink=True)
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=16
    scene.world=bpy.data.worlds.new('Studio'); scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.19,1)
    for loc,power in [((1,-2,3),900),((-2,1,1),700)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*size); light=bpy.context.object; light.data.energy=power*size*size; light.data.shape='DISK'; light.data.size=size*2; light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=center+Vector((1,-1,.65))*size*2)
    cam=bpy.context.object; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.35; cam.data.clip_end=size*20; scene.camera=cam
    scene.render.image_settings.media_type='IMAGE'
    scene.render.image_settings.file_format='PNG'
    scene.render.resolution_x=1000; scene.render.resolution_y=750; scene.render.resolution_percentage=100; scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
