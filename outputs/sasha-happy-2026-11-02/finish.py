from pathlib import Path
import hashlib,json,os,re,shutil,tempfile

root=Path.cwd(); out=root/'outputs/sasha-happy-2026-11-02'; clone=root/'.shp'; base=out/'baseline'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
checks=[]
def check(ok,label):
    if not ok: raise AssertionError(label)
    checks.append('PASS '+label)
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
library='Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib'
happy='Assets/_Project/Art/Sprites/NPCs/merchant/merchant_happy.png'
validation='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
allowed=[happy,happy+'.meta',library,ink,ink[:-4]+'.json']
hashes=json.loads((out/'baseline-hashes.json').read_text())
for rel,sha in hashes.items():
    if rel!=validation:check(digest(root/rel)==sha,'Destination preimage/protected file unchanged: '+rel)
old=(base/ink).read_text(encoding='utf-8-sig'); new=(clone/ink).read_text(encoding='utf-8-sig')
check(new.count('# face: 1001: Happy')==2,'Exactly two Happy tags')
check(new.replace('# face: 1001: Happy','# face: 1001: Normal')==old,'Only approved two Ink face tags changed; text/choices/loc/affection untouched')
old_json=json.loads((base/(ink[:-4]+'.json')).read_text(encoding='utf-8-sig'))
new_json=json.loads((clone/(ink[:-4]+'.json')).read_text(encoding='utf-8-sig'))
diffs=[]
def compare(a,b,path=''):
    check(type(a)==type(b),'JSON node type preserved: '+path) if type(a)!=type(b) else None
    if isinstance(a,dict):
        assert a.keys()==b.keys();
        for key in a:compare(a[key],b[key],path+'/'+key)
    elif isinstance(a,list):
        assert len(a)==len(b)
        for i,(x,y) in enumerate(zip(a,b)):compare(x,y,path+'/'+str(i))
    elif a!=b:diffs.append((path,a,b))
compare(old_json,new_json)
check(len(diffs)==2 and all(a=='^face: 1001: Normal' and b=='^face: 1001: Happy' for _,a,b in diffs),'Compiled JSON differs only in the same two face tags')
check(digest(clone/happy)==(out/'attachment-hash.txt').read_text(),'PNG matches supplied bytes exactly')
guid=re.search(r'(?m)^guid: ([0-9a-f]{32})$',(clone/(happy+'.meta')).read_text())[1]
text=(clone/library).read_text()
check('m_Name: Happy' in text and guid in text,'Happy library entry binds Unity-generated sprite GUID')
for label in ['Normal','Surprised']:
    pattern=r'(?ms)    - m_Name: '+label+r'\n.*?(?=    - m_Name:|    m_FromMain: 0\n    m_EntryOverrideCount:)'
    check(re.search(pattern,text)[0]==re.search(pattern,(base/library).read_text())[0],'Old library mapping preserved: '+label)
report=(clone/'validation.txt').read_text();check('PASS: 39 focused native checks.' in report,'Focused native import/library/Ink branch checks passed')
for rel in allowed:
    dst=root/rel
    if rel not in hashes:check(not dst.exists(),'New asset path is free: '+rel)
    fd,tmp=tempfile.mkstemp(prefix='.'+dst.name+'-',suffix='.happy-stage',dir=dst.parent);os.close(fd)
    try:shutil.copyfile(clone/rel,tmp);os.replace(tmp,dst)
    finally:Path(tmp).unlink(missing_ok=True)
    check(digest(dst)==digest(clone/rel),'Copied verified file: '+rel)
shutil.copyfile(clone/'validation.txt',out/'validation.txt')
(out/'verified-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in [*allowed,validation]},indent=2))
(out/'preservation.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} preservation/copyback checks.\n')
print(f'PASS: {len(checks)} preservation/copyback checks; five assets copied.')
