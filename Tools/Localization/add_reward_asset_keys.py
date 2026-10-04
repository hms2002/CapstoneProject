"""Read existing reward captions; never writes Unity YAML."""
import csv,json,pathlib,re,sys
sys.path.insert(0,'Temp/LocalizationPython')
import yaml
root=pathlib.Path('.')
folder=root/'DataSheets/Localization'
inventory=json.loads((folder/'AssetInventory.json').read_text(encoding='utf-8'))
keys={r['key'] for r in inventory['rows']}
scripts={re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]:p.name[:-8] for p in (root/'Assets/_Project/Runtime').rglob('*.cs.meta')}
added=[]
for path in (root/'Assets/_Project/Data').rglob('*.asset'):
    source=path.read_text(encoding='utf-8-sig',errors='replace')
    if '  rewardText:' not in source:continue
    content=yaml.safe_load('\n'.join(line for line in source.splitlines() if not line.startswith(('%','---'))))['MonoBehaviour']
    text=content.get('rewardText')
    if not text:continue
    guid=re.search(r'^guid: (\w+)',path.with_suffix('.asset.meta').read_text(),re.M)[1]
    key='asset.'+guid+'.rewardText'
    if key in keys:continue
    row=dict(key=key,path=path.as_posix(),type=scripts[content['m_Script']['guid']],owner=content['m_Name'],field='rewardText',text=text)
    inventory['rows'].append(row);added.append(row)
(folder/'AssetInventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps([r['text'] for r in added],ensure_ascii=False,indent=2))
translations='''무기와 유물 해금|Unlock weapons and relics|武器と遺物を解放|解锁武器与遗物|解鎖武器與遺物
상점 판매 가격 20% 할인|20% discount on shop prices|店の販売価格が20%割引|商店售价优惠20%|商店售價優惠20%
TeleportNpc 이용 가능|Teleport NPC available|転送NPCを利用可能|可使用传送NPC|可使用傳送NPC
상자의 내용물을 리롤할 수 있다.|Allows chest contents to be rerolled.|宝箱の中身を再抽選できる。|可重新抽取宝箱内容。|可重新抽取寶箱內容。
10% 확률로 유물의 레벨이 +1된 상태로 등장|Relics have a 10% chance to appear with +1 level.|10%の確率で遺物のレベルが+1された状態で出現|遗物有10%的概率以等级+1的状态出现|遺物有10%的機率以等級+1的狀態出現
상자 내용물에 유물 1개 추가 등장|Chests contain 1 additional relic.|宝箱に遺物が1個追加される|宝箱额外出现1件遗物|寶箱額外出現1件遺物
비전투 이동속도 +50%|Movement speed outside combat +50%|非戦闘時の移動速度 +50%|非战斗移动速度 +50%|非戰鬥移動速度 +50%
잡동사니에서 나오는 유물 수  증가|More relics appear from junk.|がらくたから出る遺物の数が増加|杂物中出现的遗物数量增加|雜物中出現的遺物數量增加
최대 체력 +1|Maximum health +1|最大体力 +1|最大生命值 +1|最大生命值 +1
유물 슬롯 4칸 확장|Expand relic inventory by 4 slots.|遺物スロットを4枠拡張|遗物栏位增加4格|遺物欄位增加4格
시작 시 포션 1개 지급 |Receive 1 potion at the start.|開始時にポーションを1個獲得|开始时获得1瓶药水|開始時獲得1瓶藥水
일회성 추가 체력 부여|Grant temporary extra health.|一時的な追加体力を付与|获得一次性额外生命值|獲得一次性額外生命值
상점 활성화|Enable shop|店を有効化|启用商店|啟用商店
상점 물품 20% 할인|20% discount on shop items|店の商品が20%割引|商店物品优惠20%|商店物品優惠20%
상점 새로고침 1회 가능|Allows 1 shop refresh.|店を1回更新できる|可刷新商店1次|可重新整理商店1次'''
languages=('ko','en','ja','zh-Hans','zh-Hant')
with (folder/'CommonTranslations.tsv').open(encoding='utf-8-sig',newline='') as f:
    dictionary={row['ko']:row for row in csv.DictReader(f,delimiter='\t')}
dictionary.update({line.split('|')[0]:dict(zip(languages,line.split('|'))) for line in translations.splitlines()})
with (folder/'CommonTranslations.tsv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=languages,delimiter='\t');writer.writeheader();writer.writerows(dictionary.values())
