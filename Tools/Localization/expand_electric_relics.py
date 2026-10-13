"""Add reviewed electric content without rebuilding unrelated localization keys."""
import csv
import io
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LANGUAGES = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
REPORT = ROOT / 'Library/CodexElectricRelics'
manifest = json.loads((REPORT / 'authoring.json').read_text(encoding='utf-8-sig'))
translations = {
    'ui.item_keyword.electrocuted.description': [
        '감전 시 피해를 주고, 감전 상태는 4초 동안 지속됩니다.\n주변 감전 적을 따라 기본 최대 3명에게 피해가 연쇄됩니다.',
        'Electrocution deals damage, and the electrocuted state lasts for 4 seconds.\nDamage chains through nearby electrocuted enemies, hitting a base maximum of 3 targets.',
        '感電時にダメージを与え、感電状態は4秒間続きます。\n周囲の感電中の敵へダメージが連鎖し、基本で最大3体に命中します。',
        '感电时造成伤害，感电状态持续4秒。\n伤害沿附近已感电的敌人连锁传播，基础最多命中3个目标。',
        '感電時造成傷害，感電狀態持續4秒。\n傷害沿附近已感電的敵人連鎖傳播，基礎最多命中3個目標。'],
    'relic.electric.nearby.template': [
        '● 반경 {radius}m 안의 [[감전]]된 적 1명당 [[전기 속성]] {value}\n● 최대 {cap}명까지 적용',
        '● [[전기 속성]] {value} per [[감전]] enemy within {radius}m\n● Counts up to {cap} enemies',
        '● 半径{radius}m以内の[[감전]]中の敵1体につき[[전기 속성]] {value}\n● 最大{cap}体まで',
        '● 半径{radius}m内，每个[[감전]]的敌人提供[[전기 속성]] {value}\n● 最多计算{cap}个敌人',
        '● 半徑{radius}m內，每個[[감전]]的敵人提供[[전기 속성]] {value}\n● 最多計算{cap}個敵人'],
    'relic.electric.move.template': [
        '● [[추가 이동속도]] {step}마다 [[전기 속성]] {value}\n● 최대 {cap}단계까지 적용',
        '● [[전기 속성]] {value} per {step} [[추가 이동속도]]\n● Up to {cap} steps',
        '● [[추가 이동속도]]{step}ごとに[[전기 속성]] {value}\n● 最大{cap}段階まで',
        '● 每{step}[[추가 이동속도]]提供[[전기 속성]] {value}\n● 最多{cap}档',
        '● 每{step}[[추가 이동속도]]提供[[전기 속성]] {value}\n● 最多{cap}階'],
    'relic.electric.dash.template': [
        '● 이동 스킬 사용 시 {duration} 동안 [[전기 속성]] {value}\n● 최대 4중첩, 다시 사용하면 모든 중첩의 지속시간 갱신',
        '● Using a movement skill grants [[전기 속성]] {value} for {duration}\n● Up to 4 stacks; another use refreshes all stack durations',
        '● 移動スキル使用時、{duration}の間[[전기 속성]] {value}\n● 最大4重複、再使用するとすべての重複の持続時間を更新',
        '● 使用移动技能时，[[전기 속성]] {value}，持续{duration}\n● 最多4层，再次使用刷新所有层的持续时间',
        '● 使用移動技能時，[[전기 속성]] {value}，持續{duration}\n● 最多4層，再次使用刷新所有層的持續時間'],
}
translations.update({
    'relic.electric.scene.template': [
        '● [[감전]]된 적 1명당 [[전기 속성]] {value}',
        '● [[전기 속성]] {value} per [[감전]] enemy',
        '● [[감전]]中の敵1体につき[[전기 속성]] {value}',
        '● 每个[[감전]]的敌人提供[[전기 속성]] {value}',
        '● 每個[[감전]]的敵人提供[[전기 속성]] {value}'],
    'relic.electric.health.template': [
        '● [[최대 체력]] {health_step}칸마다 [[전기 속성]] {value}',
        '● [[전기 속성]] {value} per {health_step} cell of [[최대 체력]]',
        '● [[최대 체력]]{health_step}ごとに[[전기 속성]] {value}',
        '● 每{health_step}格[[최대 체력]]提供[[전기 속성]] {value}',
        '● 每{health_step}格[[최대 체력]]提供[[전기 속성]] {value}'],
    'relic.electric.extension.template': [
        '● 연쇄에 맞은 적의 [[감전]] 지속시간 {extension} 증가\n● 최대 {cap}까지 증가',
        '● Extend the [[감전]] duration of enemies hit by a chain by {extension}\n● Up to {cap}',
        '● 連鎖に命中した敵の[[감전]]持続時間を{extension}延長\n● 最大{cap}まで延長',
        '● 连锁命中敌人的[[감전]]持续时间增加{extension}\n● 最多延长至{cap}',
        '● 連鎖命中敵人的[[감전]]持續時間增加{extension}\n● 最多延長至{cap}'],
    'relic.electric.targets.template': [
        '● 전기 연쇄의 최대 대상 수 {value} 증가\n● 최초 대상 포함 기본 3명',
        '● Maximum electric chain targets {value}\n● Base count: 3, including the initial target',
        '● 電気の連鎖の最大対象数 {value}\n● 最初の対象を含め基本3体',
        '● 电连锁最大目标数 {value}\n● 基础3个，包括最初的目标',
        '● 電連鎖最大目標數 {value}\n● 基礎3個，包括最初的目標'],
    'relic.electric.paralysis.template': [
        '● [[감전]] 부여 시 {duration} 동안 마비\n● 보스에게는 적용되지 않음',
        '● Applying [[감전]] causes paralysis for {duration}\n● Does not affect bosses',
        '● [[감전]]を付与すると{duration}の間麻痺\n● ボスには無効',
        '● 施加[[감전]]时，麻痹{duration}\n● 对首领无效',
        '● 施加[[감전]]時，麻痺{duration}\n● 對首領無效'],
    'relic.electric.expiry.template': [
        '● 내가 부여한 [[감전]]의 지속시간이 끝나면 [[전기 속성]]의 {electric_ratio} 피해',
        '● When the duration of [[감전]] you applied ends, deal {electric_ratio} of [[전기 속성]] as damage',
        '● 自分が付与した[[감전]]の持続時間が終わると[[전기 속성]]の{electric_ratio}のダメージ',
        '● 自己施加的[[감전]]持续时间结束时，造成[[전기 속성]]的{electric_ratio}伤害',
        '● 自己施加的[[감전]]持續時間結束時，造成[[전기 속성]]的{electric_ratio}傷害'],
    'relic.electric.echo.template': [
        '● 연쇄 번개를 한 번 더 반복하며, 피해는 {damage_ratio} 적용\n● 최초 대상은 제외하며 [[감전]] 지속시간은 늘어나지 않음',
        '● Repeat chain lightning once more at {damage_ratio} damage\n● Excludes the initial target; [[감전]] duration is not extended',
        '● 連鎖する雷をもう一度繰り返し、ダメージは{damage_ratio}\n● 最初の対象を除き、[[감전]]持続時間は延長されない',
        '● 连锁闪电以{damage_ratio}伤害再重复一次\n● 不包括最初的目标，[[감전]]持续时间不会延长',
        '● 連鎖閃電以{damage_ratio}傷害再重複一次\n● 不包括最初的目標，[[감전]]持續時間不會延長'],
    'relic.electric.conductor.template': [
        '● 연쇄될 때마다 연쇄 피해 {step_bonus} 증가\n● 최대 {max_bonus} 증가',
        '● Each chain step increases chain damage by {step_bonus}\n● Up to {max_bonus} extra damage',
        '● 連鎖するたびに連鎖ダメージが{step_bonus}増加\n● 最大{max_bonus}増加',
        '● 每次连锁使连锁伤害增加{step_bonus}\n● 最多增加{max_bonus}',
        '● 每次連鎖使連鎖傷害增加{step_bonus}\n● 最多增加{max_bonus}'],
})
names = [
    ['천둥의 심장', 'Thunder Heart', '雷鳴の心臓', '雷鸣之心', '雷鳴之心'],
    ['생명의 전류', 'Life Current', '生命の電流', '生命电流', '生命電流'],
    ['대시 코일', 'Dash Coil', 'ダッシュコイル', '冲刺线圈', '衝刺線圈'],
    ['뇌명의 매듭', 'Thunder Knot', '雷鳴の結び目', '雷鸣之结', '雷鳴之結'],
    ['공명의 종', 'Resonance Bell', '共鳴の鐘', '共鸣之钟', '共鳴之鐘'],
    ['저릿한 족쇄', 'Tingling Shackles', '痺れの足枷', '麻痹镣铐', '麻痺鐐銬'],
    ['전류 항아리', 'Current Jar', '電流の壺', '电流罐', '電流罐'],
    ['메아리 코일', 'Echo Coil', 'エコーコイル', '回声线圈', '回聲線圈'],
    ['도체 고리', 'Conductor Ring', '導体の輪', '导体环', '導體環'],
]
descriptions = [
    ['감전된 적의 수에 비례해 전기 속성이 증가합니다.', 'Electric increases with the number of electrocuted enemies.', '感電中の敵の数に応じて電気属性が増加します。', '电属性随感电敌人的数量增加。', '電屬性隨感電敵人的數量增加。'],
    ['최대 체력에 비례해 전기 속성이 증가합니다.', 'Electric increases with maximum HP.', '最大HPに応じて電気属性が増加します。', '电属性随生命值上限增加。', '電屬性隨生命值上限增加。'],
    ['이동 스킬을 사용하면 3초 동안 전기 속성이 증가합니다. 최대 4중첩이며 다시 사용하면 모든 중첩의 지속시간이 갱신됩니다.', 'Using a movement skill increases Electric for 3 seconds, up to 4 stacks. Another use refreshes all stack durations.', '移動スキルを使うと3秒間電気属性が増加し、最大4重複します。再使用するとすべての重複の持続時間が更新されます。', '使用移动技能后，电属性增加，持续3秒，最多4层。再次使用刷新所有层的持续时间。', '使用移動技能後，電屬性增加，持續3秒，最多4層。再次使用刷新所有層的持續時間。'],
    ['연쇄에 맞은 적의 감전 지속시간이 늘어납니다. 최대 4초까지 늘어납니다.', 'Extends electrocution duration on enemies hit by a chain, up to 4 seconds.', '連鎖に命中した敵の感電持続時間が最大4秒まで延長されます。', '连锁命中敌人的感电持续时间延长，最多延长至4秒。', '連鎖命中敵人的感電持續時間延長，最多延長至4秒。'],
    ['전기 연쇄가 맞힐 수 있는 최대 대상 수가 증가합니다.', 'Increases the maximum number of targets an electric chain can hit.', '電気の連鎖が命中できる最大対象数が増加します。', '增加电连锁可命中的最大目标数。', '增加電連鎖可命中的最大目標數。'],
    ['적에게 감전을 부여하면 0.1초 동안 마비시킵니다. 보스에게는 적용되지 않습니다.', 'Applying electrocution to an enemy paralyzes it for 0.1 seconds. Does not affect bosses.', '敵に感電を付与すると0.1秒間麻痺させます。ボスには無効です。', '对敌人施加感电时，使其麻痹0.1秒，对首领无效。', '對敵人施加感電時，使其麻痺0.1秒，對首領無效。'],
    ['내가 부여한 감전의 지속시간이 끝나면 전기 속성의 40%만큼 피해를 줍니다.', 'When the duration of electrocution you applied ends, deals 40% of Electric as damage.', '自分が付与した感電の持続時間が終わると、電気属性の40%のダメージを与えます。', '自己施加的感电持续时间结束时，造成电属性40%的伤害。', '自己施加的感電持續時間結束時，造成電屬性40%的傷害。'],
    ['연쇄 번개를 50% 피해로 한 번 더 반복합니다. 최초 대상은 제외하며 감전 지속시간은 늘어나지 않습니다.', 'Repeats chain lightning once more at 50% damage. Excludes the initial target and does not extend electrocution duration.', '連鎖する雷を50%のダメージでもう一度繰り返します。最初の対象を除き、感電持続時間は延長されません。', '连锁闪电以50%伤害再重复一次，不包括最初的目标，感电持续时间不会延长。', '連鎖閃電以50%傷害再重複一次，不包括最初的目標，感電持續時間不會延長。'],
    ['연쇄될 때마다 연쇄 피해가 20%씩 증가합니다. 최대 60%까지 증가합니다.', 'Each chain step increases chain damage by 20%, up to 60%.', '連鎖するたびに連鎖ダメージが20%ずつ増加し、最大60%まで増加します。', '每次连锁使连锁伤害增加20%，最多增加60%。', '每次連鎖使連鎖傷害增加20%，最多增加60%。'],
]
inventory_path = ROOT / 'DataSheets/Localization/AssetInventory.json'
inventory = json.loads(inventory_path.read_text(encoding='utf-8-sig'))
mapping = ROOT / 'Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs'
mapping_text = mapping.read_text(encoding='utf-8')
new_mapping_lines = []
assert len(manifest['entries']) == 9
for index, entry in enumerate(manifest['entries']):
    for field, values in [('displayName', names[index]), ('description', descriptions[index])]:
        assert values[0] == entry[field]
        key, identity = entry[field + 'Key'], entry[field + 'Identity']
        translations[key] = values
        records = [r for r in inventory['rows'] if r['key'] == key]
        record = dict(key=key, path=entry['definitionPath'], type='RelicDefinition', owner=Path(entry['definitionPath']).stem, field=field, text=values[0])
        if records:
            assert len(records) == 1
            assert all(records[0][k] == record[k] for k in ('key', 'path', 'type', 'owner', 'field'))
            records[0]['text'] = values[0]
        else:
            inventory['rows'].append(record)
        line = '        { ' + json.dumps(identity, ensure_ascii=False) + ', ' + json.dumps(key) + ' },\n'
        if json.dumps(identity, ensure_ascii=False) in mapping_text:
            assert line.rstrip() in mapping_text
        else:
            new_mapping_lines.append(line)
assert len(translations) == 30
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
after_by_key = {r[0]: r for r in after[1:]}
assert len(after_by_key) == len(after) - 1
assert all(after_by_key[r[0]] == r for r in before[1:] if r[0] not in translations)
csv_path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8'))
(REPORT / 'content.csv').write_text(csv_record(['key', *LANGUAGES]) + ''.join(csv_record([key, *values]) for key, values in translations.items()), encoding='utf-8', newline='')
(REPORT / 'expected.json').write_text(json.dumps({'rows': [{'key': key, 'values': values} for key, values in translations.items()]}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(translations)} electric keys prepared in five languages; unrelated CSV records preserved.')
