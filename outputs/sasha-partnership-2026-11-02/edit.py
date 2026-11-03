from pathlib import Path
import csv,hashlib,io,json,os,re,shutil,tempfile,time
root=Path.cwd();out=root/'outputs/sasha-partnership-2026-11-02';base=out/'baseline';stage=out/'staging';assert not base.exists()
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
shared='Assets/_Project/Data/Localization/GameText Shared Data.asset'
tables=['Assets/_Project/Data/Localization/GameText_'+code+'.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']]
fixture='Assets/_Project/Editor/Tools/Dialogue/NpcAffectionDialogueValidation.cs'
files=[ink,ink[:-4]+'.json',shared,*tables,'DataSheets/Localization/GameText.csv','DataSheets/Localization/InkInventory.json',fixture]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for rel in files:
    dst=base/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/rel,dst)
(out/'baseline-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in files},indent=2))
protected=['Assets/_Project/Runtime/Features/Dialogue/NpcAffectionDialogue.cs','Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab','Assets/_Project/Prefabs/UI/Portraitlmage.prefab','Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib']
protected += [rel+'.meta' for rel in files if (root/(rel+'.meta')).exists()]
protected += [p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Art/Sprites/NPCs/merchant').glob('*') if p.is_file()]
(out/'protected-hashes.json').write_text(json.dumps({rel:digest(root/rel) for rel in protected},indent=2))
def staged(rel,text):
    dst=stage/rel;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_text(text,encoding='utf-8',newline='')
values={
0:['어서 와, 동업자님! 용사님은 정문에서 싸우고, 나는 뒷문에서 챙기고! 오늘도 분업이 척척이네!',"Welcome, partner! You fight at the front door, I collect things at the back! Our teamwork is flawless again today!",'いらっしゃい、相棒！ 勇者様は正面で戦って、私は裏口で回収！ 今日も分業はばっちりだね！','欢迎，合伙人！勇者大人在正门战斗，我在后门收货！今天的分工也配合得真好！','歡迎，合夥人！勇者大人在正門戰鬥，我在後門收貨！今天的分工也配合得真好！'],
5:['물론 용사님 몫도 따로 빼 놨지. 헤헤, 나 의리 있는 상인이잖아?',"Of course I set aside your share. Hehe, I'm a merchant who looks after her partners, right?",'もちろん勇者様の分も取り分けてあるよ。へへ、私って義理堅い商人でしょ？','当然也给勇者大人留了一份。嘿嘿，我可是个讲义气的商人呀？','當然也給勇者大人留了一份。嘿嘿，我可是個講義氣的商人呀？'],
1:['그럼 내 몫 좀 보여 줘.',"Then show me my share.",'じゃあ、私の分を見せて。','那就让我看看我的那份吧。','那就讓我看看我的那份吧。'],
2:['좋아! 동업자니까 먼저 살 기회를 주는 거야. 계산은 이쪽!',"Sure! As my partner, you get the first chance to buy. Pay over here!",'いいよ！ 相棒だから先に買うチャンスをあげるの。お会計はこっち！','好呀！因为是合伙人，才给你优先购买的机会。来这边结账！','好呀！因為是合夥人，才給你優先購買的機會。來這邊結帳！'],
3:['동업자 몫이면 공짜 아니야?',"Shouldn't a partner's share be free?",'相棒の取り分ならタダじゃないの？','合伙人的份额不应该免费吗？','合夥人的份額不應該免費嗎？'],
4:['왜?! ‘용사님한테 팔 몫’이지! 내가 물건까지 챙겨 왔는데, 동업자끼리 너무한 거 아니야?!',"Why?! It's the share I'm selling to you! I even brought the goods. Isn't that a bit much to ask of your partner?!",'なんで？！ 「勇者様に売る分」だよ！ 私が品物まで持ってきたのに、相棒にそれはひどくない？！','为什么？！是“卖给勇者大人的那份”呀！我连货都带来了，合伙人之间这样也太过分了吧？！','為什麼？！是「賣給勇者大人的那份」呀！我連貨都帶來了，合夥人之間這樣也太過分了吧？！']}
rows={f'dialogue.merchant.run.affection_0_c.{i}':v for i,v in values.items()}
(out/'rows.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
source=(root/ink).read_text(encoding='utf-8-sig')
knot=f'''=== affection_0_c ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
{values[0][0]} # loc:dialogue.merchant.run.affection_0_c.0
{values[5][0]} # loc:dialogue.merchant.run.affection_0_c.5
+ [{values[1][0]} # loc:dialogue.merchant.run.affection_0_c.1]
    # face: 1001: Happy
    # add_aff: 1
    {values[2][0]} # loc:dialogue.merchant.run.affection_0_c.2
    -> END
+ [{values[3][0]} # loc:dialogue.merchant.run.affection_0_c.3]
    # face: 1001: Surprised
    # choice_fail
    {values[4][0]} # loc:dialogue.merchant.run.affection_0_c.4
    -> END

'''
source,count=re.subn(r'(?ms)^=== affection_0_c ===\n.*?(?=^=== )',lambda _:knot,source,count=1);assert count==1
staged(ink,source)
rel='DataSheets/Localization/GameText.csv';raw=(root/rel).read_text(encoding='utf-8-sig');lines=raw.splitlines(keepends=True);reader=csv.reader(io.StringIO(raw));result=[];start=0;seen=set()
for row in reader:
    line=''.join(lines[start:reader.line_num]);start=reader.line_num
    if row and row[0] in rows:
        stream=io.StringIO();csv.writer(stream,lineterminator='\n').writerow([row[0],*rows[row[0]]]);line=stream.getvalue();seen.add(row[0])
    result.append(line)
assert len(seen)==5
stream=io.StringIO();newkey='dialogue.merchant.run.affection_0_c.5';csv.writer(stream,lineterminator='\n').writerow([newkey,*rows[newkey]]);result.append(stream.getvalue());staged(rel,''.join(result))
def records(text):return {m[1]:m for m in re.finditer(r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)',text)}
text=(root/shared).read_text(encoding='utf-8-sig');entries=records(text)
ids={key:next(i for i,m in entries.items() if 'm_Key: '+key+'\n' in m[0]) for key in rows if key!=newkey}
# Use the installed Localization generator's epoch/time/machine/sequence layout.
epoch=int(re.search(r'm_CustomEpoch: (\d+)',text)[1]);machine=(int(hashlib.sha256(newkey.encode()).hexdigest()[:8],16)%1023)+1
newid=((int(time.time()*1000)-epoch)<<22)|(machine<<12)
while str(newid) in entries:newid+=1
assert 0<newid<2**63
template=entries[ids['dialogue.merchant.run.affection_0_c.4']][0]
block=template.replace('m_Id: '+ids['dialogue.merchant.run.affection_0_c.4'],'m_Id: '+str(newid),1).replace('m_Key: dialogue.merchant.run.affection_0_c.4','m_Key: '+newkey,1)
last=list(entries.values())[-1];staged(shared,text[:last.end()]+block+text[last.end():]);ids[newkey]=str(newid)
for index,rel in enumerate(tables):
    text=(root/rel).read_text(encoding='utf-8-sig');entries=records(text);updated=text
    for key,id in ids.items():
        if key==newkey:continue
        old=entries[id][0];new=re.sub(r'(?ms)^    m_Localized: .*?(?=^    m_Metadata:)',lambda _:'    m_Localized: '+json.dumps(rows[key][index],ensure_ascii=True)+'\n',old,count=1)
        updated=updated.replace(old,new,1)
    template=entries[ids['dialogue.merchant.run.affection_0_c.4']][0]
    new=re.sub(r'(?ms)^    m_Localized: .*?(?=^    m_Metadata:)',lambda _:'    m_Localized: '+json.dumps(rows[newkey][index],ensure_ascii=True)+'\n',template,count=1).replace('m_Id: '+ids['dialogue.merchant.run.affection_0_c.4'],'m_Id: '+str(newid),1)
    current=records(updated);last=list(current.values())[-1];staged(rel,updated[:last.end()]+new+updated[last.end():])
(out/'ids.json').write_text(json.dumps(ids,indent=2))
rel='DataSheets/Localization/InkInventory.json';old=(root/rel).read_text(encoding='utf-8-sig');spans=[m for m in re.finditer(r'(?ms)^    \{\n.*?^    \}',old) if json.loads(m[0])['path']==ink];items=[]
for line_num,line in enumerate(source.splitlines(),1):
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0];choice=value.startswith('+ [')
        if choice:value=value[3:]
        items.append({'key':match[1],'path':ink,'line':line_num,'text':value,'choice':choice})
assert len(items)==51
replacement=',\n'.join('\n'.join('    '+line for line in json.dumps(item,ensure_ascii=False,indent=2).splitlines()) for item in items)
staged(rel,old[:spans[0].start()]+replacement+old[spans[-1].end():])
print('Staged partnership dialogue and 30 translations; existing IDs/records retained; one new key.')
