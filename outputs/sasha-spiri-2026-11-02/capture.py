from pathlib import Path
import hashlib,json,shutil
root=Path.cwd();out=root/'outputs/sasha-spiri-2026-11-02';base=out/'baseline';assert not base.exists()
files=['Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs',*['Assets/_Project/Editor/Tools/Dialogue/'+name+'.cs' for name in ['MerchantAffectionRewardsAuthoring','NpcAffectionDialogueAuthoring','NpcAffectionDialogueValidation']], 'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink','Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json','Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','DataSheets/Localization/GameText.csv','DataSheets/Localization/InkInventory.json',*['Assets/_Project/Data/Localization/'+name+'.asset' for name in ['GameText Shared Data','GameText_ko','GameText_en','GameText_ja','GameText_zh-Hans','GameText_zh-Hant']]]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for rel in files:
    dst=base/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/rel,dst)
(out/'baseline-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in files},indent=2))
protected=set()
for folder in ['Assets/_Project/Scenes','Assets/_Project/Prefabs','Assets/_Project/Art/Sprites/NPCs/merchant','Assets/_Project/Data/Dialogue/Affection','Assets/_Project/Data/Dialogue/NPC/SpriteLibrary','ProjectSettings']:
    protected.update(p.relative_to(root).as_posix() for p in (root/folder).rglob('*') if p.is_file())
protected.update(rel+'.meta' for rel in files if (root/(rel+'.meta')).exists())
(out/'protected-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in sorted(protected)},indent=2))
print('Captured user preimages and',len(protected),'protected files.')
