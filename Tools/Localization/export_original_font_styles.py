"""Read original TMP material references from HEAD; never write Unity YAML."""
import json
import re
import subprocess
from pathlib import Path

paths = {r['path'] for r in json.loads(Path('DataSheets/Localization/StaticUiBindings.json').read_text(encoding='utf-8'))['rows']}
paths.update(str(p).replace('\\', '/') for p in Path('Assets/_Project/Prefabs/UI').rglob('*.prefab') if 'GlobalUIRoot_' not in str(p) and '_Backup' not in str(p))
rows = []
for path in sorted(paths):
    result = subprocess.run(['git', 'show', 'HEAD:' + path], capture_output=True, text=True, encoding='utf-8')
    if result.returncode:
        continue
    blocks = {}
    for match in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- |\Z)', result.stdout, re.M | re.S):
        blocks[match[2]] = (match[1], match[3])
    names, transforms = {}, {}
    for identity, (kind, block) in blocks.items():
        if kind == '1':
            name = re.search(r'^  m_Name: (.*)$', block, re.M)
            if name:
                names[identity] = name[1].strip('"')
        if kind in ('4', '224'):
            go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)
            father = re.search(r'm_Father: \{fileID: (-?\d+)\}', block)
            if go and father:
                transforms[identity] = (go[1], father[1])
    def hierarchy(t):
        go, parent = transforms[t]
        return (hierarchy(parent) + '/' if parent in transforms else '') + names.get(go, '')
    owners = {go: hierarchy(t) for t, (go, _) in transforms.items()}
    for kind, block in blocks.values():
        if kind != '114' or 'm_fontAsset:' not in block:
            continue
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)
        material = re.search(r'm_sharedMaterial: \{fileID: (-?\d+), guid: (\w+)', block)
        if go and material and go[1] in owners:
            rows.append(dict(path=path, owner=owners[go[1]], guid=material[2], localId=int(material[1])))
Path('Temp').mkdir(exist_ok=True)
Path('Temp/original-font-styles.json').write_text(json.dumps(dict(rows=rows), ensure_ascii=False), encoding='utf-8')
print(f'Exported {len(rows)} original TMP styles from Git HEAD (read only).')
