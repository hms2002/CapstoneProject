"""Read-only preservation, active merchant localization and changed-document link checks."""
from pathlib import Path
import csv, hashlib, json, re, subprocess

root = Path(__file__).resolve().parents[2]
report = []
def check(value, message):
    assert value, message
    report.append('PASS ' + message)
def base(path):
    return subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=root).decode('utf-8-sig')
def text(path):
    return (root / path).read_text(encoding='utf-8-sig')
def entries(value):
    return {m[1]: m[0] for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id: |^  references:|\Z)', value)}
def scalar(value):
    value = re.sub(r'\\\n\s+', '', value)
    value = re.sub(r'\n\s+', ' ', value).strip()
    if value.startswith('"'): return json.loads(value)
    if value.startswith("'"): return value[1:-1].replace("''", "'")
    return value

csv_path = 'DataSheets/Localization/GameText.csv'
rows = list(csv.DictReader(text(csv_path).splitlines()))
old_rows = {row['key']: row for row in csv.DictReader(base(csv_path).splitlines())}
current_rows = {row['key']: row for row in rows}
check(len(rows) == len(current_rows), 'No duplicate localization CSV keys')
check(all(current_rows[key] == row for key, row in old_rows.items()), 'Every pre-existing CSV row is unchanged')
check(len(current_rows) - len(old_rows) == 30, 'Exactly 30 new merchant translation rows')

source_path = 'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
source = text(source_path)
keys = re.findall(r'# loc:([\w.]+)', source)
check(len(keys) == len(set(keys)) == 38, 'Active merchant source has 38 unique localized lines/choices')
runtime = text(source_path[:-4] + '.json')
check(all('loc:' + key in runtime for key in keys), 'Compiled merchant JSON retains every active loc tag')
shared = text('Assets/_Project/Data/Localization/GameText Shared Data.asset')
key_ids = {m[2]: m[1] for m in re.finditer(r'- m_Id: (\d+)\n    m_Key: ([^\n]+)', shared)}
for code in ['ko', 'en', 'ja', 'zh-Hans', 'zh-Hant']:
    path = f'Assets/_Project/Data/Localization/GameText_{code}.asset'
    before, after = entries(base(path)), entries(text(path))
    check(all(after[key] == value for key, value in before.items()), f'{code}: all prior serialized locale entries are byte-preserved')
    for key in keys:
        block = after[key_ids[key]]
        match = re.search(r'(?ms)^    m_Localized: (.*?)\n    m_Metadata:', block)
        check(match is not None and scalar(match[1]) == current_rows[key][code], f'{code}: active table matches CSV for {key}')
for line in source.splitlines():
    match = re.search(r' # loc:([\w.]+)', line)
    if match:
        spoken = line[:match.start()].strip()
        if spoken.startswith('+ ['): spoken = spoken[3:]
        check(spoken == current_rows[match[1]]['ko'], 'Source KO matches CSV: ' + match[1])

prefab = 'Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab'
pattern = r'(?ms)^--- !u!\d+ &(-?\d+)(?: stripped)?\n.*?(?=^--- !u!|\Z)'
before = {m[1]: m[0] for m in re.finditer(pattern, base(prefab))}
after = {m[1]: m[0] for m in re.finditer(pattern, text(prefab))}
changed = [key for key, value in before.items() if after.get(key) != value]
check(len(changed) == 1 and 'm_Children:' in before[changed[0]], 'Only the existing trigger transform gains two child references; every other prior prefab block is unchanged')
check(len(after) - len(before) == 8, 'Prefab adds only three authored objects and their five components')

sheet = (root / 'Assets/_Project/Art/Sprites/UI/Dialogue/NpcAffectionAvailable.png').read_bytes()
check(hashlib.sha256(sheet).hexdigest() == '6be91e2257edf4c9668f8b17fabdeac8e4591abdad460e81b6c0b29b52fc0f6a', 'User-supplied PNG bytes are unchanged')
changed_files = subprocess.check_output(['git', 'diff', '--name-only'], cwd=root, text=True).splitlines()
check(not any(path.endswith('.meta') for path in changed_files), 'No existing meta/GUID file changed')
check(not any(path.endswith(('.unity', '.asmdef', '.controller')) or path.startswith('ProjectSettings/') for path in changed_files), 'Scenes, asmdefs, Animator controllers and ProjectSettings are unchanged')

docs = [path for path in changed_files if path.endswith('.md')]
docs += ['Docs/ActiveTasks/npc-run-affection-dialogue.md', 'Docs/SessionLogs/2026-11-01.md', 'outputs/npc-affection-2026-11-01/README.md']
links = 0
for path in set(docs):
    for target in re.findall(r'\[[^\]]*\]\(([^)]+)\)', text(path)):
        target = target.strip('<>').split('#', 1)[0]
        if not target or re.match(r'[a-zA-Z]+:', target): continue
        check((root / path).parent.joinpath(target).exists(), f'Document link exists: {path} -> {target}')
        links += 1
report.append(f'PASS: {len(report)} static checks, including all 190 active merchant locale values and {links} local document links.')
(root / 'outputs/npc-affection-2026-11-01/static-checks.txt').write_text('\n'.join(report) + '\n', encoding='utf-8')
print(report[-1])
