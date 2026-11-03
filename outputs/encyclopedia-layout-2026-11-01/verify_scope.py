from pathlib import Path
import csv
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
BEFORE = OUT / 'before'
PREFAB = Path('Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab')


def records(path):
    return {m.group(2): (m.group(1), m.group(3)) for m in re.finditer(
        r'^--- !u!(\d+) &(\d+)([^\n]*\n.*?)(?=^--- !u!|\Z)',
        path.read_text(encoding='utf-8-sig'), re.M | re.S)}


def field(text, key):
    match = re.search(r'^  ' + re.escape(key) + r': (.*)$', text, re.M)
    return match.group(1) if match else ''


def ref(value):
    match = re.search(r'fileID: (\d+)', value)
    return match.group(1) if match else ''


def paths(blocks):
    transforms = {k: v[1] for k, v in blocks.items() if v[0] in ('4', '224')}
    by_go = {ref(field(v, 'm_GameObject')): k for k, v in transforms.items()}
    def node(k):
        if k not in transforms:
            return ''
        text = transforms[k]
        go = blocks.get(ref(field(text, 'm_GameObject')), ('', ''))[1]
        return node(ref(field(text, 'm_Father'))) + '/' + field(go, 'm_Name')
    return {k: node(by_go.get(k if v[0] == '1' else ref(field(v[1], 'm_GameObject')), '')) for k, v in blocks.items()}


before, after = records(BEFORE / PREFAB), records(ROOT / PREFAB)
old_paths, new_paths = paths(before), paths(after)
changed = [k for k in before.keys() & after.keys() if before[k] != after[k]]
missing = sorted(before.keys() - after.keys())
allowed = ['/Book/LeftPage/TitleGroup', '/Book/LeftPage/SlotGrid', '/Book/LeftPage/PageButtonGroup',
           '/Book/RightPage/ItemDetailPanel', '/Book/RightPage/ItemPageContent',
           '/Book/CreatureRightPage/UndiscoveredDetail', '/Book/CreatureFullIllustration']
unexpected = [k for k in changed if not any(p in old_paths[k] or p in new_paths[k] for p in allowed)
              and old_paths[k] not in ('/EncyclopediaUI/Book/RightPage', '/EncyclopediaUI/Book/CreatureRightPage')]
font_changes = [k for k in changed for name in ('m_fontAsset', 'm_sharedMaterial', 'm_fontSharedMaterials')
                if field(before[k][1], name) != field(after[k][1], name)]
with (BEFORE / 'DataSheets/Localization/GameText.csv').open(encoding='utf-8-sig', newline='') as stream:
    old_csv = {row[0]: row for row in list(csv.reader(stream))[1:]}
with (ROOT / 'DataSheets/Localization/GameText.csv').open(encoding='utf-8-sig', newline='') as stream:
    new_csv = {row[0]: row for row in list(csv.reader(stream))[1:]}
new_keys = sorted(new_csv.keys() - old_csv.keys())
expected_keys = sorted(['encyclopedia.item_not_acquired', 'encyclopedia.monster_not_encountered', 'encyclopedia.boss_not_encountered'])
save_checks = []
for record in json.loads((OUT / 'save-backup.json').read_text()):
    save_checks.append(hashlib.sha256(Path(record['path']).read_bytes()).hexdigest() == record['sha256'])
report = {
    'prefab_original_records': len(before), 'prefab_added_records': len(after.keys() - before.keys()),
    'changed_records': [{'id': k, 'path': old_paths[k] or new_paths[k], 'type': before[k][0]} for k in changed],
    'missing_records': missing, 'unexpected_changes': [{'id': k, 'path': old_paths[k]} for k in unexpected],
    'original_font_material_changes': font_changes,
    'original_csv_entries_preserved': all(new_csv.get(k) == v for k, v in old_csv.items()),
    'new_localization_keys': new_keys, 'expected_keys_only': new_keys == expected_keys,
    'five_translations_each': all(len(new_csv[k]) == 6 and all(new_csv[k]) for k in new_keys),
    'save_hashes_preserved': save_checks,
}
report['passed'] = (not missing and not unexpected and not font_changes
                    and report['original_csv_entries_preserved'] and report['expected_keys_only']
                    and report['five_translations_each'] and all(save_checks))
target = OUT / 'scope-report.json'
temp = target.with_suffix('.json.writing')
temp.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
temp.replace(target)
print(json.dumps({k: v for k, v in report.items() if k != 'changed_records'}, indent=2))
raise SystemExit(0 if report['passed'] else 1)
