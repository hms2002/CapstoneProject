# Path Dependency Audit

This document tracks path/string dependencies that must be handled before moving the remaining data, prefab, Resources, scene, and Addressables assets.

## Current Search Counts

Search scope: `Assets/_Project/**/*.cs` after runtime/editor script migration.

| Pattern | Count |
|---|---:|
| `Resources.Load` | 78 |
| `Resources.LoadAll` | 3 |
| `Addressables.LoadAssetAsync` | 1 |
| `SceneManager.LoadScene` | 9 |
| `AssetDatabase.` | 322 |
| `Directory.GetFiles` | 1 |
| `File.ReadAllText` | 7 |
| `Application.dataPath` | 6 |
| hard-coded `Assets/` string paths | 74 |

## High-Risk Groups To Keep In Place For Now

- `Assets/_Project/Resources`: runtime `Resources.Load` and `Resources.LoadAll` callers still depend on these paths.
- `Assets/_Project/Resources`: audio, input, dialogue, cue, and UI runtime/editor defaults still depend on Resources-relative or AssetDatabase paths.
- Legacy `Assets/LeeJunMo/Datas/Loading`: moved to `_Project/Data/SceneFlow/LoadingManifests`; `PrewarmTraceRuntime` still reads the old trace path as legacy input.
- Scene route assets moved to `_Project/Data/SceneFlow/Routes`; validator constants were updated to the new paths.
- Prefab files have moved into `_Project/Prefabs`; map tool, validation, balance, encyclopedia, element gauge, tutorial, and weapon authoring constants were updated to the new real asset paths.
- Item/attribute definition folders with tool constants: balance and tag tools still use exact folder paths for scanning/creation.

## Low-Risk Data Already Moved

- Boss HUD themes, player stat display data, damage formulas, element gauge data, monster spawn sets/profiles, weapon loadouts, item data, attribute/init profile data, non-Resources ability data, run timer config, runtime restore catalog, monster balance data, encyclopedia catalog, dialogue/upgrade/intro data, loot tables, route assets, loading manifests, and editor presentation workbench data were moved with their `.meta` files.
- Audio clips under the old `Assets/Audio` root were moved into `_Project/Audio`. The old `Assets/Audio/...` strings still exist intentionally as Addressables `m_Address` values and matching loading registry address keys.
- Font, material, shader, texture, and physics material roots were moved into `_Project/Art` and `_Project/Data`; editor constants that needed real asset paths were updated.
- Visual scripting graph assets and project rendering settings were moved into `_Project/Data/VisualScripting` and `_Project/Settings`.
- Remaining boss/monster ability data and dialogue audio profiles under old `Assets/Script` were moved into `_Project/Data`; editor tool constants that needed real asset paths were updated.
- Remaining non-Resources/non-Ink dialogue visual data and intro/outro sprites under `Assets/LeeJunMo/Datas` were moved into `_Project`.
- Non-Resources cue prefabs under `Assets/HeoMinSeok/_Project/Data` were moved into `_Project/Prefabs/VFX/Cues`.
- Legacy prefab roots Assets/Prefabs and Assets/HeoMinSeok/_Project/Prefabs were moved into _Project/Prefabs by domain: bosses, monsters, map, items, player, UI, and VFX.
- Root `Assets/Editor` editor scripts were moved into `_Project/Editor/Tools` by use case.
- Root `Assets/Tests` PlayMode tests were moved into `_Project/Tests/PlayMode`.
- Root animation/controller folders were moved into `_Project/Art/Animations`, and the encyclopedia editor tool constants were updated to the new paths.
- Root Assets/Sprites was moved into _Project/Art/Sprites; code/editor constants that needed real sprite asset paths were updated, while Addressables or loading aliases should still be handled only in a dedicated remap pass.
- Validation after the move found `0` missing `.meta` files and `0` orphan `.meta` files under checked data roots.

## Next Safe Refactor Candidates

