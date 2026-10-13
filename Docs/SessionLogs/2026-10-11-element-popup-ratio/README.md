# Element popup grouping and damage-ratio sizing

The user approved the preview rule: combine a primary hit with additional HP damage of the same element, show different elements separately at smaller sizes based on their damage ratio, and retain the game's existing popup position allocator. This continuation builds on the [direct-hit implementation](../2026-10-10-element-hits/README.md).

## Changed files

- [CombatDamageAction.cs](../../../Assets/_Project/Runtime/Core/Combat/CombatDamageAction.cs): groups equal element tags within one hit, including the tagged primary; attributes actual HP loss to the groups and selects a full-size primary. An element-only hit uses the largest group. No additional effect application, stat lookup or gauge update is introduced.
- [DamagePopupRequest.cs](../../../Assets/_Project/Runtime/Core/Presentation/DamagePopupRequest.cs): carries readonly runtime `SizeScale`, default 1, through the existing request/factory. No serialized schema or enum change.
- [DamagePopupFormatProfileSO.cs](../../../Assets/_Project/Runtime/UI/Combat/DamagePopup/DamagePopupFormatProfileSO.cs) and [DamagePopupService.cs](../../../Assets/_Project/Runtime/UI/Combat/DamagePopup/DamagePopupService.cs): apply scale to start/end size in the authored profile and code fallback. Color, critical styles, player damage red, motion, lifetime, pooling and placement stay in their existing owners.
- [CombatPresentationRegressionPlayModeTests.cs](../../../Assets/_Project/Tests/PlayMode/Procedural/CombatPresentationRegressionPlayModeTests.cs): adds grouped-primary, duplicate-tag, element-only primary, shield/clamp and critical coverage; checks scale/color alongside existing single-hit and gauge behavior.
- [ElementDirectHitDamage.md](../../StructureMemory/ElementDirectHitDamage.md): updates the current projection rules and removes the obsolete separate-Crimson-Fire-popup pitfall. [DecisionLog](../../DecisionLog.md) records the approved grouping/size/placement boundary; the dated SessionLog records this continuation.

Secondary size factor: `0.42 + 0.43 * sqrt(clamp01(secondary weight / merged primary weight))`. This preserves a readable lower bound and caps secondary scale at 85% of the selected style. Separate attacks, DoT ticks and gauge effects are not grouped across time. Source channel arrays are not modified.

## Verification

- MSBuild `Editor.csproj`: exit 0; runtime/Core/UI source included.
- MSBuild `ProceduralPlayModeTests.csproj`: exit 0; the changed test file is included in the generated project. Generated project files were not edited.
- Unity 6000.4.2f1 compiled/imported and passed **25/25** selected Play Mode cases, CLI exit 0, with zero failed/skipped cases. [NUnit results](playmode-results.xml): elemental hit/group/size cases 18, existing player popup color 1, Crimson formula preservation 3, existing popup pooling/lifecycle 3.
- SHA256 comparison of 253 protected gauge/effect/popup assets, prefab/meta files, Packages and ProjectSettings files: changed 0. [Validation summary](validation-summary.json) records the counts and final source fingerprints.
- Read-only independent review and focused whitespace/source checks found no actionable issue. Existing assembly directions, nonserialized request metadata and prefab-facing references are preserved.

The local build and Unity logs are under `Library/CodexElementPopupRatio/` (`msbuild-editor.log`, `msbuild-tests.log`, `unity-tests.log`). Existing obsolete/unassigned-field warnings remain. The focused Unity command is:

```powershell
unity test . --editor-version 6000.4.2f1 --mode PlayMode --filter 'ElementHitDamagePlayModeTests|PlayerPopupColors_OverrideDamageStylesButPreserveOtherTargets|CrimsonDamage_|FramePerformancePopupPlayModeTests' --output Library/CodexElementPopupRatio/playmode-results.xml --timeout 300 --format json -- -nographics -logFile Library/CodexElementPopupRatio/unity-tests.log
```

## Limitations and documentation impact

No interactive all-weapon playtest or Player build is claimed. Integer formatting still rounds each popup independently, as before. Popup locations use the existing allocator rather than the preview layout. Existing gauge, burn, shock and hazard mechanisms keep their current paths; separately authorized changes from other chats were preserved.

Doc Impact: SessionLog + StructureMemory + DecisionLog. ErrorLog was not updated. Architecture/Contracts and Presentation HTML were not changed; no Presentation stale candidate was identified for this presentation-only grouping continuation.
