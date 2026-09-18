from pathlib import Path
import re

root=Path(__file__).resolve().parents[1]
backup=root/'Temp/boss-feedback-assets-before'
backup.mkdir(parents=True,exist_ok=True)
changed=[]
def save(path,before,after):
    if before==after:return
    destination=backup/path.name
    if not destination.exists():destination.write_bytes(path.read_bytes())
    path.write_text(after,encoding='utf-8',newline='')
    changed.append(str(path.relative_to(root)))
def read(path):
    return path.read_bytes().decode('utf-8-sig')
def block(text,id):
    match=re.search(r'^--- !u!\d+ &'+str(id)+r'(?: stripped)?\r?\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)
    assert match, id
    return match.group()

# Only the bound dialogue body TMP vertical alignment changes.
path=root/'Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab'
before=read(path)
body=block(before,3519369922744626469)
assert 'm_VerticalAlignment: 512' in body
after=before.replace(body,body.replace('m_VerticalAlignment: 512','m_VerticalAlignment: 256'))
save(path,before,after)

for i in range(1,4):
    path=root/f'Assets/_Project/Data/Loot/Tables/Table_Stage{i}.asset'
    before=read(path)
    newline='\r\n' if '\r\n' in before else '\n'
    replacement='\n'.join(['  chestWeaponCountProfile:','    minCount: 0','    maxCount: 1','    weights:','    - count: 0','      weight: 50','    - count: 1','      weight: 50','']).replace('\n',newline)
    after,n=re.subn(r'  chestWeaponCountProfile:\r?\n.*?(?=  chestRelicCountProfile:)',lambda _:replacement,before,flags=re.S)
    assert n==1
    save(path,before,after)

# Add the same authored status components and nested HUD used by DemonKing.prefab.
path=root/'Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity'
before=read(path)
assert 'guid: bd236fe0fd3a852418b8fd1eeed3ba29' not in before
source=read(root/'Assets/_Project/Prefabs/Bosses/DemonKing/DemonKing.prefab')
ids=[3912652946484721394,5641705097071250553,6676367481214134617,9188518342805337522,7473390132855219111,637836406317778627]
mapping={str(old):str(7811180001+i) for i,old in enumerate(ids)}
mapping.update({'945520286179334166':'4104743717066173154','1708932354766596942':'7124681141591174978'})
for new in list(mapping.values())[:6]:assert not re.search(r'^--- !u!\d+ &'+new+r'\b',before,re.M)
addition=''.join(block(source,id) for id in ids)
addition=re.sub(r'(?<=&)\d+|(?<=fileID: )\d+',lambda m:mapping.get(m.group(),m.group()),addition)
go=block(before,4104743717066173154)
updated=go.replace('  m_Layer: 31','  - component: {fileID: 7811180001}\n  - component: {fileID: 7811180002}\n  m_Layer: 31')
tr=block(before,7124681141591174978)
updated_tr=tr.replace('  m_Children:\n','  m_Children:\n  - {fileID: 7811180004}\n  - {fileID: 7811180006}\n')
# Normalize only replacement snippets to the scene's original line endings.
if '\r\n' in before:
    updated=go.replace('  m_Layer: 31','  - component: {fileID: 7811180001}\r\n  - component: {fileID: 7811180002}\r\n  m_Layer: 31')
    updated_tr=tr.replace('  m_Children:\r\n','  m_Children:\r\n  - {fileID: 7811180004}\r\n  - {fileID: 7811180006}\r\n')
assert updated!=go and updated_tr!=tr
after=before.replace(go,updated).replace(tr,updated_tr)
insert=after.find('--- !u!1660057539')
assert insert>=0
after=after[:insert]+addition+after[insert:]
declared=re.findall(r'^--- !u!\d+ &(\d+)',after,re.M)
assert len(declared)==len(set(declared))
save(path,before,after)
print('\n'.join(changed))
