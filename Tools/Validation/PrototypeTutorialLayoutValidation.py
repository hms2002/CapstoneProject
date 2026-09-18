"""Validate the authored prototype map without modifying Unity assets.

Run from the project root: python Tools/Validation/PrototypeTutorialLayoutValidation.py
Checks walkable goal access, gate bypasses, enclosure and entrance-guide references.
This geometric check does not replace Unity physics/Play Mode verification.
"""
import json
import re
from collections import deque
from pathlib import Path

scene = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
text = scene.read_text(encoding='utf-8-sig')
matches = list(re.finditer(r'(?ms)^--- !u!(\d+) &(\d+).*?(?=^--- !u!|\Z)', text))
blocks = {int(m[2]): m[0] for m in matches}
assert len(matches) == len(blocks), 'Duplicate serialized IDs'
assert all(int(i) == 0 or int(i) in blocks for i in re.findall(r'\{fileID: (\d+)\}', text)), 'Dangling local reference'

def vector(block, key):
    m = re.search(rf'{key}: \{{x: ([^,]+), y: ([^,}}]+)', block)
    return float(m[1]), float(m[2])

def world(transform):
    b = blocks[transform]
    x, y = vector(b, 'm_LocalPosition')
    sx, sy = vector(b, 'm_LocalScale')
    parent = int(re.search(r'm_Father: \{fileID: (\d+)\}', b)[1])
    if parent:
        px, py, psx, psy = world(parent)
        return px + x * psx, py + y * psy, sx * psx, sy * psy
    return x, y, sx, sy

objects = {}
for m in matches:
    if m[1] != '1' or ' stripped' in m[0].splitlines()[0]:
        continue
    b = m[0]
    raw = re.search(r'^  m_Name: (.*)', b, re.M)[1]
    name = json.loads(raw) if raw.startswith('"') else raw
    transform = int(re.search(r'component: \{fileID: (\d+)\}', b)[1])
    objects.setdefault(name, []).append((int(m[2]), transform))

def rectangle(transform):
    x, y, sx, sy = world(transform)
    return x - abs(sx) / 2, x + abs(sx) / 2, y - abs(sy) / 2, y + abs(sy) / 2

floors = [(name, rectangle(entries[0][1])) for name, entries in objects.items() if name.endswith('_Floor')]
walls = [rectangle(t) for _, t in objects['OuterWall']]
gates = [rectangle(objects[f'MissionGate_{i:02d}'][0][1]) for i in range(1, 5)]
spawn = world(objects['TutorialStart'][0][1])[:2]
director = next(b for b in blocks.values() if '  movementGoal:' in b and '  dashGoal:' in b)
goals = {key: world(int(re.search(rf'  {key}: \{{fileID: (\d+)\}}', director)[1]))[:2]
         for key in ['movementGoal', 'dashGoal']}

# Conservative 0.4-unit body radius, quarter-unit grid. Include exterior space to catch wall leaks.
radius = .4
def cell(point):
    return tuple(round(v * 4) for v in point)

def reachable(stage):
    barriers = walls + [g for i, g in enumerate(gates) if (stage < 5 if i == 3 else stage <= i)]
    def blocked(c):
        x, y = c[0] / 4, c[1] / 4
        return any(a - radius <= x <= b + radius and d - radius <= y <= e + radius
                   for a, b, d, e in barriers)
    start = cell(spawn)
    assert not blocked(start), 'Spawn inside a collider'
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        for c in [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)]:
            if c in seen or not (-60 <= c[0] <= 68 and -48 <= c[1] <= 244) or blocked(c):
                continue
            seen.add(c)
            queue.append(c)
    assert all(any(a <= x / 4 <= b and d <= y / 4 <= e for _, (a, b, d, e) in floors)
               for x, y in seen), f'Exterior wall leak in stage {stage}'
    return seen

movement = reachable(0)
dash = reachable(1)
combat = reachable(2)
completed = reachable(4)
assert cell(goals['movementGoal']) in movement, 'Movement goal unreachable'
assert cell(goals['dashGoal']) not in movement, 'Movement gate bypass'
assert cell(goals['dashGoal']) in dash, 'Dash goal blocked by its own gate'
assert cell((0, 25.5)) not in dash, 'Dash gate bypass'
assert all(cell(p) in combat for p in [(0, 25.5), (-5, 31), (7, 31), (0, 35.5)]), 'Shared room stations disconnected'
assert cell((0, 42)) not in completed, 'Chest completion gate bypass'
assert cell((0, 51)) in reachable(5), 'Final boss portal unreachable after chest confirmation'
portal = [b for b in blocks.values() if 'guid: 7339d19fe1a9c3d4c842710e0be44fb4' in b]
assert len(portal) == 1 and 'targetSceneName: DarkLord_Tutorial' in portal[0]

guides = [b for b in blocks.values() if 'guid: 19b824b7b6e54d93a1c7d06e5a7230d1' in b]
assert sorted(int(re.search(r'  action: (\d+)', b)[1]) for b in guides) == [4, 6]
for b in guides:
    for key in ['glyph', 'fallbackLabel']:
        assert int(re.search(rf'  {key}: \{{fileID: (\d+)\}}', b)[1]) in blocks
assert '길게 눌러 연속 공격' in ''.join(json.loads(s) for s in re.findall(r'^  m_text: (".*")$', text, re.M))
navigation = [b for b in blocks.values() if 'guid: 24454d6c4aa547239b7ded1b4a9bcc31' in b]
assert len(navigation) == 1, 'Expected one tutorial-only chest guide'
guide = navigation[0]
for key in ['targetChest', 'tutorial', 'inputShield', 'rightClickGlyph', 'instruction']:
    assert int(re.search(rf'  {key}: \{{fileID: (\d+)\}}', guide)[1]) in blocks
assert len(re.findall(r'^  - \{fileID:', guide, re.M)) == 4, 'Spotlight must have four authored shades'
shield_id = objects['InputShield_RightClickFirstSlotOnly'][0][0]
assert 'm_IsActive: 0' in blocks[shield_id], 'Tutorial shield must start hidden'
shield_images = [b for b in blocks.values() if f'm_GameObject: {{fileID: {shield_id}}}' in b
                 and 'guid: fe87c0e1cc204ed48ad3b37840f39efc' in b]
assert len(shield_images) == 1 and 'm_RaycastTarget: 1' in shield_images[0]
assert 'm_Color: {r: 0, g: 0, b: 0, a: 0}' in shield_images[0], 'Input shield must not tint the spotlight hole'
print(f'LAYOUT_PASS: {len(blocks)} unique blocks; entrance glyphs and chest guidance wired; goals accessible; gates unskippable; enclosed connected map.')

# A read-only verification snapshot for optional map preview tools.
Path('Temp').mkdir(exist_ok=True)
Path('Temp/tutorial-layout-preview.json').write_text(json.dumps({
    'floors': floors, 'walls': walls, 'gates': gates, 'spawn': spawn, 'goals': goals,
    'signs': {name: world(objects[name][0][1])[:2] for name in ['DashEntranceGuide', 'AttackEntranceGuide']}
}), encoding='utf-8')
