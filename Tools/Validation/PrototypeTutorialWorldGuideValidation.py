"""Validate authored tutorial world-guide wiring and preservation of painted map data."""
from pathlib import Path
import re

def parse(path):
    text = path.read_text(encoding='utf-8-sig')
    matches = list(re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', text))
    blocks = {m[1]: m[0] for m in matches}
    assert len(matches) == len(blocks)
    assert all(i == '0' or i in blocks for i in re.findall(r'\{fileID: (\d+)\}', text)), 'Dangling scene reference'
    return blocks
b = parse(Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity'))
expected = {'AttackEntranceGuide': (4, 2, 0), 'MovementFloorGuide': (0, 0, 0),
            'Skill1FloorGuide': (7, 3, 1), 'Skill2FloorGuide': (8, 3, 1),
            'ChestFloorGuide': (5, 4, 0), 'PortalFloorGuide': (5, 5, 0)}
for name, (action, stage, animated) in expected.items():
    obj = next(t for t in b.values() if f'  m_Name: {name}\n' in t)
    components = re.findall(r'component: \{fileID: (\d+)\}', obj)
    guide = next(b[i] for i in components if 'guid: 19b824b7b6e54d93a1c7d06e5a7230d1' in b[i])
    for key, value in [('action', action), ('visibleStage', stage), ('animate', animated)]:
        assert f'  {key}: {value}\n' in guide
    assert 'tutorial: {fileID: 8100000003}' in guide
    content = re.search(r'presentationRoot: {fileID: (\d+)}', guide)[1]
    assert content in b
    labels = [b[i] for tr in re.findall(r'^  - {fileID: (\d+)}', b[content], re.M)
              for i in re.findall(r'component: {fileID: (\d+)}', b[re.search(r'm_GameObject: {fileID: (\d+)}', b[tr])[1]])
              if '  m_text:' in b[i]]
    assert len(labels) == (5 if name == 'MovementFloorGuide' else 2)
    assert all('guid: bc5dc7440ad16f045b3a71f996721562' in t for t in labels)
assert '8200000170' not in b and 'visibleStage:' not in b['8200000167'], 'Background removal or persistent dash changed'
for i in range(8200000109, 8200000149, 5): assert 'm_IsActive: 0' in b[str(i)]
nav = next(t for t in b.values() if 'guid: 24454d6c4aa547239b7ded1b4a9bcc31' in t)
for field in ['worldChest', 'worldArrow']:
    assert re.search(field + r': {fileID: (\d+)}', nav)[1] in b
assert any('guid: 7776d4bd1e804757a06eeb2eb256a829' in t for t in b.values())
movement = b['8800000003']
bindings = [b[i] for i in re.findall(r'component: {fileID: (\d+)}', movement)
            if 'guid: 19b824b7b6e54d93a1c7d06e5a7230d1' in b[i]]
assert {int(re.search(r'  action: (\d+)', t)[1]) for t in bindings} == {0, 1, 2, 3}
for guide in bindings:
    action = int(re.search(r'  action: (\d+)', guide)[1])
    glyph = b[re.search(r'  glyph: {fileID: (\d+)}', guide)[1]]
    obj = b[re.search(r'm_GameObject: {fileID: (\d+)}', glyph)[1]]
    tr = b[re.search(r'component: {fileID: (\d+)}', obj)[1]]
    x, y = map(float, re.search(r'm_LocalPosition: {x: ([\d.-]+), y: ([\d.-]+)', tr).groups())
    assert (x, y) == {0: (-3, .35), 2: (-3.6, -.3), 1: (-3, -.3), 3: (-2.4, -.3)}[action]
    assert 'glyphSize: 0.55' in guide
assert 'm_text: "를 눌러 대쉬."' in b['8200000166']
snapshot = Path('Temp/tutorial-before-stage-guides.unity')
# Raw tile preservation applies before the approved grid-normalization migration.
# After migration use TutorialGridNormalizationSceneValidation.py for expanded coverage.
if snapshot.exists() and 'm_LocalScale: {x: 0.5, y: 0.5, z: 1}' in b['8600000016']:
    old = parse(snapshot)
    for i, t in old.items():
        if t.startswith('--- !u!1839735485 '): assert b[i] == t, 'Tilemap paint changed'
print('WORLD_GUIDES_PASS: six stage/action bindings, animated skills, BlackOutline text, no attack background, persistent dash, chest outline/Hub arrow, tilemap references intact (normalized paint coverage checked separately).')
