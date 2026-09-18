"""Validate tutorial blockout removal and authored portal/dodge doors."""
from pathlib import Path
import re
scene = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity').read_text(encoding='utf-8-sig')
b = {m[1]: m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', scene)}
assert '  gates: []' in b['8100000003']
for go in ['8100000005', '8100000014', '8100000023', '8100000032']:
    assert 'm_IsActive: 0' in b[go]
    for c in re.findall(r'component: {fileID: (\d+)}', b[go]):
        if b[c].startswith(('--- !u!212 ', '--- !u!61 ')): assert 'm_Enabled: 0' in b[c]
for i in ['8000000485', '8000000490', '8100000043', '8100000046']:
    assert 'm_Enabled: 0' in b[i]
assert scene.count('m_SourcePrefab: {fileID: 100100000, guid: 14f7910c6104c084b8cbbf8d25e018de') == 2
for key, value in [('m_LocalPosition.y', '45.6'), ('doorType', '2'), ('isPermanent', '0')]:
    assert f'propertyPath: {key}\n      value: {value}\n' in b['9000000000']
assert 'bossPortalDoor: {fileID: 9000000002}' in b['8100000003']
assert 'portalDoorChest: {fileID: 8400000034}' in b['8100000003']
assert 'm_TransformParent: {fileID: 8000000002}' in b['9000000000']
assert 'dodgeEntranceDoor: {fileID: 9200000002}' in b['8100000003']
assert '  - {fileID: 9200000001}' in b['8000000002']
for key, value in [('m_LocalPosition.y', '-0.5'), ('m_LocalScale.x', '1'), ('m_LocalScale.y', '1'), ('doorType', '2'), ('isPermanent', '0')]:
    assert f'propertyPath: {key}\n      value: {value}\n' in b['9200000000']
assert 'm_TransformParent: {fileID: 8000000002}' in b['9200000000']
assert len(b) == len(re.findall(r'^--- !u!\d+ &', scene, re.M))
print('TUTORIAL_DOOR_SCENE_PASS: portal chest wiring and locked nonpersistent dodge entrance door at (0,-0.5), scale one.')
