from pathlib import Path
import hashlib,json,os,re,shutil,tempfile
root=Path.cwd();out=root/'outputs/sasha-active-dialogues-2026-11-02';base=out/'baseline';assert not base.exists()
npc='Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset'
sources=['Assets/_Project/Editor/Tools/Dialogue/'+name+'.cs' for name in ['MerchantAffectionRewardsAuthoring','MerchantAffectionRewardsValidation','NpcAffectionDialogueAuthoring','NpcAffectionDialogueValidation']]
digest=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
for rel in [npc,*sources]:
    dst=base/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/rel,dst)
(out/'baseline-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in [npc,*sources]},indent=2))
protected=['Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs','Assets/_Project/Runtime/Features/Dialogue/Affection/AffectionManager.cs','Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab','DataSheets/Localization/GameText.csv','DataSheets/Localization/InkInventory.json']
for folder in ['Assets/_Project/Data/Dialogue/Ink/AnimatedVariants','Assets/_Project/Data/Dialogue/Affection','Assets/_Project/Data/Dialogue/NPC/SpriteLibrary','Assets/_Project/Data/Localization','Assets/_Project/Art/Sprites/NPCs/merchant']:
    protected.extend(p.relative_to(root).as_posix() for p in (root/folder).rglob('*') if p.is_file())
protected.extend(rel+'.meta' for rel in [npc,*sources])
(out/'protected-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in protected},indent=2))
data=(root/npc).read_bytes();text=data.decode('utf-8');removed=[]
pattern=r'(?m)^  - affectionLevel: (\d+)\r?\n    startPath: (affection_[^\r\n]+)\r?\n    requiredThemeId: [^\r\n]*\r?\n'
inactive={'affection_1_a','affection_1_b','affection_2_a','affection_2_b'}
def replace(m):
    if m[2] in inactive:removed.append(m[2]);return ''
    return m[0]
text=re.sub(pattern,replace,text);assert set(removed)==inactive
fd,tmp=tempfile.mkstemp(prefix='.MSShopNpc-',suffix='.active-stage',dir=(root/npc).parent);os.close(fd)
try:
    Path(tmp).write_bytes(text.encode('utf-8'));assert (root/npc).read_bytes()==data;os.replace(tmp,root/npc)
finally:Path(tmp).unlink(missing_ok=True)
print('Disabled four unfinished candidates; preserved their Ink, translations, greetings, rewards and four selected candidates.')
