---
status: active
authority: reference
category: overview
last_reviewed: 2026-05-16
---

# Document Inventory

This inventory records the role assigned to each project Markdown document during the Codex + Obsidian memory-system setup.

## Overview

| Document | Role |
| --- | --- |
| `README.md` | Primary documentation router |
| `CurrentTask.md` | Active task scope and done criteria |
| `DecisionLog.md` | Durable project decisions |
| `ErrorLog.md` | Recurring mistakes and prevention rules |
| `Overview/document-inventory.md` | Documentation inventory and role map |
| `Overview/game-overview.md` | Project identity and high-level game structure |
| `Overview/current-project-context.md` | Current project scope and recent context |

## Architecture

| Document | Role |
| --- | --- |
| `Architecture/BossEncounterArchitecture.md` | Boss FSM and encounter flow |
| `Architecture/CombatArchitecture.md` | Combat damage and hit flow |
| `Architecture/DialogueArchitecture.md` | Dialogue, NPC, and affinity systems |
| `Architecture/GameplayAbilityWeaponArchitecture.md` | Weapon and GAS boundary |
| `Architecture/GameplayStatusArchitecture.md` | Player status runtime and HUD projection |
| `Architecture/GameplayBuffDebuffArchitecture.md` | Buff/debuff structure |
| `Architecture/GameplayDebuffApplicationArchitecture.md` | Debuff application path |
| `Architecture/RuntimeSaveArchitecture.md` | Runtime state save/restore |
| `Architecture/LoadingScopes.md` | Loading scope policy |

## Contracts

| Document | Role |
| --- | --- |
| `Contracts/WeaponCleanupContract.md` | Weapon cleanup rules |
| `Contracts/MobCleanupContract.md` | General mob cleanup rules |
| `Contracts/PresentationAuthoringContract.md` | Presentation authoring rules |
| `Contracts/display-presentation-rules.md` | Display and presentation rules |

## Guides

| Document | Role |
| --- | --- |
| `Guides/GeneralMobFSMAuthoringGuide.md` | General mob FSM authoring checklist |
| `Guides/DualWeaponPatternGuide.md` | Dual weapon authoring pattern |
| `Guides/CodebaseReviewGuidelines.md` | Review and refactor guidance |
| `Guides/playtest-smoke-checklist.md` | Playtest smoke checklist |
| `Guides/ContentAuthoring/README.md` | Repeatable combat-content authoring pipeline hub |
| `Guides/ContentAuthoring/WeaponAuthoringPipeline.md` | Weapon and GAS authoring pipeline |
| `Guides/ContentAuthoring/MobAuthoringPipeline.md` | General mob population, FSM, battle, and death-result authoring pipeline |
| `Guides/ContentAuthoring/BossAuthoringPipeline.md` | Boss encounter, battle, and battle-end authoring pipeline |
| `Guides/ContentAuthoring/RelicAuthoringPipeline.md` | Relic definition, logic, proc, inventory, and loot authoring pipeline |
| `Guides/ContentAuthoring/ConsumableAuthoringPipeline.md` | Consumable definition, use-effect, inventory, and loot authoring pipeline |
| `Guides/ContentAuthoring/LootRewardIntegrationPipeline.md` | Loot, reward, database, chest, boss reward, and world pickup integration pipeline |

## Structure Memory

| Document | Role |
| --- | --- |
| `StructureMemory/README.md` | Structure memory operating rules and index |
| `StructureMemory/ScriptSystemMap.md` | Top-level project script responsibility map |
| `StructureMemory/ScriptSystems/README.md` | Focused script system map index |
| `StructureMemory/ScriptSystems/WeaponAndGASStructure.md` | Weapon, GAS, combat, and player-adjacent runtime script map |
| `StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md` | Boss, mob, spawn, hazard, and enemy cleanup script map |
| `StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md` | Inventory, chest UI, HUD, world drop, and interaction script map |
| `StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md` | Dialogue, NPC feature, affection, merchant, and upgrade script map |
| `StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md` | Scene transition, runtime restore, save data, run timer, and map shortcut script map |
| `StructureMemory/ScriptSystems/LootRewardStructure.md` | Loot, reward, boss reward, pickup, and currency script map |
| `StructureMemory/ScriptSystems/LoadingPresentationStructure.md` | Loading, presentation, global UI, camera, audio, input, and settings script map |
| `StructureMemory/UIFlowInputBlocking.md` | Game-flow UI input blocking structure map |

