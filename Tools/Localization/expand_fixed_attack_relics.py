"""Prepare only the reviewed fixed-attack relic text; preserve unrelated records."""
import csv
import io
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LANGUAGES = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
REPORT = ROOT / 'Library/CodexFixedAttackRelics'
manifest = json.loads((REPORT / 'authoring.json').read_text(encoding='utf-8-sig'))
descriptions = {
    'RD_WornWhetstone': [
        '공격력이 고정 수치로 증가합니다.', 'Increases Attack by a fixed amount.', '攻撃力が固定値で増加します。', '攻击力增加固定数值。', '攻擊力增加固定數值。'],
    'RD_MasterWhetstone': [
        '공격력이 고정 수치로 증가하고, 기본공격 피해가 비율로 증가합니다.', 'Increases Attack by a fixed amount and basic attack damage by a percentage.', '攻撃力が固定値で、通常攻撃ダメージが割合で増加します。', '攻击力增加固定数值，普通攻击伤害按比例增加。', '攻擊力增加固定數值，普通攻擊傷害按比例增加。'],
    'RD_HeavyBracelet': [
        '이동속도가 비율로 감소하고, 공격력이 고정 수치로 증가합니다.', 'Reduces movement speed by a percentage and increases Attack by a fixed amount.', '移動速度が割合で低下し、攻撃力が固定値で増加します。', '移动速度按比例降低，攻击力增加固定数值。', '移動速度按比例降低，攻擊力增加固定數值。'],
    'RD_LastBreath': [
        '체력이 50% 이하일 때 공격력이 3 증가합니다.', 'While HP is at most 50%, increases Attack by 3.', 'HPが50%以下のとき、攻撃力が3増加します。', '生命值不高于50%时，攻击力增加3。', '生命值不高於50%時，攻擊力增加3。'],
    'RD_RedContract': [
        '적 처치 시 4초 동안 공격력이 2 증가합니다.', 'Killing an enemy increases Attack by 2 for 4 seconds.', '敵を倒すと4秒間攻撃力が2増加します。', '击杀敌人后，攻击力增加2，持续4秒。', '擊殺敵人後，攻擊力增加2，持續4秒。'],
    'RD_RotatingIornBall': [
        '적 처치 시 4초 동안 공격력이 2 증가하고 이동속도가 10% 증가합니다.', 'Killing an enemy increases Attack by 2 and movement speed by 10% for 4 seconds.', '敵を倒すと4秒間攻撃力が2、移動速度が10%増加します。', '击杀敌人后，攻击力增加2，移动速度增加10%，持续4秒。', '擊殺敵人後，攻擊力增加2，移動速度增加10%，持續4秒。'],
    'RD_FeatherOrbit': [
        '플레이어 주위를 도는 깃털을 생성합니다. 레벨에 따라 고정 피해 3/4/5/6/7을 주며, 이동속도가 높을수록 더 빠르게 회전합니다.',
        'Creates feathers that orbit the player. They deal fixed damage of 3/4/5/6/7 by level and orbit faster at higher movement speed.',
        'プレイヤーの周囲を回る羽を生成します。レベルに応じて3/4/5/6/7の固定ダメージを与え、移動速度が高いほど速く回転します。',
        '生成环绕玩家的羽毛。各等级造成3/4/5/6/7固定伤害，移动速度越高，旋转越快。',
        '生成環繞玩家的羽毛。各等級造成3/4/5/6/7固定傷害，移動速度越高，旋轉越快。'],
}
templates = {
    'Relic Logic_Common_LastBreath': [
        '● 체력이 {max_ratio} 이하일 때 [[{stat}]] {value}',
        '● While HP is at most {max_ratio}: [[{stat}]] {value}',
        '● HPが{max_ratio}以下のとき：[[{stat}]] {value}',
        '● 生命值不高于{max_ratio}时：[[{stat}]] {value}',
        '● 生命值不高於{max_ratio}時：[[{stat}]] {value}'],
    'Relic Logic_Common_RedContract': [
        '● {trigger} {duration} 동안 [[{stat}]] {value}',
        '● {trigger}: [[{stat}]] {value} for {duration}',
        '● {trigger}：{duration}間、[[{stat}]] {value}',
        '● {trigger}：{duration}内[[{stat}]] {value}',
        '● {trigger}：{duration}內[[{stat}]] {value}'],
    'Relic Logic_Move Speed On Kill_Managed': [
        '● 적 처치 시 {duration} 동안 [[공격력]] {attack_bonus}\n● [[이동속도]] {move_speed_bonus}',
        '● Killing an enemy: [[공격력]] {attack_bonus} for {duration}\n● [[이동속도]] {move_speed_bonus}',
        '● 敵を倒すと{duration}間[[공격력]] {attack_bonus}\n● [[이동속도]] {move_speed_bonus}',
        '● 击杀敌人后，[[공격력]] {attack_bonus}，持续{duration}\n● [[이동속도]] {move_speed_bonus}',
        '● 擊殺敵人後，[[공격력]] {attack_bonus}，持續{duration}\n● [[이동속도]] {move_speed_bonus}'],
    'Relic Logic_Feather Orbit_Managed': [
        '● 깃털 {feather_count}개가 주위를 회전하며 적을 공격\n● 깃털 피해 {damage_coef}\n● 회전 반경 {radius}\n● 같은 적을 다시 공격하는 기본 간격 {hit_cooldown}',
        '● {feather_count} feathers orbit you and attack enemies\n● Feather damage {damage_coef}\n● Orbit radius {radius}\n● Base repeat-hit interval against the same enemy {hit_cooldown}',
        '● {feather_count}枚の羽が周囲を回り、敵を攻撃\n● 羽のダメージ {damage_coef}\n● 回転半径 {radius}\n● 同じ敵を再攻撃する基本間隔 {hit_cooldown}',
        '● {feather_count}片羽毛环绕并攻击敌人\n● 羽毛伤害 {damage_coef}\n● 旋转半径 {radius}\n● 再次攻击同一敌人的基础间隔 {hit_cooldown}',
        '● {feather_count}片羽毛環繞並攻擊敵人\n● 羽毛傷害 {damage_coef}\n● 旋轉半徑 {radius}\n● 再次攻擊同一敵人的基礎間隔 {hit_cooldown}'],
}
translations = {'code.reliclogic_featherorbit_managed.cef8ff8aec': templates['Relic Logic_Feather Orbit_Managed']}
assert len(manifest['entries']) == 7 and len(manifest['texts']) == 11
inventory_path = ROOT / 'DataSheets/Localization/AssetInventory.json'
inventory = json.loads(inventory_path.read_text(encoding='utf-8-sig'))
mapping = ROOT / 'Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs'
mapping_text = mapping.read_text(encoding='utf-8')
new_mapping_lines = []
for row in manifest['texts']:
    assert row['field'] in ('description', 'effectTemplate')
    values = descriptions[row['owner']] if row['field'] == 'description' else templates[row['owner']]
    assert values[0] == row['text'] and len(values) == len(LANGUAGES)
    key, identity = row['key'], row['identity']
    assert key not in translations
    translations[key] = values
    records = [r for r in inventory['rows'] if r['key'] == key]
    record = {field: row[field] for field in ('key', 'path', 'type', 'owner', 'field', 'text')}
    if records:
        assert len(records) == 1
        assert all(records[0][field] == record[field] for field in ('key', 'path', 'type', 'owner', 'field'))
        records[0]['text'] = values[0]
    else:
        inventory['rows'].append(record)
    line = '        { ' + json.dumps(identity, ensure_ascii=False) + ', ' + json.dumps(key) + ' },\n'
    if json.dumps(identity, ensure_ascii=False) in mapping_text:
        assert line.rstrip() in mapping_text
    else:
        new_mapping_lines.append(line)
