"""Check opening references and the complete authored escape route against painted floor/wall tiles."""
from pathlib import Path
import math
import re

scene = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity').read_text(encoding='utf-8-sig')
parts = list(re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', scene))
blocks = {m[1]: m[0] for m in parts}
assert len(parts) == len(blocks), 'Duplicate scene IDs'
assert all(ref == '0' or ref in blocks for ref in re.findall(r'\{fileID: (\d+)\}', scene))
owner = blocks['8100000003']
assert 'opening:\n    enabled: 1' in owner
assert 'gunnerVisual: {fileID: 9300000003}' in owner
assert 'gunnerSpeech: {fileID: 9300000002}' in owner
assert 'addedObject: {fileID: 9300000002}' in blocks['8600000001']
assert 'guid: d58f7ddaaeeb3da489b8e9fbafa66102' in blocks['9300000002']
assert 'guid: 62446638c99411c4c9669c3f3efc113d' in blocks['9300000002']
assert 'fileID: 7181906875664307922' in blocks['9300000003']
ground = {tuple(map(int, p)) for p in re.findall(r'first: {x: (-?\d+), y: (-?\d+), z: 0}', blocks['8600000020'])}
walls = {tuple(map(int, p)) for p in re.findall(r'first: {x: (-?\d+), y: (-?\d+), z: 0}', blocks['8600000024'])}
route = [(6, -2), (0, -2), (0, 21)]
for (ax, ay), (bx, by) in zip(route, route[1:]):
    steps = int(math.hypot(bx-ax, by-ay) * 20)
    for i in range(steps + 1):
        x, y = ax + (bx-ax)*i/steps, ay + (by-ay)*i/steps
        # Check a body-width corridor, including both sides of integer-cell boundaries.
        for dx in (-.25, .25):
            for dy in (-.25, .25):
                cell = math.floor(x+dx), math.floor(y+dy)
                assert cell in ground and cell not in walls, ('Escape route blocked', (x, y), cell)
for x, y in route:
    assert f'{{x: {x}, y: {y}, z: 0}}' in owner
# Runtime intentionally reads opening.gunnerStart, not the instance's edit-time transform.
assert '    escapeSpeed: 6' in owner
assert 'openingCameraPoint: {fileID: 9300000005}' in owner
assert 'm_Name: TutorialOpeningCameraPoint' in blocks['9300000004']
assert 'm_Father: {fileID: 0}' in blocks['9300000005']
assert 'fileID: 9300000005' in blocks['9223372036854775807']
print('TUTORIAL_OPENING_SCENE_PASS: runtime start/escape clearance, speech references, speed 6, authored camera anchor and root registration.')
