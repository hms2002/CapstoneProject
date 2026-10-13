# Element direct-hit damage validation

Approved continuation of the failed chat `01a12563-c16d-7992-ba8e-c1b92895d50c` (속성 피해 구현 위치 찾기). Direct basic/skill hits add four final-stat HP channels with the same final multiplier and critical result as their base hit. Existing gauge, shock, burn and puddle mechanisms stay in their existing paths.

- Unity 6000.4.2f1 compiled/imported the project and passed **12/12** new cases in [playmode-results.xml](playmode-results.xml), with zero failed/skipped cases. [Unity log](unity-tests.log).
- **4/4** existing player-popup/Crimson formula cases passed in [preservation-results.xml](preservation-results.xml), with zero failed/skipped cases. [Unity log](unity-preservation.log).
- MSBuild `Editor.csproj` and `ProceduralPlayModeTests.csproj` exited 0. Generated projects already include every changed C# source file; no generated projects were edited. [Editor log](msbuild-editor.log), [test assembly log](msbuild-tests.log). Existing obsolete API/unassigned-field warnings remain.
- Static review checked direct-hit callers, payload cloning, namespaces/assemblies, explicit nonserialized metadata, unchanged serialized schemas, and scoped whitespace/Markdown links.

Run commands from the repository root:

```powershell
unity test . --mode PlayMode --filter ElementHitDamagePlayModeTests --output Docs/SessionLogs/2026-10-10-element-hits/playmode-results.xml --timeout 300 --format json -- -nographics -logFile Library/CodexElementHits/unity-tests.log
unity test . --mode PlayMode --filter 'PlayerPopupColors_OverrideDamageStylesButPreserveOtherTargets|CrimsonDamage_' --output Docs/SessionLogs/2026-10-10-element-hits/preservation-results.xml --timeout 300 --format json -- -nographics -logFile Library/CodexElementHits/unity-preservation.log
```

New coverage: creation-time final-stat/critical capture; elemental-only hits; one-hit and absorb shields; invulnerability; lethal HP clamping and proportional popup totals; one outgoing/incoming modifier application and gameplay event flow; default-null legacy exclusions; all four default element colors with critical styles; exact fallback suppression during synchronous target auto-heal.

Limitations: no interactive all-weapon playtest or Player build. Integer popup formatting still rounds each component upward, so displayed integer sums can exceed fractional raw HP loss. After the first NUnit result was written, unrelated `GameSettingsService.SelectLanguageLocale` raised an Addressables invalid-operation-handle exception during shutdown; this was outside the selected cases and those source hunks were not changed. No Console-clean shutdown claim is made. Already-dead target hit/gauge/kill-event handling remains Suggested Later.

See [the session record](../2026-10-10.md) for file-by-file changes and [the flow map](../../StructureMemory/ElementDirectHitDamage.md) for current ownership. Doc Impact: SessionLog + StructureMemory + RefactorBacklog + DecisionLog. This task did not update ErrorLog or Presentation HTML; no Presentation stale candidate was identified. Architecture/Contracts promotion remains a separately approved future documentation decision.
