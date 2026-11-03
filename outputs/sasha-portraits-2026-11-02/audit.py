from pathlib import Path
import csv,hashlib,io,json,os,re,shutil,sys,tempfile
root=Path.cwd();out=root/'outputs/sasha-portraits-2026-11-02';clone=root/'.spv';base=out/'baseline';checks=[]
manifest=json.loads((out/'baseline-manifest.json').read_text())
def read(folder,rel):return (folder/rel).read_text(encoding='utf-8-sig')
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
def atomic_copy(src,dest):
    dest.parent.mkdir(parents=True,exist_ok=True)
    # Unity can memory-map an asset while open; replace its file atomically instead of truncating it.
    handle,temp=tempfile.mkstemp(prefix='.'+dest.name+'-',suffix='.sasha-stage',dir=dest.parent)
    os.close(handle);temporary=Path(temp)
    try:shutil.copyfile(src,temporary);os.replace(temporary,dest)
    finally:
        if temporary.exists():temporary.unlink()
for rel,expected in json.loads((out/'protected-hashes.json').read_text()).items():
    check(digest(root/rel)==expected,'Protected scene/reward/placement/runtime/settings: '+rel)
for rel in manifest:
    if rel.endswith('.cs'):check(digest(root/rel)==digest(clone/rel),'Native-verified Editor source: '+rel)
for rel,expected in json.loads((out/'supplied-image-hashes.json').read_text()).items():
    check(digest(clone/rel)==expected,'Exact supplied PNG retained: '+rel)
for rel in ['Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png.meta','Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib.meta']:
    check(digest(root/rel)==manifest[rel] and digest(clone/rel)==manifest[rel],'Existing texture/library metadata and GUID preserved: '+rel)
actor='Assets/_Project/Prefabs/UI/Portraitlmage.prefab'
check(digest(root/actor)==digest(clone/actor),'Authored portrait prefab unchanged by native preview')

ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
source=read(clone,ink);previous=read(base,ink);keys=set(re.findall(r'loc:([a-zA-Z0-9_.]+)',source))
check(len(keys)==49,'49 active merchant source keys')
check(len(re.findall(r'^    # face: 1001: Surprised$',source,re.M))==8,'All eight no-gain choice branches use Surprised')
old_without_faces=re.sub(r'(?m)^\s*# face:[^\n]*\n','',previous)
new_without_knot=re.sub(r'=== affection_1_c ===\n.*?(?==== affection_2_a ===)','',source,flags=re.S)
check(re.sub(r'(?m)^\s*# face:[^\n]*\n','',new_without_knot)==old_without_faces,'Old Ink dialogue/outcomes unchanged outside approved face tags')
csv_path='DataSheets/Localization/GameText.csv'
def rows(folder):
    data=list(csv.reader(io.StringIO(read(folder,csv_path))));return data[0],{row[0]:row for row in data[1:]}
old_head,old_rows=rows(base);new_head,new_rows=rows(clone)
new_keys={f'dialogue.merchant.run.affection_1_c.{i}' for i in range(6)}
check(old_head==new_head,'CSV language header unchanged')
check(set(new_rows)-set(old_rows)==new_keys and set(old_rows)<=set(new_rows),'Exactly six new CSV keys')
check(all(row==new_rows[key] for key,row in old_rows.items()),'All existing CSV translations unchanged')
check(all(len(new_rows[key])==6 and all(new_rows[key][1:]) for key in new_keys),'30 new locale values are complete')
for line in source.splitlines():
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0]
        if value.startswith('+ ['):value=value[3:]
        check(new_rows[match[1]][1]==value,'Korean source matches CSV: '+match[1])