assert len(translations) == 12
for values in translations.values():
    assert len(values) == len(LANGUAGES)
    assert len({tuple(sorted(re.findall(r'\{[a-z_]+\}', text))) for text in values}) == 1
if new_mapping_lines:
    assert mapping_text.count('    };') == 1
    mapping.write_text(mapping_text.replace('    };', ''.join(new_mapping_lines) + '    };'), encoding='utf-8')
inventory_path.write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

csv_path = ROOT / 'DataSheets/Localization/GameText.csv'
raw = csv_path.read_bytes()
text = raw.decode('utf-8-sig')
lines = text.splitlines(keepends=True)
reader = csv.reader(io.StringIO(text, newline=''))
chunks, existing, previous_line = [], {}, 0
newline = '\r\n' if '\r\n' in text else '\n'
def csv_record(row):
    output = io.StringIO(newline='')
    csv.writer(output, lineterminator=newline).writerow(row)
    return output.getvalue()
for row in reader:
    span = ''.join(lines[previous_line:reader.line_num])
    previous_line = reader.line_num
    if row and row[0] in translations:
        assert row[0] not in existing
        existing[row[0]] = row[1:]
        chunks.append(csv_record([row[0], *translations[row[0]]]))
    else:
        chunks.append(span)
for key, values in translations.items():
    if key not in existing:
        chunks.append(csv_record([key, *values]))
updated = ''.join(chunks)
before = list(csv.reader(io.StringIO(text, newline='')))
after = list(csv.reader(io.StringIO(updated, newline='')))
assert before[0] == ['key', *LANGUAGES]
after_by_key = {row[0]: row for row in after[1:]}
assert len(after_by_key) == len(after) - 1
assert all(after_by_key[row[0]] == row for row in before[1:] if row[0] not in translations)
csv_path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8'))
(REPORT / 'content.csv').write_text(csv_record(['key', *LANGUAGES]) + ''.join(csv_record([key, *values]) for key, values in translations.items()), encoding='utf-8')
print(f'{len(translations)} fixed-attack relic keys prepared in five languages; unrelated physical CSV records preserved.')
