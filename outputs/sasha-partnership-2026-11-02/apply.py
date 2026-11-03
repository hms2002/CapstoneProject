from pathlib import Path
import csv,hashlib,io,json,os,re,shutil,tempfile
root=Path.cwd();out=root/'outputs/sasha-partnership-2026-11-02';base=out/'baseline';stage=out/'staging';checks=[]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
def read(folder,rel):return (folder/rel).read_text(encoding='utf-8-sig')
baseline=json.loads((out/'baseline-hashes.json').read_text());protected=json.loads((out/'protected-hashes.json').read_text())
for rel,sha in protected.items():check(digest(root/rel)==sha,'Protected metadata/portrait/NPC/route/prefab unchanged: '+rel)
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
pattern=r'(?ms)^=== affection_0_c ===\n.*?(?=^=== )'
check(re.sub(pattern,'',read(base,ink))==re.sub(pattern,'',read(stage,ink)),'Only partnership Ink knot replaced')
def stripped(obj):
    if isinstance(obj,dict):return {k:stripped(v) for k,v in obj.items() if k!='affection_0_c'}
    if isinstance(obj,list):return [stripped(v) for v in obj]
    return obj
check(stripped(json.loads(read(base,ink[:-4]+'.json')))==stripped(json.loads(read(stage,ink[:-4]+'.json'))),'All other compiled Ink containers preserved')
rows=json.loads((out/'rows.json').read_text(encoding='utf-8'));ids=json.loads((out/'ids.json').read_text())
def csvrows(folder):return {r[0]:r for r in csv.reader(io.StringIO(read(folder,'DataSheets/Localization/GameText.csv'))) if r}
a,b=csvrows(base),csvrows(stage);newkey='dialogue.merchant.run.affection_0_c.5'
check(set(b)-set(a)=={newkey} and set(a)<=set(b),'CSV adds only the second-opening key')
check(all(b[k]==v for k,v in a.items() if k not in rows),'Every unrelated CSV row preserved')
check(all(b[k]==[k,*v] for k,v in rows.items()),'Six approved text rows match all five locales')
def records(text):return {m[1]:m[0] for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text)}
shared='Assets/_Project/Data/Localization/GameText Shared Data.asset';a,b=records(read(base,shared)),records(read(stage,shared))
check(set(b)-set(a)=={ids[newkey]} and all(b[k]==v for k,v in a.items()),'Every old shared key/ID/raw record preserved; one key added')
check(read(stage,shared).replace(b[ids[newkey]],'',1)==read(base,shared),'Shared header/metadata/generator are unchanged')
for index,code in enumerate(['ko','en','ja','zh-Hans','zh-Hant']):
    rel='Assets/_Project/Data/Localization/GameText_'+code+'.asset';old,new=records(read(base,rel)),records(read(stage,rel));restored=read(stage,rel)
    check(set(new)-set(old)=={ids[newkey]} and set(old)<=set(new),'Locale adds only the new key ID: '+code)
    for key,values in rows.items():
        block=new[ids[key]];match=re.search(r'(?m)^    m_Localized: ([^\n]*)$',block)
        check(match is not None and json.loads(match[1])==values[index],'YAML string value equals CSV: '+key+'/'+code)
        if key!=newkey:
            p=r'(?ms)^    m_Localized: .*?(?=^    m_Metadata:)'
            check(re.sub(p,'',block)==re.sub(p,'',old[ids[key]]),'Locale ID/metadata retained: '+key+'/'+code)
            restored=restored.replace(block,old[ids[key]],1)
    restored=restored.replace(new[ids[newkey]],'',1)
    check(restored==read(base,rel),'All other locale bytes preserved: '+code)
source=read(stage,ink);active=set(re.findall(r'loc:([a-zA-Z0-9_.]+)',source));check(len(active)==51,'51 active merchant keys')
for line in source.splitlines():
    m=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if m:
        value=line.strip().split(' # loc:')[0]
        if value.startswith('+ ['):value=value[3:]
        check(csvrows(stage)[m[1]][1]==value,'Korean source/CSV match: '+m[1])
inv='DataSheets/Localization/InkInventory.json';a=json.loads(read(base,inv));b=json.loads(read(stage,inv))
check([r for r in a['rows'] if r['path']!=ink]==[r for r in b['rows'] if r['path']!=ink],'Other Ink inventories preserved')
check({r['key'] for r in b['rows'] if r['path']==ink}==active,'Merchant inventory covers every active line/choice')
check('PASS: 21 focused Ink checks' in (out/'ink-checks.txt').read_text(),'Production compiler/Ink branch verification passed')
files=[rel for rel in baseline if not rel.endswith('.cs')]
for rel in files:check(digest(root/rel)==baseline[rel],'No conflicting target edit: '+rel)
for rel in files:
    dst=root/rel;fd,tmp=tempfile.mkstemp(prefix='.'+dst.name+'-',suffix='.partnership-stage',dir=dst.parent);os.close(fd)
    try:shutil.copyfile(stage/rel,tmp);os.replace(tmp,dst)
    finally:Path(tmp).unlink(missing_ok=True)
    check(digest(dst)==digest(stage/rel),'Copied verified text/data file: '+rel)
fixture='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
(out/'verified-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in [*files,fixture]},indent=2))
(out/'data-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} focused data/preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} focused data/preservation checks; 10 text/data files applied.')
