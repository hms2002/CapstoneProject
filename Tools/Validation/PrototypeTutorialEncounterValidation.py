"""Validate authored tutorial encounter references and serialized tile palettes."""
from pathlib import Path
import collections
import re

scene = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity').read_text(encoding='utf-8-sig')
blocks = {m[1]: m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', scene)}
owner = blocks['8100000003']
for field in ['tutorialGunner', 'gunnerRetreatPoint', 'chestLock']:
    ref = re.search(field + r': \{fileID: (\d+)\}', owner)[1]
    assert ref in blocks, field
spawns = re.search(r'  skillSpawnPoints:.*?(?=  gunnerRetreatPoint:)', owner, re.S)[0]
spawn_ids = re.findall(r'fileID: (\d+)', spawns)
assert len(set(spawn_ids)) == 4 and all(i in blocks for i in spawn_ids)
assert 'skillTargets: []' in owner
assert 'guid: fda3cefab78502b40b55e2b827a2ecd1' in owner
assert 'm_AddedComponents:\n    - targetCorrespondingSourceObject:' in blocks['8000000476']
assert re.search(r'propertyPath: m_IsActive\n      value: 1', blocks['8000000476'])
maps = [b for b in blocks.values() if b.startswith('--- !u!1839735485 ')]
assert len(maps) == 2
shipping = Path('Assets/_Project/Scenes/TutorialCorridor.unity').read_text(encoding='utf-8-sig')
for tilemap in maps:
    rows = re.findall(r'(?ms)^  - first:.*?(?=^  - first:|^  m_AnimatedTiles:|^  m_TileAssetArray:)', tilemap)
    assert rows
    for array, key in [('m_TileAssetArray', 'm_TileIndex'), ('m_TileSpriteArray', 'm_TileSpriteIndex'),
                       ('m_TileMatrixArray', 'm_TileMatrixIndex'), ('m_TileColorArray', 'm_TileColorIndex')]:
        data = re.search(rf'(?ms)^  {array}:\n.*?(?=^  \w+:)', tilemap)[0]
        counts = collections.Counter(int(re.search(key + r': (\d+)', row)[1]) for row in rows)
        refs = list(map(int, re.findall(r'm_RefCount: (\d+)', data)))
        assert all(0 <= i < len(refs) for i in counts)
        assert refs == [counts[i] for i in range(len(refs))], array
        for guid in re.findall(r'guid: (\w+)', data):
            assert guid in shipping, 'Tile palette must come from shipping tutorial'
print('ENCOUNTER_PASS: gunner, four spawn references, warrior prefab, active locked chest, two shipping tile palettes and refcounts.')
