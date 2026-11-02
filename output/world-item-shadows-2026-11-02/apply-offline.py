from pathlib import Path
from PIL import Image
import re, json, math, hashlib

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).parent
PREFAB=ROOT/'Assets/_Project/Prefabs/Items/Pickups/PF_WorldItemPickup2D.prefab'
SPRITE_GUID='66a4ec10a50936e449f2c1fd782999b2'
SCRIPT_GUID='13be847c51db46c69ea288527f3d4c61'
shadow_path=ROOT/'Assets/_Project/Art/Sprites/Player/Pixel_Plains_Premium_Pack/Characters 24x24/Shadow.png'

def rect_for(path, fileid):
    text=Path(str(path)+'.meta').read_text(encoding='utf-8-sig')
    if re.search(r'  spriteMode: 1$',text,re.M):
        with Image.open(path) as im:return (0,0,*im.size)
    for block in re.split(r'(?m)^    - serializedVersion: \d+\n',text):
        match=re.search(r'^      internalID: (-?\d+)$',block,re.M)
        if not match or int(match[1])!=fileid:continue
        r=re.search(r'      rect:\n        serializedVersion: \d+\n        x: ([\d.]+)\n        y: ([\d.]+)\n        width: ([\d.]+)\n        height: ([\d.]+)',block)
        if r:return tuple(int(float(v)) for v in r.groups())
    raise ValueError(f'Sprite rect missing {path} {fileid}')

def mask_for(path,rect,width,height):
    with Image.open(path) as raw:
        rgba=raw.convert('RGBA'); x,y,w,h=rect
        sprite=rgba.crop((x,rgba.height-y-h,x+w,rgba.height-y))
        return [[sprite.getpixel((min(w-1,math.floor((i+.5)*w/width)),min(h-1,math.floor((j+.5)*h/height))))[3]>0
                 for j in range(height)]for i in range(width)]

def contact_for(icon,shadow):
    offset=None
    left=round((len(icon)-24)/2)
    for sx in range(24):
        ix=left+sx
        if not 0<=ix<len(icon):continue
        bottom=next((y for y in reversed(range(len(icon[ix])))if icon[ix][y]),None)
        top=next((y for y in range(8)if shadow[sx][y]),None)
        if bottom is not None and top is not None:offset=max(offset or -1000,bottom-top+1)
    if offset is None:raise ValueError('No shared opaque contour')
    # An independent collision check: contact has no overlap, contact-1 does.
    def overlaps(dy):
        return any(0<=left+sx<len(icon) and 0<=sy+dy<len(icon[0]) and shadow[sx][sy] and icon[left+sx][sy+dy]
                   for sx in range(24)for sy in range(8))
    assert not overlaps(offset) and overlaps(offset-1)
    return offset

definitions=[]; references={}
for kind in ['Relics/Definitions','Consumables']:
    for p in (ROOT/'Assets/_Project/Data/Items'/kind).rglob('*.asset'):
        text=p.read_text(encoding='utf-8-sig')
        # Includes RelicDefinition subclasses, excluding unrelated consumable logic assets.
        if not re.search(r'^  (?:relicId|consumableId):',text,re.M):continue
        m=re.search(r'^  icon: \{fileID: (-?\d+), guid: ([a-f0-9]+), type: 3\}',text,re.M)
        definitions.append(str(p.relative_to(ROOT)))
        if m:references.setdefault(m[2],set()).add(int(m[1]))
guid_paths={}
for meta in (ROOT/'Assets/_Project/Art').rglob('*.png.meta'):
    text=meta.read_text(encoding='utf-8-sig'); m=re.search(r'^guid: (\w+)$',text,re.M)
    if m and m[1] in references:guid_paths[m[1]]=Path(str(meta)[:-5])
assert set(references)==set(guid_paths),set(references)-set(guid_paths)
shadow=mask_for(shadow_path,(0,0,17,6),24,8)
entries=[]
for guid,ids in references.items():
    path=guid_paths[guid]
    for fileid in ids:
        rect=rect_for(path,fileid);w,h=rect[2:]
        width=max(1,round(32*w/max(w,h)));height=max(1,round(32*h/max(w,h)))
        offset=contact_for(mask_for(path,rect,width,height),shadow)
        entries.append({'guid':guid,'fileID':fileid,'path':str(path.relative_to(ROOT)),
                        'rect':rect,'contact':offset,'centerFromTop':offset/32})
entries.sort(key=lambda e:(e['path'],e['fileID']))

text=PREFAB.read_text(encoding='utf-8-sig')
if SCRIPT_GUID in text:raise RuntimeError('Already applied; use the Unity authoring menu for rebakes.')
(OUT/'pickup-before.prefab.txt').write_text(text,encoding='utf-8')
text=text.replace('  - component: {fileID: 8600000000000000001}\n','  - component: {fileID: 8600000000000000001}\n  - component: {fileID: 8600000000000000014}\n',1)
text=text.replace('  - {fileID: 2408791743630095105}\n','  - {fileID: 2408791743630095105}\n  - {fileID: 8600000000000000012}\n',1)
blocks=re.split(r'(?m)(?=^--- !u!)',text)
go=next(b for b in blocks if b.startswith('--- !u!1 &3299226741398321030\n'))
go=go.replace('3299226741398321030','8600000000000000011').replace('2408791743630095105','8600000000000000012').replace('9003696209070177760','8600000000000000013').replace('m_Name: Render','m_Name: Shadow')
tr=next(b for b in blocks if b.startswith('--- !u!4 &2408791743630095105\n'))
tr=tr.replace('3299226741398321030','8600000000000000011').replace('2408791743630095105','8600000000000000012')
tr=tr.replace('m_LocalScale: {x: 1, y: 1, z: 1}','m_LocalScale: {x: 4.4117646, y: 4.1666665, z: 1}')
sr=next(b for b in blocks if b.startswith('--- !u!212 &9003696209070177760\n'))
sr=sr.replace('3299226741398321030','8600000000000000011').replace('9003696209070177760','8600000000000000013')
sr=sr.replace('  m_Enabled: 1','  m_Enabled: 0',1)
sr=sr.replace('{fileID: 2100000, guid: 0d09aba533d2fa84da4a54832b741dc2, type: 2}',
              '{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}')
sr=sr.replace('m_SortingOrder: 0','m_SortingOrder: -1')
sr=re.sub(r'm_Sprite: .*',f'm_Sprite: {{fileID: 4093480436322176185, guid: {SPRITE_GUID}, type: 3}}',sr)
sr=sr.replace('m_Color: {r: 1, g: 1, b: 1, a: 1}','m_Color: {r: 1, g: 1, b: 1, a: 0.34}')
mb=f'''--- !u!114 &8600000000000000014
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 4439982264372595963}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  sourceRenderer: {{fileID: 9003696209070177760}}
  shadowRenderer: {{fileID: 8600000000000000013}}
  contacts:
'''
for e in entries:
    mb+=f"  - sprite: {{fileID: {e['fileID']}, guid: {e['guid']}, type: 3}}\n    centerFromTop: {e['centerFromTop']}\n"
PREFAB.write_text(text+go+tr+sr+mb,encoding='utf-8',newline='\n')
(OUT/'baked-contacts.json').write_text(json.dumps({'definitions':definitions,'entries':entries,'previewGapPixels':-4,
    'previewRasterSize':32,'width':.75,'height':.25,'alpha':.34,'independentContourCollisionChecks':len(entries)*2},indent=2),encoding='utf-8')
print(json.dumps({'definitions':len(definitions),'uniqueSprites':len(entries),'collisionChecks':len(entries)*2}))
