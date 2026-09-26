import bpy, math, random, os, json, bmesh
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree
random.seed(73)
OUT=os.environ.get("ANADROMO_OUTPUT",os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
geo=bpy.data.collections.get('Collection'); geo.name='01 · Caverna continua'
def col(name):
    c=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c); return c
details=col('02 · Formaciones y derrumbes'); flora=col('03 · Colonias bioluminiscentes'); refs=col('04 · Rutas y cámaras'); lights=col('05 · Iluminación de presentación')
def move(o,c):
    for old in list(o.users_collection): old.objects.unlink(o)
    c.objects.link(o)
def mesh(name,v,f,c=geo,mat=None):
    me=bpy.data.meshes.new(name); me.from_pydata(v,[],f); me.update(); ob=bpy.data.objects.new(name,me); c.objects.link(ob)
    if mat: me.materials.append(mat)
    return ob
def mat(name,color,rough=.8,emit=0):
    m=bpy.data.materials.new(name); m.use_nodes=True; m.diffuse_color=(*color,1)
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=rough
    if emit: p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=emit
    return m
rock=mat('AN_Basalto_Estratificado',(.12,.145,.132),.68)
n=rock.node_tree.nodes; l=rock.node_tree.links; p=n.get('Principled BSDF')
tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=1.35; tex.inputs['Detail'].default_value=5; tex.inputs['Roughness'].default_value=.72
coord=n.new('ShaderNodeTexCoord'); l.new(coord.outputs['Object'],tex.inputs['Vector'])
ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position=.2; ramp.color_ramp.elements[0].color=(.018,.027,.029,1)
ramp.color_ramp.elements[1].position=.8; ramp.color_ramp.elements[1].color=(.23,.215,.155,1)
ramp.color_ramp.elements.new(.48).color=(.075,.105,.097,1)
l.new(tex.outputs['Fac'],ramp.inputs[0]); l.new(ramp.outputs[0],p.inputs['Base Color'])
fine=n.new('ShaderNodeTexNoise'); fine.inputs['Scale'].default_value=22; fine.inputs['Detail'].default_value=3.5; l.new(coord.outputs['Object'],fine.inputs['Vector'])
bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.5; bump.inputs['Distance'].default_value=.09; l.new(fine.outputs['Fac'],bump.inputs['Height']); l.new(bump.outputs[0],p.inputs['Normal'])
p.inputs['Coat Weight'].default_value=.20; p.inputs['Coat Roughness'].default_value=.32
sediment=mat('AN_Sedimento_Caliza',(.16,.155,.115),.88)
cyan=mat('AN_Flora_Turquesa',(.035,.55,.38),.45,3)
blue=mat('AN_Flora_Azul',(.035,.21,.6),.45,2.6)
stem=mat('AN_Flora_Tallo',(.045,.135,.10),.8)

# Cavities are volumes united by voxel remeshing. No overlapping tube junctions.
rooms={
'A':('Umbral',(0,0,0),(4.5,6,3.5)),
'B':('Sala de las mareas',(0,17,-2),(8,8.5,5.4)),
'C':('Cámara del derrumbe',(-17,30,-4),(6.5,7,4.6)),
'D':('Jardín ciego',(17,32,-5),(7.5,6.5,5)),
'E':('Catedral sumergida',(0,51,-7),(10,12,8.5)),
'F':('Fosa del eco',(-18,57,-10),(6,7.5,4.5)),
'G':('Bóveda de las raíces',(14,69,-12),(7,9,5.5)),
'H':('Sifón de salida',(0,89,-14),(5.5,7,4.5))}
edges=[
('A','B',[(0,7,-.5),(-2.8,10,-1.4)],1.45),
('B','C',[(-10,20,-3),(-13,25,-3.6)],1.3),
('B','D',[(10,19,-2.8),(14,24,-4)],1.55),
('C','E',[(-17,40,-5.5),(-10,44,-6.5)],1.35),
('D','E',[(17,43,-6),(11,47,-6.5)],2.0),
('C','F',[(-25,37,-6),(-26,47,-8.5)],1.45),
('E','F',[(-10,54,-8.5)],1.25),
('E','G',[(8,60,-9),(12,61,-11)],1.25),
('F','H',[(-17,69,-11),(-10,77,-13),(-8,86,-14)],1.6),
('G','H',[(14,80,-13),(7,85,-14)],1.65),
('D','X1',[(27,33,-6),(30,38,-6.5)],1.5),
('F','X2',[(-27,60,-11),(-29,66,-12)],1.25),
('G','X3',[(25,66,-12),(29,71,-13)],1.35)]
dead={'X1':(30,40,-6.5),'X2':(-29,68,-12),'X3':(30,73,-13)}
volumes=[]
for key,(name,center,size) in rooms.items():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=32,location=center)
    ob=bpy.context.object; ob.name='Volumen_'+key; move(ob,geo)
    for v in ob.data.vertices:
        co=v.co.copy(); perturb=noise.noise_vector(co*3.2+Vector(center)*.13)[0]*.11
        co*=1+perturb
        co.z=max(co.z,-.79) # Broad sedimentary floor, vaulted irregular ceiling.
        v.co=Vector((co.x*size[0],co.y*size[1],co.z*size[2]))
    volumes.append(ob)
