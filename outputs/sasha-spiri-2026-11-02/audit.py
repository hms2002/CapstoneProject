from pathlib import Path
import csv,hashlib,io,json,os,re,shutil,tempfile
root=Path.cwd();out=root/'outputs/sasha-spiri-2026-11-02';base=out/'baseline';clone=root/'.ssv';checks=[]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
def read(folder,rel):return (folder/rel).read_text(encoding='utf-8-sig')
manifest=json.loads((out/'baseline-hashes.json').read_text())
for rel,sha in json.loads((out/'protected-hashes.json').read_text()).items():check(digest(root/rel)==sha,'Protected file unchanged: '+rel)
sources=[rel for rel in manifest if rel.endswith('.cs')]
for rel in sources:check(digest(root/rel)==digest(clone/rel),'Native project uses final source: '+rel)
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
pattern=r'(?ms)^=== affection_0_b ===\n.*?(?=^=== )'
check(re.sub(pattern,'',read(base,ink))==re.sub(pattern,'',read(clone,ink)),'All other Ink conversations/choices/outcomes preserved')
source=read(clone,ink);newkeys=set(re.findall(r'loc:([a-zA-Z0-9_.]+)',source))
changed=json.loads((out/'changed-rows.json').read_text(encoding='utf-8'));changedkeys=set(changed)
check(len(newkeys)==50,'50 active merchant localization keys')
def rows(folder):return {row[0]:row for row in csv.reader(io.StringIO(read(folder,'DataSheets/Localization/GameText.csv'))) if row}
old,new=rows(base),rows(clone)
check(set(new)-set(old)=={'dialogue.merchant.run.affection_0_b.5'} and set(old)<=set(new),'Only second-opening key added to CSV')
check(all(new[key]==row for key,row in old.items() if key not in changedkeys),'All unrelated CSV rows preserved')
check(all(new[key]==[key,*values] for key,values in changed.items()),'All 30 replacement/added translations match approved staging')
for line in source.splitlines():
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0]
        if value.startswith('+ ['):value=value[3:]
        check(new[match[1]][1]==value,'Korean source/CSV match: '+match[1])
def entries(text):return {m[1]:m[0] for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text)}
shared='Assets/_Project/Data/Localization/GameText Shared Data.asset'
old_shared=entries(read(base,shared))
allowed_ids={key for key,block in old_shared.items() if any('m_Key: '+candidate+'\n' in block for candidate in changedkeys)}
check(len(allowed_ids)==5,'Five existing localization IDs retained for replacement text')
tables=[shared,*['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]]
for rel in tables:
    old,new=entries(read(base,rel)),entries(read(clone,rel));text=read(clone,rel)
    check(len(new)==len(old)+1 and set(old)<=set(new),'Exactly one shared/locale record added: '+rel)
    for key,block in old.items():
        if rel!=shared and key in allowed_ids:
            pattern_value=r'(?ms)^    m_Localized: .*?(?=^    m_Metadata:)'
            check(re.sub(pattern_value,'    m_Localized: <value>\n',block)==re.sub(pattern_value,'    m_Localized: <value>\n',new[key]),'Replaced locale ID/metadata unchanged: '+rel+'/'+key)
            continue
        if block==new[key]:continue
        normalize=lambda value:re.sub(r'\\u([0-9a-fA-F]{4})',lambda m:'\\u'+m[1].upper(),value)
        equivalent=normalize(block)==normalize(new[key])
        if not equivalent:
            p=r'(?m)^    m_Localized: ([^\n]*)$';a=re.search(p,block);b=re.search(p,new[key])
            if a and b:
                decode=lambda value:json.loads(value) if value.startswith('"') and value.endswith('"') else value
                equivalent=re.sub(p,'    m_Localized: <same>',block)==re.sub(p,'    m_Localized: <same>',new[key]) and decode(a[1])==decode(b[1])
        check(equivalent,'Only cosmetic serialization drift: '+rel+'/'+key)
        text=text.replace(new[key],block,1)
    (clone/rel).write_bytes(text.encode('utf-8'));restored=entries(text)
    check(all(restored[key]==block for key,block in old.items() if rel==shared or key not in allowed_ids),'All unrelated old locale records preserved: '+rel)
npc='Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset';a=read(base,npc);b=read(clone,npc)
check(re.sub(r'(?m)^    requiredThemeId:.*\n','',b)==a,'NPC changes only add required-theme fields to existing candidates')
check('startPath: affection_0_b\n    requiredThemeId: dragon' in b and b.count('requiredThemeId: dragon')==1,'Only affection_0_b requires dragon')
inv='DataSheets/Localization/InkInventory.json';a=json.loads(read(base,inv));b=json.loads(read(clone,inv))
check([r for r in a['rows'] if r['path']!=ink]==[r for r in b['rows'] if r['path']!=ink],'Unrelated Ink inventory preserved')
check({r['key'] for r in b['rows'] if r['path']==ink}==newkeys,'Inventory covers all active merchant lines')
report=read(clone,'outputs/sasha-spiri-2026-11-02/validation.txt')
check('focused native checks; production routing' in report and 'PASS:' in report,'Native stage/Ink/localization checks passed')
files=[ink,ink[:-4]+'.json',npc,'DataSheets/Localization/GameText.csv',inv,*tables]
for rel in files:check(digest(root/rel)==manifest[rel],'No conflicting destination edit: '+rel)
for rel in files:
    dst=root/rel;fd,tmp=tempfile.mkstemp(prefix='.'+dst.name+'-',suffix='.spiri-stage',dir=dst.parent);os.close(fd)
    try:shutil.copyfile(clone/rel,tmp);os.replace(tmp,dst)
    finally:Path(tmp).unlink(missing_ok=True)
    check(digest(dst)==digest(clone/rel),'Verified atomic copy: '+rel)
shutil.copyfile(clone/'outputs/sasha-spiri-2026-11-02/validation.txt',out/'validation.txt')
(out/'verified-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in [*files,*sources]},indent=2))
(out/'preservation.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} preservation checks; {len(files)} reviewed asset/data files copied.')
