from pathlib import Path
import hashlib,json,re
root=Path.cwd();out=root/'outputs/sasha-partnership-2026-11-02';checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for filename in ['verified-hashes.json','protected-hashes.json']:
    for rel,sha in json.loads((out/filename).read_text()).items():check(digest(root/rel)==sha,'Final verified/protected file: '+rel)
source='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
check(source.replace('/','\\') in (root/'Editor.csproj').read_text(encoding='utf-8-sig'),'Generated Editor project covers changed fixture')
check(all(line.rstrip()==line for line in (root/source).read_text(encoding='utf-8-sig').splitlines()),'Fixture whitespace')
report=out/'final-checks.txt';report.touch(exist_ok=True)
for rel in ['Docs/SessionLogs/2026-11-02.md','Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md']:
    path=root/rel
    for link in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
        if '://' not in link and not link.startswith('#'):
            check((path.parent/link.split('#')[0].strip('<>')).resolve().exists(),'Document target: '+link)
report.write_text('\n'.join(checks)+f'\nPASS: {len(checks)} final checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} final checks.')
