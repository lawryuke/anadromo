from pathlib import Path
import shutil
root = Path(__file__).resolve().parents[2]
log = (root/'tools/validation/terrain-import.log').read_text(encoding='utf-8-sig')
assert 'CONVERSION PASS: New Terrain; max height error=0;' in log
assert 'CONVERSION PASS: New Terrain 1; max height error=0;' in log
assert 'Application will terminate with return code 0' in log
for name in ['New Terrain.asset', 'New Terrain 1.asset']:
    source = root/'tools/terrain-rebuild/Assets/Converted'/name
    destination = root/'src/anadromo/Assets'/name
    backup = root/'tools/desktop-backup'/name
    if not backup.exists():
        shutil.copy2(destination, backup)
        shutil.copy2(str(destination)+'.meta', str(backup)+'.meta')
    shutil.copy2(source, destination)
    print('Installed compatible terrain, original GUID retained:', name)
(root/'tools/validation/ocean-environment.request').write_text('validate\n')
