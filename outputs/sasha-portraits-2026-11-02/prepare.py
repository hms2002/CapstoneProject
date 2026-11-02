from pathlib import Path
import csv,hashlib,io,json,re,shutil

root=Path.cwd();out=root/'outputs/sasha-portraits-2026-11-02';stage=out/'staging';clone=root/'.spv'
def stage_bytes(rel,data):
    path=stage/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
images={
 'Assets/_Project/Art/Sprites/NPCs/merchant/merchant.png':'C:/Users/user/AppData/Local/Temp/codex-clipboard-0fd7dcd4-16d7-4c32-996f-a6ff052c539d.png',
 'Assets/_Project/Art/Sprites/NPCs/merchant/merchant_surprised.png':'C:/Users/user/AppData/Local/Temp/codex-clipboard-9fd69219-7557-4416-a59b-7b3da28d5332.png'
}
for rel,src in images.items():stage_bytes(rel,Path(src).read_bytes())
(out/'supplied-image-hashes.json').write_text(json.dumps({rel:hashlib.sha256((stage/rel).read_bytes()).hexdigest() for rel in images},indent=2),encoding='utf-8')

ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
source=(root/ink).read_text(encoding='utf-8-sig')
source,count=re.subn(r'(?m)^    # choice_fail\n(?:    # face: 1001: Normal\n)?','    # face: 1001: Surprised\n    # choice_fail\n',source)
assert count==7
opening='어서 와, 용사님! 왜 이렇게 싸냐고? 헤헤, 내 장사엔 다 계산이 있지!'
explanation='매입비는 안 들었지, 용사님이 시선도 끌어 줬지, 싸게 팔면 또 찾아올 테지! 그러니까 이 물건은 깎아 줘도 남는 장사야. 헤헤, 빈틈없지?'
new_rows=[
 ['dialogue.merchant.run.affection_1_c.0',opening,'Welcome, Hero! Why so cheap? Hehe, there is a calculation behind every deal!','いらっしゃい、勇者様！ どうしてこんなに安いのかって？ へへ、私の商売には全部計算があるんだよ！','欢迎，勇者大人！问我为什么这么便宜？嘿嘿，我做生意可都是算好的！','歡迎，勇者大人！問我為什麼這麼便宜？嘿嘿，我做生意可都是算好的！'],
 ['dialogue.merchant.run.affection_1_c.1',explanation,'No buying costs, you kept everyone distracted, and a bargain brings you back! So I still make a profit on this item at a discount. Hehe, flawless, right?','仕入れはタダ、勇者様が気も引いてくれた、安く売ればまた来てくれる！ だからこの品は値引きしても儲かるの。へへ、隙がないでしょ？','进货没花钱，勇者大人又帮我引开了注意，卖便宜点你还会再来！所以这件东西打折也能赚。嘿嘿，滴水不漏吧？','進貨沒花錢，勇者大人又幫我引開了注意，賣便宜點你還會再來！所以這件東西打折也能賺。嘿嘿，滴水不漏吧？'],
 ['dialogue.merchant.run.affection_1_c.2','그걸 나한테 설명해도 돼?','Should you be telling me that?','それ、私に説明して大丈夫？','这也能直接告诉我？','這也能直接告訴我？'],
 ['dialogue.merchant.run.affection_1_c.3','왜 안 돼? 알아도 살 거잖아. 내가 골랐고, 가격까지 깎아 줬는데!','Why not? You will buy it anyway. I picked it out and even lowered the price!','なんで？ 知ってても買うでしょ。私が選んで、値段まで下げたんだから！','有什么不行？知道了你也会买吧。我挑的货，还给你打了折呢！','有什麼不行？知道了你也會買吧。我挑的貨，還給你打了折呢！'],
 ['dialogue.merchant.run.affection_1_c.4','그래도 안 살 건데.','I am still not buying it.','それでも買わないけど。','可我还是不买。','可我還是不買。'],
 ['dialogue.merchant.run.affection_1_c.5','왜?! 가격도 깎았고, 설명도 공짜였는데?! ……설명을 더 해야 하나?','Why?! I lowered the price, and the explanation was free too! ...Do I need to explain more?','なんで？！ 値引きもしたし、説明だってタダだったのに？！ ……もっと説明しなきゃダメ？','为什么？！价格都降了，说明也是免费的？！……还得再多解释一点吗？','為什麼？！價格都降了，說明也是免費的？！……還得再多解釋一點嗎？']
]
knot=f'''=== affection_1_c ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
{opening} # loc:dialogue.merchant.run.affection_1_c.0
{explanation} # loc:dialogue.merchant.run.affection_1_c.1
+ [그걸 나한테 설명해도 돼? # loc:dialogue.merchant.run.affection_1_c.2]
    # face: 1001: Normal
    # add_aff: 1
    왜 안 돼? 알아도 살 거잖아. 내가 골랐고, 가격까지 깎아 줬는데! # loc:dialogue.merchant.run.affection_1_c.3
    -> END
+ [그래도 안 살 건데. # loc:dialogue.merchant.run.affection_1_c.4]
    # face: 1001: Surprised
    # choice_fail
    왜?! 가격도 깎았고, 설명도 공짜였는데?! ……설명을 더 해야 하나? # loc:dialogue.merchant.run.affection_1_c.5
    -> END

'''
assert '=== affection_1_c ===' not in source
source=source.replace('=== affection_2_a ===',knot+'=== affection_2_a ===')
stage_bytes(ink,source.encode('utf-8'))
csv_rel='DataSheets/Localization/GameText.csv';old=(root/csv_rel).read_bytes().decode('utf-8-sig');newline='\r\n' if '\r\n' in old else '\n'
existing={row[0] for row in csv.reader(io.StringIO(old)) if row}
assert not any(row[0] in existing for row in new_rows)
buf=io.StringIO(newline='');csv.writer(buf,lineterminator=newline).writerows(new_rows)
stage_bytes(csv_rel,(old.rstrip('\r\n')+newline+buf.getvalue()).encode('utf-8'))

