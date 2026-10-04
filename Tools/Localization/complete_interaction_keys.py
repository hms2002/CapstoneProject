"""Reviewed migration of interaction captions; no runtime sentence matching."""
import csv, json, pathlib, re
root = pathlib.Path('.')
folder = root / 'DataSheets/Localization'
languages = ('ko','en','ja','zh-Hans','zh-Hant')
data = '''{0}번 남음|{0} remaining|残り{0}回|剩余{0}次|剩餘{0}次
오늘의 운동은 이미 끝났어. 다음에도 건강하게 만나자!|You've finished today's workout. Stay healthy until next time!|今日の運動はもう終わったよ。また元気に会おう！|今天的锻炼已经结束了。下次也要健健康康地见面！|今天的鍛鍊已經結束了。下次也要健健康康地見面！
공격력 +{0:0.#}|Attack +{0:0.#}|攻撃力 +{0:0.#}|攻击力 +{0:0.#}|攻擊力 +{0:0.#}
이동속도 +{0:0.#}%|Movement speed +{0:0.#}%|移動速度 +{0:0.#}%|移动速度 +{0:0.#}%|移動速度 +{0:0.#}%
경험치|Experience|経験値|经验值|經驗值
그건 안해도 되겠는데? 다른 운동을 해봐.|You don't need that one. Try another workout.|それは必要なさそうだね。別の運動をしてみて。|你似乎不需要这个，试试别的锻炼吧。|你似乎不需要這個，試試別的鍛鍊吧。
레벨업 !|Level up!|レベルアップ！|升级！|升級！
{0} 보상을 적용할 수 없습니다.|Unable to apply the {0} reward.|{0}の報酬を適用できません。|无法应用{0}奖励。|無法套用{0}獎勵。
배송할 소포가 없습니다.|You have no parcel to deliver.|配達する小包がありません。|没有可配送的包裹。|沒有可配送的包裹。
보상을 생성할 수 없어 소포를 배송하지 않았습니다.|The parcel was not delivered because the reward could not be created.|報酬を生成できないため、小包は配達されませんでした。|无法生成奖励，因此未配送包裹。|無法產生獎勵，因此未配送包裹。
Epic 유물 보상을 생성할 수 없어 소포를 배송하지 않았습니다.|The parcel was not delivered because the Epic relic reward could not be created.|エピック遺物の報酬を生成できないため、小包は配達されませんでした。|无法生成史诗遗物奖励，因此未配送包裹。|無法產生史詩遺物獎勵，因此未配送包裹。
다음 배송 지점을 예약할 수 없습니다.|Unable to reserve the next delivery point.|次の配達先を予約できません。|无法预约下一个配送点。|無法預約下一個配送點。
한개? 흠 뭐 고마워.|One? Well, thanks.|一個？まあ、ありがと。|一个？嗯，谢谢。|一個？嗯，謝謝。
두개 정도면 딱 좋지.|Two is just right.|二個くらいがちょうどいいね。|两个就刚刚好。|兩個就剛剛好。
세개? 야 그만 가져가. 나 잘린다고.|Three? Hey, stop taking them. You'll get me fired.|三個？おい、もう持ってくな。クビになっちまうよ。|三个？喂，别再拿了。我会被炒的。|三個？喂，別再拿了。我會被炒的。
그만가져가라고 했지.|I told you to stop taking them.|もう持ってくなって言っただろ。|我说了别再拿了。|我說了別再拿了。
가방을 좀 비우지그래?|Why don't you clear some space in your bag?|かばんを少し空けたらどう？|先清理一下背包吧？|先清理一下背包吧？
마정석 {0}개 바치기|Offer {0} magic stones|魔晶石を{0}個捧げる|献上{0}颗魔晶石|獻上{0}顆魔晶石
체력 {0} 바치기|Offer {0} health|体力を{0}捧げる|献上{0}生命值|獻上{0}生命值
이동 경로를 사용할 수 없습니다.|This travel route is unavailable.|この移動経路は利用できません。|无法使用此移动路线。|無法使用此移動路線。
아직 이용할 수 없습니다.|Not available yet.|まだ利用できません。|尚不可用。|尚不可用。
지금은 이동할 수 없습니다.|You cannot travel right now.|今は移動できません。|现在无法移动。|現在無法移動。
맵을 준비하지 못했습니다. 빈 맵으로 진입하지 않도록 이동을 보류했습니다.|The map could not be prepared. Travel was paused to avoid entering an empty map.|マップを準備できませんでした。空のマップへの移動を保留しました。|地图准备失败。为避免进入空地图，已暂停移动。|地圖準備失敗。為避免進入空地圖，已暫停移動。
다시 시도|Retry|再試行|重试|重試
맵 생성 실패|Map generation failed|マップ生成失敗|地图生成失败|地圖生成失敗
대화하기|Talk|話す|交谈|交談
구매|Buy|購入|购买|購買
새로고침|Refresh|更新|刷新|重新整理
이동하기|Travel|移動する|移动|移動
작동하기|Activate|作動させる|启动|啟動
살펴보기|Inspect|調べる|查看|查看
조사하기|Investigate|調査する|调查|調查
획득하기|Pick up|拾う|拾取|拾取
도감 보기|Open encyclopedia|図鑑を見る|查看图鉴|查看圖鑑
열기|Open|開ける|打开|打開
굳게 잠겨있다|Firmly locked|固く閉ざされている|牢牢锁住了|牢牢鎖住了
상자 열기|Open chest|宝箱を開ける|打开宝箱|打開寶箱
잠김 ({0})|Locked ({0})|施錠中（{0}）|已锁定（{0}）|已鎖定（{0}）
시작 방으로 돌아가기|Return to the starting room|開始地点に戻る|返回起始房间|返回起始房間
운동기구 사용하기|Use exercise equipment|運動器具を使う|使用锻炼器材|使用鍛鍊器材
소포 배송하기|Deliver parcel|小包を配達する|配送包裹|配送包裹
소포 가져가기|Take parcel|小包を持っていく|拿取包裹|拿取包裹
운동기구 세 개 중 하나를 사용해! 근력은 공격력, 바퀴는 이동속도, 통나무는 경험치를 올려줘!|Use one of the three exercise stations! Strength boosts attack, the wheel boosts movement speed, and the log grants experience!|三つの運動器具から一つ選んで！筋トレは攻撃力、車輪は移動速度、丸太は経験値を上げるよ！|从三种器材中选一种吧！力量训练提高攻击力，转轮提高移动速度，木桩增加经验值！|從三種器材中選一種吧！力量訓練提高攻擊力，轉輪提高移動速度，木樁增加經驗值！
옆의 상자 더미에서 소포를 가져가 주세요. 소포는 유물 슬롯을 차지하며 버릴 수 없습니다.|Please take a parcel from the pile of boxes beside me. Parcels occupy relic slots and cannot be discarded.|隣の箱の山から小包を持っていってください。小包は遺物スロットを使い、捨てられません。|请从旁边的箱堆拿取包裹。包裹占用遗物栏位，无法丢弃。|請從旁邊的箱堆拿取包裹。包裹佔用遺物欄位，無法丟棄。
이미 울린 경보 종입니다.|This alarm bell has already been rung.|この警報の鐘はもう鳴らされています。|这个警报钟已经敲响过了。|這個警報鐘已經敲響過了。
경보 종을 사용할 수 없습니다.|The alarm bell cannot be used.|警報の鐘を使用できません。|无法使用警报钟。|無法使用警報鐘。
경보 종이 울렸다!|The alarm bell rang!|警報の鐘が鳴った！|警报钟响了！|警報鐘響了！
경보 종 전투 완료!|Alarm bell battle complete!|警報の鐘の戦闘完了！|警报钟战斗完成！|警報鐘戰鬥完成！
세 보스를 모두 처치해야 이동할 수 있습니다.|Defeat all three bosses to travel.|移動するには三体のボスをすべて倒す必要があります。|击败全部三名首领后才能移动。|擊敗全部三名首領後才能移動。
제물이 부족합니다|Not enough offerings|供物が足りません|祭品不足|祭品不足
무기가 없어|I don't have a weapon.|武器がない。|我没有武器。|我沒有武器。
무기가 없으면 출발할 수 없어|I can't leave without a weapon.|武器がないと出発できない。|没有武器就不能出发。|沒有武器就不能出發。
무기를 먼저 챙겨야겠어|I should get a weapon first.|先に武器を用意しよう。|得先拿好武器。|得先拿好武器。'''
translated = [line.split('|') for line in data.splitlines()]
assert all(len(row) == 5 for row in translated)
with (folder / 'CommonTranslations.tsv').open(encoding='utf-8-sig',newline='') as f:
    dictionary={row['ko']:row for row in csv.DictReader(f,delimiter='\t')}
