"""Author the real prototype encounter using existing corridor tiles and monster prefabs.
Run once on the placeholder scene; preserves UI/portal references and authored collision footprint.
"""
from pathlib import Path
import re, json, collections
scene_path=Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
def blocks(s): return {m[1]:m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)',s)}
s=scene_path.read_text(encoding='utf-8-sig'); b=blocks(s)
assert 'TutorialTileGrid' not in s, 'Already authored; do not overwrite subsequent scene edits'
source=blocks(Path('Assets/_Project/Scenes/TutorialCorridor.unity').read_text(encoding='utf-8-sig'))
layout=json.loads(Path('Temp/tutorial-layout-preview.json').read_text())
next_id=8600000000
newroots=[]
def alloc():
 global next_id
 next_id+=1
 return str(next_id)
def remap(t,m):
 t=re.sub(r'(&)(\d+)',lambda q:q[1]+m.get(q[2],q[2]),t,count=1)
 return re.sub(r'\{fileID: (\d+)\}',lambda q:'{fileID: '+m.get(q[1],q[1])+'}',t)
def change(i,key,value):
 b[i]=re.sub(rf'(?m)^  {key}:.*$',f'  {key}: {value}',b[i])
def empty(name,pos,parent='0'):
 go,tr=alloc(),alloc()
 b[go]=f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_IsActive: 1
'''
 b[tr]=f'''--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''
 if parent=='0': newroots.append(tr)
 return go,tr
# Gunner is an actual authored prefab instance, with tutorial-owned AI instead of the normal shot runner.
prefab=Path('Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinGunner.prefab')
pb=blocks(prefab.read_text(encoding='utf-8-sig'))
guid=re.search(r'guid: (\w+)',Path(str(prefab)+'.meta').read_text())[1]
root='7164257353146660579'; rt='5546223340966227954'
scriptguid=re.search(r'guid: (\w+)',Path('Assets/_Project/Runtime/Features/Monsters/Common/CommonCorridor/GoblinGunner.cs.meta').read_text())[1]
component=next(i for i,t in pb.items() if 'guid: '+scriptguid in t)
instance,transform,gunner=alloc(),alloc(),alloc()
b[instance]=f'''--- !u!1001 &{instance}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
'''
for path,value in [('m_LocalPosition.x',0),('m_LocalPosition.y',21),('m_LocalPosition.z',0),('m_LocalRotation.x',0),('m_LocalRotation.y',0),('m_LocalRotation.z',0),('m_LocalRotation.w',1)]:
 b[instance]+=f'''    - target: {{fileID: {rt}, guid: {guid}, type: 3}}
      propertyPath: {path}
      value: {value}
      objectReference: {{fileID: 0}}
'''
b[instance]+=f'''    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
'''
b[transform]=f'''--- !u!4 &{transform} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {rt}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance}}}
  m_PrefabAsset: {{fileID: 0}}
'''
b[gunner]=f'''--- !u!114 &{gunner} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {component}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {scriptguid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
'''
newroots.append(instance)
seq='8100000003'
oldtargets=re.findall(r'\{fileID: (\d+)\}',re.search(r'  skillTargets:.*?(?=  bullets:)',b[seq],re.S)[0])+[re.search(r'attackTarget: \{fileID: (\d+)\}',b[seq])[1]]
for tr in oldtargets:
 instance_id=re.search(r'm_PrefabInstance: {fileID: (\d+)',b[tr])[1]
 b[instance_id]=b[instance_id].replace('    m_RemovedComponents:', '    - target: {fileID: 1956472550783629341, guid: 5a0eb9c3258276e4ab0789f47447d1c8, type: 3}\n      propertyPath: m_IsActive\n      value: 0\n      objectReference: {fileID: 0}\n    m_RemovedComponents:',1)
