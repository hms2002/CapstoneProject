"""Retain glossary identity in every locale; caption translation is a separate key."""
import csv
import re
from pathlib import Path

folder = Path('DataSheets/Localization')
languages = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
with (folder / 'CommonTranslations.tsv').open(encoding='utf-8-sig', newline='') as f:
    reviewed = {r['ko']: r for r in csv.DictReader(f, delimiter='\t')}
with (folder / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
pattern = re.compile(r'\[\[(.*?)\]\]')
terms = set()
for row in rows:
    original = pattern.findall(row['ko'])
    terms.update(t for t in original if '{' not in t)
    for language in languages[1:]:
        localized = pattern.findall(row[language])
        if len(localized) != len(original):
            raise ValueError('Glossary link count changed: ' + row['key'] + '/' + language)
        iterator = iter(original)
        row[language] = pattern.sub(lambda m: '[[' + next(iterator) + ']]', row[language])
keys = {r['key'] for r in rows}
for term in sorted(terms):
    if term not in reviewed:
        raise ValueError('Missing glossary caption translations: ' + term)
    if 'term.' + term not in keys:
        rows.append(dict(key='term.' + term, **{k: reviewed[term][k] for k in languages}))
with (folder / 'GameText.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=('key',) + languages)
    writer.writeheader()
    writer.writerows(rows)
print(f'{len(terms)} glossary identities preserved across five languages.')
