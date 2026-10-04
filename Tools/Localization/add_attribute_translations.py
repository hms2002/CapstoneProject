"""Author readable translations of the existing stat IDs; never change those IDs."""
import csv
import json
from pathlib import Path

folder = Path('DataSheets/Localization')
base = {
    'Health': ('HP', 'HP', '生命值', '生命值'),
    'MaxHealth': ('Max HP', '最大HP', '生命值上限', '生命值上限'),
    'MoveSpeed': ('Movement Speed', '移動速度', '移动速度', '移動速度'),
    'SoulHeart': ('Temporary HP', '一時HP', '临时生命值', '臨時生命值'),
    'Attack': ('Attack Power', '攻撃力', '攻击力', '攻擊力'),
    'AttackSpeed': ('Attack Speed', '攻撃速度', '攻击速度', '攻擊速度'),
    'CritChance': ('Critical Chance', 'クリティカル率', '暴击率', '暴擊率'),
    'CritMultiplier': ('Critical Multiplier', 'クリティカル倍率', '暴击倍率', '暴擊倍率'),
    'Evasion': ('Evasion', '回避率', '闪避率', '閃避率'),
    'Final': ('Final Damage', '最終ダメージ', '最终伤害', '最終傷害'),
    'Normal': ('Basic Attack Damage', '通常攻撃ダメージ', '普通攻击伤害', '普通攻擊傷害'),
    'Skill': ('Skill Damage', 'スキルダメージ', '技能伤害', '技能傷害'),
    'BloodElement': ('Bleed Damage', '出血ダメージ', '出血伤害', '出血傷害'),
    'ElectricElement': ('Electric Damage', '電気ダメージ', '电伤害', '電傷害'),
    'FireElement': ('Fire Damage', '火炎ダメージ', '火焰伤害', '火焰傷害'),
    'PoisonElement': ('Poison Damage', '毒ダメージ', '中毒伤害', '中毒傷害'),
    'MaxBloodElementGauge': ('Bleed Threshold', '出血発現閾値', '出血触发阈值', '出血觸發閾值'),
    'MaxElectricElementGauge': ('Electric Threshold', '電気発現閾値', '电属性触发阈值', '電屬性觸發閾值'),
    'MaxFireElementGauge': ('Burn Threshold', '火傷発現閾値', '灼烧触发阈值', '灼燒觸發閾值'),
    'MaxPoisonElementGauge': ('Poison Threshold', '毒発現閾値', '中毒触发阈值', '中毒觸發閾值'),
    'KnockBackPower': ('Knockback Power', 'ノックバック威力', '击退力度', '擊退力度'),
    'KnockbackResistance': ('Knockback Resistance', 'ノックバック耐性', '击退抗性', '擊退抗性'),
    'MaxStagger': ('Max Stagger', '最大よろめき値', '硬直值上限', '硬直值上限'),
    'Stagger': ('Stagger', 'よろめき値', '硬直值', '硬直值'),
    'StaggerResistance': ('Stagger Resistance', 'よろめき耐性', '硬直抗性', '硬直抗性'),
}
modifiers = {'Add': (' Bonus', '加算', '加成', '加成'),
             'Base': (' Base', '基礎値', '基础值', '基礎值'),
             'Mul': (' Multiplier', '倍率', '倍率', '倍率')}
inventory = json.loads((folder / 'AssetInventory.json').read_text(encoding='utf-8'))['rows']
names = dict.fromkeys(r['text'] for r in inventory if r['type'] == 'UnityGAS.AttributeDefinition')
with (folder / 'CommonTranslations.tsv').open('a', encoding='utf-8', newline='') as f:
    writer = csv.writer(f, delimiter='\t')
    for original in names:
        name = original.removesuffix('Attribute')
        modifier = next((m for m in modifiers if name.endswith(m)), None)
        if modifier: name = name[:-len(modifier)]
        translated = base[name]
        if modifier:
            translated = tuple(label + suffix for label, suffix in zip(translated, modifiers[modifier]))
        writer.writerow((original,) + translated)
print(f'{len(names)} stat ID display translations added.')