change(seq,'attackTarget',f'{{fileID: {transform}}}')
b[seq]=re.sub(r'  skillTargets:.*?(?=  bullets:)', '  skillTargets: []\n',b[seq],flags=re.S)
spawns=[empty('WarriorSpawn_'+str(i+1),p)[1] for i,p in enumerate([(-3,28),(3,28),(-3,32),(3,32)])]
retreat=empty('GunnerRetreatPoint',(0,27))[1]
# Reserve one pending encounter item to keep the visible chest genuinely locked.
chestgo='8400000033'
chestlock=alloc()
b['8000000476']=b['8000000476'].replace('    m_AddedComponents: []', f'    m_AddedComponents:\n    - targetCorrespondingSourceObject: {{fileID: 7530156284439312542, guid: 296eaa6f1547cb74e990055ce1319470, type: 3}}\n      insertIndex: -1\n      addedObject: {{fileID: {chestlock}}}')
b['8000000476']=b['8000000476'].replace('propertyPath: m_IsActive\n      value: 0','propertyPath: m_IsActive\n      value: 1')
b[chestlock]=f'''--- !u!114 &{chestlock}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {chestgo}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: e70ff4a1c8da75a4fad7c6bbd18d056d, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  presentationAnchor: {{fileID: 0}}
'''
change(chestgo,'m_IsActive','1')
wp=blocks(Path('Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab').read_text(encoding='utf-8-sig'))
wg=re.search(r'guid: (\w+)',Path('Assets/_Project/Runtime/Features/Monsters/Common/CommonCorridor/GoblinWarrior.cs.meta').read_text())[1]
wc=next(i for i,t in wp.items() if 'guid: '+wg in t)
b[seq]+=f'''  tutorialGunner: {{fileID: {gunner}}}
  skillMonsterPrefab: {{fileID: {wc}, guid: fda3cefab78502b40b55e2b827a2ecd1, type: 3}}
  skillSpawnPoints:
'''+''.join(f'  - {{fileID: {tr}}}\n' for tr in spawns)+f'''  gunnerRetreatPoint: {{fileID: {retreat}}}
  chestLock: {{fileID: {chestlock}}}
'''
# Hide colored floor/wall mockup sprites, retaining the reviewed collision layout and marker geometry.
for i,t in list(b.items()):
 if not t.startswith('--- !u!1 ') or ' stripped' in t.splitlines()[0]:continue
 name=re.search(r'm_Name: (.*)',t)[1].strip('"')
 if name.endswith('_Floor') or name=='OuterWall':
  for c in re.findall(r'component: \{fileID: (\d+)\}',t):
   if b[c].startswith('--- !u!212 '):change(c,'m_Enabled','0')
