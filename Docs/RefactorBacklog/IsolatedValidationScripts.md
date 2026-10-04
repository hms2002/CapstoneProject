# Isolated validation scripts

Status: partially-refactored
Reviewed: 2026-09-28

## Problem and decision

Tools/Validation accumulated per-task Unity probes. They live outside Assets, are not normal NUnit tests, and many require temporary projects, copied assemblies, asset imports or stubs. No common fixture builder/runner was found in Tools or project Editor scripts. Small size alone is not grounds for deleting a test: camera-query and boss-HUD lifecycle checks still cover real regressions.

This cleanup removes completed one-off experiments, not production fixes or the existing Assets/_Project/Tests suites. 46 C# files / 6,440 lines became 43 files / 6,031 lines (398 retired lines plus 11 recently added, unexecuted spotlight-check lines). Retained means useful coverage, not currently validated or ready to run.

## Retired probes

Sources can be inspected with `git show b3ff9ad5a72945c77a35d16b4972f5dd72caba03:Tools/Validation/<name>.cs`. Historical SessionLogs and ErrorLog mentions describe past evidence, not current execution instructions.

| Removed file | Reason and retained evidence | Coverage limitation |
| --- | --- | --- |
| ApprenticeChargeRenderProbe.cs | Before/after GPU experiment requires Assets/Before. CombatPresentationRegressionPlayModeTests already covers progressive reveal, external masks and aim cleanup/stale tokens; SessionLogs/2026-09-16.md records native results. | The historical pixel-for-pixel world-light comparison is not claimed to be replaced by the regular tests. |
| TutorialGridNormalizationValidation.cs | Completed migration compares temporary GridBefore.unity and GridAfter.unity. Current authored checks remain in TutorialGridNormalizationSceneValidation.py / TutorialUnitGridValidation.py. | Current checks do not reproduce the historical native before/after geometry proof. |
| KillLockIdleParticleRegression.cs | One-off particle authoring and rewritten import fixture, requiring Spark.png, ParticleMaterial.mat and Assets/Fixture; production prefab is already authored. SessionLogs/2026-09-21.md records results. | No automated replacement added. On changes check emission, unlock/relock, view/root disable/re-enable, opened-state restore, nested particle/state-owner/material/sprite bindings for both chest variants. |

## Retained inventory

All remaining files are under Tools/Validation. The table records inspected purpose, not a new execution result. Similar system names are not proof of redundant coverage.

