from pathlib import Path
import hashlib, json, re, sys

root = Path.cwd()
out = root / 'outputs/sasha-portraits-2026-11-02'
checks = []
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def check(ok, label):
    if not ok:
        raise AssertionError(label)
    checks.append('PASS ' + label)

for filename in ['verified-copy-hashes.json', 'verified-source-hashes.json', 'protected-hashes.json']:
    for rel, expected in json.loads((out / filename).read_text()).items():
        check(digest(root / rel) == expected, 'Final verified file/protected hash: ' + rel)
baseline = json.loads((out / 'baseline-manifest.json').read_text())
for rel, expected in baseline.items():
    if rel.endswith('.meta'):
        check(digest(root / rel) == expected, 'Old metadata identity preserved: ' + rel)
sources = json.loads((out / 'verified-source-hashes.json').read_text())
for rel in sources:
    project = root / 'Editor.csproj'
    check(rel.replace('/', '\\') in project.read_text(encoding='utf-8-sig'), 'Generated project covers changed source: ' + rel)
    check(all(line.rstrip() == line for line in (root / rel).read_text(encoding='utf-8-sig').splitlines()), 'Source whitespace: ' + rel)

# Create the report before validating documentation links to this result.
report = out / 'final-checks.txt'
report.touch(exist_ok=True)
docs = ['Docs/ActiveTasks/sasha-discount-portraits.md', 'Docs/ActiveTasks/npc-dialogue-cumulative-pool.md', 'Docs/SessionLogs/2026-11-02.md', 'Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md', 'Docs/TaskIndex.md', 'Docs/ErrorLog.md']
for rel in docs:
    path = root / rel
    for target in re.findall(r'\]\(([^)]+)\)', path.read_text(encoding='utf-8-sig')):
        if '://' in target or target.startswith('#'):
            continue
        target = target.split('#')[0].strip('<>')
        check((path.parent / target).resolve().exists(), 'Document local target exists: ' + rel + ' -> ' + target)
log = (out / 'unity-validation.log').read_text(encoding='utf-8-sig')
check('NPC_AFFECTION_PASS: 430 checks.' in log, 'Native final pass marker')
check(not re.search(r'error CS\d+|Exception:|Shader error', log), 'No native CSharp/exception/shader error')
check('PASS: 232 preservation checks.' in (out / 'static-checks.txt').read_text(encoding='utf-8-sig'), 'Copyback preservation receipt')
if '--after-cleanup' in sys.argv:
    check(not (root / '.spv').exists(), 'Owned native scratch project removed')
report.write_text('\n'.join(checks) + f'\nPASS: {len(checks)} final checks.\n', encoding='utf-8')
print(f'PASS: {len(checks)} final source/asset/document checks.')