# Tilemaps use the exact serialized tile/sprite palette from the shipping tutorial.
gridgo,gridtr=empty('TutorialTileGrid',(0,0));grid=alloc()
b[gridgo]=b[gridgo].replace('  m_Layer:',f'  - component: {{fileID: {grid}}}\n  m_Layer:',1)
b[grid]=remap(source['1764990986'],{'1764990986':grid,'1764990985':gridgo})
change(gridtr,'m_LocalScale','{x: 0.5, y: 0.5, z: 1}')
children=[]
for sourcego,tmid,name,rects,chosen in [('167450169','167450172','TutorialGround',[v for _,v in layout['floors']],3),('236309114','236309120','TutorialWalls',layout['walls'],37)]:
 go,tr=empty(name,(0,0),gridtr);children.append(tr)
 tm,renderer=alloc(),alloc()
 sr=next(c for c in re.findall(r'component: \{fileID: (\d+)\}',source[sourcego]) if source[c].startswith('--- !u!483693784 '))
 b[go]=b[go].replace('  m_Layer:',f'  - component: {{fileID: {tm}}}\n  - component: {{fileID: {renderer}}}\n  m_Layer:',1)
 b[renderer]=remap(source[sr],{sr:renderer,sourcego:go})
 t=remap(source[tmid],{tmid:tm,sourcego:go})
 rows=re.findall(r'(?ms)^  - first:.*?(?=^  - first:|^  m_AnimatedTiles:|^  m_TileAssetArray:)',t)
 samples={int(re.search(r'm_TileIndex: (\d+)',row)[1]):row for row in rows}
 sample=samples[chosen]
 cells={(x,y) for x in range(-23,29) for y in range(-19,116) if any(a<=x*.5+.25<=c and d<=y*.5+.25<=e for a,c,d,e in rects)}
 floor_cells={(x,y) for x in range(-23,29) for y in range(-19,116) if any(a<=x*.5+.25<=c and d<=y*.5+.25<=e for _,(a,c,d,e) in layout['floors'])}
 def select(x,y):
  if name=='TutorialGround': return 42 if (x*17+y*31)%47==0 else 43 if (x*7+y*11)%179==0 else 3
  if (x,y-1) in floor_cells:return 22
  if (x+1,y) in floor_cells:return 28
  if (x-1,y) in floor_cells:return 27
  if (x,y+1) in floor_cells:return 19
  return 37
 selected=[re.sub(r'first: \{.*?\}',f'first: {{x: {x}, y: {y}, z: 0}}',samples[select(x,y)],count=1) for x,y in sorted(cells,key=lambda p:(p[1],p[0]))]
 tiles=''.join(selected)
 t=re.sub(r'(?s)(  m_Tiles:\n).*?(?=  m_AnimatedTiles:|  m_TileAssetArray:)',lambda m:m[1]+tiles,t,count=1)
 # Recount palette references for the newly authored cells.
 for array,indexkey in [('m_TileAssetArray','m_TileIndex'),('m_TileSpriteArray','m_TileSpriteIndex'),('m_TileMatrixArray','m_TileMatrixIndex'),('m_TileColorArray','m_TileColorIndex')]:
  match=re.search(rf'(?ms)^  {array}:\n.*?(?=^  \w+:)',t)
  if not match:continue
  counts=collections.Counter(int(re.search(indexkey+r': (\d+)',row)[1]) for row in selected);counter=[-1]
  def count(m):counter[0]+=1;return 'm_RefCount: '+str(counts[counter[0]])
  t=t[:match.start()]+re.sub(r'm_RefCount: \d+',count,match[0])+t[match.end():]
 t=re.sub(r'  m_Origin:.*','  m_Origin: {x: -23, y: -19, z: 0}',t)
 t=re.sub(r'  m_Size:.*','  m_Size: {x: 52, y: 135, z: 1}',t)
 b[tm]=t
b[gridtr]=b[gridtr].replace('  m_Children: []','  m_Children:\n'+''.join(f'  - {{fileID: {tr}}}\n' for tr in children).rstrip())
# Use the existing goblin projectile art in the authored, tutorial-controlled projectile pool.
for tr in re.findall(r'\{fileID: (\d+)\}',re.search(r'  bullets:.*?(?=  bulletTop:)',b[seq],re.S)[0]):
 go=re.search(r'm_GameObject: {fileID: (\d+)',b[tr])[1]
 change(tr,'m_LocalScale','{x: 0.7, y: 0.7, z: 1}')
 change(go,'m_IsActive','0')
 for c in re.findall(r'component: \{fileID: (\d+)\}',b[go]):
  if b[c].startswith('--- !u!212 '):
   change(c,'m_Sprite','{fileID: 3052830885205025137, guid: 97a5cb8d98509e04f82d188e3a474ab2, type: 3}')
   change(c,'m_Color','{r: 1, g: 1, b: 1, a: 1}')
roots=b.pop('9223372036854775807')+''.join(f'  - {{fileID: {i}}}\n' for i in newroots)
scene_path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(b.values())+roots,encoding='utf-8')
print('Authored tilemaps, gunner prefab, four warrior spawn markers and chest lock.')
