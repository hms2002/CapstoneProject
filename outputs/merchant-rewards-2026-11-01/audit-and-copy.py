from pathlib import Path
import csv, hashlib, io, json, re, shutil, sys

root = Path.cwd()
out = root / 'outputs/merchant-rewards-2026-11-01'
clone = root / '.mrv'
baseline = out / 'baseline'
manifest = json.loads((out / 'baseline-manifest.json').read_text())
checks = []

def check(ok, message):
    if not ok: raise AssertionError(message)
    checks.append('PASS ' + message)

def read(folder, rel): return (folder / rel).read_text(encoding='utf-8-sig')
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

existing = [
    'Assets/_Project/Data/Dialogue/NPC/MSShopNpc.asset',
    'Assets/_Project/Data/Dialogue/Affection/SashaShopDiscount.asset',
    'Assets/_Project/Prefabs/UI/Dialogue/ShopSlot_RunGold.prefab',
    'Assets/_Project/Prefabs/UI/Dialogue/ShopSlot.prefab',
    'Assets/_Project/Data/Localization/GameText Shared Data.asset',
    *['Assets/_Project/Data/Localization/GameText_' + code + '.asset' for code in ['ko','en','ja','zh-Hans','zh-Hant']],
    'Assets/_Project/Runtime/Core/Presentation/GameTextAssetKeys.g.cs',
    'DataSheets/Localization/GameText.csv', 'DataSheets/Localization/AssetInventory.json'
]
new = []
for name in ['SashaSpecialOffer','SashaGoldRetention','SashaBetterRelics']:
    path = 'Assets/_Project/Data/Dialogue/Affection/' + name + '.asset'
    new += [path, path + '.meta']
for path in [
    'Assets/_Project/Runtime/Features/Dialogue/Affection/Effect/MerchantAffectionRewardEffect.cs',
    'Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantOfferPricing.cs',
    'Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsAuthoring.cs',
    'Assets/_Project/Editor/Tools/Dialogue/MerchantAffectionRewardsValidation.cs'
]:
    check(digest(root / path) == digest(clone / path), 'Native-verified source equals destination: ' + path)
    new.append(path + '.meta')

for path in existing:
    if '--copy' in sys.argv:
        check(digest(root / path) == manifest[path], 'No concurrent edit before scoped copy: ' + path)
for path in new:
    check((clone / path).is_file(), 'Unity authored new asset/metadata: ' + path)
    check(not (root / path).exists() or digest(root / path) == digest(clone / path), 'No conflicting new asset/GUID: ' + path)
carpet = 'Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab'
check(digest(root / carpet) == manifest[carpet], 'User-adjusted merchant prefab and bubble anchor remain byte-identical')

old_discount = 'asset.844437f5d3184cf69ce3cabd2160232e.rewardText'
new_keys = []
for name in ['SashaSpecialOffer','SashaGoldRetention','SashaBetterRelics']:
    path = 'Assets/_Project/Data/Dialogue/Affection/' + name + '.asset'
    guid = re.search(r'^guid: (\w+)', read(clone, path + '.meta'), re.M)[1]
    key = 'asset.' + guid + '.rewardText'
    new_keys.append(key)
    check('"MerchantAffectionRewardEffect/' + name + '/rewardText", "' + key + '"' in read(clone, existing[-3]), 'New reward runtime map matches Unity GUID: ' + name)

csv_path = 'DataSheets/Localization/GameText.csv'
def csv_rows(folder):
    rows = list(csv.reader(io.StringIO(read(folder, csv_path))))
    return rows[0], {row[0]: row for row in rows[1:]}
old_head, old_rows = csv_rows(baseline)
new_head, new_rows = csv_rows(clone)
check(old_head == new_head, 'CSV language/header contract preserved')
check(set(new_rows) - set(old_rows) == set(new_keys + ['shop.special_offer']), 'Exactly four translation keys added')
check(all(row == new_rows[key] for key, row in old_rows.items() if key != old_discount), 'All previous CSV translations except approved discount preserved')
check(all(len(new_rows[key]) == 6 and all(new_rows[key][1:]) for key in new_keys + [old_discount, 'shop.special_offer']), '25 reward/badge translations present')

def entries(source):
    pattern = r'(?ms)^  - m_Id: (\d+)\n.*?(?=^  - m_Id:|^  m_Metadata:|^  references:|\Z)'
    return {match[1]: match[0] for match in re.finditer(pattern, source)}
