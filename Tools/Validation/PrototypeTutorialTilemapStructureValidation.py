"""Check authored Ground/Wall configuration; does not approximate sprite collider geometry."""
from pathlib import Path
import re

def parse(path):
    text = path.read_text(encoding='utf-8-sig')
    matches = list(re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', text))
    blocks = {m[1]: m[0] for m in matches}
    assert len(matches) == len(blocks), 'Duplicate scene IDs'
    assert all(i == '0' or i in blocks for i in re.findall(r'\{fileID: (\d+)\}', text)), 'Missing local reference'
    return blocks

blocks = parse(Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity'))
source = parse(Path('Assets/_Project/Scenes/ShadowCorridor.unity'))
def value(block, field):
    return re.search(rf'(?m)^  {field}: (.*)', block)[1]

for go, name, layer in [('8600000018', 'Ground', '7'), ('8600000022', 'Wall', '30')]:
    assert value(blocks[go], 'm_Name') == name
    assert value(blocks[go], 'm_Layer') == layer
for target, template in [('8600000021', '167450171'), ('8600000025', '236309119')]:
    for field in ['m_SortingLayerID', 'm_SortingOrder', 'm_Mode']:
        assert value(blocks[target], field) == value(source[template], field)
assert 'm_CompositeOperation: 1' in blocks['8700000001']
assert 'm_BodyType: 2' in blocks['8700000002']
assert 'm_GenerationType: 0' in blocks['8700000003']
for i in ['8700000001', '8700000002', '8700000003']:
    assert f'component: {{fileID: {i}}}' in blocks['8600000022']
    assert 'm_GameObject: {fileID: 8600000022}' in blocks[i]
for obj in blocks.values():
    if '  m_Name: OuterWall\n' not in obj: continue
    for i in re.findall(r'component: \{fileID: (\d+)\}', obj):
        if blocks[i].startswith('--- !u!61 '): assert 'm_Enabled: 0' in blocks[i]
snapshot = Path('Temp/tutorial-before-tile-structure.unity')
# Raw tile preservation applies before the approved grid-normalization migration.
# After migration use TutorialGridNormalizationSceneValidation.py for expanded coverage.
if snapshot.exists() and 'm_LocalScale: {x: 0.5, y: 0.5, z: 1}' in blocks['8600000016']:
    before = parse(snapshot)
    for i, block in before.items():
        if block.startswith('--- !u!1839735485 ') or i in ['8600000016', '8600000019', '8600000023']:
            assert blocks[i] == block, 'Painted tile data/grid transform changed: ' + i
    print('PAINT_PRESERVED: all tilemap blocks and grid transforms match the pre-change snapshot.')
print('TILEMAP_STRUCTURE_PASS: Ground/Wall layers, reference-map sorting, merged static collision, inactive blockout colliders.')
