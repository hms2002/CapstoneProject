from pathlib import Path
import csv, hashlib, io, json, re, shutil, sys

root=Path.cwd(); out=root/'outputs/npc-dialogue-pool-2026-11-02'; clone=root/'.ncv'; baseline=out/'baseline'
manifest=json.loads((out/'baseline-manifest.json').read_text()); checks=[]
def read(folder,rel):return (folder/rel).read_text(encoding='utf-8-sig')
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)

for rel in manifest:
    if rel.endswith('.cs') or rel.endswith('.ink') or rel.endswith('GameText.csv'):
        check(digest(root/rel)==digest(clone/rel),'Native source equals working source: '+rel)
for rel,expected in json.loads((out/'protected-hashes.json').read_text()).items():
    check(digest(root/rel)==expected,'Protected scene/reward/settings/placement: '+rel)

ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
source=read(root,ink); previous=read(baseline,ink)
check(re.sub(r'=== affection_0_c ===\n.*?(?==== affection_1_a ===)','',source,flags=re.S)==previous,'Previous Ink dialogue remains exact outside partnership knot')
keys=set(re.findall(r'loc:([a-zA-Z0-9_.]+)',source));check(len(keys)==43,'43 active source localization keys')
csv_path='DataSheets/Localization/GameText.csv'
def rows(folder):
    data=list(csv.reader(io.StringIO(read(folder,csv_path))));return data[0],{row[0]:row for row in data[1:]}
before_header,before=rows(baseline);after_header,after=rows(root)
added={f'dialogue.merchant.run.affection_0_c.{i}' for i in range(5)}
check(before_header==after_header,'CSV language header preserved')
check(set(after)-set(before)==added and set(before)<=set(after),'Exactly five CSV keys added')
check(all(after[key]==row for key,row in before.items()),'All previous CSV translations preserved')
check(all(len(after[key])==6 and all(after[key][1:]) for key in added),'25 new locale values complete')
for line in source.splitlines():
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0]
        if value.startswith('+ ['):value=value[3:]
        check(after[match[1]][1]==value,'Korean source equals CSV: '+match[1])

def entries(text):
    return {m[1]:m[0] for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text)}
for rel in ['Assets/_Project/Data/Localization/GameText Shared Data.asset', *['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]]:
    old,new=entries(read(baseline,rel)),entries(read(clone,rel))
    native_text=read(clone,rel)
    for key,block in old.items():
        if block==new.get(key):continue
        normalize=lambda value:re.sub(r'\\u([0-9a-fA-F]{4})',lambda m:'\\u'+m[1].upper(),value)
        equivalent=key in new and normalize(block)==normalize(new[key])
        if not equivalent and key in new:
            pattern=r'(?m)^    m_Localized: ([^\n]*)$'
            old_value=re.search(pattern,block);new_value=re.search(pattern,new[key])
            if old_value and new_value:
                decode=lambda value:json.loads(value) if value.startswith('"') and value.endswith('"') else value
                equivalent=(re.sub(pattern,'    m_Localized: <same-value>',block)==re.sub(pattern,'    m_Localized: <same-value>',new[key]) and decode(old_value[1])==decode(new_value[1]))
        check(equivalent,'Only cosmetic YAML quoting/escape casing changed on old record: '+rel+'/'+key)
        # Restore the user's original spelling of unrelated YAML escapes after native semantic validation.
        native_text=native_text.replace(new[key],block,1)
    (clone/rel).write_bytes(native_text.encode('utf-8'))
    new=entries(native_text)
    check(all(block==new.get(key) for key,block in old.items()),'All old localization records/IDs unchanged: '+rel)
    check(len(new)==len(old)+5,'Exactly five localization records added: '+rel)

npc='Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset'
old_npc,new_npc=read(baseline,npc),read(clone,npc)
pattern=r'(?ms)^  affectionDialogues:\n.*?(?=^  greetingPaths:)'
check(re.sub(pattern,'',old_npc)==re.sub(pattern,'',new_npc),'NPC fields/rewards/Ink binding unchanged outside candidate list')
paths=re.findall(r'  - affectionLevel: (\d+)\n    startPath: ([^\n]+)',new_npc)
check(paths==[('0','affection_0_a'),('0','affection_0_b'),('0','affection_0_c'),('1','affection_1_a'),('1','affection_1_b'),('2','affection_2_a'),('2','affection_2_b')],'Seven candidate paths with minimum thresholds 0/1/2')
inv='DataSheets/Localization/InkInventory.json'; old_inv=json.loads(read(baseline,inv)); new_inv=json.loads(read(root,inv))
check([r for r in old_inv['rows'] if r['path']!=ink]==[r for r in new_inv['rows'] if r['path']!=ink],'Other Ink source inventory entries unchanged')
check({r['key'] for r in new_inv['rows'] if r['path']==ink}==keys,'Merchant source inventory covers every active key')

report=read(clone,'outputs/npc-dialogue-pool-2026-11-02/validation.txt')
check('PASS:' in report and 'Every unlocked path is reachable' in report and 'Cap-positive choice continues once' in report,'Native pool/cap/state validation passed')
copy_files=[npc,'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json','Assets/_Project/Data/Localization/GameText Shared Data.asset', *['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]]
if '--copy' in sys.argv:
    for rel in copy_files:
        check(digest(root/rel) in (manifest[rel],digest(clone/rel)),'No conflicting edit before copy: '+rel)
    for rel in copy_files:shutil.copyfile(clone/rel,root/rel)
    for path in (clone/'outputs/npc-dialogue-pool-2026-11-02').glob('*'):
        if path.is_file():shutil.copyfile(path,out/path.name)
    (out/'verified-copy-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in copy_files},indent=2),encoding='utf-8')
    (out/'verified-source-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in manifest if rel.endswith(('.cs','.ink'))},indent=2),encoding='utf-8')
    checks.append('PASS Copied only approved NPC/JSON/localization assets; no existing metadata, prefab, scene, font or settings copied')
(out/'static-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} preservation checks.')
