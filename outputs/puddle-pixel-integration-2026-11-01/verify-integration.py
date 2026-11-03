from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from PIL import Image

out = Path(__file__).resolve().parent
root = out.parents[1]
baseline = json.loads((out / 'baseline-hashes.json').read_text(encoding='utf-8'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
protected = []
for path, old in baseline['hashes'].items():
    if path not in baseline['allowed_changed']:
        assert sha(root / path) == old, 'Protected file changed: ' + path
        protected.append(path)

texture_map = {
    'AlcoholGround.png': 'alcohol-sheet.png', 'FireGround.png': 'fire-ground-sheet.png',
    'CenterIgnition.png': 'center-ignition-sheet.png', 'EmissionShape.png': 'alcohol-1.png',
    'BubbleVariants.png': 'bubble-variants-sheet.png', 'NearFlames.png': 'ground-flame-particle-sheet.png',
    'AirborneFlames.png': 'airborne-flame-particle-sheet.png',
}
for name, source in texture_map.items():
    assert sha(root / 'Assets/_Project/Art/Textures/Puddles/ApprovedPixel' / name) == sha(out / 'source' / source)

def records(path):
    text = path.read_text(encoding='utf-8-sig')
    matches = list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*\n', text, re.M))
    result = {}
    for i, match in enumerate(matches):
        chunk = text[match.end():matches[i+1].start() if i+1 < len(matches) else len(text)]
        # Unity emits component fields at exactly two spaces. Keep each nested
        # value as canonical text rather than adding a YAML dependency.
        fields = list(re.finditer(r'^  ([A-Za-z_][\w]*):[^\S\n]?', chunk, re.M))
        value = {}
        for j, field in enumerate(fields):
            end = fields[j+1].start() if j+1 < len(fields) else len(chunk)
            value[field[1]] = '\n'.join(line.rstrip() for line in chunk[field.end():end].strip().splitlines())
        # Unity updates this assembly display string after the existing assembly split.
        value.pop('m_EditorClassIdentifier', None)
        result[match[2]] = (int(match[1]), value)
    return result

prefab_checks = {}
particle_script = re.search(r'guid: (\w+)', (root / 'Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleParticleVisual.cs.meta').read_text()).group(1)
shader_script = re.search(r'guid: (\w+)', (root / 'Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleShaderVisual.cs.meta').read_text()).group(1)
for name in ['AlcoholPuddle.prefab', 'FirePuddle.prefab']:
    before = records(out / 'baseline' / name)
    after = records(root / 'Assets/_Project/Prefabs/Map/Puddles' / name)
    authored_names = {v.get('m_Name') for kind, v in after.values() if kind == 1}
    assert {'PixelNearFlames', 'PixelAirborneFlames'} <= authored_names, 'Approved particle children were not applied'
    assert before.keys() <= after.keys(), 'Existing prefab IDs removed'
    changed = []
    protected_records = 0
    for ident, (kind, value) in before.items():
        assert after[ident][0] == kind
        current = after[ident][1]
        if value == current:
            protected_records += 1
            continue
        fields = sorted(k for k in value.keys() | current.keys() if value.get(k) != current.get(k))
        if kind == 114:
            if particle_script in value.get('m_Script', ''):
                assert set(fields) <= {'usePixelArtwork', 'pixelFlameEmissionRates', 'flameLayerRadiusScale', 'flameParticleSystems'}, fields
            else:
                assert shader_script in value.get('m_Script', '') and fields == ['materialTemplate'], (name, ident, fields)
        else:
            assert kind in (4, 23, 198, 199, 1001), (name, kind, ident, fields)
            if kind == 23:
                assert set(fields) <= {'m_Materials'}, fields
            if kind == 4:
                assert set(fields) <= {'m_Children', 'm_Father', 'm_LocalPosition', 'm_LocalRotation', 'm_LocalScale', 'm_LocalEulerAnglesHint'}, fields
        changed.append({'id': ident, 'class_id': kind, 'fields': fields})
    prefab_checks[name] = {'unchanged_existing_records': protected_records, 'changed_existing_records': changed, 'added_records': len(after)-len(before)}

def rgba(path):
    return list(Image.open(path).convert('RGBA').get_flattened_data())
prior = root / 'outputs/puddle-pixel-reference-2026-11-01/revision-17-bright-beer-soft-foam'
for f in range(1, 5):
    old, new = rgba(prior / f'alcohol-frame-{f}.png'), rgba(out / 'source' / f'alcohol-{f}.png')
    assert [p[3] for p in old] == [p[3] for p in new]
assert rgba(out / 'source/center-ignition-1.png') == rgba(out / 'source/alcohol-1.png')
assert rgba(out / 'source/center-ignition-16.png') == rgba(out / 'source/fire-ground-1.png')
result = {'protected_file_count': len(protected), 'protected_files': protected, 'native_export_textures_exact': list(texture_map),
          'beer_alpha_unchanged_from_revision17': True, 'center_ignition_endpoints_exact': True, 'prefab_semantic_checks': prefab_checks}
test_result = out / 'playmode-results.xml'
if test_result.exists():
    suite = ET.parse(test_result).getroot()
    result['playmode'] = dict(suite.attrib)
    assert suite.attrib.get('failed') == '0', result['playmode']
(out / 'preservation-verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'protected_files': len(protected), 'textures': len(texture_map), 'prefabs': {k: {'unchanged': v['unchanged_existing_records'], 'changed': len(v['changed_existing_records']), 'added': v['added_records']} for k, v in prefab_checks.items()}}, indent=2))
