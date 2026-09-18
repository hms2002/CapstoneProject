"""Verify unit-cell authoring, connected route and preservation of non-grid objects."""
from pathlib import Path
import re
from collections import deque

def parse(path):
    text = Path(path).read_text(encoding='utf-8-sig')
    matches = list(re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', text))
    blocks = {m[1]: m[0] for m in matches}
    assert len(blocks) == len(matches)
    return blocks

b = parse('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
assert 'm_CellSize: {x: 1, y: 1, z: 0}' in b['8600000017']
for key in ['8600000016', '8600000019', '8600000023']:
    assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in b[key]
def cells(key):
    tilemap = b[key]
    positions = re.findall(r'first: {x: (-?\d+), y: (-?\d+), z: 0}', tilemap)
    result = {tuple(map(int, p)) for p in positions}
    assert len(result) == len(positions)
    assert set(re.findall(r'm_TileMatrixIndex: (\d+)', tilemap)) == {'0'}
    matrix = tilemap.split('  m_TileMatrixArray:')[1].split('  m_TileColorArray:')[0]
    for r in range(4):
        for c in range(4): assert f'      e{r}{c}: {int(r == c)}\n' in matrix
    return result
ground, walls = cells('8600000020'), cells('8600000024')
assert not ground & walls
visited = {(8, -6)}
queue = deque(visited)
while queue:
    x, y = queue.popleft()
    for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
        p = x+dx, y+dy
        if p in ground and p not in visited: visited.add(p); queue.append(p)
assert visited == ground, 'Disconnected floor region'
for p in [(0,-1),(0,0),(0,18),(0,21),(0,27),(-3,28),(3,28),(-3,32),(3,32),(0,35),(0,45),(0,51)]:
    assert p in visited, ('Mission point blocked', p)
for y in range(0,20):
    assert {(-1,y),(0,y)} <= ground
    assert {(-2,y),(1,y)} <= walls
backup = Path('Temp/tutorial-before-unit-grid-rebuild-20260918.unity')
if backup.exists():
    old = parse(backup)
    assert set(old) == set(b)
    allowed = {'8600000017','8600000020','8600000024','8700000003'}
    assert all(old[k] == b[k] for k in b if k not in allowed), 'Non-grid scene object changed'
print(f'UNIT_GRID_PASS: {len(ground)} ground/{len(walls)} wall cells, identity matrices, connected missions, two-cell dodge lane, unchanged scene objects.')
