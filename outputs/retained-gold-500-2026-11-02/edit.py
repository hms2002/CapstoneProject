from pathlib import Path
import hashlib,json,os,re,tempfile
root=Path.cwd();out=root/'outputs/retained-gold-500-2026-11-02';base=out/'baseline';assert not base.exists()
asset='Assets/_Project/Data/Dialogue/Affection/SashaGoldRetention.asset'
author='Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs'
validation='Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs'
csv='DataSheets/Localization/GameText.csv';inv='DataSheets/Localization/AssetInventory.json'
tables=['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]
files=[asset,author,validation,csv,inv,*tables];digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for rel in files:
    dst=base/rel;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes((root/rel).read_bytes())
(out/'baseline-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in files},indent=2))
protected=['Assets/_Project/Runtime/UI/HUD/CurrencyUI.cs','Assets/_Project/Runtime/Features/Loot/CurrencyManager.cs','Assets/_Project/Runtime/Infrastructure/Save/GamePlayDataManager.cs','Assets/_Project/Runtime/Infrastructure/Save/RunSessionProgressCommitPolicy.cs','Assets/_Project/Runtime/Infrastructure/Save/RunSessionLifecycleService.cs','Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs','Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','Assets/_Project/Data/Localization/GameText Shared Data.asset']
protected += [rel+'.meta' for rel in files if (root/(rel+'.meta')).exists()]
protected += [p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Data/Dialogue/Affection').glob('*.asset') if p.name!='SashaGoldRetention.asset']
(out/'protected-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in protected},indent=2))
def edit(rel,func):
    raw=(root/rel).read_bytes();nl='\r\n' if b'\r\n' in raw else '\n';text=raw.decode('utf-8').replace('\r\n','\n');new=func(text).replace('\n',nl).encode('utf-8')
    assert new!=raw,rel
    fd,tmp=tempfile.mkstemp(prefix='.'+(root/rel).name+'-',suffix='.gold500-stage',dir=(root/rel).parent);os.close(fd)
    try:Path(tmp).write_bytes(new);assert (root/rel).read_bytes()==raw;os.replace(tmp,root/rel)
    finally:Path(tmp).unlink(missing_ok=True)
edit(asset,lambda text:text.replace('    100\\uAE4C','    500\\uAE4C').replace('goldRetentionLimit: 100','goldRetentionLimit: 500'))
def authoring(text):
    text=text.replace('Configure(gold, "goldRetentionLimit", 100);','Configure(gold, "goldRetentionLimit", 500);')
    lines=text.splitlines(keepends=True)
    return ''.join(line.replace('100','500') if 'Text(gold,' in line else line for line in lines)
edit(author,authoring)
def tests(text):
    for a,b in [('level >= 2 ? 100 : 0','level >= 2 ? 500 : 0'),('goldRetentionLimit == 100','goldRetentionLimit == 500'),('new[] { 0, 60, 100, 180 }','new[] { 0, 60, 100, 180, 300, 499, 500, 501, 800 }'),('requestType, run, profile, 100','requestType, run, profile, 500'),('Math.Min(amount, 100)','Math.Min(amount, 500)')]:
        assert a in text,a;text=text.replace(a,b)
    return text
edit(validation,tests)
key='asset.9610f69b0692a734ab7aa1eccc8264f3.rewardText'
def csv_edit(text):
    lines=text.splitlines(keepends=True);selected=[i for i,line in enumerate(lines) if line.startswith(key+',')];assert len(selected)==1
    index=selected[0];assert lines[index].count('100')==5;lines[index]=lines[index].replace('100','500');return ''.join(lines)
edit(csv,csv_edit)
def inventory(text):
    p=r'(?ms)\{[^{}]*"key": "'+re.escape(key)+r'"[^{}]*\}';m=re.search(p,text);assert m and m[0].count('100')==1
    return text[:m.start()]+m[0].replace('100','500')+text[m.end():]
edit(inv,inventory)
shared=(root/'Assets/_Project/Data/Localization/GameText Shared Data.asset').read_text(encoding='utf-8-sig')
m=re.search(r'(?m)^  - m_Id: (\d+)\n    m_Key: '+re.escape(key)+r'\n',shared);assert m;id=m[1]
for rel in tables:
    def table(text):
        m=re.search(r'(?ms)^  - m_Id: '+id+r'\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text);assert m
        p=r'(?ms)(^    m_Localized: )(.*?)(?=^    m_Metadata:)';value=re.search(p,m[0]);assert value and value[2].count('100')==1
        new=m[0][:value.start(2)]+value[2].replace('100','500')+m[0][value.end(2):]
        return text[:m.start()]+new+text[m.end():]
    edit(rel,table)
(out/'localization-id.txt').write_text(id)
print('Raised reward cap, setup defaults, existing boundary expectations and all five localized descriptions to 500.')
