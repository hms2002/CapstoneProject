# Rook wall-impact stagger — 2026-11-05

- [Runtime change](../../Assets/_Project/Runtime/Features/Monsters/Slime/RookChargeRunner.cs): real body collision or a confirmed forward blocker consumes one impact; charge-owned immunity is removed before adding 50% of the live maximum stagger gauge.
- Direct and near-wall fallback paths share one guard. Cancellation flag and ability token, death, disable and paralysis are respected. Corner/multiple-collider events cannot apply twice in one dash. Bare timeout without a blocker does not apply impact feedback or stagger.
- The authored Rook uses no stagger resistance or regeneration and its existing 3-second Groggy effect. Two fresh impacts trigger it; attacks can make it trigger sooner. Other immunity stacks remain respected.
- [Current structure map](../../Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#rook-wall-impact-stagger-2026-11-05).
- [Session log](../../Docs/SessionLogs/2026-11-05-CubeHood-rook-wall-stagger.md).
- [Gameplay MSBuild log](msbuild.log). Source/reference/diff inspection performed. No automated tests, Unity import/Editor compilation, manual Play Mode or Player build were run.

Manual follow-up: two separate wall crashes, corner collisions, closed/open doors, no-wall timeout, removed blocker, cancellation/death/disable, terminal-frame paralysis and existing player-contact behavior.
