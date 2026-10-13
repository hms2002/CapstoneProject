# UI batching implementation and validation, 2026-10-07–08

The user approved the audited UI optimizations with visual and functional preservation as the boundary. The baseline was the current dirty workspace, including concurrent localization/menu work, rather than Git HEAD. Eleven source/prefab files were swapped only under SHA256 checks with the Editor closed; both new atlas assets and their original GUID pairs were held outside Assets during the final baseline run. All optimized hashes and atlas pairs were restored afterwards.

## Applied changes

| Files under Assets/_Project | Change and purpose |
| --- | --- |
| Runtime/UI/HUD/WeaponSkillHudSlotPresenter.cs, WeaponSkillHUD2D.cs, SwapWeaponSkillHUD2D.cs | Share immutable idle ready-shader materials by shader/setup texture; reuse slot-owned active flash instances and release resources at owner cleanup. The old shader's exact alpha is preserved. |
| Runtime/UI/Dialogue/SimpleTextAnimator.cs | Skip forced TMP regeneration/geometry upload for stable effect-free text. Keep actual wave/shake geometry, traversal and RNG behavior. |
| Runtime/UI/Common/MenuButtonHighlightPresentation.cs | Reuse local text bounds until invalidated; continue live transforms, sword placement and motion. Preserve the starting workspace's menu placement changes. |
| Runtime/UI/HUD/Status/StatusHudEntryView.cs, Runtime/UI/Localization/LocalizeTmpFontEvent.cs | Use one authored progress style through the existing TMP fallback-material manager instead of per-entry outline clones, preserving locale atlas overrides, normal-style restoration and reference cleanup. |
| Runtime/UI/Upgrade/UpgradeLakePresentation.cs | Skip identical preset copies using source identity and native material CRC; synchronize changed presets immediately and retain dynamic updates. |
| Runtime/UI/Inventory/Common/PlayerInventoryPanelView.cs, ItemSlotUI.cs | Reuse three exact-size panel-owned groups. Explicitly detach old events/hover/drag/selection before adapter disposal. Changed groups detach immediately before deferred destruction. Generic chest slot disable behavior stays intact. |
| Art/Font/UI_StatusProgress_BlackOutline.mat, Prefabs/UI/status_HUD_EntryView.prefab | New shared progress preset and one approved serialized reference, authored through Unity APIs. |
| Art/SpriteAtlases/UI_LevelRewardCards.spriteatlasv2, UI_MenuChrome.spriteatlasv2 | Four card Sprite subassets and three ordinary menu chrome textures in separate Point/no-mip/sRGB atlases. Original Sprite IDs, rects, pivots, borders and references are preserved. |
| Editor/Build/UiBatchingAssetAuthoring.cs | Idempotent SpriteAtlas V2 prebuild authoring and approved style/prefab authoring. No settings or package changes. |

New Play Mode regression files: ReadyFlashMaterialLifecyclePlayModeTests, UiTextMeshRefreshRegressionPlayModeTests, StatusSharedMaterialPlayModeTests, UpgradeLakeMaterialSynchronizationPlayModeTests, PlayerInventorySlotReusePlayModeTests and UiAtlasPixelEquivalencePlayModeTests. One existing Chest test routine was corrected to inspect the two permanent authored cells and generated selection slots; its loot, return-order and close-cancellation checks remain intact.

New Editor validation tools: UiBatchingValidationRunner, UiBatchingLiveScenarios and UiBatchingStructuralScenarios. They exercise real authored UI/controller/actor ownership in the existing TestScene sandbox, restore temporary state and never save scenes. No new runtime manager, singleton, DDOL owner or UI hierarchy authoring path was added.

## Verified evidence

- Unity 6000.4.2f1 compilation/import and API asset authoring executed successfully. Final generated-project Editor and ProceduralPlayModeTests MSBuild were run; their logs are retained with the report.
- **119/119 native Play Mode cases passed, zero skipped:** ready flash 13, text/menu 24, status material 17, lake 14, inventory reuse 7, atlas 18, existing status performance 16, input cleanup 7 and chest regressions 3. [NUnit results](third-tests.xml).
- Final protected-hash auditing found two prefab sword defaults touched by the first, failed button-only test clone. The corrected fixture now checks remapped ownership while inactive. A narrow Unity API restoration reset the sword; because prefab roundtripping added unrelated nested overrides, the exact starting snapshot was then restored. The whole GlobalUIRoot starting SHA256 matches again, and [the corrected menu regression passed](menu-final-tests.xml) without changing that hash. The temporary recovery entry point was removed. Final scene/settings/UI shader/profile invariants passed.
- Atlas tests compare original **imported** textures against actual packed Images with exact RGBA, white/translucent tint, masks and Simple/Sliced geometry. They do not substitute raw PNG pixels for the previous import pipeline. Five-font status and masked pixel comparisons, actual async locale/pool restoration, material reference destruction, ready shader RGBA/stencil/lifetime and text geometry/RNG comparisons all passed.
- The existing font adapter/package still owns locale/font loading. Live capture waits for GameSettingsService's saved-language initialization, global locale preload, active font callbacks and two completed stable LateUpdates. Final before/after active Graphic count, locale and active TMP font-asset/path multisets matched in all 18 cases.
- Native UI Profiler membership is available in all final cases. Runtime Profiler.enabled alone did not create Editor frame history; the validated runner additionally uses ProfilerDriver recording and UI/UIDetails areas. Material/Graphic inventories are not treated as batch counts.
- Three persistent GameSave slot hashes were unchanged. Representative HUD count was seven heart tokens. Temporary normal weapons, reward-offer state, locale and sandbox actors were cleaned up; MaxHP was never inflated.

