"""Authoring helper. Sentence matching is used only during migration, never at runtime.

Wrap reviewed C# UI literals with explicit keys; keep unmapped strings in an audit.
Run from project root. Existing explicit keys and CSV translations are preserved.
"""
import csv
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path('.')
FOLDER = ROOT / 'DataSheets/Localization'
with (FOLDER / 'CommonTranslations.tsv').open(encoding='utf-8-sig', newline='') as f:
    translations = {r['ko'].replace('\\n', '\n'): [r[k].replace('\\n', '\n') for k in ('ko','en','ja','zh-Hans','zh-Hant')]
                    for r in csv.DictReader(f, delimiter='\t')}
with (FOLDER / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
keys = {r['key'] for r in rows}
audit = []
changed = []
literal = re.compile(r'(?P<prefix>\$?)(?P<literal>"(?:[^"\\]|\\.)*")')

def interpolation(text):
    """Number interpolation expressions without evaluating gameplay values."""
    fmt, args, i = '', [], 0
    while i < len(text):
        if text[i:i+2] in ('{{','}}'):
            fmt += text[i:i+2]; i += 2; continue
        if text[i] != '{':
            fmt += text[i]; i += 1; continue
        start = i + 1; i += 1; depth = 0
        while i < len(text):
            if text[i] in '([': depth += 1
            elif text[i] in ')]': depth -= 1
            if text[i] == '}' and depth == 0: break
            i += 1
        if i == len(text): raise ValueError('Unclosed interpolation')
        expr = text[start:i]; depth = 0; split = None
        for j, c in enumerate(expr):
            if c in '([': depth += 1
            elif c in ')]': depth -= 1
            elif c == ':' and depth == 0: split = j; break
        spec = expr[split:] if split is not None else ''
        value = expr[:split] if split is not None else expr
        fmt += '{' + str(len(args)) + spec + '}'
        args.append(value); i += 1
    return fmt, args

for path in (ROOT / 'Assets/_Project/Runtime').rglob('*.cs'):
    relative = path.as_posix()
    allowed = any(part in relative for part in (
        '/UI/', '/Infrastructure/Input/', '/Features/Tutorial/', '/Features/Items/Relics/',
        '/Features/Progression/', '/Features/Loot/', '/Features/Dialogue/', '/Features/Map/',
        '/Infrastructure/SceneFlow/', '/Presentation/Dialogue/NPC/', '/Features/Items/Weapons/Data/'))
    if not allowed or any(part in relative for part in ('/Debug/', '/Cheats/')): continue
    try: source = path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError: continue
    lines = source.splitlines(keepends=True); result = []
    for n, line in enumerate(lines):
        if (line.lstrip().startswith(('//','*','"')) or
            any(s in line for s in ('Debug.Log','Tooltip(','Header(','SerializeField','GameText.','throw new')) or
            re.search(r'\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?string\b', line) and '=' in line and '=>' not in line):
            result.append(line); continue
        replacements = []
        for match in literal.finditer(line):
            try: text = json.loads(match.group('literal'))
            except ValueError: continue
            if not re.search('[가-힣]',text) or re.match(r'\[[A-Za-z]', text): continue
            try: fmt, args = interpolation(text) if match.group('prefix') else (text, [])
            except ValueError: continue
            translated = translations.get(fmt)
            key = 'code.' + path.stem.lower() + '.' + hashlib.sha1(fmt.encode()).hexdigest()[:10]
            if translated is None:
                audit.append(dict(path=relative, line=n+1, key=key, text=fmt)); continue
            call = ('GameText.Format' if args else 'GameText.Get') + '(' + json.dumps(key) + ', ' + json.dumps(fmt, ensure_ascii=False)
            if args: call += ', ' + ', '.join(args)
            call += ')'
            replacements.append((match.start(),match.end(),call))
            if key not in keys:
                rows.append(dict(zip(('key','ko','en','ja','zh-Hans','zh-Hant'), [key]+translated))); keys.add(key)
        for start,end,call in reversed(replacements): line=line[:start]+call+line[end:]
        result.append(line)
    updated = ''.join(result)
    if updated != source:
        path.write_text(updated,encoding='utf-8'); changed.append(relative)
with (FOLDER / 'GameText.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=('key','ko','en','ja','zh-Hans','zh-Hant'));writer.writeheader();writer.writerows(rows)
(FOLDER / 'SourceAudit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print('Explicit keys:',len(rows),'changed source files:',len(changed),'remaining candidate strings:',len(audit))
