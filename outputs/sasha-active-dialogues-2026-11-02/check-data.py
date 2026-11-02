from pathlib import Path
import hashlib,json,re
root=Path.cwd();out=root/'outputs/sasha-active-dialogues-2026-11-02';base=out/'baseline';checks=[]
def check(ok,label):
    if not ok:raise AssertionError(label)
    checks.append('PASS '+label)
digest=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
for rel,sha in json.loads((out/'protected-hashes.json').read_text()).items():check(digest(root/rel)==sha,'Protected source/text/localization/reward/metadata unchanged: '+rel)
npc='Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset';text=(root/npc).read_text(encoding='utf-8-sig');old=(base/npc).read_text(encoding='utf-8-sig')
pattern=r'(?m)^  - affectionLevel: (\d+)\n    startPath: (affection_[^\n]+)\n    requiredThemeId: ([^\n]*)\n'
inactive={'affection_1_a','affection_1_b','affection_2_a','affection_2_b'}
check(re.sub(pattern,lambda m:'' if m[2] in inactive else m[0],old)==text,'Only four unfinished candidate entries removed; all other NPC bytes preserved')
entries=[(int(m[1]),m[2],m[3].strip()) for m in re.finditer(pattern,text)]
check([p for _,p,_ in entries]==['affection_0_a','affection_0_b','affection_0_c','affection_1_c'],'Exactly the four requested paths remain')
check(entries==[(0,'affection_0_a',''),(0,'affection_0_b','dragon'),(0,'affection_0_c',''),(1,'affection_1_c','')],'Minimum unlock levels and dragon-only condition retained')
reward_section=text.split('  affectionRewards:\n')[1].split('  affectionDialogues:\n')[0]
check(re.findall(r'targetLevel: (\d+)',reward_section)==['1','2','3','4'] and 'fileID: 0' not in reward_section,'All four existing reward thresholds/bindings retained')
for theme in ['dragon','shadow',None]:
    for level in range(5):
        pool=[p for minimum,p,required in entries if minimum<=level and (not required or theme==required)]
        check(bool(pool) and not set(pool)&inactive,'No progression dead end or disabled candidate: '+str(theme)+' level '+str(level))
        check(len(pool)==(3 if level==0 else 4)-(0 if theme=='dragon' else 1),'Expected cumulative candidate count: '+str(theme)+' level '+str(level))
        check('affection_0_a' in pool and 'affection_0_c' in pool,'Reviewed +1 routes remain available through max affection: '+str(theme)+' level '+str(level))
source=(root/'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink').read_text(encoding='utf-8-sig')
check(all('=== '+path+' ===' in source for path in inactive),'Disabled Ink drafts remain available for future reactivation')
check('greetingPaths:' in text and 'fullBackpackGreetingPaths:' in text,'Ordinary/full-backpack greetings remain configured')
for rel in json.loads((out/'baseline-hashes.json').read_text()):
    if not rel.endswith('.cs'):continue
    check(rel.replace('/','\\') in (root/'Editor.csproj').read_text(encoding='utf-8-sig'),'Generated project includes changed Editor source: '+rel)
    check(all(line.rstrip()==line for line in (root/rel).read_text(encoding='utf-8-sig').splitlines()),'Source whitespace: '+rel)
(out/'verified-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in json.loads((out/'baseline-hashes.json').read_text())},indent=2))
(out/'data-checks.txt').write_text('\n'.join(checks)+f'\nPASS: {len(checks)} scoped data/progression/preservation checks.\n',encoding='utf-8')
print(f'PASS: {len(checks)} scoped data/progression/preservation checks.')