## Refactor Backlog

| Document | Role |
| --- | --- |
| `RefactorBacklog/README.md` | Refactor backlog operating rules and index |
| `RefactorBacklog/BossDropResponsibilitySplit.md` | BossDrop responsibility split refactor candidate |
| `RefactorBacklog/InventoryTransferResponsibilitySplit.md` | Inventory transfer policy responsibility split candidate |
| `RefactorBacklog/LootRewardPolicyBoundarySplit.md` | Loot roll, reward policy, and delivery boundary split candidate |
| `RefactorBacklog/SceneRunStateBoundarySplit.md` | Scene travel, run session state, runtime restore, and boss battle-end boundary split candidate |
| `RefactorBacklog/SceneRunStateLifecycleOwnershipSplit.md` | Scene/run/save lifecycle and naming ownership follow-up |
| `RefactorBacklog/UpgradeRuntimeBoundarySplit.md` | Upgrade runtime, effect, save, and UI flow boundary split candidate |
| `RefactorBacklog/RunModifierAggregationBoundarySplit.md` | Run modifier aggregation ownership split candidate |
| `RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md` | Runtime-created UI and presentation fallback authoring split candidate |
| `RefactorBacklog/CombatElementBuildUpSourceUnification.md` | Combat elemental build-up source-of-truth refactor candidate |
| `RefactorBacklog/BossHudSpecialCaseSourceSplit.md` | Boss HUD special-case source/adapter split candidate |

## Reviews

| Document | Role |
| --- | --- |
| `Reviews/AIFSMAbilityIntegrationReview.md` | AI/FSM integration review |
| `Reviews/MobAIArchitectureDirectionReview.md` | Mob AI direction review |
| `Reviews/PatternDataOwnershipReview.md` | Pattern data ownership review |
| `Reviews/PersonalizedBTAbilityStructureProposal.md` | Personalized BT proposal |
| `Reviews/PlayerStatusDirectionReview.md` | Player status direction review |
| `Reviews/WeaponGASAssessment.md` | Weapon GAS assessment |
| `Reviews/WeaponRuntimeStateSaveReview.md` | Weapon runtime state save review |

## Notes and Handoffs

| Document | Role |
| --- | --- |
| `Notes/global-services-plan.md` | Global services planning note |
| `Notes/prototype-notes.md` | Prototype note |
| `Notes/system-notes.md` | System note |
| `Handoffs/next-thread-handoff-loading-presentation.md` | Loading/presentation handoff |

## Session Logs

| Document | Role |
| --- | --- |
| `SessionLogs/2026-05-05.md` | Dated task outcome log |
| `SessionLogs/2026-05-06.md` | Dated task outcome log |
| `SessionLogs/2026-05-07.md` | Dated task outcome log |
| `SessionLogs/2026-05-08.md` | Dated task outcome log |
| `SessionLogs/2026-05-10.md` | Dated task outcome log |
| `SessionLogs/2026-05-11.md` | Dated task outcome log |
| `SessionLogs/2026-05-12.md` | Dated task outcome log |
| `SessionLogs/2026-05-14.md` | Dated task outcome log |
| `SessionLogs/2026-05-15.md` | Dated task outcome log |

## Artifacts

| Document | Role |
| --- | --- |
| `DemonKingPattern_NotionHTML/README.md` | Demon King pattern Notion HTML export artifact index |
| `Presentation/index.html` | Human-readable project docs dashboard |
| `Presentation/architecture-overview.html` | Human-readable UML-focused project architecture overview |
| `Presentation/authoring-guide.html` | Human-readable content authoring and balancing handbook |
| `Presentation/refactor-board.html` | Human-readable refactor backlog priority board |
| `Presentation/_shared/docs-style.css` | Shared presentation dashboard styling |
| `Presentation/_shared/docs-data.js` | Thin dashboard metadata and source Markdown links |
| `Presentation/_shared/docs-render.js` | Shared dashboard rendering helpers |