dictionary.update({row[0]:dict(zip(languages,row)) for row in translated})
with (folder / 'CommonTranslations.tsv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=languages,delimiter='\t');writer.writeheader();writer.writerows(dictionary.values())
with (folder / 'CommonTranslations.tsv').open(encoding='utf-8-sig',newline='') as f:
    lookup = {row['ko']:row for row in csv.DictReader(f,delimiter='\t')}
with (folder / 'GameText.csv').open(encoding='utf-8-sig',newline='') as f:
    rows = list(csv.DictReader(f))
keys = {row['key'] for row in rows}
def add(key, ko):
    if key not in keys:
        rows.append(dict(key=key, **{lang:lookup[ko][lang] for lang in languages}));keys.add(key)
def update(path, old, new):
    source=path.read_text(encoding='utf-8-sig')
    assert old in source, (path,old)
    path.write_text(source.replace(old,new),encoding='utf-8')

# Only the display method is edited. Serialized fields and their defaults stay intact.
for path in (root/'Assets/_Project/Runtime').rglob('*.cs'):
    try: source=path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError: continue
    match=re.search(r'public override string GetInteractDescription\(\)(?:\s*=>[\s\S]*?;|\s*\{[\s\S]*?\n    \})',source)
    if not match: continue
    block=match.group()
    for field in ('interactPromptText','promptText','prompt','openPromptText','lockedPromptText','lockedPromptFormat'):
        default=re.search(r'private string '+field+r'\s*=\s*("(?:[^"\\]|\\.)*")',source)
        if not default or not re.search(r'\b'+field+r'\b',block):continue
        ko=json.loads(default[1]);ko={'Move':'이동하기','Talk':'대화하기'}.get(ko,ko)
        if ko not in lookup:raise ValueError((path,field,ko))
        key='interaction.'+path.stem.lower()+'.'+field.lower();add(key,ko)
        if json.dumps(key) not in block:
            block=re.sub(r'\b'+field+r'\b', 'GameText.Get('+json.dumps(key)+', '+field+')',block)
    if block!=match.group():
        source=source[:match.start()]+block+source[match.end():]
        path.write_text(source,encoding='utf-8')

for classname,fields in {
    'BuffyGuideNpcInteractable':{'guideText':translated[42][0]},
    'ParcelGuideNpcInteractable':{'guideText':translated[43][0]},
    'AlarmBellInteractable':{field:ko for field,ko in zip(('alreadyUsedMessage','invalidConfigurationMessage','activatedPopupMessage','clearedPopupMessage'),[row[0] for row in translated[44:48]])},
    'RequiredBossClearScenePortalAccessRule':{'blockedMessage':'세 보스를 모두 처치해야 이동할 수 있습니다.'},
    'StatueShortcut':{'InsufficientOfferingMessage':'제물이 부족합니다'},
}.items():
    path=next((root/'Assets/_Project/Runtime').rglob(classname+'.cs'))
    # Read the authored default directly, including multiline declarations.
    for field,unused in fields.items():
        source=path.read_text(encoding='utf-8-sig')
        m=re.search(r'\bstring '+field+r'\s*=\s*("(?:[^"\\]|\\.)*")',source)
        assert m,(classname,field)
        ko=json.loads(m[1]); key='interaction.'+classname.lower()+'.'+field.lower();add(key,ko)
        if 'ShowMessage('+field in source:
            update(path,'ShowMessage('+field,'ShowMessage(GameText.Get('+json.dumps(key)+', '+field+')')

path=next((root/'Assets/_Project/Runtime').rglob('HubWeaponDepartureGuide.cs'))
source=path.read_text(encoding='utf-8-sig')
old='''        if (lines != null && lines.Length > 0)
            player.Transform.GetComponent<PlayerSpeechController>()?.SpeakLine(
                lines[Random.Range(0, lines.Length)], displaySeconds);'''
new='''        if (lines != null && lines.Length > 0)
        {
            int lineIndex = Random.Range(0, lines.Length);
            player.Transform.GetComponent<PlayerSpeechController>()?.SpeakLine(
                GameText.Get("hub.weapon_required." + lineIndex, lines[lineIndex]), displaySeconds);
        }'''
if old in source:update(path,old,new)
for i,row in enumerate(translated[-3:]):add('hub.weapon_required.'+str(i),row[0])
for key,values in (
    ('units.days.value', ('{0}일','{0} days','{0}日','{0}天','{0}天')),
    ('encyclopedia.description_pending', ('설명 준비 중','Description coming soon','説明準備中','说明准备中','說明準備中')),
    ('encyclopedia.effect_missing', ('효과 정보 없음','No effect information','効果情報なし','暂无效果信息','暫無效果資訊')),
    ('inventory.confirmation_full', ('인벤토리 공간이 부족합니다. 인벤토리 아이템을 버리는 구역으로 드래그해 버린 후 다시 확정해 주세요.',
        'There is not enough inventory space. Drag an inventory item to the discard area, then confirm again.',
        'インベントリの空きが足りません。アイテムを破棄エリアへドラッグしてから、もう一度確定してください。',
        '背包空间不足。请将背包物品拖至丢弃区域后，再次确认。',
        '背包空間不足。請將背包物品拖至丟棄區域後，再次確認。')),
    ('title.profile.delete_confirmation', ('슬롯 {0}의 프로필 데이터를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.',
        'Delete the profile data in slot {0}?\nThis cannot be undone.',
        'スロット{0}のプロフィールデータを削除しますか？\nこの操作は取り消せません。',
        '删除栏位{0}的存档资料吗？\n此操作无法撤销。',
        '刪除欄位{0}的存檔資料嗎？\n此操作無法復原。')),
):
    if key not in keys: rows.append(dict(zip(('key',)+languages,(key,)+values)));keys.add(key)
for row in rows:
    if 'N일' in row['ko']:
        for lang,token in zip(languages,('N일','N days','N日','N天','N天')):
            row[lang]=row[lang].replace(token,'{remaining_runs}')
    for lang in languages:
        for token in ('stage_1_bonus','stage_2_bonus'):
            row[lang]=row[lang].replace('+{'+token+'}','{'+token+'}')
with (folder/'GameText.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=('key',)+languages);writer.writeheader();writer.writerows(rows)
print('Interaction keys:',len(rows))
