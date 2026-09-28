import bpy
from pathlib import Path
root=Path.cwd()
for s in ['Piranha','Lamprey','Angler']:
    for fmt in ['blend','fbx']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if fmt=='blend': bpy.ops.wm.open_mainfile(filepath=str(root/'SourceAssets/Predators'/(s+'_prepared.blend')))
        else: bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Predators'/(s+'.fbx')))
        print('CHECK',s,fmt,'units',bpy.context.scene.unit_settings.scale_length)
        for o in bpy.context.scene.objects:
            if o.type=='ARMATURE':
                print('ARM',o.name,'matrix',o.matrix_world)
                for b in list(o.pose.bones)[:2]: print('BONE',b.name,'rest',list(o.matrix_world@b.bone.head_local),'pose',list(o.matrix_world@b.head),'scale',list(b.scale))
            elif o.type=='MESH': print('MESH',o.name,tuple(o.dimensions),list(o.scale))
