# Project Structure Migration Status

This document records the current migration state for the `_Project` hierarchy.

## Completed In This Pass

The target `_Project` folder tree from the v2 plan was created with Unity `.meta` files.

Runtime infrastructure scripts moved into `_Project`:

- `Runtime/Infrastructure/Loading`
- `Runtime/Infrastructure/Input`
- `Runtime/Infrastructure/Input/MouseCursor`
- `Runtime/Infrastructure/Audio`
- `Runtime/Infrastructure/Camera`
- `Runtime/Infrastructure/Save`
- `Runtime/Infrastructure/SceneFlow`
- `Runtime/Infrastructure/ServiceLifetime`
- `Runtime/Infrastructure/Rendering/Telegraph`
- `Runtime/Infrastructure/Rendering/Debug`
- `Runtime/Infrastructure/Rendering/Effects`
- `Runtime/Infrastructure/Rendering/Feedback`
- `Runtime/Infrastructure/Rendering/Lighting`
- `Runtime/Infrastructure/Rendering/Tilemaps`
- `Runtime/Infrastructure/Time`
- `Runtime/Infrastructure/VFX`
- `Runtime/Infrastructure/VFX/AbilityPresentation`
- `Runtime/Infrastructure/VFX/Afterimages`
- `Runtime/Infrastructure/VFX/Cues`
- `Runtime/Infrastructure/VFX/Presentation`

Editor tools moved into `_Project`:

- `Editor/Build`
- `Editor/Build/Loading`
- `Editor/Inspectors`
- `Editor/MapTool`
- `Editor/Tools/Abilities`
- `Editor/Tools/Audio`
- `Editor/Tools/Balance`
- `Editor/Tools/Bosses/DemonKing`
- `Editor/Tools/Data`
- `Editor/Tools/Debug`
- `Editor/Tools/Dialogue`
- `Editor/Tools/Enemies`
- `Editor/Tools/Elements`
- `Editor/Tools/Encyclopedia`
- `Editor/Tools/Input`
- `Editor/Tools/Monsters`
- `Editor/Tools/Player`
- `Editor/Tools/Presentation`
- `Editor/Tools/Rendering`
- `Editor/Tools/Title`
- `Editor/Tools/Tutorial`
- `Editor/Tools/Upgrade`
- `Editor/Tools/Validation`
- `Editor/Tools/VFX`
- `Editor/Tools/VFX/GameplayCues`
- `Editor/Tools/Weapons`
- `Editor/Tools/Weapons/Abilities`

Core gameplay systems moved into `_Project`:

- `Runtime/Core/Abilities`
- `Runtime/Core/Attributes`
- `Runtime/Core/Combat`
- `Runtime/Core/Combat/Movement`
- `Runtime/Core/Cues`
- `Runtime/Core/Effects`
- `Runtime/Core/Elements`
- `Runtime/Core/Interaction`
- `Runtime/Core/Tags`
- `Runtime/Core/Inventory`

Feature-specific runtime files moved into `_Project`:

