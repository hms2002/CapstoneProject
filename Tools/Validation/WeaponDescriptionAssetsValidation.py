"""Validate weapon summary coverage and authored shortcut references without importing Unity assets."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'Assets/_Project/Data'

def field(text, name):
    match = re.search(r'^  ' + name + r': (.*(?:\n    [^\n]*)*)', text, re.M)
    if not match:
        return ''
    value = re.sub(r'\n    ', ' ', match[1])
    return json.loads(value) if value.startswith('"') else value

def validate():
    assets = {}
    for meta in DATA.rglob('*.asset.meta'):
        guid = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)[1]
        assets[guid] = Path(str(meta)[:-5])
    abilities = set()
    # Current playable roster follows the production drop policy (including treasure-only weapons).
    policy = (ROOT / 'Assets/_Project/Runtime/Features/Loot/LootPoolItemSelectionService.cs').read_text(encoding='utf-8-sig')
    rule = policy.split('CanDropWeapon', 1)[1].split('};', 1)[0]
    roster = set(re.findall(r'"(Weapon\.[^"]+)"', rule))
    weapons = [p for p in (DATA / 'Items/Weapons/Definitions').glob('*.asset')
               if field(p.read_text(encoding='utf-8-sig'), 'weaponId') in roster]
    for weapon in weapons:
        text = weapon.read_text(encoding='utf-8-sig')
        refs = re.findall(r'^  (?:attack|skill1|skill2|abilityLoadout):.*guid: (\w+)', text, re.M)
        for guid in refs:
            target = assets[guid]
            if '/Loadouts/' in target.as_posix():
                refs2 = re.findall(r'guid: (\w+)', target.read_text(encoding='utf-8-sig'))
                abilities.update(assets[g] for g in refs2 if g in assets and '/Abilities/Definitions/' in assets[g].as_posix())
            else:
                abilities.add(target)
    for ability in abilities:
        text = ability.read_text(encoding='utf-8-sig')
        simple = field(text, 'simpleDescription')
        assert simple.strip(), f'Missing summary: {ability}'
        assert field(text, 'description').strip(), f'Missing detailed copy: {ability}'
        assert simple.count('[[') == simple.count(']]'), f'Invalid glossary token: {ability}'
        assert simple.count('{') == simple.count('}'), f'Invalid formatting token: {ability}'

    for path, guid in [
        ('Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab', '4d2b45ce9805b674ea168ee200dab7ce'),
        ('Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab', '613841d6edc54d02ab68ad0ae0525bba'),
    ]:
        text = (ROOT / path).read_text(encoding='utf-8-sig')
        blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)[1:]
        ids = [re.search(r'&(-?\d+)', b)[1] for b in blocks]
        assert len(ids) == len(set(ids)), f'Duplicate fileID in {path}'
        objects = dict(zip(ids, blocks))
        owner = next(b for b in blocks if 'guid: ' + guid in b)
        hint = re.search(r'  weaponDescriptionHint:\n((?:    [^\n]+\n){3})', owner)[1]
        root, icon, label = re.findall(r'fileID: (\d+)', hint)
        assert all(i in objects for i in [root, icon, label]), f'Broken hint reference: {path}'
        assert 'm_Name: WeaponDescriptionHint' in objects[root]
        assert 'm_fontSize: 27\n' in objects[label] and 'm_RaycastTarget: 0' in objects[label]
        assert 'm_PreserveAspect: 1' in objects[icon] and 'm_RaycastTarget: 0' in objects[icon]
        for b in blocks:
            if not re.search(r'&870921\d+', b):
                continue
            for reference in re.findall(r'\{fileID: (-?\d+)\}', b):
                assert reference == '0' or reference in objects, f'Broken local reference {reference}: {path}'
        caption = re.search(r'  m_text: (.*)', objects[label])[1]
        assert json.loads(caption) == '자세히 설명'

    print(f'PASS: {len(weapons)} weapons, {len(abilities)} abilities with both descriptions, 2 authored hints and local references.')

if __name__ == '__main__':
    validate()
