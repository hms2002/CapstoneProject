from pathlib import Path
import json,re,shutil,xml.etree.ElementTree as ET

root=Path.cwd();out=root/'outputs/merchant-rewards-2026-11-01';clone=root/'.mrv'
clone.mkdir(exist_ok=True)
(out/'authoring-request.json').unlink(missing_ok=True)

# Add missing sources only to an external MSBuild import. Never edit Unity-generated project files.
groups={
 'Gameplay':['Assets/_Project/Runtime/Features/Dialogue/Affection/Effect/MerchantAffectionRewardEffect.cs','Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantOfferPricing.cs'],
 'Editor':['Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs','Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs']
}
doc=ET.Element('Project',xmlns='http://schemas.microsoft.com/developer/msbuild/2003')
coverage={}
for project,files in groups.items():
 existing=(root/(project+'.csproj')).read_text(encoding='utf-8-sig')
 group=ET.SubElement(doc,'ItemGroup',Condition=f"'$(MSBuildProjectName)' == '{project}'")
 coverage[project]=[]
 for rel in files:
  if rel.replace('/','\\') not in existing:ET.SubElement(group,'Compile',Include=str(root/rel))
  coverage[project].append(rel)
ET.ElementTree(doc).write(out/'include-task-sources.targets',encoding='utf-8',xml_declaration=True)
(out/'msbuild-source-coverage.json').write_text(json.dumps(coverage,indent=2),encoding='utf-8')

copied=set()
def copy(rel):
 src=root/rel;dst=clone/rel
 if not src.is_file():return
 dst.parent.mkdir(parents=True,exist_ok=True)
 if not dst.exists() or dst.stat().st_size!=src.stat().st_size or dst.stat().st_mtime_ns<src.stat().st_mtime_ns:shutil.copyfile(src,dst)
 copied.add(rel.as_posix())
def code_tree(rel):
 for p in (root/rel).rglob('*'):
  if p.is_file() and (p.suffix in ('.cs','.asmdef','.dll','.rsp') or p.name.endswith(('.cs.meta','.asmdef.meta','.dll.meta','.rsp.meta'))):copy(p.relative_to(root))
for rel in ['Assets/_Project/Runtime','Assets/_Project/Editor','Assets/Plugins','Assets/Ink']:code_tree(rel)
for rel in ['Packages','ProjectSettings']:
 for p in (root/rel).rglob('*'):
  if p.is_file():copy(p.relative_to(root))

# Resolve the exact serialized dependency closure, including source-script GUIDs, nested prefabs and fonts.
index={}
for p in (root/'Assets').rglob('*.meta'):
 try:
  with p.open('rb') as f:head=f.read(160)
  match=re.search(rb'(?m)^guid: ([a-f0-9]{32})',head)
  if match:index[match[1].decode()]=p.with_suffix('').relative_to(root)
 except OSError:pass
seeds=[
 'Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','Assets/_Project/Data/Dialogue/Affection/SashaShopDiscount.asset',
 'Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab','Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab','Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab',
 'Assets/_Project/Data/Items/Weapons/Definitions/WD_ApprenticeHeroSword.asset','Assets/_Project/Data/Items/Relics/Definitions/RD_BronzeDice.asset',
 'Assets/_Project/Data/Items/Relics/Definitions/RD_BurningFeather.asset','Assets/_Project/Data/Items/Consumables/CD_HealPotion.asset'
]
seeds += [p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Data/Localization').rglob('*') if p.suffix=='.asset']
queue=list(map(Path,seeds));visited=set();missing=set()
while queue:
 rel=queue.pop();key=rel.as_posix()
 if key in visited:continue
 visited.add(key);src=root/rel
 if not src.is_file():continue
 copy(rel);copy(Path(str(rel)+'.meta'))
 if src.suffix.lower() in ('.cs','.dll','.png','.ttf','.otf','.mp3','.wav','.fbx','.aseprite'):continue
 data=src.read_bytes()
 for guid in re.findall(rb'guid: ([a-f0-9]{32})',data):
  guid=guid.decode()
  if guid in index:queue.append(index[guid])
  elif guid.strip('0'):missing.add(guid)
for p in (root/'DataSheets/Localization').glob('*'):
 if p.is_file():copy(p.relative_to(root))
for rel in ['Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink','Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json']:
 copy(Path(rel));copy(Path(rel+'.meta'))
(clone/'outputs/merchant-rewards-2026-11-01').mkdir(parents=True,exist_ok=True)
(out/'validation-workspace.json').write_text(json.dumps({'project':str(clone),'copiedFiles':len(copied),'dependencyAssets':len(visited),'missingOrBuiltinGuids':sorted(missing)},indent=2),encoding='utf-8')
print(json.dumps({'copiedFiles':len(copied),'dependencyAssets':len(visited),'unresolvedGuids':len(missing)}))