- `Runtime/Features/Player/Abilities`
- `Runtime/Features/Player/Abilities/Data`
- `Runtime/Features/Player/Animation`
- `Runtime/Features/Player/Combat`
- `Runtime/Features/Player/Health`
- `Runtime/Features/Player/Input`
- `Runtime/Features/Player/Interaction`
- `Runtime/Features/Player/Inventory`
- `Runtime/Features/Player/Presentation`
- `Runtime/Features/Player/Scene`
- `Runtime/Features/Monsters/Common/Combat`
- `Runtime/Features/Monsters/Common`
- `Runtime/Features/Monsters/Common/FSM`
- `Runtime/Features/Monsters/Common/Abilities`
- `Runtime/Features/Monsters/Common/CommonCorridor`
- `Runtime/Features/Monsters/Common/BeerMonster`
- `Runtime/Features/Monsters/Common/CorridorCandlestickMonster`
- `Runtime/Features/Monsters/Common/DeadsSkeleton`
- `Runtime/Features/Monsters/Common/TreasureMonster`
- `Runtime/Features/Monsters/Slime`
- `Runtime/Features/Monsters/Slime/Abilities`
- `Runtime/Features/Monsters/Shadow`
- `Runtime/Features/Monsters/Shadow/Abilities`
- `Runtime/Features/Monsters/Shadow/ShadowMonster`
- `Runtime/Features/Monsters/Shadow/ShadowServant`
- `Runtime/Features/Monsters/Shadow/StrangeCandlestick`
- `Runtime/Features/Monsters/Spawning`
- `Runtime/Features/Items`
- `Runtime/Features/Items/Consumables`
- `Runtime/Features/Items/Relics`
- `Runtime/Features/Items/Relics/VFX`
- `Runtime/Features/Items/Weapons`
- `Runtime/Features/Items/Weapons/Inventory`
- `Runtime/Features/Items/Display`
- `Runtime/Features/Loot`
- `Runtime/Features/Loot/Chests`
- `Runtime/Features/Loot/Chests/LockedChest`
- `Runtime/Features/Loot/WorldDrops`
- `Runtime/Features/Map/Construction`
- `Runtime/Features/Map/Gimmicks`
- `Runtime/Features/Map/Gimmicks/SlimeCorridor`
- `Runtime/Features/Map/Portals`
- `Runtime/Features/Map/Shortcuts`
- `Runtime/Features/Map/Puddles`
- `Runtime/Features/Bosses/Common/Rewards`
- `Runtime/Features/Bosses/Common/Behavior`
- `Runtime/Features/Bosses/Common/FSM`
- `Runtime/Features/Bosses/Common/FSM/Configs`
- `Runtime/Features/Bosses/Common/FSM/Core`
- `Runtime/Features/Bosses/Common/FSM/States`
- `Runtime/Features/Bosses/DragonBoss`
- `Runtime/Features/Bosses/ShadowBoss`
- `Runtime/Features/Bosses/SlimeQueen`
- `Runtime/Features/Bosses/DemonKing`
- `Runtime/Features/Bosses/DemonKing/Abilities`
- `Runtime/Features/Bosses/DemonKing/Actors`
- `Runtime/Features/Cheats`
- `Runtime/Features/Enemies/Common`
- `Runtime/Features/Dialogue/MiYeonSi`
- `Runtime/Features/Dialogue`
- `Runtime/Features/Dialogue/Affection`
- `Runtime/Features/Dialogue/NPC`
- `Runtime/Features/Dialogue/RunSpecial`
- `Runtime/Features/Encyclopedia`
- `Runtime/Features/Player/Status`
- `Runtime/Features/Progression/RunModifiers`
- `Runtime/Features/Progression/RunTimer`
- `Runtime/Features/Progression/Upgrades`
- `Runtime/Features/Progression/Ending`
- `Runtime/Features/Tutorial/Training`
- `Runtime/Features/Tutorial`

UI runtime files moved into `_Project`:

- `Runtime/UI/Title`
- `Runtime/UI/Chest`
- `Runtime/UI/Combat/ElementGauge`
- `Runtime/UI/Combat/DamagePopup`
- `Runtime/UI/Common`
- `Runtime/UI/Debug`
- `Runtime/UI/HUD`
- `Runtime/UI/HUD/RunTimer`
- `Runtime/UI/HUD/WorldPrompt`
- `Runtime/UI/Inventory`
- `Runtime/UI/Settings`
- `Runtime/UI/Dialogue/SpeechBubble`
- `Runtime/UI/Dialogue`
- `Runtime/UI/Encyclopedia`
- `Runtime/UI/Popup`
- `Runtime/UI/Upgrade`

The moved files kept their `.cs.meta` files with them so Unity MonoScript GUIDs remain stable.

Data assets moved into `_Project` in this pass:

