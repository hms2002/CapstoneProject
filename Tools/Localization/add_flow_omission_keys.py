"""Merge reviewed flow display translations; preserve authored asset fields and fixed keys."""
import csv,json
from pathlib import Path
folder=Path('DataSheets/Localization')
languages=('ko','en','ja','zh-Hans','zh-Hant')
with (folder/'GameText.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
by_key={r['key']:r for r in rows}
inventory=json.loads((folder/'AssetInventory.json').read_text(encoding='utf-8'))
flow=json.loads((folder/'FlowNewAssetFields.json').read_text(encoding='utf-8'))['rows']
translations=json.loads((folder/'FlowTranslations.json').read_text(encoding='utf-8'))
assert len(flow)==len(translations)==72
for asset,translation in zip(flow,translations):
    assert translation['ko']==asset['text'],asset['key']
    item=dict(key=asset['key'],**translation)
    if item['key'] in by_key:
        assert by_key[item['key']]['ko']==item['ko']
    else: rows.append(item);by_key[item['key']]=item
    if not any(r['key']==asset['key'] for r in inventory['rows']):inventory['rows'].append(asset)
extras=json.loads((folder/'FlowFixedTranslations.json').read_text(encoding='utf-8'))
for item in extras:
    if item['key'] not in by_key: rows.append(item);by_key[item['key']]=item
with (folder/'GameText.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=('key',)+languages);w.writeheader();w.writerows(rows)
(folder/'AssetInventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(rows)} keys; {len(inventory["rows"])} asset fields; 72 flow fields connected.')
