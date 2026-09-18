from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
project = root / 'src/anadromo'
backup = root / 'tools/desktop-backup'
backup.mkdir(exist_ok=True)

for name in ['Act1_OpenOcean', 'Blockout_Test']:
    p = project / f'Assets/_Project/Scenes/{name}.unity'
    text = p.read_text(encoding='utf-8-sig')
    saved = backup / p.name
    if not saved.exists():
        saved.write_bytes(p.read_bytes())
    blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)
    for i, block in enumerate(blocks):
        if any(x in block for x in ['Unity.InputSystem::UnityEngine.InputSystem.XR.TrackedPoseDriver',
                'Unity.XR.Interaction.Toolkit::', 'Unity.XR.CoreUtils::Unity.XR.CoreUtils.XROrigin',
                'Assembly-CSharp::Anadromo.Locomotion.SwimLocomotion']):
            blocks[i] = block.replace('  m_Enabled: 1', '  m_Enabled: 0')
        if 'Assembly-CSharp::Anadromo.Mechanics.DebugVuelo' in block:
            blocks[i] = block.replace('  m_Enabled: 0', '  m_Enabled: 1')
        if 'Unity.RenderPipelines.Universal.Runtime::UnityEngine.Rendering.Universal.UniversalAdditionalCameraData' in block:
            blocks[i] = block.replace('  m_AllowXRRendering: 1', '  m_AllowXRRendering: 0')
    p.write_text(''.join(blocks), encoding='utf-8', newline='\n')
    print('Desktop controls:', name)

p = project / 'Assets/Settings/UniversalRenderPipelineGlobalSettings.asset'
text = p.read_text(encoding='utf-8-sig')
saved = backup / p.name
if not saved.exists():
    saved.write_bytes(p.read_bytes())
unsupported = ['WorldRenderPipelineResources', 'MipGenRenderPipelineRuntimeResources',
    'ScreenSpaceAmbientOcclusionBlueNoiseResources', 'ScreenSpaceAmbientOcclusionCoreResources',
    'ScreenSpaceReflectionPersistentResources', 'UniversalRenderPipelineFilmGrainResources']
for entry in re.findall(r'^    - rid: .*?(?=^    - rid: |\Z)', text, re.M | re.S):
    if any('class: ' + name + ',' in entry for name in unsupported):
        rid = re.search(r'rid: (-?\d+)', entry).group(1)
        text = text.replace(entry, '')
        text = re.sub(r'^      - rid: ' + rid + r'\n', '', text, flags=re.M)
        print('Removed unavailable URP resource:', rid)
p.write_text(text, encoding='utf-8', newline='\n')
