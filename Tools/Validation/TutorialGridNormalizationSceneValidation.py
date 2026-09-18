"""Validate normalized tutorial grid while preserving half-unit painted coverage."""
from pathlib import Path
import re

def parse(p): return {m[1]:m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)',p.read_text(encoding='utf-8-sig'))}
def cells(t):
 rows=re.findall(r'(?ms)^  - first:.*?(?=^  - first:|^  m_AnimatedTiles:|^  m_TileAssetArray:)',t)
 return {tuple(map(int,re.search(r'first: {x: (-?\d+), y: (-?\d+)',r).groups())):tuple(int(re.search(k+r': (\d+)',r)[1]) for k in ['m_TileIndex','m_TileSpriteIndex','m_TileColorIndex','m_TileMatrixIndex']) for r in rows}
b=parse(Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity'))
if 'm_CellSize: {x: 1, y: 1, z: 0}' in b['8600000017']:
 import runpy
 runpy.run_path('Tools/Validation/TutorialUnitGridValidation.py')
 raise SystemExit(0)
assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in b['8600000016']
assert 'm_CellSize: {x: 0.5, y: 0.5, z: 0}' in b['8600000017']
backup=Path('Temp/tutorial-before-normalization.unity')
if backup.exists():
 old=parse(backup)
 for i in ['8600000020','8600000024']:
  before=cells(old[i]);after=cells(b[i]);expanded={}
  for (x,y),value in after.items():
   size=2 if i=='8600000020' and value[3]==7 else 1
   for dx in range(size):
    for dy in range(size):
     key=(x+dx,y+dy);assert key not in expanded;expanded[key]=value[:3]
  assert expanded=={k:v[:3] for k,v in before.items()},'Paint identity/coverage changed'
 allowed={'8600000016','8600000017','8600000020','8600000024','8700000003'}
 assert set(old)==set(b)
 assert all(old[i]==b[i] for i in old if i not in allowed),'Unrelated authored object changed'
 print('EXACT_PAINT_COVERAGE_PASS: every original cell retains its tile/sprite/color identity, with identical object positions.')
print('NORMALIZED_GRID_PASS: unit Transform scale, half-unit cell spacing, same-paint ground merging.')
