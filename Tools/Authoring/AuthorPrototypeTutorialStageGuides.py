"""Author stage-bound world guides and the existing Hub arrow without repainting the map."""
from pathlib import Path
import re, json

path = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity')
original = path.read_text(encoding='utf-8-sig')
def parse(s):
    return {m[1]: m[0] for m in re.finditer(r'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)', s)}
b = parse(original)
assert 'Skill1FloorGuide' not in original, 'Already authored'
template = {str(i): b[str(i)] for i in range(8200000168, 8200000187)}
next_id = 8800000000
def alloc():
    global next_id
    next_id += 1
    return str(next_id)
def field(i, key, val):
    b[i] = re.sub(rf'(?m)^  {key}:.*$', lambda m: f'  {key}: {val}', b[i])
def remap(t, mapping):
    t = re.sub(r'&(\d+)', lambda m: '&' + mapping.get(m[1], m[1]), t, count=1)
    return re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: ' + mapping.get(m[1], m[1]) + '}', t)
def configure(mapping, name, position, action, stage, animated, text):
    def i(n): return mapping[str(n)]
    root, tr, component = i(8200000168), i(8200000169), i(8200000186)
    field(root, 'm_Name', name)
    field(tr, 'm_LocalPosition', f'{{x: {position[0]}, y: {position[1]}, z: 0}}')
    # Remove the old background; the border belongs to the font material.
    b[tr] = b[tr].replace(f'  - {{fileID: {i(8200000171)}}}\n', '')
    for n in [8200000170, 8200000171, 8200000172]: b.pop(i(n))
    content, ct = alloc(), alloc()
    b[content] = remap(b[root], {root: content, tr: ct})
    b[content] = b[content].replace(f'  - component: {{fileID: {component}}}\n', '')
    field(content, 'm_Name', 'Presentation')
    b[ct] = remap(b[tr], {tr: ct, root: content})
    field(ct, 'm_LocalPosition', '{x: 0, y: 0, z: 0}')
    field(ct, 'm_Father', f'{{fileID: {tr}}}')
    b[tr] = re.sub(r'(?s)  m_Children:.*?(?=  m_Father:)', f'  m_Children:\n  - {{fileID: {ct}}}\n', b[tr])
    for child in [8200000174, 8200000177, 8200000182]: field(i(child), 'm_Father', f'{{fileID: {ct}}}')
    field(component, 'action', str(action))
    b[component] += f'  tutorial: {{fileID: 8100000003}}\n  visibleStage: {stage}\n  presentationRoot: {{fileID: {ct}}}\n  animate: {int(animated)}\n'
    # TMP YAML strings may span multiple lines after Unity saves them.
    label = i(8200000185)
    b[label] = re.sub(r'(?ms)^  m_text:.*?(?=^  \w+:)', lambda m: '  m_text: ' + json.dumps(text, ensure_ascii=True) + '\n', b[label])
    for n in [8200000180, 8200000185]:
        field(i(n), 'm_sharedMaterial', '{fileID: 2100000, guid: bc5dc7440ad16f045b3a71f996721562, type: 2}')
    return tr

configure({i: i for i in template}, 'AttackEntranceGuide', (0, 22.2), 4, 2, False,
          '길게 눌러 연속 공격\n3타 콤보 × 3회')
children = []
for name, pos, action, stage, animate, text in [
    ('MovementFloorGuide', (8, -6.9), 0, 0, False, '위쪽으로 이동하자.'),
    ('Skill1FloorGuide', (-4, 29.2), 7, 3, True, '길게 눌러 충전 후 놓기\n적 4마리 처치'),
    ('Skill2FloorGuide', (5, 29.2), 8, 3, True, '스킬로 적 3마리 처치'),
    ('ChestFloorGuide', (0, 34.1), 5, 4, False, '상자를 열어 아이템을 선택하자.'),
    ('PortalFloorGuide', (0, 42), 5, 5, False, '포탈로 이동하자.')]:
    mapping = {i: alloc() for i in template}
    for i, t in template.items(): b[mapping[i]] = remap(t, mapping)
    children.append(configure(mapping, name, pos, action, stage, animate, text))
b['8000000002'] = b['8000000002'].replace('  m_Father:', ''.join(f'  - {{fileID: {i}}}\n' for i in children) + '  m_Father:', 1)
for i in range(8200000109, 8200000149, 5): field(str(i), 'm_IsActive', '0')
for i in ['8200000161', '8200000166']:
    field(i, 'm_sharedMaterial', '{fileID: 2100000, guid: bc5dc7440ad16f045b3a71f996721562, type: 2}')

# A scene-authored instance of the same prefab used by HubWeaponDepartureGuide.
instance, arrow = alloc(), alloc()
b[instance] = f'''--- !u!1001 &{instance}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: 6100123456789012345, guid: 7776d4bd1e804757a06eeb2eb256a829, type: 3}}
      propertyPath: m_IsActive
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6101123456789012345, guid: 7776d4bd1e804757a06eeb2eb256a829, type: 3}}
      propertyPath: m_LocalPosition.y
      value: 37
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: 7776d4bd1e804757a06eeb2eb256a829, type: 3}}
'''
b[arrow] = f'''--- !u!4 &{arrow} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 6101123456789012345, guid: 7776d4bd1e804757a06eeb2eb256a829, type: 3}}
  m_PrefabInstance: {{fileID: {instance}}}
  m_PrefabAsset: {{fileID: 0}}
'''
chest = alloc()
b[chest] = f'''--- !u!114 &{chest} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: 2992899921198328162, guid: 296eaa6f1547cb74e990055ce1319470, type: 3}}
  m_PrefabInstance: {{fileID: 8000000476}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 676583283aa75a942a115efbb0fdfd2d, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
'''
nav = next(i for i, t in b.items() if 'guid: 24454d6c4aa547239b7ded1b4a9bcc31' in t)
b[nav] += f'  worldChest: {{fileID: {chest}}}\n  worldArrow: {{fileID: {arrow}}}\n'
roots = b.pop('9223372036854775807') + f'  - {{fileID: {arrow}}}\n'
for i in b:
    if i.startswith('880000'): b[i] = re.sub(r'(?m)[ \t]+$', '', b[i])
output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(b.values()) + roots
assert path.read_text(encoding='utf-8-sig') == original, 'Concurrent scene change'
staged = Path('Temp/tutorial-stage-guides.unity'); staged.write_text(output, encoding='utf-8'); staged.replace(path)
print('Authored six stage guides, removed attack background, retired old room labels and connected Hub arrow/chest outline.')