# Replace only the merchant record span, retaining every unrelated inventory byte.
inventory_rel='DataSheets/Localization/InkInventory.json';old=(root/inventory_rel).read_text(encoding='utf-8-sig');decoder=json.JSONDecoder();pos=old.index('[',old.index('"rows"'))+1;spans=[]
while True:
    while old[pos] in ' \r\n\t,':pos+=1
    if old[pos]==']':break
    record,end=decoder.raw_decode(old,pos);spans.append((pos,end,record));pos=end
selected=[span for span in spans if span[2]['path']==ink]
records=[]
for line_num,line in enumerate(source.splitlines(),1):
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0];choice=value.startswith('+ [')
        if choice:value=value[3:]
        records.append({'key':match[1],'path':ink,'line':line_num,'text':value,'choice':choice})
assert len(records)==49
replacement=',\n'.join('\n'.join('    '+line for line in json.dumps(row,ensure_ascii=False,indent=2).splitlines()) for row in records)[4:]
stage_bytes(inventory_rel,(old[:selected[0][0]]+replacement+old[selected[-1][1]:]).encode('utf-8'))

def copy(rel):
    src=root/rel;dest=clone/rel
    if src.is_file():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dest)
for rel in ['Assets/_Project/Runtime','Assets/_Project/Editor','Assets/Plugins','Assets/Ink']:
    for p in (root/rel).rglob('*'):
        if p.is_file() and (p.suffix in ('.cs','.asmdef','.dll','.rsp') or p.name.endswith(('.cs.meta','.asmdef.meta','.dll.meta','.rsp.meta'))):copy(p.relative_to(root))
for rel in ['Packages','ProjectSettings']:
    for p in (root/rel).rglob('*'):
        if p.is_file():copy(p.relative_to(root))
index={}
for meta in (root/'Assets').rglob('*.meta'):
    try:
        with meta.open('rb') as f:match=re.search(rb'(?m)^guid: ([0-9a-f]{32})',f.read(160))
        if match:index[match[1].decode()]=meta.with_suffix('').relative_to(root)
    except OSError:pass
seeds=['Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset','Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab','Assets/_Project/Prefabs/UI/Portraitlmage.prefab']
seeds.extend(p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Data/Localization').rglob('*.asset'))
seeds.extend(p.relative_to(root).as_posix() for p in (root/'Assets/TextMesh Pro').rglob('*') if p.is_file() and any(part in ('Resources','Shaders') for part in p.parts))
queue=list(map(Path,seeds));seen=set();missing=set()
while queue:
    rel=queue.pop()
    if rel.as_posix() in seen:continue
    seen.add(rel.as_posix());src=root/rel
    if not src.is_file():continue
    copy(rel);copy(Path(str(rel)+'.meta'))
    if src.suffix.lower() in ('.cs','.dll','.png','.ttf','.otf','.wav','.mp3','.fbx','.aseprite'):continue
    for value in re.findall(rb'guid: ([0-9a-f]{32})',src.read_bytes()):
        guid=value.decode()
        if guid in index:queue.append(index[guid])
        elif guid.strip('0'):missing.add(guid)
for p in (root/'DataSheets/Localization').glob('*'):
    if p.is_file():copy(p.relative_to(root))
for suffix in ['.ink','.ink.meta','.json','.json.meta']:copy(Path('Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated'+suffix))
for path in stage.rglob('*'):
    if path.is_file():dest=clone/path.relative_to(stage);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,dest)
manifest_path=clone/'Packages/manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
for folder in (root/'Library/PackageCache').iterdir():
    if folder.is_dir() and (folder/'package.json').is_file():
        name=json.loads((folder/'package.json').read_text(encoding='utf-8-sig'))['name'];manifest['dependencies'][name]='file:'+folder.as_posix()
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(out/'validation-workspace.json').write_text(json.dumps({'workspace':str(clone),'dependencyAssets':len(seen),'missingOrBuiltinGuids':sorted(missing)},indent=2),encoding='utf-8')
print('Staged exact portraits, eight refusal faces and six locale rows. Isolated workspace ready:',len(seen))
