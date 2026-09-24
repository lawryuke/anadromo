"""Deterministic, offline authoring of Escenario4 and its low-poly basalt mesh.
Run from the Unity project root. No source scene or shared asset is modified.
"""
raise SystemExit('Generador antiguo deshabilitado para conservar la calidad visual. Usa Unity: Anadromo > Escenario 4 > Actualizar arte, o -executeMethod Scenario4ArtUpgrade.Build.')

from pathlib import Path
import math
import random
import re
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
SOURCE = (ASSETS / '_Project/Scenes/TerrainTestVisuales.unity').read_text(encoding='utf-8-sig')
blocks = {int(m.group(2)): m.group(0) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+).*?(?=^--- !u!|\Z)', SOURCE, re.M | re.S)}
rng = random.Random(404)

def guid(path):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'guid: (\w+)', meta.read_text()).group(1)
    value = uuid.uuid5(uuid.NAMESPACE_URL, 'anadromo/escenario4/' + str(path.relative_to(ROOT)).replace('\\', '/')).hex
    meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n', encoding='utf-8')
    return value

def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding='utf-8')
    return guid(path)

def vec(v):
    return '{' + ', '.join(f'{k}: {n:.7g}' for k,n in zip('xyzw', v)) + '}'

def quat(yaw):
    return (0, math.sin(yaw / 2), 0, math.cos(yaw / 2))

def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def norm(v):
    size = math.sqrt(sum(x*x for x in v))
    return tuple(x / max(1e-8,size) for x in v)

# Native Mesh uses the same Unity 6 serialization layout as the project's authored Orca mesh.
mesh_template = (ASSETS / '_Project/Art/Models/Orca/OrcaSwimMesh.asset').read_text()
segments, rings = 20, 10
points = [(0,-.5,0)]
for row in range(1,rings):
    latitude = -math.pi/2 + row*math.pi/rings
    for column in range(segments):
        a = column * math.tau/segments
        radius = .5 * math.cos(latitude) * (.96+.055*math.sin(a*3+latitude*2)+.035*math.cos(a*5-latitude*3))
        points.append((radius*math.cos(a), .5*math.sin(latitude)+.008*math.sin(a*4+latitude), radius*math.sin(a)))
points.append((.02,.5,-.03))
faces=[]
for j in range(segments):
    faces.append((0,1+j,1+(j+1)%segments))
    for row in range(rings-2):
        a=1+row*segments+j; b=1+row*segments+(j+1)%segments; c=a+segments; d=b+segments
        faces.extend(((a,c,b),(b,c,d)))
    last=1+(rings-2)*segments
    faces.append((len(points)-1,last+(j+1)%segments,last+j))
vertices=[]
for face in faces:
    a,b,c=(points[i] for i in face)
    normal=norm(cross(sub(b,a),sub(c,a)))
    center=tuple((a[i]+b[i]+c[i])/3 for i in range(3))
    if sum(normal[i]*center[i] for i in range(3))<0:
        b,c=c,b; normal=tuple(-n for n in normal)
    tangent=norm(cross((0,1,0) if abs(normal[1])<.99 else (1,0,0),normal))
    for p in (a,b,c):
        radial=norm(p)
        smooth=norm(tuple(radial[i]*.8+normal[i]*.2 for i in range(3)))
        vertices.extend((*p,*smooth,*tangent,1,p[0]+.5,p[2]+.5))
vertex_count=len(vertices)//12
data=struct.pack('<'+'f'*len(vertices),*vertices).hex()
indices=struct.pack('<'+'I'*vertex_count,*range(vertex_count)).hex()
aabb='      m_Center: {x: 0, y: 0, z: 0}\n      m_Extent: {x: 0.55, y: 0.55, z: 0.55}\n'
mesh=mesh_template.replace('OrcaSwimMesh','Escenario4_Basalt')
mesh=re.sub(r'  m_SubMeshes:.*?(?=  m_Shapes:)',f'  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n    indexCount: {vertex_count}\n    topology: 0\n    baseVertex: 0\n    firstVertex: 0\n    vertexCount: {vertex_count}\n    localAABB:\n'+aabb,mesh,flags=re.S)
mesh=re.sub(r'  m_IndexBuffer:.*','  m_IndexBuffer: '+indices,mesh)
mesh=re.sub(r'    m_VertexCount: \d+',f'    m_VertexCount: {vertex_count}',mesh)
mesh=re.sub(r'    m_DataSize: \d+',f'    m_DataSize: {len(vertices)*4}',mesh)
mesh=re.sub(r'    _typelessdata:.*','    _typelessdata: '+data,mesh)
mesh=re.sub(r'  m_LocalAABB:\n.*?(?=  m_MeshUsageFlags:)', '  m_LocalAABB:\n'+aabb.replace('      ','    '),mesh,flags=re.S)
mesh=mesh[:mesh.index('  m_MeshLodInfo:')]
mesh_guid=write(ASSETS/'_Project/Art/Models/Escenario4_Basalt.asset',mesh)

