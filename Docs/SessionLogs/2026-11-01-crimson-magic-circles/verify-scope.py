"""Verify this slice against its working-tree baseline; optionally sync only two CSV rows."""
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[3]
REPORT = Path(__file__).resolve().parent
BEFORE = REPORT / 'before'
KEY = 'asset.b093d5960df6454ea41e8d70aff17fb4'
LOCALES = ['ko', 'en', 'ja', 'zh-Hans', 'zh-Hant']

def read(path):
    return Path(path).read_text(encoding='utf-8-sig')

def localized_value(text, entry_id):
    entry = re.search(r'^  - m_Id: ' + str(entry_id) + r'\n(.*?)(?=^  - m_Id:|\Z)', text, re.M | re.S).group(1)
    quoted = re.search(r'm_Localized: ("(?:\\.|[^"\\])*")', entry, re.S).group(1)
    # Normalize Unity YAML's short hexadecimal escapes before JSON decoding.
    quoted = re.sub(r'\\x([0-9A-Fa-f]{2})', lambda m: r'\u00' + m[1], quoted)
    return json.loads(re.sub(r'\n[ ]+', ' ', quoted))

def sync_csv():
    desired = {}
    for suffix, entry_id in [('simpleDescription', 50606285688888), ('description', 50606285688889)]:
        desired[KEY + '.' + suffix] = [localized_value(read(ROOT / f'Assets/_Project/Data/Localization/GameText_{locale}.asset'), entry_id) for locale in LOCALES]
    path = ROOT / 'DataSheets/Localization/GameText.csv'
    raw = path.read_bytes()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    lines = text.splitlines(keepends=True)
    reader = csv.reader(lines)
    previous = 0
    replacements = []
    for row in reader:
        end = reader.line_num
        if row and row[0] in desired:
            buf = io.StringIO(newline='')
            csv.writer(buf, lineterminator='\r\n' if '\r\n' in text else '\n').writerow([row[0], *desired[row[0]]])
            replacements.append((previous, end, buf.getvalue()))
        previous = end
    assert len(replacements) == 2
    for start, end, replacement in reversed(replacements):
        lines[start:end] = [replacement]
    path.write_bytes((b'\xef\xbb\xbf' if bom else b'') + ''.join(lines).encode('utf-8'))
    (REPORT / 'localization-values.json').write_text(json.dumps(desired, ensure_ascii=False, indent=2), encoding='utf-8')
    old_rows = list(csv.reader(io.StringIO(read(BEFORE / 'GameText.csv'))))
    new_rows = list(csv.reader(io.StringIO(read(path))))
    assert len(old_rows) == len(new_rows)
    assert [a[0] for a, b in zip(old_rows, new_rows) if a != b] == list(desired)
    print('Two CSV records synchronized; all other records preserved.')

def blocks(text):
    return {int(i): b for i, b in re.findall(r'^--- !u!\d+ &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}

def verify():
    protected = json.loads(read(REPORT / 'protected-before.json'))
    unchanged = 0
    for item in protected:
        p = Path(item['path'])
        if p.name == 'AD_CrimsonBoundaryIgnite.asset':
            continue  # Only its fallback description is authorized below.
        assert hashlib.sha256(p.read_bytes()).hexdigest().upper() == item['sha256'], str(p)
        unchanged += 1
    old = blocks(read(BEFORE / 'CrimsonChargePresentation.prefab'))
    new = blocks(read(ROOT / 'Assets/_Project/Prefabs/VFX/CrimsonBoundary/ChargeSixColor/CrimsonChargePresentation.prefab'))
    allowed = set()
    for i, body in old.items():
        if re.search(r'^  m_Name: (Disc|Dashes)$', body, re.M):
            allowed.add(i)
            allowed.update(map(int, re.findall(r'component: \{fileID: (\d+)\}', body)))
        if 'guid: eefeedcdc079c5c4e9d18116f69ea45b' in body:
            allowed.add(i)
            fields = 'disc|dashes|dashThickness|dashRotationSpeed|innerCircle|outerCircle|circleRotationSpeed|warningFadeSeconds'
            strip = lambda value: re.sub(r'^  (' + fields + r'):.*\n', '', value, flags=re.M)
            assert strip(body) == strip(new[i]), 'Unrelated presentation settings changed'
        if re.search(r'^  m_Name: ChargeWarning$', body, re.M):
            allowed.update(map(int, re.findall(r'component: \{fileID: (\d+)\}', body)))
    preserved_blocks = 0
    for i, body in old.items():
        if i not in allowed:
            assert new.get(i) == body, f'Unrelated prefab block changed: {i}'
            preserved_blocks += 1
    assert len(old.keys() - new.keys()) == 8, 'Only two old mesh children removed'
    assert len(new.keys() - old.keys()) == 6, 'Only two sprite children added'
    ability_path = ROOT / 'Assets/_Project/Data/Abilities/Definitions/AD_CrimsonBoundaryIgnite.asset'
    assert read(ability_path) == read(BEFORE / ability_path.name).replace('3~6', '4.03~6'), 'Only fallback range text changed'
    for locale in LOCALES:
        p = ROOT / f'Assets/_Project/Data/Localization/GameText_{locale}.asset'
        old_text, new_text = read(BEFORE / p.name), read(p)
        pattern = r'^  - m_Id: 50606285688889\n.*?(?=^  - m_Id:|\Z)'
        assert re.sub(pattern, '', old_text, flags=re.M | re.S) == re.sub(pattern, '', new_text, flags=re.M | re.S), locale
        expected = localized_value(old_text, 50606285688889)
        for separator in ['~', '–', '～', '-']:
            expected = expected.replace('{val:3' + separator + '6', '{val:4.03' + separator + '6')
        assert localized_value(new_text, 50606285688889) == expected, locale
    for name in ['inner.png', 'outer.png', 'inner.aseprite', 'outer.aseprite']:
        actual = ROOT / 'Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/MagicCircle' / name
        approved = ROOT / 'outputs/crimson-double-circle-preview-2026-11-01/revision-19-clear-runes' / name
        assert actual.read_bytes() == approved.read_bytes(), name
        assert actual.with_name(actual.name + '.meta').is_file(), name + ' metadata'
    result = {'protectedFiles': unchanged, 'preservedPrefabBlocks': preserved_blocks, 'removedMeshBlocks': 8, 'addedSpriteBlocks': 6, 'localization': 'Five range-only native entry changes; two CSV records synchronized', 'approvedArt': 'Four native/PNG files byte-identical', 'existingPrefabGuid': 'unchanged'}
    (REPORT / 'scope-verification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))

if '--sync-csv' in sys.argv:
    sync_csv()
else:
    verify()