def entries(text):return {m[1]:m[0] for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text)}
tables=['Assets/_Project/Data/Localization/GameText Shared Data.asset',*['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]]
for rel in tables:
    old,new=entries(read(base,rel)),entries(read(clone,rel));native_text=read(clone,rel)
    for key,block in old.items():
        if block==new.get(key):continue
        normalize=lambda value:re.sub(r'\\u([0-9a-fA-F]{4})',lambda m:'\\u'+m[1].upper(),value)
        equivalent=key in new and normalize(block)==normalize(new[key])
        if not equivalent and key in new:
            pattern=r'(?m)^    m_Localized: ([^\n]*)$';old_value=re.search(pattern,block);new_value=re.search(pattern,new[key])
            if old_value and new_value:
                decode=lambda value:json.loads(value) if value.startswith('"') and value.endswith('"') else value
                equivalent=(re.sub(pattern,'    m_Localized: <same>',block)==re.sub(pattern,'    m_Localized: <same>',new[key]) and decode(old_value[1])==decode(new_value[1]))
        check(equivalent,'Only cosmetic YAML drift on old locale record: '+rel+'/'+key)
        native_text=native_text.replace(new[key],block,1)
    (clone/rel).write_bytes(native_text.encode('utf-8'));new=entries(native_text)
    check(all(block==new.get(key) for key,block in old.items()),'All old locale IDs/records preserved: '+rel)
    check(len(new)==len(old)+6,'Exactly six entries added: '+rel)
npc='Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset';old_npc,new_npc=read(base,npc),read(clone,npc)
pattern=r'(?ms)^  affectionDialogues:\n.*?(?=^  greetingPaths:)'
check(re.sub(pattern,'',old_npc)==re.sub(pattern,'',new_npc),'NPC fields unchanged outside approved candidate list')
paths=re.findall(r'  - affectionLevel: (\d+)\n    startPath: ([^\n]+)',new_npc)
check(paths==[('0','affection_0_a'),('0','affection_0_b'),('0','affection_0_c'),('1','affection_1_a'),('1','affection_1_b'),('1','affection_1_c'),('2','affection_2_a'),('2','affection_2_b')],'Eight cumulative candidate paths and thresholds')
inv='DataSheets/Localization/InkInventory.json';old_inv=json.loads(read(base,inv));new_inv=json.loads(read(clone,inv))
check([r for r in old_inv['rows'] if r['path']!=ink]==[r for r in new_inv['rows'] if r['path']!=ink],'Unrelated Ink inventory unchanged')
check({r['key'] for r in new_inv['rows'] if r['path']==ink}==keys,'Merchant inventory covers every active source key')
surprise_meta='Assets/_Project/Art/Sprites/NPCs/merchant/merchant_surprised.png.meta'
guid=re.search(r'^guid: ([0-9a-f]{32})$',read(clone,surprise_meta),re.M)[1]
library='Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib'
lib=read(clone,library)
check('m_Name: Surprised' in lib and guid in lib and 'b83e15b332c652242a1bee028b89af00' in lib,'Library binds old Normal identity and Unity-generated Surprised GUID')
report=read(clone,'outputs/sasha-portraits-2026-11-02/validation.txt')
check('PASS: 430 checks;' in report,'430 native checks passed')
copy_files=['Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png','Assets/_Project/Art/Sprites/NPCs/merchant/merchant_surprised.png',surprise_meta,library,npc,ink,'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json',csv_path,inv,*tables]
if '--copy' in sys.argv:
    for rel in copy_files:
        current=root/rel
        if rel in manifest:check(digest(current) in (manifest[rel],digest(clone/rel)),'No conflicting destination edit: '+rel)
        else:check(not current.exists() or digest(current)==digest(clone/rel),'New destination is absent or exact: '+rel)
    for rel in copy_files:
        atomic_copy(clone/rel,root/rel)
    for path in (clone/'outputs/sasha-portraits-2026-11-02').glob('*'):
        if path.is_file():shutil.copyfile(path,out/path.name)
    (out/'verified-copy-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in copy_files},indent=2),encoding='utf-8')
    (out/'verified-source-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in manifest if rel.endswith('.cs')},indent=2),encoding='utf-8')
    checks.append('PASS Copied only reviewed portraits/library/NPC/Ink/localization; no UI prefab, scene, existing metadata, settings or reward copied')
(out/'static-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} preservation checks.')