scripts={name:guid(ASSETS/f'_Project/Scripts/Mechanics/{name}.cs') for name in (
    'Scenario4Manager','Scenario4Swimmer','BloopMouthRisingController','FallingRockShowerSpawner','FallingRock','Scenario4VisibilityController','Scenario4Indicators')}
rock_guid='d3c9f45a1eaf4ee8988977a9c8df2f8d'
particle_guid='beafe573f55c41d9b91be24a6c35cc07'

# Emissive route marker material, URP Unlit with fog support inherited from URP.
lit_source=(ASSETS/'_Project/Art/Materials/TerrainTestVisuales_RockMassif.mat').read_text()
cave_shader=guid(ASSETS/'_Project/Art/Materials/Escenario4Cave.shader')
def stone_material(name,color):
    material=lit_source.replace('TerrainTestVisuales_RockMassif',name)
    material=re.sub(r'  m_Shader:.*',f'  m_Shader: {{fileID: 4800000, guid: {cave_shader}, type: 3}}',material)
    material=re.sub(r'(_BaseColor: )\{[^}]+\}',lambda m:m[1]+color,material)
    return write(ASSETS/f'_Project/Art/Materials/{name}.mat',material)
rock_guid=stone_material('Escenario4_WetBasalt','{r: 0.64, g: 0.72, b: 0.68, a: 1}')
limestone_guid=stone_material('Escenario4_Limestone','{r: 0.78, g: 0.73, b: 0.59, a: 1}')
falling_guid=stone_material('Escenario4_FallingStone','{r: 0.92, g: 0.81, b: 0.6, a: 1}')
# Existing abyssal shader supports emission-like bright base colours and fog.
beacon_source=(ASSETS/'_Project/Art/Materials/Bloop_Eyes.mat').read_text()
beacon_source=beacon_source.replace('Bloop_Eyes','Escenario4_Guide')
guide_shader=guid(ASSETS/'_Project/Art/Materials/Escenario4Guide.shader')
beacon_source=re.sub(r'  m_Shader:.*',f'  m_Shader: {{fileID: 4800000, guid: {guide_shader}, type: 3}}',beacon_source)
beacon_source=re.sub(r'(_BaseColor: )\{[^}]+\}',r'\1{r: 0.12, g: 1, b: 0.75, a: 1}',beacon_source)
beacon_guid=write(ASSETS/'_Project/Art/Materials/Escenario4_Guide.mat',beacon_source)
cue_shader=guid(ASSETS/'_Project/Art/Materials/Escenario4Cue.shader')
cue_source=beacon_source.replace('Escenario4_Guide','Escenario4_Warning').replace(guide_shader,cue_shader)
cue_source=re.sub(r'(_BaseColor: )\{[^}]+\}',r'\1{r: 1, g: 1, b: 1, a: 1}',cue_source)
cue_guid=write(ASSETS/'_Project/Art/Materials/Escenario4_Warning.mat',cue_source)

docs=[]; nodes=[]; next_id=4000000000
def alloc():
    global next_id
    next_id+=1
    return next_id

def base(go):
    return f'  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n'

def node(name, pos=(0,0,0), scale=(1,1,1), rotation=(0,0,0,1), parent=None, tag='Untagged'):
    n=dict(go=alloc(), tr=alloc(), name=name,pos=pos,scale=scale,rot=rotation,parent=parent,children=[],components=[],tag=tag)
    n['components'].append(n['tr']); nodes.append(n)
    if parent: parent['children'].append(n['tr'])
    return n

