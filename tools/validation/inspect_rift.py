from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]
text=(root/'src/anadromo/Assets/_Project/Scenes/Blockout_Test.unity').read_text(encoding='utf-8-sig')
blocks=re.split(r'(?=^--- !u!)', text, flags=re.M)
objects={}
transforms={}
for b in blocks:
    header=re.match(r'--- !u!(\d+) &(\d+)',b)
    if not header: continue
    kind,ident=header.groups()
    if kind=='1': objects[ident]=re.search(r'  m_Name: (.*)',b).group(1)
    if kind=='4': transforms[ident]=b
for ident,b in transforms.items():
    go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b).group(1)
    name=objects.get(go,'?')
    if name.startswith('Rock_') or name.startswith('Krill'): continue
    print(name, 'transform='+ident)
    print('\n'.join(l.strip() for l in b.splitlines() if any(k in l for k in ['m_LocalPosition:','m_LocalScale:','m_Father:'])))
    for c in blocks:
        if f'm_GameObject: {{fileID: {go}}}' in c and ('MeshFilter:' in c or 'MeshRenderer:' in c):
            print('\n'.join(l.strip() for l in c.splitlines() if 'guid:' in l))