def smooth_path(points):
    pts=[Vector(p) for p in points]; out=[]
    for i in range(len(pts)-1):
        p0=pts[max(i-1,0)]; p1=pts[i]; p2=pts[i+1]; p3=pts[min(i+2,len(pts)-1)]
        count=max(5,math.ceil((p2-p1).length/.45))
        for k in range(count):
            t=k/count
            out.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
    return out+[pts[-1]]
routes=[]
for index,(a,b,mids,rad) in enumerate(edges):
    coords=[rooms[a][1]]+mids+[rooms[b][1] if b in rooms else dead[b]]
    pts=smooth_path(coords); routes.append({'from':a,'to':b,'radius':rad,'points':[list(p) for p in pts]})
    vs=[]; fs=[]; sides=32
    for i,center in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        right=tangent.cross(Vector((0,0,1))).normalized(); up=right.cross(tangent).normalized()
        radius=rad*(1+.11*math.sin(i*.21+index)+.06*math.cos(i*.51))
        for j in range(sides):
            angle=math.tau*j/sides; var=.09*math.sin(j*3.1+i*.23)
            vs.append(center+right*math.cos(angle)*(radius+var)+up*max(-.8,math.sin(angle))*(radius*1.18+var))
    for i in range(len(pts)-1):
        for j in range(sides):
            a0=i*sides+j; b0=i*sides+(j+1)%sides
            fs.append((a0,a0+sides,b0+sides,b0))
    fs += [tuple(range(sides)),tuple((len(pts)-1)*sides+j for j in reversed(range(sides)))]
    ob=mesh('Volumen_Galeria_%02d'%index,vs,fs); volumes.append(ob)
for name,center in dead.items():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=center)
    ob=bpy.context.object;move(ob,geo);ob.name='Volumen_Fondo_'+name
    for v in ob.data.vertices:v.co*=2.1
    volumes.append(ob)
bpy.ops.object.select_all(action='DESELECT')
for ob in volumes: ob.select_set(True)
bpy.context.view_layer.objects.active=volumes[0]; bpy.ops.object.join(); cave=bpy.context.object; cave.name='Caverna_Laberinto_Continua'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
print('UNION_VOLUME',len(cave.data.vertices),flush=True)
cave.data.remesh_voxel_size=.19; cave.data.remesh_voxel_adaptivity=0
bpy.ops.object.voxel_remesh()
smooth=cave.modifiers.new('Erosion de uniones','SMOOTH'); smooth.factor=1.15; smooth.iterations=4
bpy.ops.object.modifier_apply(modifier=smooth.name)
for name,scale,strength,depth in [('Estratos erosionados',1.7,1.05,2),('Roca quebrada',.42,.22,1)]:
    tex=bpy.data.textures.new(name,'CLOUDS'); tex.noise_scale=scale; tex.noise_depth=depth; tex.noise_type='HARD_NOISE'
    dis=cave.modifiers.new(name,'DISPLACE'); dis.texture=tex; dis.strength=strength; dis.mid_level=.5; dis.texture_coords='GLOBAL'
    bpy.ops.object.modifier_apply(modifier=dis.name)
bm=bmesh.new(); bm.from_mesh(cave.data)
# Open the entrance and exit, then turn the cavity surface inward.
remove=[v for v in bm.verts if v.co.y < -5.25 or v.co.y>94.8]
bmesh.ops.delete(bm,geom=remove,context='VERTS'); bmesh.ops.reverse_faces(bm,faces=list(bm.faces)); bm.to_mesh(cave.data); bm.free()
cave.data.materials.append(rock)
for p0 in cave.data.polygons: p0.use_smooth=True
print('CAVITY_READY',len(cave.data.vertices),flush=True)
bpy.context.view_layer.update()
bvh=BVHTree.FromObject(cave,bpy.context.evaluated_depsgraph_get())

def hit_floor(x,y,z):
    result=bvh.ray_cast(Vector((x,y,z)),Vector((0,0,-1)),40)
    return result[0]