def component(n, typ, name, text):
    ident=alloc(); n['components'].append(ident)
    docs.append(f'--- !u!{typ} &{ident}\n{name}:\n'+base(n['go'])+text)
    return ident

def mono(n, name, fields):
    return component(n,114,'MonoBehaviour',f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {scripts[name]}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::Anadromo.Mechanics.{name}\n'+fields)

def copied(n, oldid, replacements=None):
    ident=alloc(); n['components'].append(ident)
    doc=blocks[oldid]
    doc=re.sub(r'^(--- !u!\d+ &)-?\d+',lambda m:m[1]+str(ident),doc)
    doc=re.sub(r'  m_GameObject: \{fileID: \d+\}',f'  m_GameObject: {{fileID: {n["go"]}}}',doc)
    if replacements:
        for key,value in replacements.items(): doc=re.sub(rf'^(  {re.escape(key)}: ).*',lambda m:m[1]+str(value),doc,flags=re.M)
    docs.append(doc)
    return ident

render_template=next(b for b in blocks.values() if b.startswith('--- !u!23 '))
def rock(name,pos,scale,rotation=(0,0,0,1),parent=None,material=rock_guid,collider=True):
    n=node(name,pos,scale,rotation,parent)
    component(n,33,'MeshFilter',f'  m_Mesh: {{fileID: 4300000, guid: {mesh_guid}, type: 2}}\n')
    ident=alloc(); n['components'].append(ident)
    doc=re.sub(r'^--- !u!23 &\d+',f'--- !u!23 &{ident}',render_template)
    doc=re.sub(r'  m_GameObject: \{fileID: \d+\}',f'  m_GameObject: {{fileID: {n["go"]}}}',doc)
    doc=re.sub(r'  m_Materials:.*?(?=  m_StaticBatchInfo:)',f'  m_Materials:\n  - {{fileID: 2100000, guid: {material}, type: 2}}\n',doc,flags=re.S)
    doc=re.sub(r'  m_AdditionalVertexStreams:.*','  m_AdditionalVertexStreams: {fileID: 0}',doc)
    doc=re.sub(r'  m_LightProbeUsage:.*','  m_LightProbeUsage: 0',doc)
    doc=re.sub(r'  m_ProbeAnchor:.*','  m_ProbeAnchor: {fileID: 0}',doc)
    doc=re.sub(r'  m_LightProbeVolumeOverride:.*','  m_LightProbeVolumeOverride: {fileID: 0}',doc)
    docs.append(doc)
    if collider:
        component(n,64,'MeshCollider',f'  m_Material: {{fileID: 0}}\n  m_IsTrigger: 0\n  m_Enabled: 1\n  serializedVersion: 5\n  m_Convex: 0\n  m_CookingOptions: 30\n  m_Mesh: {{fileID: 4300000, guid: {mesh_guid}, type: 2}}\n')
    return n

world=node('ESCENARIO 4 - La garganta del Bloop')
walls=node('Paredes de basalto',parent=world)
for row,y in enumerate(range(-18,51,6)):
    for col in range(18):
        a=col*math.tau/18+(row%2)*.055
        radius=15.3+rng.uniform(-.25,.5)
        rock(f'Acantilado {row:02}-{col:02}',(math.cos(a)*radius,y,math.sin(a)*radius),
            (7.4,rng.uniform(10,12),6),quat(math.pi/2-a),walls,material=limestone_guid if (row+col)%5==0 else rock_guid)
floor=rock('Fondo del abismo',(0,-22,0),(36,4,36),parent=world)
route_root=node('Ruta espiral - luces y refugios',parent=world)
route=[]; anchors=[]; shelters=[]
for i in range(25):
    a=i/24*math.tau*1.25
    y=2+i*1.5
    p=(10.4*math.cos(a),y,10.4*math.sin(a))
    marker=node(f'Ruta {i:02}',p,parent=route_root); route.append(marker)
    # Small luminous mineral clusters offset toward the wall, not colliders in the swim path.
    for cluster in range(3):
        az=a+(cluster-1)*.022
        rock(f'Mineral luminoso {i:02}-{cluster}',(11.55*math.cos(az),y+(cluster%2)*.15,11.55*math.sin(az)),
            (.13,.32+cluster*.08,.13),quat(az),parent=route_root,material=beacon_guid,collider=False)
    if i%3==0:
        rock(f'Cornisa {i:02}',(12*math.cos(a),y-2.2,12*math.sin(a)),(5,1.3,5),quat(-a),route_root)
    if i%2==0 and i<24:
        anchor=node(f'Grieta del techo {i:02}',(p[0],y+6,p[2]),parent=world); anchors.append(anchor)
        rock(f'Saliente fracturado {i:02}',(12.6*math.cos(a),y+7.5,12.6*math.sin(a)),(6,2.4,5),quat(-a),world)
        for mineral in range(3):
            az=a+(mineral-1)*.06
            rock(f'Estalactita {i:02}-{mineral}',(12.8*math.cos(az),y+6.4,12.8*math.sin(az)),
                (.45,1.6+mineral*.3,.5),quat(az),world,material=limestone_guid)
    if i in (5,12,19):
        shelter=node(f'Refugio lateral {i:02}',(11.7*math.cos(a),y,11.7*math.sin(a)),parent=world); shelters.append(shelter)
        rock(f'Techo del refugio {i:02}',(11.7*math.cos(a),y+2.7,11.7*math.sin(a)),(5,1.5,5),quat(-a),world)

for index,y in enumerate((10,22,34)):
    rock(f'Arco transversal fracturado {index}',(0,y,0),(19,2.4,4.5),quat(index*1.2+.4),world)

exit_node=node('Salida - grieta superior',(0,39,11.2),parent=world)
rock('Dintel de salida',(0,42,11.8),(7,2.2,4),parent=world)
rock('Jamba izquierda',(-3.4,39,11.8),(1.5,6,4),parent=world)
rock('Jamba derecha',(3.4,39,11.8),(1.5,6,4),parent=world)

light_template=next((k for k,b in blocks.items() if b.startswith('--- !u!108 ')))
sun=node('Luz filtrada de superficie',(0,46,0),rotation=(.5,-.5,.5,.5),parent=world)
copied(sun,light_template,{'m_Type':1,'m_Intensity':1.1,'m_Color':'{r: 0.4, g: 0.8, b: 0.85, a: 1}'})
for label,pos,intensity,radius in [('Luz de salida',(0,40,9),4,14),('Luz del inicio',(10,5,1),1.8,9)]:
    n=node(label,pos,parent=world)
    copied(n,light_template,{'m_Type':2,'m_Intensity':intensity,'m_Range':radius,'m_Color':'{r: 0.15, g: 0.85, b: 0.7, a: 1}'})

player=node('Jugador - nadador',(10.4,2,0),rotation=quat(0),parent=world)
camera=node('Main Camera',parent=player,tag='MainCamera')
cam_id=copied(camera,863163185,{'far clip plane':140,'near clip plane':.08,'field of view':75})
copied(camera,863163184)
copied(camera,863163182,{'m_RendererIndex':0,'m_RequiresOpaqueTextureOption':1,'m_RequiresColorTexture':1})
# Authored Input System HMD bindings from the existing scene, active only in XR at runtime.
pose_id=copied(camera,863163183)
component(player,143,'CharacterController','  m_Material: {fileID: 0}\n  m_IsTrigger: 0\n  m_Enabled: 1\n  serializedVersion: 3\n  m_Height: 1\n  m_Radius: 0.35\n  m_SlopeLimit: 90\n  m_StepOffset: 0.1\n  m_SkinWidth: 0.04\n  m_MinMoveDistance: 0\n  m_Center: {x: 0, y: 0, z: 0}\n')
swim_id=mono(player,'Scenario4Swimmer',f'  viewer: {{fileID: {cam_id}}}\n  speed: 3.4\n  sensitivity: 0.12\n  canMove: 0\n')

bloop=node('Bloop - boca ascendente',(0,-10,0),parent=world)
# Preserve imported prefab links; remove the Act 1 movement component entirely.
prefab=blocks[1810196898]
prefab=prefab.replace('m_TransformParent: {fileID: 177583813}',f'm_TransformParent: {{fileID: {bloop["tr"]}}}')
prefab=re.sub(r'(propertyPath: m_LocalPosition\.[xyz]\n      value: ).*',r'\g<1>0',prefab)
prefab=re.sub(r'    m_AddedComponents:.*?(?=  m_SourcePrefab:)', '    m_AddedComponents: []\n',prefab,flags=re.S)
docs.extend((prefab,blocks[88480905],blocks[88480908]))
bloop['children'].append(88480908)
bloop_id=mono(bloop,'BloopMouthRisingController','  visual: {fileID: 88480908}\n  captureRadius: 13.5\n  baseRiseSpeed: 0.35\n  maximumRiseSpeed: 0.65\n  attackRiseSpeed: 0.8\n  warningDuration: 3\n  approachDuration: 4\n  recoveryDuration: 6\n  mouthHeightOffset: 0\n')

def refs(name,objects): return '  '+name+':\n'+''.join(f'  - {{fileID: {o["tr"]}}}\n' for o in objects)
spawner=node('Derrumbes - pool de rocas',parent=world)
rocks_id=mono(spawner,'FallingRockShowerSpawner',refs('ceilingAnchors',anchors)+f'  rockMesh: {{fileID: 4300000, guid: {mesh_guid}, type: 2}}\n  rockMaterial: {{fileID: 2100000, guid: {falling_guid}, type: 2}}\n  sedimentMaterial: {{fileID: 2100000, guid: {particle_guid}, type: 2}}\n  cueMaterial: {{fileID: 2100000, guid: {cue_guid}, type: 2}}\n  poolSize: 18\n  warningDuration: 2.2\n  fallSpeed: 2.4\n  diameterRange: {{x: 0.25, y: 0.7}}\n')
vision=node('Percepcion submarina E4',parent=world)
vision_id=mono(vision,'Scenario4VisibilityController',f'  surfaceY: 48\n  calmVisibility: 15\n  minimumVisibility: 11\n  particleMaterial: {{fileID: 2100000, guid: {particle_guid}, type: 2}}\n'+refs('shelters',shelters))
manager=mono(world,'Scenario4Manager',f'  viewer: {{fileID: {cam_id}}}\n  swimmer: {{fileID: {swim_id}}}\n  rocks: {{fileID: {rocks_id}}}\n  bloop: {{fileID: {bloop_id}}}\n  vision: {{fileID: {vision_id}}}\n  exit: {{fileID: {exit_node["tr"]}}}\n  exitRadius: 2.6\n  awakeningDuration: 5\n'+refs('route',route))

scene=''.join(blocks[k] for k in (1,2,3,4))
scene=re.sub(r'  m_Sun:.*','  m_Sun: {fileID: 0}',scene)
scene=scene.replace('  m_Fog: 0','  m_Fog: 1').replace('  m_FogMode: 1','  m_FogMode: 3').replace('  m_FogDensity: 0.025','  m_FogDensity: 0.10116')
scene=re.sub(r'  m_SkyboxMaterial:.*','  m_SkyboxMaterial: {fileID: 0}',scene)
for n in nodes:
    scene+=f'--- !u!1 &{n["go"]}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n'
    scene+=''.join(f'  - component: {{fileID: {c}}}\n' for c in n['components'])
    scene+=f'  m_Layer: 0\n  m_Name: {n["name"]}\n  m_TagString: {n["tag"]}\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
    scene+=f'--- !u!4 &{n["tr"]}\nTransform:\n'+base(n['go'])+f'  serializedVersion: 2\n  m_LocalRotation: {vec(n["rot"])}\n  m_LocalPosition: {vec(n["pos"])}\n  m_LocalScale: {vec(n["scale"])}\n  m_ConstrainProportionsScale: 0\n'
    scene+=('  m_Children:\n'+''.join(f'  - {{fileID: {c}}}\n' for c in n['children'])) if n['children'] else '  m_Children: []\n'
    scene+=f'  m_Father: {{fileID: {n["parent"]["tr"] if n["parent"] else 0}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
scene+=''.join(docs)
scene+=f'--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {{fileID: {world["tr"]}}}\n'
scene_guid=write(ASSETS/'_Project/Scenes/Escenario4.unity','%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+scene)
print(f'Escenario4: {len(nodes)} objects, {len(faces)} rock triangles, {len(anchors)} ceiling sectors, GUID {scene_guid}')
