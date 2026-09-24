"""Offline integrity and route-clearance checks; does not alter assets."""
raise SystemExit('La geometria escaneada requiere validacion en Unity: Anadromo > Escenario 4 > Validar escena (-executeMethod Scenario4Validation.Run).')

from pathlib import Path
import re
import struct
import math

root=Path(__file__).resolve().parents[1]
scene=(root/'Assets/_Project/Scenes/Escenario4.unity').read_text(encoding='utf-8-sig')
matches=list(re.finditer(r'^--- !u!(\d+) &(-?\d+).*?(?=^--- !u!|\Z)',scene,re.M|re.S))
blocks={int(m[2]):(int(m[1]),m[0]) for m in matches}
assert len(blocks)==len(matches),'Duplicate scene IDs'
for ident,(kind,block) in blocks.items():
    for ref in re.finditer(r'\{fileID: (-?\d+)([^}]*?)\}',block,re.S):
        target=int(ref[1])
        assert target==0 or 'guid:' in ref[2] or target in blocks, f'Dangling local reference {ident} -> {target}'

def field(text,name): return re.search(r'^  '+name+r': (.*)$',text,re.M)[1]
def vector(text,name): return tuple(float(n) for n in re.findall(r'[xyzw]: ([-\d.eE+]+)',field(text,name)))
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def rotate(p,q): return add(p,add(mul(cross(q[:3],p),2*q[3]),mul(cross(q[:3],cross(q[:3],p)),2)))

transforms={int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]):b for k,b in blocks.values() if k==4 and 'stripped' not in b.splitlines()[0]}
names={i:field(b,'m_Name') for i,(k,b) in blocks.items() if k==1 and 'stripped' not in b.splitlines()[0]}
route=sorted((name,vector(transforms[go],'m_LocalPosition')) for go,name in names.items() if re.fullmatch(r'Ruta \d+',name))
assert len(route)==25
mesh=(root/'Assets/_Project/Art/Models/Escenario4_Basalt.asset').read_text()
data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',mesh)[1])
verts=[struct.unpack_from('<fff',data,i) for i in range(0,len(data),48)]
assert len(verts)==int(re.search(r'm_VertexCount: (\d+)',mesh)[1])
indices=bytes.fromhex(re.search(r'm_IndexBuffer: (\w+)',mesh)[1])
assert len(indices)==len(verts)*4

def intersects(origin,direction,tri):
    a,b,c=tri; e1=sub(b,a); e2=sub(c,a); h=cross(direction,e2); det=dot(e1,h)
    if abs(det)<1e-8:return False
    f=1/det; s=sub(origin,a); u=f*dot(s,h)
    if u<0 or u>1:return False
    q=cross(s,e1); v=f*dot(direction,q)
    if v<0 or u+v>1:return False
    t=f*dot(e2,q)
    return 0<=t<=1

geometry=[]
for _,(kind,b) in blocks.items():
    if kind!=64:continue
    go=int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]); tr=transforms[go]
    scale=vector(tr,'m_LocalScale'); pos=vector(tr,'m_LocalPosition'); rot=vector(tr,'m_LocalRotation')
    points=[add(pos,rotate(tuple(v[i]*scale[i] for i in range(3)),rot)) for v in verts]
    lower=tuple(min(p[i] for p in points) for i in range(3))
    upper=tuple(max(p[i] for p in points) for i in range(3))
    geometry.append((names[go],points,lower,upper))
offsets=[(0,0,0),(.4,0,0),(-.4,0,0),(0,.4,0),(0,-.4,0),(0,0,.4),(0,0,-.4)]
failures=set()
for i in range(1,len(route)):
    a=route[i-1][1]; delta=sub(route[i][1],a)
    for offset in offsets:
        start=add(a,offset); end=add(start,delta)
        for name,points,lower,upper in geometry:
            if any(max(start[k],end[k])<lower[k] or min(start[k],end[k])>upper[k] for k in range(3)):continue
            if any(intersects(start,delta,points[j:j+3]) for j in range(0,len(points),3)):
                failures.add(f'Route segment {i} blocked by {name}')
assert not failures,'\n'.join(sorted(failures))
assert all(route[i][1][1]>route[i-1][1][1] for i in range(1,len(route)))
print(f'PASS: {len(blocks)} scene records; all local references resolve; mesh buffers valid; 24 route segments clear at 7 offsets against {len(geometry)} rock colliders.')
