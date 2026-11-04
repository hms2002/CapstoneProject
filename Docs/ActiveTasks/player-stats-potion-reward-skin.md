---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-31
---

# Approved player stats, potion HUD and reward popup skins

- Mode: Implementation. User approved the latest combined UI direction with `이거로 하자 ㅇㅇ`, after requesting one integration of all reviewed skins.
- Goal: Apply player-stats-potion-ui-v3 and reward-popup-ui-v1 Aseprite artwork to the actual authored uGUI views, then the approved stats v5 grouping/row-height and relic delta layout below.
- Allowed: New sprite sheets/source artwork/import metadata; main GlobalUIRoot prefab's potion HUD and RewardPanel; PlayerStatUI, PlayerStatSectionView, PlayerStatRowView, EffectSlotUI, UnlockSlotUI prefabs; scoped Editor authoring/native validation; narrowly necessary presentation-only fixes; task/session/structure memory records.
- Preserve: Existing13 stat definitions/calculation/selection deltas and inventory/chest reuse; four50x50 potion slots with centered36x36 icon and overlaid key badge, current HUD placement; shared reward queue, granting owners, close callback, dialogue continuation, input locks, hover and opening/closing lifetime; existing fonts/localization bindings, GUIDs and references. Original Basic3+Combat10 grouping is superseded by the approved follow-up below.
- Forbidden: Scene saves, other UI redesign, upgrade tree changes, balance/save/input/settings/asmdef changes, new managers/singletons, overwriting shared legacy artwork or item icons, broad runtime refactors.
- Plan: Import the two approved source sheets with Point filtering and explicit sliced sprite borders. Style the five small reusable prefabs, then the two approved GlobalUIRoot subtrees through native PrefabUtility with scoped preservation. Keep all variable text/icon data separate from the artwork. Compile, capture native views and exercise their existing state/close paths, inspect serialized diffs, then update memory.
- Risks: Nested prefab overrides, TMP/localization callback serialization, layout ownership, long text and multiple rewards. Preserve existing font/material and unrelated nested instance snapshots before each save. The running Unity6000.2 batch belongs to a different LoveTrain project; this checkout uses6000.4.2f1 and must not interfere with that process.
- Done: Approved appearance is present in saved project assets;13 stats and four potion slots still project current state; both reward types retain their completion behavior; native import/compile/render checks and appropriate existing regression tests/builds pass; remaining manual Player verification is reported honestly.
- Verification: Source hashes and slice metadata, native captures of stats/potion/reward variants including locale/multiple-reward layout, existing relevant PlayMode regression suite, MSBuild of generated projects covering changed C#, scoped prefab diff and documentation links.
- References: Existing InventoryAndChestUIStructure and UpgradeUiStructure memory; approved sources in `C:/Users/nadom/.codex/visualizations/2026/10/31/player-stats-potion-ui-v3/` and `C:/Users/nadom/.codex/visualizations/2026/10/31/reward-popup-ui-v1/`.

## Result

- Applied both reviewed sheets and six authorized prefabs. No runtime gameplay/presenter code, scenes, settings or existing GUIDs changed.
- Unity6000.4.2f1 native import/compile and26 GPU captures passed;433 assertions cover5 locales,13 stat rows/live health refresh, four centered potion slots/consumption, reward opening/closing once, multiple rewards and unlock layouts.
- MSBuild Editor.csproj exit0, covering both new Editor files. Selected existing Play Mode suite:73/75 passed, skipped0. Two unrelated failures remain: a Korean-only warning assertion under the saved Chinese locale, and the previously recorded LightningSpearMark exact floating-point color assertion. Neither test uses the newly skinned prefabs. See [evidence](../SessionLogs/2026-10-31-approved-ui-skin/README.md); the suite is not reported as wholly passing.
- Source artwork hashes match approval files; existing fonts/materials/local references and unrelated GlobalUIRoot blocks are preserved. Only newly added Unity empty-scalar trailing spaces were trimmed after native verification.
- Full game dialogue/upgrade queue flow, all resolutions and standalone Player build were not manually exercised. Doc Impact: SessionLog + existing StructureMemory; no new ErrorLog/DecisionLog entry or Presentation HTML stale candidate.

## Approved follow-up — stats v5 and relic delta preview

- Mode: Implementation. User approved integration with `그럼 이제 적용해줘` after reviewing38px rows and green/red relic acquisition deltas.
- Scope: existing Basic/Combat section definition entry arrays (Basic7 / Other6); existing Combat section authored/localized title to Other Stats in5 locales, including its CSV source row; attack-speed definition Percent×100 with0 decimals; shared row prefab height38 and matching existing authoring source; extend existing native validator to exercise real stat projection/acquisition and captures; narrow documentation updates.
- Order: Basic = HP, Attack, MoveSpeed, AttackSpeed, CritChance, CritDamage, Evasion. Other = Fire, Bleed, Poison, Electric, SkillDamage, NormalDamage.
- Preserve: current gameplay calculations and acquisition owners, existing delta rich-text colors/rounding/clearing behavior,136px value column with12–18px auto sizing, width390/row342/gap6, fonts/materials/skin sheets/GUIDs, inventory/chest shared references. No potion/reward/GlobalUIRoot reauthoring, scenes, schemas, settings or runtime manager changes.
- Acceptance:13 rows in7/6 groups; actual default panel390×742 with38px rows;1.0 attack speed→100%, positive/negative percentage deltas and actual acquired values agree; canceled selection clears delta; text fits in5 locales; native compile/render, scoped regression checks, MSBuild and serialized diff review. Full game manual play remains a separate check.

### Follow-up result

- Applied the approved grouping,38px rows, attack-speed percent format and five-locale Other title/CSV source. Runtime calculation and selection projection are unchanged.
- Native Unity562 checks/11 captures and21/21 focused existing Play Mode regressions passed; Editor.csproj MSBuild exit0.49-file before-snapshot audit preserved existing references/metadata and all out-of-scope prefabs. The actual negative-delta fixture uses120→110 because existing movement/normal multiplier floors disallow100→90; no balance change was made.
- [Final evidence and changed files](../SessionLogs/2026-10-31-stat-revision/README.md). Full-scene/manual resolution coverage and standalone Player build remain unexecuted. Doc Impact: SessionLog + existing StructureMemory; no new ErrorLog/DecisionLog entry or Presentation HTML stale candidate.
