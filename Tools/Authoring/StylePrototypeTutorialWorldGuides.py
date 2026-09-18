"""Apply bold, larger, tightly spaced floor signage and place both entry guides in the first room."""
from pathlib import Path
import re

path = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
original = path.read_text(encoding='utf-8-sig')
b = {m[1]: m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', original)}
paint = {i: t for i, t in b.items() if t.startswith('--- !u!1839735485 ')}
def field(i, key, value):
    b[i] = re.sub(rf'(?m)^  {key}:.*$', f'  {key}: {value}', b[i])
def go(component): return re.search(r'm_GameObject: {fileID: (\d+)}', b[component])[1]
def transform(obj): return re.search(r'component: {fileID: (\d+)}', b[obj])[1]

guides = [i for i, t in b.items() if 'guid: 19b824b7b6e54d93a1c7d06e5a7230d1' in t]
assert len(guides) == 7
for i in guides:
    root = go(i); tr = transform(root)
    glyph = re.search(r'  glyph: {fileID: (\d+)}', b[i])[1]
    fallback = re.search(r'  fallbackLabel: {fileID: (\d+)}', b[i])[1]
    content = re.search(r'  presentationRoot: {fileID: (\d+)}', b[i])
    content = content[1] if content else tr
    children = re.findall(r'^  - {fileID: (\d+)}', b[content], re.M)
    labels = [c for child in children for c in re.findall(r'component: {fileID: (\d+)}', b[go(child)])
              if '  m_text:' in b[c]]
    assert len(labels) == 2
    field(transform(go(glyph)), 'm_LocalPosition', '{x: -3, y: 0, z: 0}')
    for label in labels:
        field(label, 'm_fontStyle', '1')
        size = '32' if label == fallback else '40'
        field(label, 'm_fontSize', size); field(label, 'm_fontSizeBase', size)
        field(label, 'm_enableAutoSizing', '0')
        rect = transform(go(label))
        if label == fallback:
            field(rect, 'm_AnchoredPosition', '{x: -3, y: 0}')
            field(rect, 'm_LocalPosition', '{x: -3, y: 0, z: -0.1}')
        else:
            field(label, 'm_HorizontalAlignment', '1')
            field(rect, 'm_Pivot', '{x: 0, y: 0.5}')
            field(rect, 'm_AnchoredPosition', '{x: -2.35, y: 0}')
            field(rect, 'm_LocalPosition', '{x: -2.35, y: 0, z: -0.1}')
    if '  m_Name: MovementFloorGuide\n' in b[root]:
        field(tr, 'm_LocalPosition', '{x: 8, y: -4.2, z: 0}')
    elif '  m_Name: DashEntranceGuide\n' in b[root]:
        field(tr, 'm_LocalPosition', '{x: 8, y: -2.5, z: 0}')

# Match the other background-free floor signs, retaining the persistent dash visibility policy.
b['8200000150'] = b['8200000150'].replace('  - {fileID: 8200000152}\n', '')
for i in ['8200000151', '8200000152', '8200000153']: b.pop(i, None)
assert all(b[i] == t for i, t in paint.items())
assert path.read_text(encoding='utf-8-sig') == original, 'Concurrent scene change'
staged = Path('Temp/tutorial-guide-typography.unity')
staged.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(b.values()), encoding='utf-8')
staged.replace(path)
print('Styled seven guides: Bold 40 pt text / 32 pt fallback; 0.25-unit icon gap; movement and dash in the first room.')