- `Data/Monsters/HudThemes`: boss HUD health bar themes.
- `Data/UI/StatDisplays`: player stat panel and stat display entries.
- `Data/Attributes/Formulas`: damage formula/profile assets.
- `Data/Attributes/ElementGauges`: element gauge definitions, catalog, and buildup profile.
- `Data/Monsters/SpawnSets`: stage monster set assets.
- `Data/Monsters/SpawnProfiles`: room monster spawn profile assets.
- `Data/Items/Weapons/Loadouts`: weapon ability loadout/strategy assets.
- `Data/Items`: consumable, relic, weapon definition, weapon strategy, weapon logic data, weapon display visual assets.
- `Data/Attributes`: attribute definitions and init profiles, excluding Resources-backed tag/cue assets.
- `Data/Abilities`: ability definitions, strategies, effects, cues, and common logic data, excluding Resources-backed tag/cue assets.
- `Data/Progression/RunTimer`: run timer configuration.
- `Data/SceneFlow/RuntimeRestore`: player runtime restore catalog.
- `Data/Monsters/Balance`: remaining monster data assets.
- `Data/Encyclopedia`: encyclopedia catalog.
- `Data/Dialogue`: affection, merchant, NPC, dialogue theme, run NPC, and speech data assets.
- `Data/Progression/Upgrades`: upgrade database and upgrade effect assets.
- `Data/Dialogue/IntroAndOutro`: intro and outro sequence assets.
- `Data/Loot/Tables`: grave and stage loot table assets.
- `Data/Items/ItemDatabase.asset`: central item database asset.
- `Data/SceneFlow/Routes`: run route catalog and corridor/boss route set assets.
- `Data/SceneFlow/LoadingManifests`: load manifests, loading registry, bootstrap config, and prewarm trace json files.
- `Editor/Data/Presentation`: editor presentation workbench profile.

Audio assets moved into `_Project` in this pass:

- `Audio/BGM`
- `Audio/Bosses/BossPattern`
- `Audio/Monsters/CorridorMonsterSound`
- `Audio/UI/InUI`
- `Audio/Player/PlayerActing`
- `Audio/Player/Weapon`
- `Audio/Player/Pickups`
- `Audio/Map/InteractableObject`
- `Audio/Combat`
- `Audio/Imported`
- `Audio/Cele`

The old `Assets/Audio/...` strings remain intentionally as Addressables `m_Address` values and matching loading registry address keys. They are stable address aliases, not current file paths, and should be remapped only during an Addressables-specific pass.

PlayMode tests moved into `_Project` in this pass:

- `Tests/PlayMode`

Animation assets moved into `_Project` in this pass:

- `Art/Animations/Encyclopedia`
- `Art/Animations/Dialogue/Layouts`
- `Art/Animations/Dialogue/Portraits`
- `Art/Animations/Bosses/DragonBoss`
Sprite assets moved into `_Project` in this pass:

- `Art/Sprites/Anim`
- `Art/Sprites/AttackEffects`
- `Art/Sprites/Characters`
- `Art/Sprites/Effects`
- `Art/Sprites/Environment`
- `Art/Sprites/Items`
- `Art/Sprites/Map/Shortcuts/Statue`
- `Art/Sprites/Map/Gimmicks/DrainPipe`
- `Art/Sprites/Resource_WeaponAndSandBack`
- `Art/Sprites/ThirdParty`
- `Art/Sprites/UI`
- `Art/Sprites/VFX`

The old `Assets/Sprites` root was removed after moving its remaining root-level animation, controller, sprite, UI, weapon, map, and VFX assets. Code/editor constants that used real sprite asset paths were updated to `Assets/_Project/Art/Sprites/...`.
- `Art/Animations/Bosses/ShadowBoss`

Art/support assets moved into `_Project` in this pass:

- `Art/Fonts`
- `Art/Materials`
- `Art/Shaders`
- `Art/Textures`
- `Data/PhysicsMaterials`
- `Data/VisualScripting/Graphs`
- `Settings`

