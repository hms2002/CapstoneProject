"""Read-only source, palette, GUID and prefab preservation verification."""
from pathlib import Path
import hashlib
import json
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
REPORT = Path(__file__).resolve().parent
ART = ROOT / 'Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle'
APPROVED = ROOT / 'outputs/crimson-double-circle-preview-2026-11-01/revision-25-three-circles'
PREFAB = ROOT / 'Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def records(path):
    text = path.read_text(encoding='utf-8-sig')
    return {m[1]: m[0] for m in re.findall(r'(^--- !u!\d+ &(\d+)\n.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}

palette = {(182,61,40,255), (241,107,50,255), (255,198,90,255)}
images = {}
for name in ('inner', 'outer', 'center'):
    for ext in ('png', 'aseprite'):
        assert sha(ART / f'{name}.{ext}') == sha(APPROVED / f'{name}.{ext}')
    image = Image.open(ART / f'{name}.png').convert('RGBA')
    colors = set(image.getdata())
    opaque = {c for c in colors if c[3]}
    assert image.size == (192,192) and opaque <= palette
    meta = (ART / f'{name}.png.meta').read_text()
    for setting in ('filterMode: 0', 'enableMipMap: 0', 'spritePixelsToUnits: 16', 'spriteMode: 1', 'spriteMeshType: 0'):
        assert setting in meta, (name, setting)
    images[name] = {'size': image.size, 'bounds': image.getbbox(), 'opaqueColors': sorted(opaque)}

# Only the moved center circle is removed from the previous middle artwork.
before_inner = Image.open(REPORT / 'before/inner.png').convert('RGBA')
new_inner = Image.open(ART / 'inner.png').convert('RGBA')
removed = 0
for y in range(192):
    for x in range(192):
        old, new = before_inner.getpixel((x,y)), new_inner.getpixel((x,y))
        if old != new:
            radius = ((x-95.5)**2 + (y-95.5)**2)**.5
            assert 21.3 < radius < 23.0 and old[3] == 255
            removed += 1

before, after = records(REPORT / 'before/CrimsonChargePresentation.prefab'), records(PREFAB)
assert before.keys() <= after.keys()
changed = [key for key in before if before[key] != after[key]]
assert len(changed) == 2
assert len(after) - len(before) == 3
for key in changed:
    cleaned = re.sub(r'^  - \{fileID: 7767539457939635003\}\n|^  centerCircle: \{fileID: 7034498596890150099\}\n', '', after[key], flags=re.M)
    assert cleaned == before[key], key
text = PREFAB.read_text()
for name, field in (('InnerCircle','innerCircle'),('OuterCircle','outerCircle'),('CenterCircle','centerCircle')):
    assert len(re.findall(rf'^  m_Name: {name}$', text, re.M)) == 1
    renderer_id = re.search(rf'^  {field}: \{{fileID: (\d+)\}}$', text, re.M)[1]
    meta = (ART / f'{field.replace("Circle", "")}.png.meta').read_text()
    guid = re.search(r'^guid: (\w+)$',meta,re.M)[1]
    assert guid in after[renderer_id]

allowed = {str(ART/'inner.png'), str(ART/'inner.aseprite'), str(PREFAB)}
baseline = json.loads((REPORT/'baseline-hashes.json').read_text(encoding='utf-8-sig'))
protected = 0
for item in baseline:
    path = Path(item['Path'])
    if str(path) in allowed:
        continue
    assert sha(path) == item['Hash'], str(path)
    protected += 1
gif = Image.open(APPROVED/'three-circle-rotation.gif')
assert gif.size == (480,480) and gif.n_frames == 240
result = {'status':'PASS','art':images,'movedCenterPixels':removed,
          'existingPrefabRecords':len(before),'unchangedPrefabRecords':len(before)-len(changed),
          'modifiedRecords':changed,'newRecords':3,'protectedFilesUnchanged':protected,
          'previewGif':{'size':gif.size,'frames':gif.n_frames}}
(REPORT/'source-verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
