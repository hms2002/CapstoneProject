"""Read source text only: inventory retirement scope and incoming references; never run tests."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
test_names = [
    'UiAtlasPixelEquivalencePlayModeTests', 'UiRenderLayerAuthoredPlayModeTests',
    'UiTextMeshRefreshRegressionPlayModeTests', 'UiRectMaskRegressionPlayModeTests',
    'MonsterHealthBarMeshPlayModeTests', 'PuddleProjectileVisualPlayModeTests',
]
tool_names = [
    'Weapons/CrimsonChargeAssetAuthoring', 'Weapons/CrimsonChargeVisualValidation',
    'UI/PlayerStatsAndRewardUiAuthoring', 'Validation/ApprovedUiSkinValidation',
    'UI/SettingsSkinAuthoring', 'Validation/SettingsSkinValidation',
    'Title/TitleLanguageMenuAuthoring', 'Validation/TitleLanguageMenuValidation',
    'Encyclopedia/EncyclopediaLayoutAuthoring', 'Encyclopedia/EncyclopediaSkinAuthoring',
    'Encyclopedia/EncyclopediaCreatureUIAuthoring',
    'Encyclopedia/EncyclopediaCreatureLocalizationAuthoring',
    'Monsters/MonsterHealthBarMeshAuthoring', 'Validation/MonsterPixelHealthBarValidation',
    'Validation/PuddlePixelArtworkAuthoring', 'Validation/PuddlePixelVisualValidation',
    'UI/UiRenderLayerAuthoring',
]
paths = [ROOT/f'Assets/_Project/Tests/PlayMode/Procedural/{name}.cs' for name in test_names]
paths += [ROOT/f'Assets/_Project/Editor/Tools/{name}.cs' for name in tool_names]
paths += [ROOT/f'Tools/Art/{name}' for name in (
    'make_settings_skin.lua', 'make_title_language_button.lua',
    'prepare_title_language_glyph.lua', 'remove_encyclopedia_book_corners.lua', 'TitleLanguageGlyph.png')]
paths += [ROOT/'Tools/Validation/SettingsSkinScopeValidation.py']
selected = set(paths)
types = {}
for path in paths:
    if path.suffix == '.cs':
        for name in re.findall(r'^(?:(?:public|internal|static|sealed|abstract|partial)\s+)*class\s+(\w+)', path.read_text(encoding='utf-8-sig'), re.M):
            types[name] = str(path.relative_to(ROOT))
incoming = []
for path in (ROOT/'Assets').rglob('*.cs'):
    if path in selected:
        continue
    text = path.read_text(encoding='utf-8-sig', errors='replace')
    hits = [name for name in types if re.search(r'\b'+re.escape(name)+r'\b', text)]
    if hits:
        incoming.append({'path':str(path.relative_to(ROOT)), 'types':hits})

inventory = []
for path in sorted((ROOT/'Assets/_Project/Tests').rglob('*.cs')):
    text = path.read_text(encoding='utf-8-sig')
    plain = len(re.findall(r'\[(?:Test|UnityTest)\]', text))
    cases = len(re.findall(r'\[TestCase\(', text))
    inventory.append({'path':str(path.relative_to(ROOT)), 'plainAttributes':plain, 'testCaseAttributes':cases,
                      'sourceAttributeTotal':plain+cases, 'retireWholeFile':path in selected})
files = []
for path in paths:
    candidates = [path, Path(str(path)+'.meta')] if str(path).startswith(str(ROOT/'Assets')) else [path]
    for p in candidates:
        files.append({'source':str(p.relative_to(ROOT)).replace('\\','/'),
                      'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
                      'bytes':p.stat().st_size})
result = {'scope':'Retire one-off UI/VFX acceptance and fixed migration tools; preserve gameplay/save regression.',
          'inventory':inventory, 'wholeTestFiles':len(test_names), 'editorToolFiles':len(tool_names),
          'files':files, 'incomingReferences':incoming, 'retiredTypes':types}
(HERE/'inventory-plan.json').write_text(json.dumps(result,indent=2,ensure_ascii=False), encoding='utf-8')
print(json.dumps({'testFiles':len(inventory),'wholeTestFiles':len(test_names),'editorToolFiles':len(tool_names),
    'archivedFileCountIncludingMetadata':len(files),'incomingReferences':incoming,
    'retiredWholeFileSourceCases':sum(i['sourceAttributeTotal'] for i in inventory if i['retireWholeFile']),
    'allSourceAttributeCases':sum(i['sourceAttributeTotal'] for i in inventory)},indent=2))
