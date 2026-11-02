from pathlib import Path
import hashlib,json,re
root=Path.cwd();out=root/'outputs/npc-dialogue-pool-2026-11-02';checks=[]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
for filename in ['verified-copy-hashes.json','verified-source-hashes.json','protected-hashes.json']:
    for rel,expected in json.loads((out/filename).read_text()).items():
        check(digest(root/rel)==expected,'Final verified file/protected data hash: '+rel)
sources=json.loads((out/'verified-source-hashes.json').read_text())
for rel in sources:
    if not rel.endswith('.cs'):continue
    project='Gameplay.csproj' if '/Runtime/' in rel else 'Editor.csproj'
    check(rel.replace('/','\\') in (root/project).read_text(encoding='utf-8-sig'),'Generated project covers changed source: '+rel)
    check(all(line.rstrip()==line for line in (root/rel).read_text(encoding='utf-8-sig').splitlines()),'Source whitespace: '+rel)
docs=['Docs/ActiveTasks/npc-dialogue-cumulative-pool.md','Docs/ActiveTasks/npc-run-affection-dialogue.md','Docs/ActiveTasks/merchant-affection-rewards.md','Docs/SessionLogs/2026-11-02.md','Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md','Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md','Docs/TaskIndex.md']
for rel in docs:
    path=root/rel
    for target in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
        if '://' in target or target.startswith('#'):continue
        target=target.split('#')[0].strip('<>')
        check((path.parent/target).resolve().exists(),'Document local target exists: '+rel+' -> '+target)
check('NPC_AFFECTION_PASS: 337 checks.' in (out/'unity-validation.log').read_text(encoding='utf-8-sig'),'Native final pass marker')
(out/'final-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} final checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} final source/asset/document checks.')
