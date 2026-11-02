from pathlib import Path
import csv,io,json,re,shutil
root=Path.cwd();out=root/'outputs/sasha-spiri-2026-11-02';stage=out/'staging';clone=root/'.ssv';assert not clone.exists()
ink='Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
source=(root/ink).read_text(encoding='utf-8-sig')
knot='''=== affection_0_b ===
# speaker: 1001
# face: 1001: Normal
# anim: normal
어서 와, 용사님! 오늘은 그 술주정뱅이 용 옆에서 골라 왔어. 반짝이는 건 많더라! # loc:dialogue.merchant.run.affection_0_b.0
다들 왜 그렇게 무서워하지? 취해서 자는 동안 살짝 가져오면 되는데. 안 깨우면 되는 거잖아? # loc:dialogue.merchant.run.affection_0_b.5
+ [너, 들키면 어쩌려고? # loc:dialogue.merchant.run.affection_0_b.1]
    # face: 1001: Happy
    # add_aff: 1
    헤헤, 내 걱정이야? 괜찮아, 도망갈 길도 다 봐 뒀어! 오래 장사하려면 그 정도는 해야지. # loc:dialogue.merchant.run.affection_0_b.2
    -> END
+ [정말 눈치 못 챈 걸까? # loc:dialogue.merchant.run.affection_0_b.3]
    # face: 1001: Surprised
    # choice_fail
    ……알면서 그냥 뒀다고? 왜? ……아무튼, 용사님이 사면 이제 용사님 물건이야! # loc:dialogue.merchant.run.affection_0_b.4
    -> END

'''
source,count=re.subn(r'(?ms)^=== affection_0_b ===\n.*?(?=^=== )',knot,source,count=1);assert count==1
def staged(rel,data):
    dst=stage/rel;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(data)
staged(ink,source.encode('utf-8'))
values={
0:['어서 와, 용사님! 오늘은 그 술주정뱅이 용 옆에서 골라 왔어. 반짝이는 건 많더라!',"Welcome, Hero! I picked these out beside that drunken dragon today. Plenty of shiny things there!",'いらっしゃい、勇者様！ 今日はあの酔いどれ竜のそばで選んできたんだ。キラキラした物がいっぱいあったよ！','欢迎，勇者大人！今天这些是从那条醉鬼龙旁边挑来的。亮晶晶的东西可真多呀！','歡迎，勇者大人！今天這些是從那條醉鬼龍旁邊挑來的。亮晶晶的東西可真多呀！'],
5:['다들 왜 그렇게 무서워하지? 취해서 자는 동안 살짝 가져오면 되는데. 안 깨우면 되는 거잖아?',"Why is everyone so scared? Just slip a few things away while that dragon sleeps off the booze. As long as I don't wake the dragon, right?",'みんな、どうしてそんなに怖がるの？ 酔って寝てる間に、こっそり持ってくればいいのに。起こさなきゃいいんでしょ？','大家为什么那么害怕？趁那条龙喝醉睡着时悄悄拿走不就好了。别吵醒不就行了吗？','大家為什麼那麼害怕？趁那條龍喝醉睡著時悄悄拿走不就好了。別吵醒不就行了嗎？'],
1:['너, 들키면 어쩌려고?',"What if you get caught?",'見つかったらどうするの？','你被发现了怎么办？','你被發現了怎麼辦？'],
2:['헤헤, 내 걱정이야? 괜찮아, 도망갈 길도 다 봐 뒀어! 오래 장사하려면 그 정도는 해야지.',"Hehe, worried about me? Relax, I've already checked all the escape routes! You have to do that much to stay in business.",'へへ、私の心配？ 大丈夫、逃げ道も全部見てあるよ！ 長く商売を続けるなら、それくらいしないとね。','嘿嘿，担心我呀？放心，逃跑的路我都看好了！想长久做生意，这点准备还是要有的。','嘿嘿，擔心我呀？放心，逃跑的路我都看好了！想長久做生意，這點準備還是要有的。'],
3:['정말 눈치 못 챈 걸까?',"Are you sure that dragon hasn't noticed?",'本当に気づいてないのかな？','真的没察觉到吗？','真的沒察覺到嗎？'],
4:['……알면서 그냥 뒀다고? 왜? ……아무튼, 용사님이 사면 이제 용사님 물건이야!',"...Knew all along and let me take them? Why? ...Anyway, once you buy it, it's yours!",'……気づいてて放っておいたって？ なんで？ ……とにかく、勇者様が買えば、もう勇者様の物だからね！','……明知道还放着不管？为什么？……总之，勇者大人买下之后就是你的东西啦！','……明知道還放著不管？為什麼？……總之，勇者大人買下之後就是你的東西啦！']}
rows={f'dialogue.merchant.run.affection_0_b.{i}':v for i,v in values.items()}
rel='DataSheets/Localization/GameText.csv';raw=(root/rel).read_text(encoding='utf-8-sig');result=[];seen=set()
lines=raw.splitlines(keepends=True);reader=csv.reader(io.StringIO(raw));start=0
for row in reader:
    line=''.join(lines[start:reader.line_num]);start=reader.line_num
    if not row:result.append(line);continue
    key=row[0]
    if key in rows:
        stream=io.StringIO();csv.writer(stream,lineterminator='\n').writerow([key,*rows[key]]);result.append(stream.getvalue());seen.add(key)
    else:result.append(line)
