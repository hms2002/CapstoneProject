from pathlib import Path
import hashlib, json, re, shutil

root=Path.cwd(); out=root/'outputs/sasha-happy-2026-11-02'; clone=root/'.shp'
assert not clone.exists(), 'Do not overwrite an existing scratch project'
base=out/'baseline'; base.mkdir(parents=True,exist_ok=True)
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
library='Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib'
happy='Assets/_Project/Art/Sprites/NPCs/merchant/merchant_happy.png'
normal='Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png'
surprised='Assets/_Project/Art/Sprites/NPCs/merchant/merchant_surprised.png'
validation='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
files=[ink,ink[:-4]+'.json',library,library+'.meta',normal,normal+'.meta',surprised,surprised+'.meta',validation,'DataSheets/Localization/InkInventory.json','DataSheets/Localization/GameText.csv','Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','Assets/_Project/Prefabs/UI/Portraitlmage.prefab','Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab']
files += [p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Data/Localization').glob('*.asset')]
def copy(src,dst): dst.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(src,dst)
for rel in files: copy(root/rel,base/rel)
digest=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
(out/'baseline-hashes.json').write_text(json.dumps({r:digest(root/r) for r in files},indent=2))
for rel in [ink,library,library+'.meta',normal,normal+'.meta',surprised,surprised+'.meta']:
    copy(root/rel,clone/rel)
source=(clone/ink).read_text(encoding='utf-8-sig')
for path in ['affection_0_a','affection_1_c']:
    pattern=r'(?ms)(^=== '+path+r' ===\n)(.*?)(?=^=== |\Z)'
    m=re.search(pattern,source); assert m
    block=m[0]; pos=block.index('+ ['); tail=block[pos:]
    assert tail.count('# face: 1001: Normal')==1
    updated=block[:pos]+tail.replace('# face: 1001: Normal','# face: 1001: Happy',1)
    source=source[:m.start()]+updated+source[m.end():]
(clone/ink).write_bytes(source.encode('utf-8'))
attachment=Path('C:/Users/user/AppData/Local/Temp/codex-clipboard-d91d9b4f-726c-4c7d-8417-0dae9834e402.png')
copy(attachment,clone/happy)
(out/'attachment-hash.txt').write_text(digest(attachment))
copy(root/'Temp/bin/Debug/Ink-Libraries.dll',clone/'Assets/Plugins/Ink-Libraries.dll')
copy(out/'SashaHappyBatch.cs',clone/'Assets/Editor/SashaHappyBatch.cs')
copy(root/'ProjectSettings/ProjectVersion.txt',clone/'ProjectSettings/ProjectVersion.txt')
packages={}
for folder in (root/'Library/PackageCache').iterdir():
    if folder.is_dir() and (folder/'package.json').exists():
        info=json.loads((folder/'package.json').read_text(encoding='utf-8-sig')); packages[info['name']]=(folder,info)
deps={}; queue=['com.unity.2d.animation','com.unity.modules.imageconversion','com.unity.modules.jsonserialize']
while queue:
    name=queue.pop()
    if name in deps: continue
    if name in packages:
        folder,info=packages[name]; deps[name]='file:'+folder.as_posix(); queue.extend(info.get('dependencies',{}))
    elif name.startswith('com.unity.modules.'): deps[name]='1.0.0'
    else: raise RuntimeError('Missing installed package '+name)
(clone/'Packages').mkdir(parents=True,exist_ok=True)
(clone/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps},indent=2))
print('Prepared minimal portrait/Ink-only Unity workspace; packages:',len(deps))