The old `Assets/Font/...`, `Assets/Material/...`, and `Assets/Shader/...` strings remain intentionally where they are Addressables `m_Address` values and matching loading registry address keys.

Remaining data assets under the old `Assets/Script` root moved into `_Project` in this pass:

- `Data/Abilities/Definitions/Bosses`
- `Data/Abilities/Strategies/Bosses`
- `Data/Abilities/Conditions/Bosses`
- `Data/Abilities/TelegraphStyles/Bosses`
- `Data/Abilities/Definitions/Monsters`
- `Data/Abilities/Strategies/Monsters`
- `Data/Abilities/TelegraphStyles/Monsters`
- `Data/Abilities/Effects/Bosses`
- `Data/Attributes/Definitions/Bosses`
- `Data/Bosses/ShadowBoss/BehaviorGraphs`
- `Data/Dialogue/Audio`

The old `Assets/Script/...` strings remain intentionally where they are Addressables `m_Address` values and matching loading registry address keys.

Additional dialogue and intro/outro assets moved into `_Project` in this pass:

- `Data/Dialogue/NPC/DialogueTheme`
- `Data/Dialogue/NPC/SpriteLibrary`
- `Art/Sprites/UI/IntroAndOutro`

Cue prefabs moved out of old data folders into `_Project` in this pass:

- `Prefabs/VFX/Cues`

Additional legacy prefab/script cleanup in this pass:

- `Assets/LeeJunMo/Prefab` moved into `_Project/Prefabs` by domain: camera, UI/dialogue/speech bubble, VFX, map/interactables, and loot.
- `Assets/LeeJunMo/Script` and `Assets/HeoMinSeok/_Project/Scripts` were removed after confirming they no longer contained `.cs` files. Placeholder `readme.txt` templates under the old script root were removed because `_Project` migration documents now describe folder policy.
- `GameplayTagEditor` now regenerates `UGAS_Tags.cs` at `Assets/_Project/Runtime/Core/Tags/UGAS_Tags.cs`.

Validation after this data move:

- Missing asset `.meta` files: `0`.
- Orphan `.meta` files under checked data roots: `0`.
- Old source folders for these moved groups contain no remaining `.asset` files.
- `Assets/HeoMinSeok` was removed after its remaining scene and project-owned Resources assets were moved.
- `Assets/LeeJunMo` was removed after its remaining Resources and Ink data were moved.
- Root `Assets/Audio`, `Assets/Editor`, `Assets/Tests`, `Assets/Animations`, `Assets/Animator`, `Assets/LeeJunMo/Animations`, `Assets/Font`, `Assets/Material`, `Assets/Shader`, `Assets/Textures`, `Assets/PhysicsMat`, `Assets/Graphs`, `Assets/Settings`, `Assets/Script`, `Assets/Prefabs`, `Assets/Sprites`, `Assets/TilePallet_Tile`, `Assets/Resources`, `Assets/Scenes`, `Assets/HeoMinSeok`, `Assets/LeeJunMo`, `Assets/HeoMinSeok/_Project/Prefabs`, `Assets/LeeJunMo/Prefab`, `Assets/LeeJunMo/Script`, and `Assets/HeoMinSeok/_Project/Scripts` were removed after their contents were moved or confirmed empty.

## Explicitly Not Moved Yet

- Addressables settings and group entries. Loading registry/load manifest assets have moved to `_Project/Data/SceneFlow/LoadingManifests`, but address strings are intentionally preserved as stable aliases until a dedicated Addressables remap pass.
- Historical `PrewarmTrace*.json` records may still contain old prefab/resource paths and should be regenerated or remapped separately if the recommendation window needs current paths.
- The disabled `SampleScene.unity` Build Settings entry points to a missing sample scene that was already missing before this move.
- Some moved runtime/editor scripts still contain `Resources.Load`, `SceneManager.LoadScene`, `AssetDatabase`, or hard-coded asset paths. These are tracked as follow-up refactor targets, not remaining file-move blockers.
- Third-party/package-owned folders: `Plugins`, `TextMesh Pro`, `Ink`, and `Unity.VisualScripting.Generated`.
- Local backup/diagnostic folders: `_Recovery`.

