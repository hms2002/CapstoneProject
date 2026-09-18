"""One-shot migration from normalized half-unit paint to genuine unit cells."""
from pathlib import Path
import collections
import re

SCENE = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
original = SCENE.read_text(encoding='utf-8-sig')
backup = Path('Temp/tutorial-before-unit-grid-rebuild-20260918.unity')
source = backup.read_text(encoding='utf-8-sig') if backup.exists() else original
pattern = r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)'
blocks = {m[1]: m[0] for m in re.finditer(pattern, source)}
current = {m[1]: m[0] for m in re.finditer(pattern, original)}
allowed = {'8600000017', '8600000020', '8600000024', '8700000003'}
assert set(current) == set(blocks)
assert all(current[k] == blocks[k] for k in blocks if k not in allowed), 'Concurrent scene changes; inspect before rerunning'
assert 'm_CellSize: {x: 0.5, y: 0.5, z: 0}' in blocks['8600000017']
assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in blocks['8600000016']
if not backup.exists(): backup.write_text(original, encoding='utf-8')
blocks['8600000017'] = blocks['8600000017'].replace('m_CellSize: {x: 0.5, y: 0.5, z: 0}', 'm_CellSize: {x: 1, y: 1, z: 0}')

# Rebuild connected rooms on whole cells. The dodge lane is two units wide;
# both authored doors keep their existing position and width.
rooms = [(3, 13, -8, 0), (1, 3, -3, -1), (-1, 1, -3, 20),
         (-10, 10, 20, 38), (-2, 2, 38, 46), (-7, 7, 46, 56)]
ground = {(x, y) for x0, x1, y0, y1 in rooms for x in range(x0, x1) for y in range(y0, y1)}
walls = {(x+dx, y+dy) for x, y in ground for dx in [-1, 0, 1] for dy in [-1, 0, 1]} - ground

for tid in ['8600000020', '8600000024']:
    text = blocks[tid]
    rows = re.findall(r'(?ms)^  - first:.*?(?=^  - first:|^  m_AnimatedTiles:|^  m_TileAssetArray:)', text)
    groups = collections.defaultdict(list)
    templates = {}
    for row in rows:
        x, y = map(int, re.search(r'first: {x: (-?\d+), y: (-?\d+)', row).groups())
        signature = tuple(int(re.search(k + r': (\d+)', row)[1]) for k in ['m_TileIndex', 'm_TileSpriteIndex', 'm_TileColorIndex'])
        matrix = int(re.search(r'm_TileMatrixIndex: (\d+)', row)[1])
        assert matrix == 0 or (tid == '8600000020' and matrix == 7)
        size = 2 if matrix == 7 else 1
        templates[signature] = row
        for dx in range(size):
            for dy in range(size):
                groups[((x + dx) // 2, (y + dy) // 2)].append(signature)
    newrows = []
    target = ground if tid == '8600000020' else walls
    for x, y in sorted(target, key=lambda p: (p[1], p[0])):
        nearest = min(groups, key=lambda p: ((p[0]-x)**2 + (p[1]-y)**2, p[1], p[0])) if (x, y) not in groups else (x, y)
        candidates = groups[nearest]
        # Preserve the dominant authored artwork; a touched wall cell remains solid.
        signature = collections.Counter(candidates).most_common(1)[0][0]
        row = templates[signature]
        row = re.sub(r'first: {x: -?\d+, y: -?\d+, z: 0}', f'first: {{x: {x}, y: {y}, z: 0}}', row)
        row = re.sub(r'm_TileMatrixIndex: \d+', 'm_TileMatrixIndex: 0', row)
        newrows.append(row)
    text = re.sub(r'(?s)(  m_Tiles:\n).*?(?=  m_AnimatedTiles:|  m_TileAssetArray:)', lambda m: m[1] + ''.join(newrows), text, count=1)
    matrix = '  m_TileMatrixArray:\n  - serializedVersion: 2\n    m_RefCount: ' + str(len(newrows)) + '\n    m_Data:\n'
    matrix += ''.join(f'      e{r}{c}: {int(r == c)}\n' for r in range(4) for c in range(4))
    text = re.sub(r'(?ms)^  m_TileMatrixArray:.*?(?=^  \w+:)', matrix, text)
    for array, key in [('m_TileAssetArray', 'm_TileIndex'), ('m_TileSpriteArray', 'm_TileSpriteIndex'), ('m_TileColorArray', 'm_TileColorIndex')]:
        match = re.search(rf'(?ms)^  {array}:.*?(?=^  \w+:)', text)
        counts = collections.Counter(int(re.search(key + r': (\d+)', row)[1]) for row in newrows)
        indices = iter(range(10000))
        updated = re.sub(r'm_RefCount: \d+', lambda _: 'm_RefCount: ' + str(counts[next(indices)]), match[0])
        text = text[:match.start()] + updated + text[match.end():]
    xs, ys = zip(*target)
    text = re.sub(r'(?m)^  m_Origin:.*', f'  m_Origin: {{x: {min(xs)}, y: {min(ys)}, z: 0}}', text)
    text = re.sub(r'(?m)^  m_Size:.*', f'  m_Size: {{x: {max(xs)-min(xs)+1}, y: {max(ys)-min(ys)+1}, z: 1}}', text)
    blocks[tid] = text
    print(tid, len(rows), '->', len(newrows), 'unit tiles')

for field in ['m_ColliderPaths', 'm_CompositePaths']:
    blocks['8700000003'] = re.sub(rf'(?ms)^  {field}:\n.*?(?=^  \w+:)', f'  {field}: []\n', blocks['8700000003'])
assert SCENE.read_text(encoding='utf-8-sig') == original
staged = Path('Temp/tutorial-unit-grid-staged.unity')
staged.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(blocks.values()), encoding='utf-8')
staged.replace(SCENE)