def pillar(name,origin,height,radius):
    vs=[]; fs=[]; rings=18; sides=20
    for i in range(rings):
        t=i/(rings-1); r=radius*(.44+1.0*abs(2*t-1)**1.6)
        for j in range(sides):
            ang=math.tau*j/sides; rr=r*(1+.25*math.sin(j*2.3+i*.7)+.12*math.sin(i*.8))
            vs.append(Vector(origin)+Vector((rr*math.cos(ang)+.55*math.sin(t*4+origin.x),rr*math.sin(ang)+.35*math.sin(t*5+origin.y),height*t)))
    for i in range(rings-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides; fs.append((a,b,b+sides,a+sides))
    ob=mesh(name,vs,fs,details,rock)
    for p0 in ob.data.polygons:p0.use_smooth=True
    return ob
for x,y,r in [(0,47,1.15),(0,56.5,1.45),(-5.5,56,.7),(3,14.5,1.1),(10.5,70,.7)]:
    key='B' if y<25 else 'E' if y<60 else 'G'; z=rooms[key][1][2]
    low=hit_floor(x,y,z); high=bvh.ray_cast(Vector((x,y,z)),Vector((0,0,1)),30)[0]
    if low and high: pillar('Columna_'+key+'_'+str(y),low-Vector((0,0,.3)),high.z-low.z+.6,r)

# Rounded fractured talus, aggregated in chamber margins and collapse piles.
rockobs=[]
for key,(name,center,size) in rooms.items():
    count=85 if key in ('C','F') else 36
    for k in range(count):
        angle=random.uniform(0,math.tau); dist=random.uniform(.48,.94)
        x=center[0]+math.cos(angle)*size[0]*dist; y=center[1]+math.sin(angle)*size[1]*dist
        pos=hit_floor(x,y,center[2]+1)
        if pos is None: continue
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=pos)
        ob=bpy.context.object; move(ob,details); ob.name='Derrumbe_'+key
        scale=random.uniform(.16,.7) if key not in ('C','F') else random.uniform(.25,1.2)
        for v in ob.data.vertices:
            factor=1+noise.noise_vector(v.co*3+Vector((k,0,0)))[0]*.25
            v.co=Vector((v.co.x*scale*1.2,v.co.y*scale,v.co.z*scale*.7))*factor
        ob.rotation_euler=(random.uniform(-.3,.3),random.uniform(-.3,.3),random.uniform(0,6.28))
        ob.data.materials.append(rock)
        for p0 in ob.data.polygons:p0.use_smooth=True
        rockobs.append(ob)
if rockobs:
    bpy.ops.object.select_all(action='DESELECT')
    for o in rockobs:o.select_set(True)
    bpy.context.view_layer.objects.active=rockobs[0]; bpy.ops.object.join(); bpy.context.object.name='Derrumbes_y_cantos'

# Tapered, eroded speleothems grow from the roof and terminate in curved tips.
vs=[]; fs=[]
for key,(name,center,size) in rooms.items():
    for k in range(45 if key=='E' else 22):
        x=center[0]+random.uniform(-.78,.78)*size[0]; y=center[1]+random.uniform(-.78,.78)*size[1]
        anchor=bvh.ray_cast(Vector((x,y,center[2])),Vector((0,0,1)),30)[0]
        if anchor is None: continue
        length=random.uniform(.5,2.7) if key=='E' else random.uniform(.3,1.5)
        length=min(length,max(.2,anchor.z-center[2]-1.25)); radius=random.uniform(.12,.38)
        off=len(vs); rings=9; sides=12
        drift=Vector((random.uniform(-.3,.3),random.uniform(-.3,.3),0))
        for i in range(rings):
            t=i/(rings-1); r=radius*(1-t)**1.4+.009
            for j in range(sides):
                a=math.tau*j/sides; rr=r*(1+.15*math.sin(j*2+i*.9))
                vs.append(anchor+Vector((rr*math.cos(a),rr*math.sin(a),.1-length*t))+drift*t*t)
        for i in range(rings-1):
            for j in range(sides):
                a=off+i*sides+j;b=off+i*sides+(j+1)%sides;fs.append((a,a+sides,b+sides,b))
stal=mesh('Estalactitas_erosionadas',vs,fs,details,rock)
for p0 in stal.data.polygons:p0.use_smooth=True

# Reusable bioluminescent polyp geometry, merged into two material batches.
bulbs={0:([],[]),1:([],[])}; stems_v=[]; stems_f=[]
def polyp(pos,h,typ):
    top=pos+Vector((random.uniform(-.06,.06),random.uniform(-.06,.06),h))
    q=len(stems_v)
    for z,r in [(pos,.009),(top,.005)]:
        for j in range(5):stems_v.append(z+Vector((r*math.cos(j*math.tau/5),r*math.sin(j*math.tau/5),0)))
    for j in range(5): stems_f.append((q+j,q+(j+1)%5,q+5+(j+1)%5,q+5+j))
    v,f=bulbs[typ]; off=len(v); r=random.uniform(.025,.065)
    for i in range(5):
        phi=math.pi*i/4
        for j in range(8):
            a=math.tau*j/8; v.append(top+Vector((r*math.sin(phi)*math.cos(a),r*math.sin(phi)*math.sin(a),r*.65*math.cos(phi))))
    for i in range(4):
        for j in range(8):a=off+i*8+j;b=off+i*8+(j+1)%8;f.append((a,b,b+8,a+8))
