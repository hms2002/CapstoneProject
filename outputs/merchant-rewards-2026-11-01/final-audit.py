from pathlib import Path
import hashlib, json, re

root = Path.cwd()
out = root / 'outputs/merchant-rewards-2026-11-01'
checks = []
def check(ok, text):
    if not ok: raise AssertionError(text)
    checks.append('PASS ' + text)
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path): return path.read_text(encoding='utf-8-sig')

copied = json.loads(read(out / 'authored-copy-manifest.json'))
for rel, expected in copied.items():
    check(digest(root / rel) == expected, 'Copied asset/metadata retains verified hash: ' + rel)
baseline = json.loads(read(out / 'baseline-manifest.json'))
check(digest(root / 'Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab') == baseline['Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab'], 'Manual merchant/bubble placement remains byte-preserved')
new_sources = [
    'Assets/_Project/Runtime/Features/Dialogue/Affection/Effect/MerchantAffectionRewardEffect.cs',
    'Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantOfferPricing.cs',
    'Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs',
    'Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs'
]
native_source_hashes = {}
source_record = out / 'verified-source-hashes.json'
if source_record.exists(): native_source_hashes = json.loads(read(source_record))
for rel in [rel for rel in baseline if rel.endswith('.cs')] + new_sources:
    native_file = root / '.mrv' / rel
    native_hash = digest(native_file) if native_file.exists() else native_source_hashes[rel]
    check(digest(root / rel) == native_hash, 'Final source equals Unity-verified source: ' + rel)
    native_source_hashes[rel] = native_hash
    check(not any(line.rstrip() != line for line in read(root / rel).splitlines()), 'No source trailing whitespace: ' + rel)

docs = [
    'Docs/ActiveTasks/merchant-affection-rewards.md', 'Docs/ActiveTasks/npc-run-affection-dialogue.md',
    'Docs/TaskIndex.md', 'Docs/SessionLogs/2026-11-01.md', 'Docs/DecisionLog.md',
    'Docs/StructureMemory/RunGoldAndShop.md', 'Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md',
    'Docs/StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md'
]
for rel in docs:
    path = root / rel
    links = re.findall(r'\]\(([^)]+)\)', read(path))
    for link in links:
        if '://' in link or link.startswith('#'): continue
        target = link.split('#')[0].split(' "')[0].strip('<>')
        check((path.parent / target).resolve().exists(), 'Document local link exists: ' + rel + ' -> ' + target)

report = read(out / 'validation.txt')
check('PASS: 301 native checks.' in report, 'Final native evidence is 301/301')
log = read(out / 'unity-final.log')
check('MERCHANT_REWARDS_PASS: 301 checks.' in log and 'error CS' not in log and 'Shader error' not in log, 'Final Unity log has pass marker and no C#/shader compile errors')
(out / 'final-source-doc-checks.txt').write_text('\n'.join(checks) + f'\nPASS: {len(checks)} checks.\n', encoding='utf-8')
source_record.write_text(json.dumps(native_source_hashes, indent=2), encoding='utf-8')
print(f'PASS: {len(checks)} final source/asset/document checks.')
