"""Read-only validation for the approved boss feedback assets and retained references."""
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[2]
def read(relative):return (ROOT/relative).read_text(encoding='utf-8-sig')
def block(text,id):
    found=re.search(r'^--- !u!\d+ &'+str(id)+r'(?: stripped)?\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)
    assert found, f'Missing Unity object {id}'
    return found.group()

ui=read('Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab')
tmp=block(ui,3519369922744626469)
assert 'm_VerticalAlignment: 256' in tmp
assert 'm_HorizontalAlignment: 1' in tmp
baseline=ROOT/'Temp/boss-feedback-assets-before/GlobalUIRoot.prefab'
if baseline.exists():
    old=baseline.read_text(encoding='utf-8-sig')
    assert ui==old.replace(block(old,3519369922744626469),block(old,3519369922744626469).replace('m_VerticalAlignment: 512','m_VerticalAlignment: 256'))

for i in range(1,4):
    table=read(f'Assets/_Project/Data/Loot/Tables/Table_Stage{i}.asset')
    profile=table.split('  chestWeaponCountProfile:\n')[1].split('  chestRelicCountProfile:')[0]
    assert 'minCount: 0\n    maxCount: 1' in profile
    assert re.findall(r'count: (\d+)\n      weight: (\d+)',profile)==[('0','50'),('1','50')]
    baseline=ROOT/f'Temp/boss-feedback-assets-before/Table_Stage{i}.asset'
    if baseline.exists():
        old=baseline.read_text(encoding='utf-8-sig')
        assert table.split('  chestRelicCountProfile:')[1]==old.split('  chestRelicCountProfile:')[1]

scene=read('Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity')
ids=re.findall(r'^--- !u!\d+ &(\d+)',scene,re.M)
assert len(ids)==len(set(ids))
go=block(scene,4104743717066173154)
tr=block(scene,7124681141591174978)
for id in (7811180001,7811180002):assert f'component: {{fileID: {id}}}' in go
for id in (7811180004,7811180006):assert f'- {{fileID: {id}}}' in tr
assert 'showHealthBar: 0' in block(scene,7811180001)
assert 'hudAnchor: {fileID: 7811180004}' in block(scene,7811180001)
assert 'm_Father: {fileID: 7124681141591174978}' in block(scene,7811180004)
assert 'm_TransformParent: {fileID: 7124681141591174978}' in block(scene,7811180005)
assert 'guid: 917a727de38bc4548b41ac0dae690b58' in block(scene,7811180005)
assert 'm_PrefabInstance: {fileID: 7811180005}' in block(scene,7811180006)
all_local_refs=[]
for id in range(7811180001,7811180007):
    all_local_refs += re.findall(r'\{fileID: (\d+)\}',block(scene,id))
assert all(id=='0' or id in ids for id in all_local_refs)
for name,expected in [('SangHyup_Boss_SlimeQueen',0),('HeoMinSeok_Boss_Shadow',0),('HeoMinSeok_Boss_Dragon',0),('LeeJunmo_Boss_DemonKing',1)]:
    assert f'  isFinalRouteSet: {expected}' in read(f'Assets/_Project/Scenes/{name}.unity')
print('BOSS_FEEDBACK_ASSETS_PASS: only dialogue vertical alignment, 50/50 quantity profiles, boss HUD graph/local references, final-boss policy.')
