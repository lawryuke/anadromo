from pathlib import Path
import shutil
root = Path(__file__).resolve().parents[2]
project = root / 'tools/terrain-conversion'
for folder in ['Assets/Editor', 'Packages', 'ProjectSettings']:
    (project/folder).mkdir(parents=True, exist_ok=True)
(project/'Packages/manifest.json').write_text('{"dependencies":{"com.unity.modules.terrain":"1.0.0","com.unity.modules.terrainphysics":"1.0.0","com.unity.modules.physics":"1.0.0"}}')
shutil.copy2(root/'tools/validation/TerrainCompatibility.cs', project/'Assets/Editor/TerrainCompatibility.cs')
source = root/'src/anadromo/Assets'
for name in ['New Terrain.asset', 'New Terrain 1.asset', 'New Terrain Layer.terrainlayer']:
    for suffix in ['', '.meta']:
        shutil.copy2(source/(name+suffix), project/'Assets'/(name+suffix))
print(project)
