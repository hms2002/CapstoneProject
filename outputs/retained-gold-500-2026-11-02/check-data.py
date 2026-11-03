from pathlib import Path
import csv,hashlib,io,json,re
root=Path.cwd();out=root/'outputs/retained-gold-500-2026-11-02';base=out/'baseline';checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def read(folder,rel):return (folder/rel).read_text(encoding='utf-8-sig')
for rel,sha in json.loads((out/'protected-hashes.json').read_text()).items():check(digest(root/rel)==sha,'Existing credit/HUD/lifecycle/metadata/reward preserved: '+rel)
asset='Assets/_Project/Data/Dialogue/Affection/SashaGoldRetention.asset'
check('goldRetentionLimit: 500' in read(root,asset),'Authored reward cap is 500')
check(read(root,asset).replace('    500\\uAE4C','    100\\uAE4C').replace('goldRetentionLimit: 500','goldRetentionLimit: 100')==read(base,asset),'Only cap and fallback description changed on reward asset')
author='Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs';text=read(root,author)
check('Configure(gold, "goldRetentionLimit", 500);' in text,'Reapplying setup preserves the 500 cap')
restored=text.replace('Configure(gold, "goldRetentionLimit", 500);','Configure(gold, "goldRetentionLimit", 100);')
restored=''.join(line.replace('500','100') if 'Text(gold,' in line else line for line in restored.splitlines(keepends=True))
check(read(base,author)==restored,'Authoring changes only reward cap and five descriptions')
key='asset.9610f69b0692a734ab7aa1eccc8264f3.rewardText';csvpath='DataSheets/Localization/GameText.csv'
def csvrows(folder):return {r[0]:r for r in csv.reader(io.StringIO(read(folder,csvpath))) if r}
a,b=csvrows(base),csvrows(root)
check(a.keys()==b.keys(),'No localization keys added or removed')
check(all(v==b[k] for k,v in a.items() if k!=key),'Other CSV translations preserved')
check(b[key]==[v.replace('100','500') for v in a[key]],'All five reward translations updated to 500')
id=(out/'localization-id.txt').read_text()
for code in ['ko','en','ja','zh-Hans','zh-Hant']:
    rel='Assets/_Project/Data/Localization/GameText_'+code+'.asset';old,new=read(base,rel),read(root,rel)
    p=r'(?ms)^  - m_Id: '+id+r'\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)';a=re.search(p,old);b=re.search(p,new)
    restored=re.sub(r'(?ms)(^    m_Localized: )(.*?)(?=^    m_Metadata:)',lambda m:m[1]+m[2].replace('500','100'),b[0])
    check(a is not None and b is not None and restored==a[0],'Existing reward locale ID/metadata retained: '+code)
    check(new.replace(b[0],a[0],1)==old,'All unrelated locale bytes preserved: '+code)
rel='DataSheets/Localization/AssetInventory.json';a=json.loads(read(base,rel));b=json.loads(read(root,rel))
check([r for r in a['rows'] if r['key']!=key]==[r for r in b['rows'] if r['key']!=key],'Unrelated asset inventory rows retained')
row=next(r for r in b['rows'] if r['key']==key);old=next(r for r in a['rows'] if r['key']==key)
check(row==dict(old,text=old['text'].replace('100','500')),'Reward inventory reflects the new text only')
validation='Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs';text=read(root,validation)
for token in ['level >= 2 ? 500 : 0','goldRetentionLimit == 500','new[] { 0, 60, 100, 180, 300, 499, 500, 501, 800 }','requestType, run, profile, 500','Math.Min(amount, 500)']:
    check(token in text,'Existing validation updated: '+token)
for rel in [author,validation]:
    check(rel.replace('/','\\') in read(root,'Editor.csproj'),'Generated project covers changed source: '+rel)
    check(all(line.rstrip()==line for line in read(root,rel).splitlines()),'Source whitespace: '+rel)
(out/'verified-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in json.loads((out/'baseline-hashes.json').read_text())},indent=2))
(out/'data-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} configuration/localization/preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} configuration/localization/preservation checks.')
