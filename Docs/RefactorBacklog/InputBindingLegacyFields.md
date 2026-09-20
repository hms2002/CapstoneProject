---
status: proposed
authority: refactor-backlog
category: input
last_reviewed: 2026-09-21
---

# Legacy Reward Open Field

- Status: proposed.
- Current problem: `LevelRewardSessionController.openKey` is a hidden serialized compatibility field; runtime input now uses `InputActionId.LevelRewardOpen`.
- Why retained: preserve existing scene/prefab serialization while implementing key mapping without a broad asset migration.
- Target: remove the unused field and authored `openKey` values in a separately scoped serialization cleanup.
- Risks: old installer tools, scenes or prefab overrides may still write the field. Search and migrate them together; do not restore raw-key runtime behavior.
- Trigger: the next approved level-reward authoring/scene migration, after current input behavior is accepted in Play Mode.
- Related: [Input Bindings](../StructureMemory/InputBindings.md), [Level Progression](../StructureMemory/ScriptSystems/LevelProgressionStructure.md).
