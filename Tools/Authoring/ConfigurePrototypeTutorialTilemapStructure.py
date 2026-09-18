"""Match ShadowCorridor's Ground/Wall structure without repainting any tiles."""
from pathlib import Path
import re

path = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
def parse(text):
    return {m[1]: m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', text)}

original = path.read_text(encoding='utf-8-sig')
blocks = parse(original)
source = parse(Path('Assets/_Project/Scenes/ShadowCorridor.unity').read_text(encoding='utf-8-sig'))
assert '8700000001' not in blocks, 'Already configured; do not overwrite later scene edits'
before_tiles = {i: b for i, b in blocks.items() if b.startswith('--- !u!1839735485 ')}

def set_field(i, field, value):
    blocks[i] = re.sub(rf'(?m)^  {field}:.*$', f'  {field}: {value}', blocks[i])

set_field('8600000018', 'm_Name', 'Ground')
set_field('8600000018', 'm_Layer', '7')
set_field('8600000022', 'm_Name', 'Wall')
set_field('8600000022', 'm_Layer', '30')
for target, template in [('8600000021', '167450171'), ('8600000025', '236309119')]:
    for field in ['m_SortingLayerID', 'm_SortingLayer', 'm_SortingOrder', 'm_Mode']:
        set_field(target, field, re.search(rf'(?m)^  {field}: (.*)', source[template])[1])

mapping = {'236309114': '8600000022', '236309118': '8700000001',
           '236309122': '8700000002', '236309121': '8700000003'}
new_blocks = []
for template in ['236309118', '236309122', '236309121']:
    text = source[template]
    text = re.sub(r'&(\d+)', lambda m: '&' + mapping.get(m[1], m[1]), text, count=1)
    text = re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: ' + mapping.get(m[1], m[1]) + '}', text)
    if template == '236309121':
        # Cached geometry belongs to the source map. Unity regenerates this map's paths on import.
        for field in ['m_ColliderPaths', 'm_CompositePaths']:
            text = re.sub(rf'(?ms)^  {field}:\n.*?(?=^  \w+:)', f'  {field}: []\n', text)
    new_blocks.append(text)
blocks['8600000022'] = blocks['8600000022'].replace('  m_Layer:',
    ''.join(f'  - component: {{fileID: {mapping[i]}}}\n' for i in ['236309118', '236309122', '236309121']) + '  m_Layer:', 1)

disabled = 0
for obj in list(blocks.values()):
    if not re.search(r'(?m)^  m_Name: OuterWall$', obj):
        continue
    for component in re.findall(r'component: \{fileID: (\d+)\}', obj):
        if blocks[component].startswith('--- !u!61 '):
            set_field(component, 'm_Enabled', '0')
            disabled += 1
assert disabled == 22, f'Unexpected blockout wall count: {disabled}'
assert all(blocks[i] == text for i, text in before_tiles.items()), 'Painted tile content changed'
roots = blocks.pop('9223372036854775807')
output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(blocks.values()) + ''.join(new_blocks) + roots
staged = Path('Temp/tutorial-configured-tile-structure.unity')
staged.write_text(output, encoding='utf-8')
assert path.read_text(encoding='utf-8-sig') == original, 'Scene changed concurrently; reread before applying'
staged.replace(path)
print('Configured Ground/Wall layers, sorting and composite tile collision; preserved all painted tile data; disabled 22 blockout walls.')
