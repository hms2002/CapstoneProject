"""Validate explicit key coverage and tokens without running Unity."""
import csv
import json
import re
from collections import Counter
from pathlib import Path

folder = Path('DataSheets/Localization')
languages = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
with (folder / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
keys = {r['key'] for r in rows}
assert len(keys) == len(rows), 'Duplicate translation keys'
tokens = re.compile(r'\{(\d+(?:,[+-]?\d+)?(?::[^{}]+)?|[A-Za-z_]\w*)\}')
links = re.compile(r'\[\[(.*?)\]\]')
for row in rows:
    expected = Counter(tokens.findall(row['ko']))
    identities = Counter(links.findall(row['ko']))
    for language in languages:
        assert row[language].strip(), (row['key'], language, 'empty translation')
        assert Counter(tokens.findall(row[language])) == expected, (row['key'], language, 'placeholder mismatch')
        assert Counter(links.findall(row[language])) == identities, (row['key'], language, 'glossary identity mismatch')
missing = []
for path in Path('Assets/_Project/Runtime').rglob('*.cs'):
    source = path.read_text(encoding='utf-8-sig', errors='replace')
    for key in re.findall(r'GameText\.(?:Get|Format)\("([^"]+)"\s*,', source):
        if key not in keys: missing.append((str(path), key))
assert not missing, missing
for filename in ('AssetInventory.json', 'InkInventory.json', 'StaticUiBindings.json'):
    records = json.loads((folder / filename).read_text(encoding='utf-8'))['rows']
    assert all(r['key'] in keys for r in records), filename + ': uncovered key'
print(f'PASS: {len(rows)} keys × 5 languages; runtime keys, placeholders and glossary identities covered.')
