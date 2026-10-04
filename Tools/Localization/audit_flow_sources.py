"""Read-only source audit: decode C# literals, retaining constants, defaults and English UI candidates.

Heuristics produce candidates, not proof that a branch is reachable or localized.
Review candidates against the actual game flow before changing gameplay identifiers.
"""
import json
import re
from pathlib import Path

root = Path('Assets/_Project/Runtime')
token = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|\'(?:\\.|[^\'\\])*\'|(?P<verbatim>(?:\$@|@\$|@)"(?:[^"]|"")*")|(?P<normal>\$?"(?:[^"\\]|\\.)*")')
hangul = re.compile('[가-힣]')
display = re.compile(r'\b(?:text|Text|Label|label|caption|Caption|message|Message|description|Description|prompt|Prompt|title|Title|instruction|Instruction|format|Format)\w*|SetText|ShowWarning|BuildTooltip')
rows = []
files = []
for path in root.rglob('*.cs'):
    try:
        source = path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:
        source = path.read_text(encoding='cp949')
    files.append(path.as_posix())
    for match in token.finditer(source):
        literal = match.group('normal') or match.group('verbatim')
        if not literal:
            continue
        if match.group('verbatim'):
            value = literal[literal.index('"') + 1:-1].replace('""', '"')
        else:
            try:
                value = json.loads(literal.lstrip('$'))
            except ValueError:
                continue
        start = source.rfind('\n', 0, match.start()) + 1
        end = source.find('\n', match.end())
        line = source[start:end if end != -1 else len(source)].strip()
        if not hangul.search(value) and not (display.search(line) and re.search('[A-Za-z]{3}', value)):
            continue
        if re.search(r'\[(?:Header|Tooltip|ContextMenu|CreateAssetMenu)', line):
            category = 'editor_metadata'
        elif 'GameText.' in line:
            category = 'localized_key_or_fallback'
        elif re.search(r'Debug\.|throw |Exception\(|Assert\.', line):
            category = 'developer_message'
        else:
            category = 'review_candidate'
        relative = path.relative_to(root).as_posix()
        cluster = '/'.join(relative.split('/')[:2])
        rows.append(dict(path=path.as_posix(), line=source.count('\n', 0, match.start()) + 1,
                         cluster=cluster, category=category, text=value, source=line))
output = dict(note='Source candidates only; inspect actual display call sites and authored assets. English IDs are not automatically translation targets.',
              files_scanned=len(files), rows=rows)
Path('DataSheets/Localization/FlowSourceAudit.json').write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
candidates = [r for r in rows if r['category'] == 'review_candidate']
print(f'{len(files)} runtime sources scanned; {len(rows)} classified literals; {len(candidates)} review candidates.')
for row in candidates:
    if hangul.search(row['text']):
        print(f"{row['path']}:{row['line']} {row['text'][:160]!r}")