## Measurements

Final matched runs: [baseline](baseline-final-a/live-scenario-evidence.json), [optimized](optimized-final-a/live-scenario-evidence.json), [all 18 comparisons](batch-comparison.json). RTX 2060 Max-Q, Direct3D11, Editor Play Mode; native UI preview viewport was **640×480**, despite Player-sized CLI window arguments. The actual Editor UI viewport is the relevant measurement size.

| Real screen | Native batches before → after | Interpretation |
| --- | --- | --- |
| Three reward cards, Korean/English | 12 → 9 | 25% reduction; four card parts across three cards share one atlas page, eliminating three texture transitions. |
| Three reward cards, Japanese/Chinese simplified/traditional | 9 → 6 | 33% reduction; the same atlas reason. Different font/fallback requirements explain the language totals. |
| Pause, traditional Chinese | 7 → 6 | One menu texture transition removed. |
| Key bindings, traditional Chinese | 166 → 165 | One menu texture transition removed; most image/TMP order barriers remain. |
| HUD, inventory and settings | Same totals | No draw-count gain claimed for the source caches or these screens. Settings stays at 39. |

The before/after reward material-transition count is unchanged; actual texture transitions fall by three. Pause/key binding texture transitions fall by one. Identical inventory slots keep the same instance IDs across all five optimized locale openings; the baseline creates fresh IDs at each opening. This proves retained slot reuse, not a measured GC byte reduction.

The warmed native lake operation benchmark, 31 alternating samples of 2048 operations, measured median **17.117 µs → 2.124 µs** for unconditional copy versus unchanged source+CRC, about **87.6% lower operation cost**. [Raw operation means](lake-operation-cost.json). These are operation means, not frame-time percentiles or Player FPS measurements. Stable text tests also confirm zero forced refresh/upload calls, while menu placement/invalidations remain equivalent.

Normal world HUD scenarios: [raw captures](structural-final/world-hud-60-hp-status.json). 0/30/60 visible normal-HP actors produced 0/30/60 native UI batches. Adding HP changes and six actual Electrocute states produced 42/72 batches at 30/60 actors. Viewport visibility, unchanged MaxHP and stable placement guards passed. The Canvas.BuildBatch marker, about 0.12–0.14 ms in these scenarios, is **aggregate across all active canvases**, not an isolated monster HUD CPU or GPU measurement.

## Limits and remaining candidates

This is one final matched pair of Editor captures, with additional diagnostic runs; it does not establish whole-game FPS gains or statistical CPU-frame improvements. Aggregate Canvas.BuildBatch samples vary and are not used to claim a general frame-time reduction. Native vertex fields returned zero even with proven rendering and remain unavailable. Native recorded-canvas previews were nonblank for HUD/inventory; other captures returned no texture and are not pixel equivalence proof. Strict Sprite/status/ready-shader rendering tests supply the relevant pixel evidence.

Earlier attempts were excluded: runtime-only profiler enabled no native frame history; unsynchronized font loading changed fallback batches; a saved-language bootstrap writer could replace the requested locale. A source-swap helper failed before the first baseline was established; its incorrectly labelled Editor launch was stopped and no data used. The final matched runs passed source/GUID, bootstrap/font and native membership gates. Those runs used the same affected hidden sword defaults in both variants; final restoration and the focused menu placement/ownership regression verify the original authored defaults. The full native pair was not repeated after that restoration.

Do not replace the zero-flash shader with UI/Default: actual authored Canvas diagnostics showed alpha 64 versus 128 for a translucent sample, despite similar RGB. Rapid overlapping global locale preloads also need completed test teardown; errors were investigated and test sequencing repaired rather than ignored. The pre-existing scene-exit service-root recreation assertion remains outside this UI task; no clean-shutdown or broad lifecycle fix is claimed.

HP SpriteRenderer/shared Canvas, mask replacement, image/text reorder, UI particle aggregation, minimap/world shared sprites, additional inventory/card-art atlases, outline geometry and typewriter conversion remain conditional candidates. Current costs, ownership/raster risks and required comparisons are recorded in [the narrow backlog](../../RefactorBacklog/UIBatchingStructuralCandidates.md). Current implementations preserve their appearance and behavior.

## Documentation impact

SessionLog, ActiveTask/TaskIndex, [UI rendering memory](../../StructureMemory/UIBatchingOptimization.md), existing [inventory/chest memory](../../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md), RefactorBacklog and the narrow ErrorLog entry were updated. No new DecisionLog entry. Architecture/Contracts and Presentation HTML are unchanged; no matching stale presentation candidate was identified.
