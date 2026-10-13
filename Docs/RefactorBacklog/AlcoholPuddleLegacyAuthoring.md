---
status: proposed
authority: refactor-backlog
category: prefab-authoring-compatibility
last_reviewed: 2026-10-12
---

# Alcohol puddle legacy buff authoring

- Current problem: AlcoholPuddleArea retains hidden serialized
  `alcoholBuffDefinition`, `buffRefreshIntervalSeconds` and `buffDurationSeconds`.
  These belong to the former immediate per-puddle buff and no longer drive play.
- Why it exists: Preserve existing prefab/scene serialization and field names
  while the approved exposure slice moves state to the player. This slice does
  not require reauthoring every puddle instance.
- Target shape: Remove these obsolete fields and their serialized values in an
  explicitly scoped authoring migration, after locating all prefab/scene overrides.
- Risks: Legacy instances/overrides and custom tooling may still retain or edit
  those fields; deleting source fields alone leaves misleading serialized data.
- Refactor trigger: A future puddle-authoring change includes the affected prefab
  and scene migration and validates all override preservation.
- Related documents: [Alcohol exposure map](../StructureMemory/AlcoholExposure.md),
  [approved task](../ActiveTasks/spiri-alcohol-exposure.md).
- Status: proposed.
