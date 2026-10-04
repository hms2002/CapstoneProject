"""Add reviewed display-only keys; read Unity authoring data without modifying YAML."""
import csv
import json
import re
from pathlib import Path

folder = Path('DataSheets/Localization')
languages = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
with (folder / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
keys = {r['key'] for r in rows}

def add(key, values):
    if key not in keys:
        rows.append(dict(zip(('key',) + languages, (key,) + tuple(values))))
        keys.add(key)

add('ui.item.take', ('인벤토리로 가져오기', 'Take into inventory', 'インベントリに入れる', '放入背包', '放入背包'))
add('ui.item.drop', ('버리기', 'Drop', '捨てる', '丢弃', '丟棄'))
add('ui.level_reward.rerolls', ('[R] 리롤 {0}/{1}', '[R] Reroll {0}/{1}', '[R] 再抽選 {0}/{1}', '[R] 重抽 {0}/{1}', '[R] 重抽 {0}/{1}'))
add('tutorial.dummy.damage', ('피해 {0}/{1}\nDPS {2}/{3}\n누적 {4}', 'Damage {0}/{1}\nDPS {2}/{3}\nTotal {4}', 'ダメージ {0}/{1}\nDPS {2}/{3}\n累計 {4}', '伤害 {0}/{1}\nDPS {2}/{3}\n累计 {4}', '傷害 {0}/{1}\nDPS {2}/{3}\n累計 {4}'))
add('gameover.fire_puddle', ('술에 붙은 불길', 'Burning alcohol', '酒に引火した炎', '燃烧的酒液', '燃燒的酒液'))
add('tutorial.info.hold', ('{0} 길게 누르기', 'Hold {0}', '{0}を長押し', '长按 {0}', '長按 {0}'))
add('world.nameplate.encyclopedia', ('도감', 'Encyclopedia', '図鑑', '图鉴', '圖鑑'))
add('world.nameplate.TeleportNPC', ('길잡이 슬라임', 'Guide Slime', '案内スライム', '向导史莱姆', '嚮導史萊姆'))
add('world.nameplate.ConstructionNpc', ('건설 슬라임', 'Builder Slime', '建設スライム', '建筑史莱姆', '建築史萊姆'))
for slot in range(1, 4):
    add('title.profile.slot.' + str(slot), tuple(label + str(slot) for label in ('슬롯 ', 'Slot ', 'スロット ', '存档位 ', '存檔欄 ')))
add('title.profile.play_time', ('플레이 타임', 'Play Time', 'プレイ時間', '游玩时间', '遊玩時間'))
add('title.profile.upgrade_progress', ('업그레이드 진행도', 'Upgrade Progress', '強化の進行度', '升级进度', '升級進度'))
add('title.profile.magic_stones', ('보유 마정석', 'Magic Stones Owned', '所持魔晶石', '持有魔晶石', '持有魔晶石'))
add('title.profile.clear_count', ('클리어 횟수', 'Clear Count', 'クリア回数', '通关次数', '通關次數'))

pages = {
    'Move': [
        (('이동','Movement','移動','移动','移動'), ('WASD를 눌러 이동할 수 있습니다','Use WASD to move.','WASDで移動できます。','按 WASD 移动。','按 WASD 移動。')),
        (('이동','Movement','移動','移动','移動'), ('SPACE를 눌러 짧은 거리를 주파할 수 있습니다.','Press SPACE to dash a short distance.','SPACEで短い距離をダッシュできます。','按 SPACE 冲刺一小段距离。','按 SPACE 衝刺一小段距離。'))],
    'Combat': [
        (('전투','Combat','戦闘','战斗','戰鬥'), ('마우스 왼쪽 클릭을 통해 공격이 가능합니다.','Left-click to attack.','マウスの左クリックで攻撃できます。','单击鼠标左键攻击。','點擊滑鼠左鍵攻擊。')),
        (('전투','Combat','戦闘','战斗','戰鬥'), ('마우스 우클릭, Q키를 눌러 스킬을 사용할 수 있습니다.','Use right-click or Q to activate skills.','マウスの右クリックやQでスキルを使えます。','按鼠标右键或 Q 使用技能。','按滑鼠右鍵或 Q 使用技能。'))],
    'Chest': [
        (('상자','Chest','宝箱','宝箱','寶箱'), ('전투가 종료되면 상자에 접근해 F키를 눌러 개방이 가능합니다.','After combat, approach a chest and press F to open it.','戦闘後、宝箱に近づいてFを押すと開けられます。','战斗结束后，靠近宝箱并按 F 打开。','戰鬥結束後，靠近寶箱並按 F 開啟。')),
        (('아이템','Items','アイテム','物品','物品'), ('상자 안에는 다양한 아이템이 존재하며, 다양한 능력을 가지고 있습니다.','Chests contain various items with different abilities.','宝箱にはさまざまな能力を持つアイテムが入っています。','宝箱内有各种具有不同能力的物品。','寶箱內有各種具有不同能力的物品。'))],
    'Gate': [
        (('게이트','Gate','ゲート','传送门','傳送門'), ('한 맵의 끝에는 다음 맵으로 가는 게이트가 존재합니다.','At the end of each map, a gate leads to the next map.','各マップの最後には、次のマップへ進むゲートがあります。','每张地图的尽头都有通往下一张地图的传送门。','每張地圖的盡頭都有通往下一張地圖的傳送門。')),
        (('게이트','Gate','ゲート','传送门','傳送門'), ('게이트에 접근해 F키를 눌러 상호작용할 수 있습니다.','Approach a gate and press F to interact.','ゲートに近づいてFを押すと操作できます。','靠近传送门并按 F 互动。','靠近傳送門並按 F 互動。'))]}
for tutorial_id, items in pages.items():
    for index, (title, body) in enumerate(items):
        add(f'tutorial.info.{tutorial_id}.page.{index}.title', title)
        add(f'tutorial.info.{tutorial_id}.page.{index}.body', body)

triggers = {
    '피해를 받으면': ('피해를 받으면', 'When taking damage,', 'ダメージを受けると', '受到伤害时，', '受到傷害時，'),
    '적 처치 시': ('적 처치 시', 'On defeating an enemy,', '敵を倒すと', '击败敌人时，', '擊敗敵人時，')}
inventory = json.loads((folder / 'AssetInventory.json').read_text(encoding='utf-8'))
asset_keys = {r['key'] for r in inventory['rows']}
scripts = {re.search(r'^guid: (\w+)', p.read_text(), re.M)[1]: p.name[:-8]
           for p in Path('Assets/_Project/Runtime').rglob('*.cs.meta')}
for path in Path('Assets/_Project/Data/Items/Relics/Strategies').rglob('*.asset'):
    text = path.read_text(encoding='utf-8-sig')
    match = re.search(r'^  triggerLabel: (.+)$', text, re.M)
    if not match:
        continue
    label = match[1].strip()
    if label.startswith('"'):
        label = json.loads(label)
    if label not in triggers:
        raise ValueError('Unreviewed trigger: ' + label)
    guid = re.search(r'^guid: (\w+)', path.with_suffix('.asset.meta').read_text(), re.M)[1]
    key = 'asset.' + guid + '.triggerLabel'
    add(key, triggers[label])
    if key not in asset_keys:
        inventory['rows'].append(dict(key=key, path=path.as_posix(),
            type=scripts[re.search(r'm_Script: .*guid: (\w+)', text)[1]],
            owner=re.search(r'^  m_Name: (.+)$', text, re.M)[1], field='triggerLabel', text=label))
        asset_keys.add(key)

fallbacks = {
    'burn_on_critical': (
        '● [[화상]] 상태인 적에게 [[치명타]] 적중 시 [[화상]] {burn_stacks}중첩 부여\n● 재사용 대기시간 {cooldown}',
        '● A [[치명타]] against an enemy with [[화상]] applies {burn_stacks} stacks of [[화상]]\n● Cooldown {cooldown}',
        '● [[화상]]中の敵に[[치명타]]が命中すると[[화상]]を{burn_stacks}スタック付与\n● クールダウン {cooldown}',
        '● 对处于[[화상]]状态的敌人造成[[치명타]]时，施加 {burn_stacks} 层[[화상]]\n● 冷却时间 {cooldown}',
        '● 對處於[[화상]]狀態的敵人造成[[치명타]]時，施加 {burn_stacks} 層[[화상]]\n● 冷卻時間 {cooldown}'),
    'crit_after_critical': (
        '● [[치명타]] 적중 시 {duration} 동안 [[치명타 확률]] {chance}\n● 치명타 재적중 시 {pos:지속 시간 갱신}',
        '● On a [[치명타]], gain {chance} [[치명타 확률]] for {duration}\n● Another critical hit {pos:refreshes the duration}',
        '● [[치명타]]命中時、{duration}の間[[치명타 확률]] {chance}\n● 再びクリティカルが命中すると{pos:持続時間を更新}',
        '● [[치명타]]命中时，[[치명타 확률]] {chance}，持续 {duration}\n● 再次暴击时{pos:刷新持续时间}',
        '● [[치명타]]命中時，[[치명타 확률]] {chance}，持續 {duration}\n● 再次暴擊時{pos:刷新持續時間}'),
    'crit_on_noncritical': (
        '● 치명타가 아닌 공격 적중 시 [[치명타 확률]] {chance_per_stack}\n● 최대 {maximum_stacks}회 중첩, 총 {maximum_chance}\n● 치명타 발생 시 {neg:누적 보너스 초기화}',
        '● On a non-critical hit, gain {chance_per_stack} [[치명타 확률]]\n● Stacks up to {maximum_stacks} times, totaling {maximum_chance}\n● A critical hit {neg:resets the accumulated bonus}',
        '● 非クリティカル攻撃が命中すると[[치명타 확률]] {chance_per_stack}\n● 最大{maximum_stacks}回累積、合計{maximum_chance}\n● クリティカル発生時に{neg:累積ボーナスをリセット}',
        '● 非暴击攻击命中时，[[치명타 확률]] {chance_per_stack}\n● 最多叠加 {maximum_stacks} 次，合计 {maximum_chance}\n● 暴击时{neg:重置累计加成}',
        '● 非暴擊攻擊命中時，[[치명타 확률]] {chance_per_stack}\n● 最多疊加 {maximum_stacks} 次，合計 {maximum_chance}\n● 暴擊時{neg:重置累計加成}')}
for name, values in fallbacks.items():
    add('relic.fallback.' + name, values)
# Emphasis belongs to authored translations, avoiding language-dependent substring lookup.
for row in rows:
    if row['key'] in ('code.prototypetutorialupgrade.4fb1d89fd2', 'code.prototypetutorialupgrade.1f3bc59145'):
        for language, phrase in zip(languages, ('길게', 'hold', '長押し', '长按', '長按')):
            if '<color=#FF4444>' not in row[language]:
                assert phrase in row[language], (row['key'], language)
                row[language] = row[language].replace(phrase, '<color=#FF4444>' + phrase + '</color>', 1)
with (folder / 'GameText.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=('key',) + languages)
    writer.writeheader()
    writer.writerows(rows)
(folder / 'AssetInventory.json').write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(rows)} keys; {len(inventory["rows"])} asset fields.')