assert len(seen)==5
for key in rows.keys()-seen:
    stream=io.StringIO();csv.writer(stream,lineterminator='\n').writerow([key,*rows[key]]);result.append(stream.getvalue())
staged(rel,''.join(result).encode('utf-8'))
(out/'changed-rows.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
rel='DataSheets/Localization/InkInventory.json';old=(root/rel).read_text(encoding='utf-8-sig')
spans=[(m.start(),m.end(),json.loads(m[0])) for m in re.finditer(r'(?ms)^    \{\n.*?^    \}',old)]
selected=[item for item in spans if item[2]['path']==ink];records=[]
for line_num,line in enumerate(source.splitlines(),1):
    match=re.search(r'loc:([a-zA-Z0-9_.]+)',line)
    if match:
        value=line.strip().split(' # loc:')[0];choice=value.startswith('+ [')
        if choice:value=value[3:]
        records.append({'key':match[1],'path':ink,'line':line_num,'text':value,'choice':choice})
assert len(records)==50 and len(selected)==49
replacement=',\n'.join('\n'.join('    '+line for line in json.dumps(row,ensure_ascii=False,indent=2).splitlines()) for row in records)
staged(rel,(old[:selected[0][0]]+replacement+old[selected[-1][1]:]).encode('utf-8'))
def copy(rel):
    src=root/rel;dst=clone/rel
    if src.is_file():dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
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
seeds=['Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset']
seeds.extend(p.relative_to(root).as_posix() for p in (root/'Assets/_Project/Data/Localization').rglob('*.asset'))
queue=list(map(Path,seeds));seen=set()
while queue:
    rel=queue.pop()
    if rel.as_posix() in seen:continue
    seen.add(rel.as_posix());src=root/rel
    if not src.is_file():continue
    copy(rel);copy(Path(str(rel)+'.meta'))
    if src.suffix.lower() in ('.cs','.dll','.png','.ttf','.otf','.wav','.mp3','.fbx','.aseprite'):continue
    for value in re.findall(rb'guid: ([0-9a-f]{32})',src.read_bytes()):
        if value.decode() in index:queue.append(index[value.decode()])
for p in (root/'DataSheets/Localization').glob('*'):
    if p.is_file():copy(p.relative_to(root))
for suffix in ['.ink','.ink.meta','.json','.json.meta']:copy(Path(ink[:-4]+suffix))
for path in stage.rglob('*'):
    if path.is_file():dst=clone/path.relative_to(stage);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,dst)
manifest_path=clone/'Packages/manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
for folder in (root/'Library/PackageCache').iterdir():
    if folder.is_dir() and (folder/'package.json').exists():
        name=json.loads((folder/'package.json').read_text(encoding='utf-8-sig'))['name'];manifest['dependencies'][name]='file:'+folder.as_posix()
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('Prepared replacement dialogue, six localized strings and isolated production-code workspace:',len(seen),'dependency assets.')
