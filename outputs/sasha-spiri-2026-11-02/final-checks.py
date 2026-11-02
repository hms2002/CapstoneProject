from pathlib import Path
import hashlib,json,re
root=Path.cwd();out=root/'outputs/sasha-spiri-2026-11-02';checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
digest=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
for filename in ['verified-hashes.json','protected-hashes.json']:
    for rel,sha in json.loads((out/filename).read_text()).items():check(digest(root/rel)==sha,'Final asset/source/protected hash: '+rel)
sources=[rel for rel in json.loads((out/'verified-hashes.json').read_text()) if rel.endswith('.cs')]
for rel in sources:
    project='Gameplay.csproj' if '/Runtime/' in rel else 'Editor.csproj'
    check(rel.replace('/','\\') in (root/project).read_text(encoding='utf-8-sig'),'Generated project covers source: '+rel)
    check(all(line.rstrip()==line for line in (root/rel).read_text(encoding='utf-8-sig').splitlines()),'Source whitespace: '+rel)
report=out/'final-checks.txt';report.touch(exist_ok=True)
for rel in ['Docs/ActiveTasks/sasha-spiri-dialogue.md','Docs/TaskIndex.md','Docs/SessionLogs/2026-11-02.md','Docs/DecisionLog.md','Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md']:
    path=root/rel
    for link in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
        if '://' not in link and not link.startswith('#'):
            check((path.parent/link.split('#')[0].strip('<>')).resolve().exists(),'Document target: '+link)
check('SASHA_SPIRI_PASS:' in (out/'unity-native.log').read_text(encoding='utf-8-sig'),'Focused native pass marker')
check(not (root/'.ssv').exists(),'Owned native scratch project removed')
report.write_text('\n'.join(checks)+f'\nPASS: {len(checks)} final checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} final checks.')