## Known Pre-existing Git State

Before this migration pass, these files were already deleted in git status and were not touched:

- `Assets/AddressableAssetsData/link.xml`
- `Assets/AddressableAssetsData/link.xml.meta`

## Next Recommended Migration Step

Recommended next steps:

1. Open Unity and resolve any compile errors caused by stale generated project files or missed editor/runtime folder boundaries.
2. Search and refactor remaining `Resources.Load`, `SceneManager.LoadScene`, `AssetDatabase`, and hard-coded `"Assets/..."` dependencies.
3. Continue with prefabs only after the loading registry, map tool, validation tools, and balance tool prefab path references are updated.
4. Move art, scenes, Resources, and Addressables-related assets only after their string/registry references are audited.

After each domain, run Unity compile and a small smoke test before continuing.

## Validation Notes

`dotnet build Assembly-CSharp.csproj` was attempted after moving loading infrastructure, but the command exited with code `1` without reporting compiler errors in the captured output. Use Unity Editor Console as the authoritative compile check for this migration.










Tilemap and tile palette assets moved into `_Project` in this pass:

- `Art/Tilemaps/TilePallet_Tile`

The old `Assets/TilePallet_Tile` root was removed after moving tile, palette, atlas, and small supporting prefab/sprite assets with their `.meta` files. No hard-coded `TilePallet_Tile` string references were found before or after the move.

Root Resources assets moved into `_Project` in this pass:

- `Resources/DemonKing`
- `Resources/Upgrades`
- root Resources assets such as `DefaultMouseCursorTheme`, `MonsterStageHpScalingSettings`, and `OutlineMaterial`.

The folder remains named `Resources`, now at `Assets/_Project/Resources`, so existing `Resources.Load(...)` relative paths continue to work. Code/editor constants that needed real AssetDatabase paths were updated from `Assets/Resources/...` to `Assets/_Project/Resources/...`. Old `Assets/Resources/...` strings remain only as Addressables address aliases, loading registry address keys, or historical prewarm trace records.

Project-owned nested Resources folders were merged into `_Project` in this pass:

- `Assets/HeoMinSeok/_Project/Data/Abilities/Resources` -> `Assets/_Project/Resources`
- `Assets/HeoMinSeok/_Project/Data/UI/Resources` -> `Assets/_Project/Resources`
- `Assets/LeeJunMo/Datas/Resources` -> `Assets/_Project/Resources`

Only project-owned Resources folders were merged. Package Resources under `Ink` and `TextMesh Pro` remain in place. The old address-like strings under Addressables groups and loading registries remain intentionally as stable aliases.

Scene assets moved into `_Project` in this pass:

- `Scenes`
- `HeoMinSeokScene.unity`

`ProjectSettings/EditorBuildSettings.asset` and `ProjectSettings/ProjectSettings.asset` were updated from `Assets/Scenes/...` to `Assets/_Project/Scenes/...`. The disabled `SampleScene.unity` build entry still points to a missing sample scene, which was already missing before this move.
Ink dialogue data moved into `_Project` in this pass:

- `Data/Dialogue/Ink`

The old `Assets/LeeJunMo/Datas/Inks` root was removed. Editor tool constants were updated to the new real path. Old Ink strings remain only as Addressables `m_Address` values or loading registry address keys.

## Current Validation Snapshot (2026-06-06)

This snapshot supersedes earlier validation notes that mentioned a failed `dotnet build` attempt.

