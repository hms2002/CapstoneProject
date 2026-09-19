"""Check all authored chest UI copies, including shipping GlobalUIRoot."""
from pathlib import Path
import re
paths=[Path('Assets/_Project/Prefabs/UI/ChestUI.prefab'),*Path('Assets/_Project/Prefabs/UI').glob('GlobalUIRoot*.prefab')]
for p in paths:
 s=p.read_text(encoding='utf-8-sig'); matches=list(re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)',s));b={m[1]:m[0] for m in matches}
 assert len(b)==len(matches),p
 assert all(i=='0' or i in b for i in re.findall(r'{fileID: (\d+)}',s)),p
 children=b['911709170000000021']
 for n,x in enumerate([36,116]):
  g,tr,cr,im=[str(911709180000000000+n*10+j) for j in range(4)]
  assert 'm_IsActive: 1' in b[g],p
  assert f'm_AnchoredPosition: {{x: {x}, y: -36}}' in b[tr],p
  assert 'm_SizeDelta: {x: 72, y: 72}' in b[tr],p
  assert 'm_Color: {r: 1, g: 1, b: 1, a: 1}' in b[im],p
  assert 'guid: 74dcc408cecc4fb8b0b114cceb63c68e' in b[im],p
  assert f'm_Father: {{fileID: 911709170000000021}}' in b[tr],p
  assert tr in children,p
  assert tr not in b['911709170000000001'],p
 assert 'm_Spacing: {x: 8, y: 0}' in b['911709170000000022'],p
print('CHEST_FIXED_FRAMES_PASS:',len(paths),'prefabs including all embedded GlobalUIRoot chest UIs')