def light(name,pos,color,power,radius=1):
    data=bpy.data.lights.new(name,'POINT'); data.color=color;data.energy=power;data.shadow_soft_size=radius
    o=bpy.data.objects.new(name,data);lights.objects.link(o);o.location=pos
colonies=[]
for key,(name,center,size) in rooms.items():
    for j in range(6 if key=='E' else 4):
        a=random.uniform(0,math.tau); pos=hit_floor(center[0]+math.cos(a)*size[0]*.64,center[1]+math.sin(a)*size[1]*.64,center[2])
        if pos is not None:colonies.append((pos,j%2))
for route in routes:
    pts=[Vector(v) for v in route['points']]
    for i in range(12,len(pts)-12,22):
        tangent=(pts[i+1]-pts[i-1]).normalized(); r=tangent.cross(Vector((0,0,1))).normalized()
        p0=pts[i]+r*route['radius']*.55; pos=hit_floor(p0.x,p0.y,p0.z)
        if pos is not None:colonies.append((pos,0))
for idx,(pos,typ) in enumerate(colonies):
    for j in range(random.randint(18,35)):
        x=pos.x+random.gauss(0,.28);y=pos.y+random.gauss(0,.20); ground=hit_floor(x,y,pos.z+1)
        if ground is not None and abs(ground.z-pos.z)<.7:polyp(ground,random.uniform(.08,.26),typ)
    light('Flora_Luz_%03d'%idx,pos+Vector((0,0,.45)),(.12,.7,.47) if typ==0 else (.09,.3,.75),random.uniform(12,24),.65)
for typ,(v,f) in bulbs.items():
    o=mesh('Flora_Colonias_'+str(typ),v,f,flora,cyan if typ==0 else blue)
    for p0 in o.data.polygons:p0.use_smooth=True
mesh('Flora_Tallos',stems_v,stems_f,flora,stem)

# Broad, dim light pools reveal chamber scale. Exported geometry contains no lights.
for key,(name,center,size) in rooms.items():
    light('Ambiente_'+key,Vector(center)+Vector((0,0,1.3)),(.31,.49,.55),650 if key=='E' else 260,3.5)
    light('Rebote_'+key,Vector(center)+Vector((-size[0]*.4,size[1]*.3,0)),(.55,.53,.38),220 if key=='E' else 90,2.5)
    o=bpy.data.objects.new('Sala_'+key+'_'+name,None);refs.objects.link(o);o.location=center;o.empty_display_size=.35
for idx,route in enumerate(routes):
    cv=bpy.data.curves.new('Ruta_'+route['from']+'_'+route['to'],'CURVE');cv.dimensions='3D';sp=cv.splines.new('POLY');sp.points.add(len(route['points'])-1)
    for p0,co in zip(sp.points,route['points']):p0.co=(*co,1)
    ob=bpy.data.objects.new(cv.name,cv);refs.objects.link(ob);ob.hide_render=True

scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1440;scene.render.resolution_y=960;scene.render.resolution_percentage=75
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.008,.019,.025,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.12
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=1.15
def cam(name,pos,target,lens=23):
    data=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,data);refs.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();data.lens=lens;data.clip_start=.06;return o
gallery=routes[3]['points']; gi=len(gallery)//2
cameras=[cam('01_Catedral',(-4,44,-10),(0,55,-9.6),18),cam('02_Bifurcacion',(0,12,-4),(0,21,-4),18),cam('03_Galeria',gallery[gi],gallery[min(gi+14,len(gallery)-1)],22)]
scene.camera=cameras[0]
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');cave.select_set(True);bpy.context.view_layer.objects.active=cave
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Anadromo_Laberinto_PC.blend'))
export=[o for o in list(geo.objects)+list(details.objects)+list(flora.objects)+list(refs.objects) if o.type in ('MESH','EMPTY')]
bpy.ops.object.select_all(action='DESELECT')
for o in export:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Anadromo_Laberinto_PC.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=True,axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False)
data={'rooms':rooms,'routes':routes,'dead_ends':dead,'units':'meters','design':'Connected cavity union, cycles and dead ends; wide rooms and narrow galleries.'}
json.dump(data,open(os.path.join(OUT,'distribucion.json'),'w'),indent=2)
print('SAVED_EXPORT',flush=True)
for camera in cameras:
    scene.camera=camera;scene.render.filepath=os.path.join(OUT,camera.name+'.png');bpy.ops.render.render(write_still=True);print('RENDERED',camera.name,flush=True)
print('DONE',flush=True)

