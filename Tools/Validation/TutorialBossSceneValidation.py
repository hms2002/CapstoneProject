"""Read-only serialized checks for playable tutorial boss and its original defeat/victory routes."""
from pathlib import Path
import re
import subprocess

path = 'Assets/_Project/Scenes/DarkLord_Tutorial.unity'
text = Path(path).read_text(encoding='utf-8-sig')
original = subprocess.check_output(['git', 'show', 'HEAD:' + path]).decode('utf-8-sig')

def blocks(text):
    rows = list(re.finditer(r'(?ms)^--- !u!(\d+) &(\d+).*?(?=^--- !u!|\Z)', text))
    result = {m[2]: m[0] for m in rows}
    assert len(result) == len(rows), 'Duplicate scene IDs'
    assert all(i == '0' or i in result for i in re.findall(r'\{fileID: (\d+)\}', text)), 'Dangling scene reference'
    return result

scene = blocks(text)
old = blocks(original)
def by_script(data, guid):
    return next(v for v in data.values() if f'guid: {guid}' in v)
def field(block, name):
    return re.search(rf'(?m)^  {name}:.*(?:\n    [^\n]*)*', block)[0]

sequence_guid = 'e7e68a07327e46debc435bfbd7375af9'
seq, previous = by_script(scene, sequence_guid), by_script(old, sequence_guid)
for key in ['tutorialBossNpcData', 'firstDialogueInk', 'firstDialogueStartPath', 'secondDialogueInk',
            'secondDialogueStartPath', 'showFakeGameOver', 'fakeGameOverCauseName', 'fakeGameOverMessageText',
            'fakeGameOverLocationName', 'hideFakeGameOverTimeText', 'fakeGameOverButtonLabel',
            'returnSceneName', 'useSceneTransitionService']:
    assert field(seq, key) == field(previous, key), f'Original dialogue/defeat routing changed: {key}'
assert 'combatBoss: {fileID: 669182789161305112}' in seq
assert 'combatPlayerHealth: {fileID: 11400000, guid: 3ff045849daafe84d97370c69cd17747' in seq
assert 'laserPresentation: {fileID: 0}' in seq and 'presentationHpView: {fileID: 0}' in seq
assert 'm_Enabled: 0' in scene['1606454207'], 'Scripted laser still enabled'
assert 'startCombatOnStart: 0' in scene['669182789161305112'], 'Boss can attack during opening dialogue'
assert 'guid: d1a4000000000000000000000000000c' in scene['6551344035397668021'], 'Boss animator is not DemonKing'
assert 'm_IsActive: 0' in scene['419935125'], 'Standalone EventSystem competes with GlobalUIRoot'
assert '1785454309' not in scene and '1785454310' not in scene, 'Normal boss dialogue competes with tutorial dialogue'
components = re.findall(r'component: \{fileID: (\d+)\}', scene['4104743717066173154'])
for required in ['669182789161305112', '8462024869469780234', '5277792942332035698',
                 '8412503969807124710', '9045646977701484068', '5477811148102681897']:
    assert required in components, f'Missing combat/ending component: {required}'
ending = scene['9045646977701484068']
assert 'guid: 37a1fde2603f4fd1b9c3206aa53169d1' in ending, 'Original boss victory dialogue missing'
assert 'outroPlayer: {fileID: 2012986334}' in ending
assert 'sequence: {fileID: 11400000, guid: 54e5f53ed338f354db8ebcec3fdda0bc' in scene['2012986334']
assert 'view: {fileID: 411425120}' in scene['2012986334']
assert '2107525437' in scene and 'guid: 5e15651ae66572d459df089d6e5dc0b4' in scene['2107525437']
# Every restored ordinary transform must be reachable through its parent or scene roots.
roots = scene['9223372036854775807']
for id, block in scene.items():
    if not re.match(r'--- !u!(4|224) ', block) or ' stripped' in block.splitlines()[0]: continue
    parent = re.search(r'm_Father: \{fileID: (\d+)\}', block)[1]
    if parent == '0': assert f'{{fileID: {id}}}' in roots, f'Orphan root {id}'
    else: assert f'{{fileID: {id}}}' in scene[parent], f'Orphan child {id}'

prototype = Path('Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity').read_text(encoding='utf-8-sig')
portal = by_script(blocks(prototype), '7339d19fe1a9c3d4c842710e0be44fb4')
assert 'targetSceneName: DarkLord_Tutorial' in portal
assert 'resetPlayerRuntimeStateOnTravel: 0' in portal
build = Path('ProjectSettings/EditorBuildSettings.asset').read_text()
assert '- enabled: 1\n    path: ' + path in build
code_path = 'Assets/_Project/Runtime/Features/Tutorial/TutorialBossEncounterSequence.cs'
code = Path(code_path).read_text(encoding='utf-8-sig')
old_code = subprocess.check_output(['git', 'show', 'HEAD:' + code_path]).decode('utf-8-sig')
def game_over_method(code):
    return code[code.index('    private void ShowFakeGameOver()'):code.index('    private static string ResolveFakeGameOverText')].replace('\r\n', '\n')
assert game_over_method(code) == game_over_method(old_code), 'Existing defeat-screen request changed'
print(f'TUTORIAL_BOSS_SCENE_PASS: {len(scene)} blocks; original dialogues/illustration/defeat request retained; combat restored; victory outro wired; portal destination enabled.')