- `dotnet build Assembly-CSharp.csproj`: passed with `0` warnings and `0` errors.
- Missing asset `.meta` files under `Assets`: `0`.
- Orphan `.meta` files under `Assets`: `0`.
- Missing Build Settings scene paths: `0`.
- Old runtime/editor code references to removed roots (`Assets/Scenes`, `Assets/Sprites`, `Assets/TilePallet_Tile`, old project-owned Resources, old Ink data): `0`.
- Remaining root folders are intentional/special: `_Project`, `_Recovery`, `AddressableAssetsData`, `Ink`, `Plugins`, `TextMesh Pro`, `Unity.VisualScripting.Generated`.
- Addressables `m_Address`, loading registry aliases, and historical `PrewarmTrace*.json` records may still contain old path-like strings by design. Treat them as a dedicated remap/regeneration pass, not as file-move blockers.

## Art Sprite Domain Cleanup (2026-06-06)

The old mixed `Art/Sprites` domain folders were normalized toward the target hierarchy:

- `Art/Sprites/Characters/Boss` -> `Art/Sprites/Bosses`
- `Art/Sprites/Characters/Mob` -> `Art/Sprites/Monsters`
- `Art/Sprites/Characters/Npc` and loose NPC sprites -> `Art/Sprites/NPCs`
- `Art/Sprites/Characters/Player` -> `Art/Sprites/Player`
- `Art/Sprites/Environment` -> `Art/Sprites/Map/Environment`
- `Art/Sprites/Effects` -> `Art/Sprites/VFX/Common`
- `Art/Sprites/Effects/Attack` -> `Art/Sprites/VFX/Attack`
- `Art/Sprites/Effects/Elemental` -> `Art/Sprites/VFX/Elemental`
- `Art/Sprites/Effects/WeaponEffect` -> `Art/Sprites/VFX/WeaponEffect`
- `Art/Sprites/Effects/Materials` -> `Art/Materials/VFX`
- `Art/Sprites/AttackEffects` -> `Art/Sprites/VFX/AttackEffects`
- `Art/Sprites/Anim` -> `Art/Animations/UI`
- `Art/Sprites/Resource_WeaponAndSandBack` -> `Art/Sprites/ThirdParty/WeaponAndSandBack/ResourcePackages`

Editor constants that used the old real asset paths were updated to the new locations. Addressables/loading aliases were not remapped in this pass.

Validation after this move:

- Missing asset `.meta` files under `Assets`: `0`.
- Orphan `.meta` files under `Assets`: `0`.
- Old runtime/editor/data references to removed `Art/Sprites/Characters`, `Effects`, `Environment`, `AttackEffects`, `Anim`, and `Resource_WeaponAndSandBack` paths: `0`.
- `dotnet build Assembly-CSharp.csproj`: passed with `0` errors and `232` warnings. The warnings are existing deprecated API / unused field warnings surfaced by recompilation, not missing path errors.

## VFX Typed Asset Cleanup (2026-06-06)

`Art/Sprites/VFX` was cleaned so it now keeps sprite/image source assets only, plus package license text where appropriate.

Moved typed VFX assets by responsibility:

- `.anim` and `.controller` files under `Art/Sprites/VFX` -> `Art/Animations/VFX` while preserving relative subfolders.
- `.prefab` files under `Art/Sprites/VFX` -> `Prefabs/VFX` while preserving relative subfolders.
- `DemonKingPatternVfxAssetBuilder` real asset path constants were updated for the moved HomingMagicBalt animation clips.

Validation after this move:

- `Art/Sprites/VFX` remaining non-meta extension counts: `.png` 34, `.gif` 29, `.aseprite` 3, `.txt` 1.
- Missing asset `.meta` files under `Assets`: `0`.
- Orphan `.meta` files under `Assets`: `0`.
- Old typed asset path references under `Art/Sprites/VFX` for `.anim`, `.controller`, and `.prefab`: `0`.
- `dotnet build Assembly-CSharp.csproj`: passed with `0` errors and `232` existing warnings.


