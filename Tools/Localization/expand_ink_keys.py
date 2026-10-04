"""Add fixed loc tags to reviewed active Ink lines; leave control flow untouched."""
import csv
import hashlib
import json
import re
from pathlib import Path

folder = Path('DataSheets/Localization')
root = Path('Assets/_Project/Data/Dialogue/Ink')
names = ['GrandHallScribe', 'TutorialDarkLordScript1', 'TutorialDarkLordScript2',
         'AnimatedVariants/BuffyEventDialogue', 'AnimatedVariants/DemonKingIntroDialogue_Animated',
         'AnimatedVariants/DragonBossIntroDialogue_SpiriNotion_Animated',
         'AnimatedVariants/MerchantDialogue_Animated', 'AnimatedVariants/ParcelEventDialogue',
         'AnimatedVariants/ShadowBossIntroDialogue_Animated',
         'AnimatedVariants/ShadowBossRepeatEncounterDialogue_Animated',
         'AnimatedVariants/SlimeQueenIntroDialogue_MeltaNotion_Animated',
         'AnimatedVariants/UpgradeNpcDialogue_Animated',
         'HubIntroAfterDarkLord/HubIntro_Final', 'HubIntroAfterDarkLord/HubIntro_Gate',
         'HubIntroAfterDarkLord/HubIntro_Junk', 'HubIntroAfterDarkLord/HubIntro_TrainingDummy']
languages = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
with (folder / 'CommonTranslations.tsv').open(encoding='utf-8-sig', newline='') as f:
    reviewed = {r['ko'].replace('\\n', '\n'): {k: r[k].replace('\\n', '\n') for k in languages}
                for r in csv.DictReader(f, delimiter='\t')}
with (folder / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
keys = {r['key'] for r in rows}
inventory, missing, changed = [], [], []
for name in names:
    path = root / (name + '.ink')
    source = path.read_text(encoding='utf-8-sig')
    result = []
    for number, line in enumerate(source.splitlines(), 1):
        stripped = line.strip()
        # Camera cue sentinels are side-effect messages consumed by the dialogue controller.
        if (not stripped or stripped.startswith(('//', '#', '->', '=', 'VAR ', 'CONST ', '~', '{', '}'))
                or '[camera cue]' in stripped):
            result.append(line)
            continue
        choice = re.match(r'^(\s*[+*]\s*\[)(.*)(\]\s*)$', line)
        body = choice.group(2) if choice else stripped
        text = body.split(' #', 1)[0].strip()
        existing = re.search(r'\bloc:([\w.]+)', body)
        key = existing.group(1) if existing else 'dialogue.' + path.stem.lower() + '.' + hashlib.sha1(text.encode()).hexdigest()[:10]
        record = {'key': key, 'path': path.as_posix(), 'line': number, 'text': text, 'choice': bool(choice)}
        inventory.append(record)
        if key in keys:
            result.append(line)
            continue
        translation = reviewed.get(text)
        if translation is None:
            missing.append(record)
            result.append(line)
            continue
        rows.append(dict(key=key, **translation))
        keys.add(key)
        if existing:
            result.append(line)
        elif choice:
            result.append(choice.group(1) + body + ' # loc:' + key + choice.group(3))
        else:
            result.append(line + ' # loc:' + key)
    output = '\n'.join(result) + '\n'
    if output != source.replace('\r\n', '\n'):
        path.write_text(output, encoding='utf-8')
        changed.append(path.as_posix())
with (folder / 'GameText.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=('key',) + languages)
    writer.writeheader()
    writer.writerows(rows)
(folder / 'InkInventory.json').write_text(json.dumps({'rows': inventory}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(folder / 'UntranslatedInk.json').write_text(json.dumps({'rows': missing}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(inventory)} active Ink text lines; {len(missing)} await translation; {len(changed)} sources updated.')