| File | Coverage worth retaining |
| --- | --- |
| AttackLifecycleNativeRegression.cs | real execution, walking intent, no Animator, cancellation, buffer request/start, natural completion, animation tail. |
| AttributeDepletionRegression.cs | tiny residual, threshold boundary, fractional damage, ordinary lethal damage, no duplicate depletion, small nonzero change. |
| BloomTransitionCooldownRegression.cs | Transition cancellation and cooldown behavior. |
| BossFeedbackRegression.cs | launch radius/offset/scaling/wall, circle-cast reflection, final-state re-entry/idempotence, encounter-owned completion. |
| BuffyWorkoutPresentationRegression.cs | all three rewards, popup text/colors, intro guidance, dust completion, 70% RGB/alpha, reentry, repeat rejection, failure and disable cleanup. |
| CameraFocusQueryRegression.cs | repeated camera/brain/legacy queries preserve cinematic target and priority, including absent optional legacy follower. |
| ChainFixedStepRegression.cs | Fixed-step stability, variable frame times, motion limits and reset behavior. |
| ChestAcquisitionPresentationRegression.cs | paused playback, ordered full-slot travel, one/two/empty weapon targets, centered drops and symmetric reflow, sizing, close gate, duplicate confirm, completion timing and interruption cleanup. |
| ChestBlockedSlotRegression.cs | capacity attribution, no preview writes, recovery, missing destination, red border and restoration. |
| ChestSelectionRecoveryRegression.cs | one/two weapon replacement, empty slot, duplicate and drop failure safety, mixed acquisition failure, retained relic levels, reroll budget, selected grid parenting and panel visibility. |
| ChestStatPreviewRegression.cs | multiple assertions |
| CrimsonProjectileNativeRegression.cs | wall grazing, trigger bypass, fast wall hit, wide enemy hit, ordered wall/enemy hits, duplicate guard, initial overlap. |
| CrimsonRelicAuthoredRegression.cs | real Q asset activation, spawned visible prefab, independent damage/wall shapes, side-wall grazing, piercing, front-wall stop, no wall explosion damage, owner cleanup. |
| DeathItemScatterRegression.cs | all containers preserved, relic levels, copies locked/unregistered, cleanup, empty inventory. |
| DisplaySettingsRegression.cs | Behavioral assertions and lifecycle coverage; fixture setup required. |
| DragIconCoordinateRegression.cs | overlay, camera canvas, offset viewport, scaled parent, runtime render-mode switch, visible tracking, hide/cancel. |
| DragonJumpBreathRegression.cs | jump once before three breaths, absorb without jump, cancelled jump stops chain and cleans height/warning/facing. |
| DungeonStageCompositionNativeRegression.cs | Stage/theme/seed generation combinations. |
| EgoSwordRecallRegression.cs | preparation, lifted and moving geometry, contact/arrival radii, timeout, idempotent cleanup and disable. |
| KeyBindingRegression.cs | multiple assertions; defaults, fixed keys, swap sweep, persistence, migration, minimap input. |
| LightningRelicPlacementRegression.cs | exact enemy point, nearest valid tile, six distinct reserved marks with spacing, wall exclusion and no-ground rejection. |
| MouseCursorApiRegression.cs | unreadable crop/orientation/alpha/color, scaling/PPU/hotspot/cache, all theme sprites, domains, hide/display/focus, disable/enable |
| OverheadPromptLayoutRegression.cs | 48 visibility/order combinations, scaled heights, mixed pivots, immediate gap collapse and destroyed-owner cleanup. |
| PortalGuidanceRegression.cs | Hub equipment gate; Grand Hall completion/camera gate; five directions; upright icon; player following; pause, interaction, portal disable, cleanup, rebind and slot replacement. |
| PrototypeChestNavigationRegression.cs | owner isolation; normal selection; only first-slot right click; selection unlock; keyboard/stack restoration; cancellation; tutorial stage completion. |
| PrototypeTutorialNativeRegression.cs | per-hit increment, duplicate-hit guard, post-hit cancellation, nine hits advance, gunner single-shot/retreat. |
| PrototypeTutorialTilemapNativeValidation.cs | layers, walkable ground, static merged wall; {composite.pathCount} paths; paint/erase collision refresh. |
| PrototypeTutorialWorldGuideRegression.cs | stage gating, fade/slide entrance and exit, chest outline/arrow and cleanup. |
| SashaAffectionRegression.cs | Ink branches; failure/repeat continuation; per-NPC hub/run allowance; session round-trip; negative change; run-end reset; pending/committed reward; non-stacking prices; legacy gold upgrade isolation. |
| SlimeCornerAttackRegression.cs | 16 native physics cases; four corners, blocked paths, selected direction cache. |
| SlimeNavigationRecoveryRegression.cs | unsnapped/same-tile/ground, reachable alternative, stationary overlap and simulation exclusion, stall/recovery. |
| StatusTelegraphRecoveryRegression.cs | world geometry and borders invariant under scale/reflection/shear; wall endpoint correct; progress projection/count/percent/reset and slot reuse passed. |
| TutorialBossCombatRegression.cs | real HP subscription; active boss combat; player unlock; lethal defeat cancellation; single outcome; victory handoff; normal death routing restoration; cancellation cleanup. |
| TutorialBossHudRegression.cs | deferred inactive entrance and reset on disable. |
| TutorialDodgeDoorRegression.cs | entry/side gate, close collision, one shot/no recycle, restart reset. |
| TutorialGuidanceRecoveryRegression.cs | chest ownership; dash recovery; sequential glyphs; animated inventory open/close/return; moving spotlight; cancellation; other owners preserved. |
| TutorialOpeningRegression.cs | landing/contact wake, owner cancellation, portal recovery, physics/pose restore, normalized-start input handoff, external camera ownership. |
| TutorialPortalDoorRegression.cs | chest/inventory do not unlock; potion completion opens obstacle; idempotence; restart. |
| TutorialVolleyRegression.cs | continuous camera travel, exact arrival, faster return, cancellation restore and prompt release. |
| WallSlideNativeRegression.cs | 12 native physics cases; contact/near-contact, tangent, corner, head-on, escape, legacy policy. |
| WeaponDescriptionNativeRegression.cs | multiple assertions; native input, simple/detail copy, all variant rows, lifecycle, non-weapon isolation. |
| WeaponRelicsNativeRegression.cs | independent gates, magazine acquisition/cap/reload/removal/reacquisition, conditional parallel policy, charge cleanup, 75% lava speed, piercing, wall occlusion and duplicate guard. |
| WeaponSwapPoseRegression.cs | animated pose, repeated cached swaps, nonzero authored offsets/rotation/scale, same-prefab reactivation and clear. |

## Target and refactor trigger

When one of these systems changes next, first reuse a relevant Assets/_Project/Tests test. Keep an isolated probe only if its exact fixture/dependencies and command can be reproduced and it checks a meaningful failure. Move valuable checks into an existing suite when assembly/setup permits; do not introduce another C# helper merely to reduce file size. No broad test migration or new asmdef is authorized by this cleanup.

Risks: isolated sources may drift from private members; native/GPU/player checks require Unity and may have destructive fixture-only setup. Do not copy every probe into the main project or claim a pass from source inspection. Reconstruct setup and run selected checks before relying on them.

Related: ../SessionLogs/2026-09-28.md, ../StructureMemory/ScriptSystems/TutorialSupportStructure.md, ../StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md.