shared = 'Assets/_Project/Data/Localization/GameText Shared Data.asset'
old_shared, new_shared = entries(read(baseline, shared)), entries(read(clone, shared))
check(all(block == new_shared.get(key) for key, block in old_shared.items()), 'All existing shared key IDs/metadata preserved')
check(len(new_shared) == len(old_shared) + 4, 'Shared table adds exactly four keys')
discount_id = next(key for key, block in old_shared.items() if old_discount in block)
for code in ['ko','en','ja','zh-Hans','zh-Hant']:
    path = 'Assets/_Project/Data/Localization/GameText_' + code + '.asset'
    before, after = entries(read(baseline, path)), entries(read(clone, path))
    check(all(block == after.get(key) for key, block in before.items() if key != discount_id), 'Prior locale records byte-preserved except approved discount: ' + code)
    check(len(after) == len(before) + 4, 'Exactly four locale records added: ' + code)

inventory_path = 'DataSheets/Localization/AssetInventory.json'
old_inventory = {row['key']:row for row in json.loads(read(baseline, inventory_path))['rows']}
new_inventory = {row['key']:row for row in json.loads(read(clone, inventory_path))['rows']}
check(all(row == new_inventory.get(key) for key,row in old_inventory.items() if key != old_discount), 'Existing localization inventory preserved')
check(set(new_inventory)-set(old_inventory) == set(new_keys), 'Localization inventory adds exactly three reward assets')
for key in new_keys + [old_discount]:
    check(new_inventory[key]['text'] == new_rows[key][1], 'Inventory source matches Korean CSV: ' + key)

def blocks(source):
    return {match[1]: match[0] for match in re.finditer(r'(?ms)^--- !u!\d+ &(-?\d+)\n.*?(?=^--- !u!|\Z)', source)}
for path in existing[2:4]:
    before, after = blocks(read(baseline, path)), blocks(read(clone, path))
    check(set(before) <= set(after), 'Existing prefab objects retain their fileIDs: ' + path)
    new_ids = set(after) - set(before)
    for key, old in before.items():
        changed = after[key]
        if changed == old: continue
        for new_id in new_ids:
            changed = changed.replace('  - {fileID: ' + new_id + '}\n', '')
        changed = re.sub(r'^  (originalPriceText|specialOfferRoot|specialOfferText):.*\n', '', changed, flags=re.M)
        changed = changed.replace('  priceIconPositionMode: 1\n', '  priceIconPositionMode: 0\n')
        changed = changed.replace('  m_TextWrappingMode: 0\n', '  m_TextWrappingMode: 1\n')
        # TMP canonicalizes the existing renderer's empty material to its unchanged font's own material on import.
        if old.startswith('--- !u!23 ') and '  m_Materials:\n  - {fileID: 0}\n' in old:
            changed = changed.replace('  m_Materials:\n  - {fileID: 7785331808423663526, guid: ab56e3ce9940d9648a8a4896df560905, type: 2}\n', '  m_Materials:\n  - {fileID: 0}\n')
        check(changed == old, 'Only authorized prefab properties changed: ' + path + '/' + key)
    check(sum('m_Name: OriginalPriceText\n' in block for block in after.values()) == 1 and sum('m_Name: SpecialOfferTag\n' in block for block in after.values()) == 1, 'Exactly one authored original/badge label: ' + path)

purchase = read(root, 'Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantPurchaseService.cs')
check('SpendGold(stockEntry.price)' in purchase and 'SpendMagicStone(stockEntry.price)' in purchase, 'Existing purchase path charges the same stock price projected by the label')
validation = read(clone, 'outputs/merchant-rewards-2026-11-01/validation.txt')
check('PASS:' in validation and 'Rendered visible merchant' in validation, 'Native checks include nonblank world render')
if '--copy' in sys.argv:
    for path in existing + new:
        (root / path).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(clone / path, root / path)
    for path in (clone / 'outputs/merchant-rewards-2026-11-01').glob('*'):
        if path.is_file(): shutil.copyfile(path, out / path.name)
    (out / 'authored-copy-manifest.json').write_text(json.dumps({path:digest(root/path) for path in existing+new}, indent=2), encoding='utf-8')
    checks.append('PASS Copied only whitelisted native-authored assets/metadata; no merchant placement, scene, font, Addressables, Packages or ProjectSettings copied')
(out / 'static-checks.txt').write_text('\n'.join(checks) + f'\nPASS: {len(checks)} checks.\n', encoding='utf-8')
print('\n'.join(checks[-5:]) + f'\nPASS: {len(checks)} checks.')