- Do not move `Resources`-backed data until each runtime/editor loader is converted or intentionally preserved.
- Replace single hard-coded asset paths with `AssetDatabase.FindAssets` by type where practical.
- Keep `Resources` assets in place until each runtime loader is converted to direct serialized references, Addressables, or a stable bootstrap catalog.
- Prefab movement is blocked by loading registry address keys and editor palette constants until those paths are intentionally remapped.
- Addressables address strings that look like old asset paths, including old Audio/Font/Material/Shader/Script/Prefab addresses, should be treated as stable aliases until an Addressables remap pass updates both group entries and loading registries together. Historical `PrewarmTrace*.json` prefab paths are diagnostic records and should be regenerated or remapped separately if the recommendation window needs them.







- Root Assets/TilePallet_Tile was moved into _Project/Art/Tilemaps/TilePallet_Tile; no code/data string path dependencies were found in the checked asset scopes.



- Root Assets/Resources was moved into _Project/Resources; runtime Resources relative paths are preserved, code/editor real paths were updated, and old Assets/Resources/... Addressables/loading aliases remain intentionally unchanged.



- Project-owned nested Resources folders were merged into _Project/Resources; old Assets/HeoMinSeok/.../Resources and Assets/LeeJunMo/Datas/Resources strings now remain only as Addressables/loading aliases. Package Resources under Ink/TextMesh Pro were not moved.


- Root Assets/Scenes was moved into _Project/Scenes; Build Settings and ProjectSettings scene paths were updated. The disabled missing SampleScene entry remains a pre-existing cleanup candidate.

- Project Ink dialogue data was moved from Assets/LeeJunMo/Datas/Inks into _Project/Data/Dialogue/Ink; editor constants were updated, while old Addressables/loading aliases remain intentionally unchanged.

## Current Search Counts (2026-06-06)

Search scope: `Assets/_Project/**/*.cs` after the latest root migration and path cleanup.

| Pattern | Count |
|---|---:|
| `Resources.Load` | 82 |
| `Resources.LoadAll` | 3 |
| `Addressables.LoadAssetAsync` | 1 |
| `SceneManager.LoadScene` | 11 |
| `AssetDatabase.` | 473 |
| `Directory.GetFiles` | 1 |
| `File.ReadAllText` | 7 |
| `Application.dataPath` | 7 |
| hard-coded `Assets/` string paths | 0 |

Notes:

- The remaining `Resources.Load` and `Resources.LoadAll` calls are expected because `_Project/Resources` intentionally preserves Resources-relative loading for now.
- The `AssetDatabase` count is editor-heavy and should be handled by tool-specific audits, not by broad replacement.
- Addressables and loading-registry address aliases that look like old paths are intentionally excluded from this count and should be remapped together only in a dedicated pass.

## Art Sprite Path Cleanup (2026-06-06)

Removed old real-path references for these migrated sprite-art folders:

- `Assets/_Project/Art/Sprites/Characters`
- `Assets/_Project/Art/Sprites/Effects`
- `Assets/_Project/Art/Sprites/Environment`
- `Assets/_Project/Art/Sprites/AttackEffects`
- `Assets/_Project/Art/Sprites/Anim`
- `Assets/_Project/Art/Sprites/Resource_WeaponAndSandBack`

Post-move search count for those old paths in checked `.cs`, `.md`, `.asset`, `.prefab`, and `.unity` scopes: `0`.

## VFX Typed Asset Path Cleanup (2026-06-06)

Moved typed VFX files out of sprite-art folders:

- `Assets/_Project/Art/Sprites/VFX/**/*.anim` -> `Assets/_Project/Art/Animations/VFX/**/*.anim`
- `Assets/_Project/Art/Sprites/VFX/**/*.controller` -> `Assets/_Project/Art/Animations/VFX/**/*.controller`
- `Assets/_Project/Art/Sprites/VFX/**/*.prefab` -> `Assets/_Project/Prefabs/VFX/**/*.prefab`

Post-move search count for old typed VFX asset paths in checked `.cs`, `.md`, `.asset`, `.prefab`, and `.unity` scopes: `0`.
