from pathlib import Path
import hashlib,json,re
root=Path.cwd(); out=root/'outputs/sasha-happy-2026-11-02'; checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
digest=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
for rel,sha in json.loads((out/'verified-hashes.json').read_text()).items():
    check(digest(root/rel)==sha,'Final reviewed file: '+rel)
for rel,sha in json.loads((out/'baseline-hashes.json').read_text()).items():
    if rel not in json.loads((out/'verified-hashes.json').read_text()):
        check(digest(root/rel)==sha,'Protected file preserved: '+rel)
source='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
check(source.replace('/','\\') in (root/'Editor.csproj').read_text(encoding='utf-8-sig'),'Changed Editor source covered by generated project')
check(all(line.rstrip()==line for line in (root/source).read_text(encoding='utf-8-sig').splitlines()),'Source whitespace')
for rel in ['Docs/SessionLogs/2026-11-02.md','Docs/ActiveTasks/sasha-discount-portraits.md','Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md']:
    path=root/rel
    for link in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
        if '://' not in link and not link.startswith('#'):
            check((path.parent/link.split('#')[0].strip('<>')).resolve().exists(),'Doc target: '+link)
check('SASHA_HAPPY_PASS: 39 checks.' in (out/'unity-native.log').read_text(encoding='utf-8-sig'),'Focused native pass marker')
check(not (root/'.shp').exists(),'Owned scratch project removed')
(out/'final-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} final checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} final checks.')
