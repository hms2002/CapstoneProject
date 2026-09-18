from pathlib import Path
import re,collections,json
p=Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity');original=p.read_text(encoding='utf-8-sig')
pat=r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)';b={m[1]:m[0] for m in re.finditer(pat,original)}
assert 'm_LocalScale: {x: 0.5, y: 0.5, z: 1}' in b['8600000016']
backup=Path('Temp/tutorial-before-normalization.unity')
if backup.exists(): assert backup.read_text(encoding='utf-8-sig')==original, 'Keep the existing recovery snapshot'
else: backup.write_text(original,encoding='utf-8')
b['8600000016']=b['8600000016'].replace('m_LocalScale: {x: 0.5, y: 0.5, z: 1}','m_LocalScale: {x: 1, y: 1, z: 1}')
b['8600000017']=b['8600000017'].replace('m_CellSize: {x: 1, y: 1, z: 0}','m_CellSize: {x: 0.5, y: 0.5, z: 0}')
report={}
for tid in ['8600000020','8600000024']:
 t=b[tid];rows=re.findall(r'(?ms)^  - first:.*?(?=^  - first:|^  m_AnimatedTiles:|^  m_TileAssetArray:)',t)
 cells={tuple(map(int,re.search(r'first: {x: (-?\d+), y: (-?\d+)',r).groups())):r for r in rows}
 assert all('m_TileMatrixIndex: 0\n' in r for r in rows)
 matrices=re.search(r'(?ms)^  m_TileMatrixArray:.*?(?=^  \w+:)',t)
 entries=re.findall(r'(?ms)^  - serializedVersion:.*?(?=^  - serializedVersion:|\Z)',matrices[0])
 # Each retained tile reproduces the old 0.5 world transform exactly.
 scaled=[]
 for entry in entries:
  entry=re.sub(r'(?m)^(      e[01][0-3]: )(-?[\d.]+)',lambda m:m[1]+str(float(m[2])*.5),entry)
  scaled.append(entry)
 mergeIndex=len(scaled)
 merged=entries[0]
 merged=re.sub(r'(?m)^      e03:.*','      e03: 0.25',merged);merged=re.sub(r'(?m)^      e13:.*','      e13: 0.25',merged)
 scaled.append(merged)
 removed=set();newrows=[];groups=[]
 for (x,y),r in sorted(cells.items(),key=lambda kv:(kv[0][1],kv[0][0])):
  if (x,y) in removed:continue
  group=[(x,y),(x+1,y),(x,y+1),(x+1,y+1)]
  signature=lambda row:row[row.index('    second:'):]
  canmerge=tid=='8600000020' and x%2==0 and y%2==0 and all(q in cells and q not in removed and signature(cells[q])==signature(r) for q in group)
  if canmerge:
   r=r.replace('m_TileMatrixIndex: 0',f'm_TileMatrixIndex: {mergeIndex}');removed.update(group[1:]);groups.append([x,y])
  newrows.append(r)
 t=t[:matrices.start()]+'  m_TileMatrixArray:\n'+''.join(scaled)+t[matrices.end():]
 t=re.sub(r'(?s)(  m_Tiles:\n).*?(?=  m_AnimatedTiles:|  m_TileAssetArray:)',lambda m:m[1]+''.join(newrows),t,count=1)
 for array,key in [('m_TileAssetArray','m_TileIndex'),('m_TileSpriteArray','m_TileSpriteIndex'),('m_TileMatrixArray','m_TileMatrixIndex'),('m_TileColorArray','m_TileColorIndex')]:
  m=re.search(rf'(?ms)^  {array}:.*?(?=^  \w+:)',t);counts=collections.Counter(int(re.search(key+r': (\d+)',r)[1]) for r in newrows);idx=iter(range(10000));new=re.sub(r'm_RefCount: \d+',lambda _: 'm_RefCount: '+str(counts[next(idx)]),m[0]);t=t[:m.start()]+new+t[m.end():]
 b[tid]=t;report[tid]={'before':len(rows),'after':len(newrows),'merged':groups};print(tid,len(rows),'->',len(newrows))
# Regenerate cached wall geometry in Unity under the normalized transform.
for field in ['m_ColliderPaths','m_CompositePaths']:
 b['8700000003']=re.sub(rf'(?ms)^  {field}:\n.*?(?=^  \w+:)',f'  {field}: []\n',b['8700000003'])
b['8700000003']=b['8700000003'].replace('m_VertexDistance: 0.0005','m_VertexDistance: 0.00025').replace('m_OffsetDistance: 0.00005','m_OffsetDistance: 0.000025')
assert p.read_text(encoding='utf-8-sig')==original
out=Path('Temp/tutorial-normalized-staged.unity');out.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(b.values()),encoding='utf-8');out.replace(p)
Path('Temp/tutorial-normalization-report.json').write_text(json.dumps(report),encoding='utf-8')
