from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
project = root/'src/anadromo'
report = []
for scene in ['Act1_OpenOcean', 'Blockout_Test']:
    text = (project/f'Assets/_Project/Scenes/{scene}.unity').read_text(encoding='utf-8-sig')
    blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)
    for component in ['TrackedPoseDriver', 'SwimLocomotion', 'XROrigin', 'InputActionManager']:
        matches = [b for b in blocks if 'm_EditorClassIdentifier:' in b and component in b]
        assert matches and all('  m_Enabled: 0' in b for b in matches), (scene, component)
        report.append(f'PASS: {scene}: {component} disabled')
    controller = next(b for b in blocks if 'Assembly-CSharp::Anadromo.Mechanics.DebugVuelo' in b)
    assert '  m_Enabled: 1' in controller and 'cameraTransform: {fileID: 133088352}' in controller
    report.append(f'PASS: {scene}: desktop controller enabled and camera assigned')
    assert 'm_AllowXRRendering: 1' not in text
xr = (project/'Assets/XR/XRGeneralSettingsPerBuildTarget.asset').read_text()
assert 'm_InitManagerOnStart: 1' not in xr
report.append('PASS: XR automatic startup disabled for all configured platforms')
(root/'tools/validation/desktop-configuration.txt').write_text('\n'.join(report)+'\n')
print('\n'.join(report))
rsp = (project/'Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp-Editor.rsp').read_text(encoding='utf-8-sig')
rsp = re.sub(r'^-out:.*$', '-out:"../../tools/validation/desktop-editor.dll"', rsp, flags=re.M)
rsp = re.sub(r'^-refout:.*$', '-refout:"../../tools/validation/desktop-editor.ref.dll"', rsp, flags=re.M)
if 'Assets/Editor/DesktopPlayValidation.cs' not in rsp:
    rsp += '\n"Assets/Editor/DesktopPlayValidation.cs"\n'
(root/'tools/validation/desktop-editor.rsp').write_text(rsp)
