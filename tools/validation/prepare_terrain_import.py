from pathlib import Path
import shutil
root = Path(__file__).resolve().parents[2]
project = root/'tools/terrain-rebuild'
for folder in ['Assets/Editor', 'Packages', 'ProjectSettings', 'Export']:
    (project/folder).mkdir(parents=True, exist_ok=True)
(project/'Packages/manifest.json').write_text('{"dependencies":{"com.unity.modules.terrain":"1.0.0","com.unity.modules.terrainphysics":"1.0.0","com.unity.modules.physics":"1.0.0"}}')
shutil.copy2(root/'src/anadromo/ProjectSettings/ProjectVersion.txt', project/'ProjectSettings/ProjectVersion.txt')
shutil.copy2(root/'src/anadromo/ProjectSettings/EditorSettings.asset', project/'ProjectSettings/EditorSettings.asset')
shutil.copy2(root/'tools/validation/TerrainCompatibility.cs', project/'Assets/Editor/TerrainCompatibility.cs')
for name in ['New Terrain.bin', 'New Terrain 1.bin']:
    shutil.copy2(root/'tools/terrain-conversion/Export'/name, project/'Export'/name)
print(project)
