---
status: partially-refactored
authority: refactor-backlog
category: refactor-candidate
last_reviewed: 2026-05-16
---

# Upgrade Runtime Boundary Split

## Current Problem

`UpgradeManager` currently owns too many responsibilities for one MonoBehaviour:

- upgrade unlock and purchase flow
- runtime upgrade effect handoff and player reapplication through a helper
- run-start effect timing
- save request/notification execution after unlock/purchase changes through a helper
- upgrade tree UI open/close orchestration
- upgrade tree open fade/input-blocker execution through a helper

The purchase transaction preconditions, currency spend, and rollback-on-spend-failure policy now live in `UpgradePurchaseService`. Purchase success side-effect ordering now lives in `UpgradePurchaseCompletionService`. Runtime effect reapply/run-start/hub-target handoff now lives in `UpgradeRuntimeEffectService`. Run-start effect eligibility/timing checks now live in `UpgradeRunStartEffectPolicy`. Upgrade UI open fade, owned UI push, `GameFlowInputBlocker`, interrupted-open cleanup, and toggle open/close selection now live in `UpgradeUiOpenFlow`. Scene/run/player lifecycle subscriptions and run-start guard state now live in `UpgradeRuntimeLifecycleService`. Unlock-check save request and data-change notification execution now live in `UpgradeProgressSaveService`. Singleton/persistent root adoption now lives in `UpgradeManagerLifetimeService`. These helper types now live in dedicated source files. Purchase completion callback wiring, event surface, and public orchestration still sit in `UpgradeManager`.

The code works, but future upgrade features can easily add more policy, presentation, and run lifecycle behavior to the same class.

## Why It Exists

Upgrade behavior grew from one NPC feature into a progression system with authored nodes, runtime effects, run-start rewards, currency spending, reward display, and a full-screen upgrade UI. Keeping those pieces in `UpgradeManager` made iteration direct, but it now hides several different ownership boundaries behind one entry point.

## Target Shape

Split the current responsibilities into clearer owners when implementation becomes necessary:

- Upgrade progress/purchase policy owns unlock checks, purchased IDs, and currency spend success/failure boundaries.
- Upgrade runtime effect application owns immediate effects, player reapplication, and run-start effect timing.
- Upgrade UI flow owns opening, fade presentation, stack UI calls, and `GameFlowInputBlocker` handoff.
- Save/request coordination stays explicit and does not require UI code to own persistence timing.

This split does not require changing authored `UpgradeNodeSO` schema by default.

## Risks

- Upgrade UI is prefab/scene-facing and uses serialized references.
- `UpgradeManager` is persistent and subscribes to scene load, player registration, and run lifecycle events.
- Run-start effects must not apply twice after scene transition, player respawn, or run restart.
- Purchase rollback must continue to handle currency spend failure.
- UI fade/input blocking must preserve the dialogue handoff fix recorded in `Docs/ErrorLog.md`.

## Refactor Trigger

Start this refactor when one of these becomes true:

- a new upgrade effect type needs more run lifecycle or player reapplication logic
- run-start effects apply twice, fail to apply, or become hard to reason about
- upgrade UI blocker/fade issues recur
- save timing around upgrade purchase/unlock changes becomes a bug source
- implementation work needs to move upgrade UI flow or runtime effects out of `UpgradeManager`

## Related Documents

- `Docs/StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md`
- `Docs/ErrorLog.md`
- `Docs/DecisionLog.md`
- `Docs/StructureMemory/UIFlowInputBlocking.md`
- `Docs/RefactorBacklog/RunModifierAggregationBoundarySplit.md`

## Status

`partially-refactored`

First implementation slice complete:

- Added same-file `UpgradePurchaseRequest`, `UpgradePurchaseResult`, `UpgradePurchaseFailureReason`, and `UpgradePurchaseService` helper types in `UpgradeManager.cs`.
- Moved node lookup, unlock-state validation, currency manager availability, magic stone availability, progress purchase mutation, currency spend, and rollback-on-spend-failure into `UpgradePurchaseService`.
- Kept `UpgradeManager.TryBuyUpgrade(...)` responsible for resolving the player, applying upgrade effects, applying hub target states, queueing cinematics, rebuilding run modifiers, showing rewards, unlocking dependent nodes, saving, and dispatching `OnDataChanged`.

Second implementation slice complete:

- Added same-file `UpgradeRunStartEffectRequest`, `UpgradeRunStartEffectResult`, `UpgradeRunStartEffectSkipReason`, and `UpgradeRunStartEffectPolicy` helper types in `UpgradeManager.cs`.
- Moved run-start duplicate-application guard, run-active check, scene-load observation check, and active run-content scene check into `UpgradeRunStartEffectPolicy`.
- Kept `UpgradeManager.TryApplyRunStartEffects(...)` responsible for resolving the current player, looking up save data, invoking `UpgradeEffectApplier.ApplyRunStartEffects(...)`, and marking run-start effects as applied after successful handoff.

Third implementation slice complete:

- Added `UpgradeUiOpenFlow` in `UpgradeUiOpenFlow.cs`.
- Moved upgrade UI open fade presentation, owned stack UI push, `GameFlowInputBlocker` acquire/release, and interrupted-open cleanup out of `UpgradeManager`.
- Kept `UpgradeManager.ToggleUI()` and `CloseUI()` as the compatibility/public entry points, and kept serialized fade timing fields on `UpgradeManager`.

Fourth implementation slice complete:

- Added `UpgradePurchaseCompletionService` in `UpgradePurchaseCompletionService.cs`.
- Moved purchase success side-effect ordering out of `UpgradeManager.TryBuyUpgrade(...)`: resolve player, apply upgrade, mark applied node, apply hub target states, queue cinematic, rebuild run modifiers, show reward, unlock dependents, request save, and notify data changed.
- Kept `UpgradeManager.TryBuyUpgrade(...)` as the public entry point and kept purchase validation/currency rollback in `UpgradePurchaseService`.

Fifth implementation slice complete:

- Added `UpgradeRuntimeEffectService` in `UpgradeRuntimeEffectService.cs`.
- Moved purchased effect reapply, current-player applied-node tracking, hub target state application, and run-start effect application handoff out of `UpgradeManager`.
- At that point, kept `UpgradeManager` responsible for scene/run/player event timing and the two run-start guard booleans before the later lifecycle helper split.

Sixth implementation slice complete:

- Added `UpgradePurchaseService.cs` and `UpgradeRunStartEffectPolicy.cs`.
- Moved purchase request/result/failure/service helper types and run-start request/result/skip/policy helper types out of `UpgradeManager.cs`.
- Kept type names, call sites, purchase rollback behavior, and run-start guard semantics unchanged.

Seventh implementation slice complete:

- Added same-file `UpgradeRuntimeLifecycleService` in `UpgradeManager.cs`.
- Moved `PlayerRuntimeRegistry.PlayerRegistered`, `SceneManager.sceneLoaded`, `GamePlayDataManager.OnRunStarted`, and `GamePlayDataManager.OnRunEnded` subscription/handler flow out of the `UpgradeManager` MonoBehaviour.
- Moved `hasAppliedRunStartEffectsForCurrentRun` and `hasObservedSceneLoadForCurrentRun` into the lifecycle helper.
- Preserved startup ordering, scene-loaded ordering, run-start/run-ended guard resets, public entry points, serialized fields, purchase/save/UI behavior, and runtime effect policy.

Eighth implementation slice complete:

- Added same-file `UpgradeProgressSaveService` in `UpgradeManager.cs`.
- Moved unlock-check save request execution and data-change notification execution out of the `UpgradeManager` MonoBehaviour body.
- Changed `UpgradePurchaseCompletionService` to receive an explicit save request callback instead of directly holding a save requester object.
- Kept `UpgradeManager.CheckAndUnlockNodes(...)` as the public compatibility wrapper.
- Preserved purchase completion ordering: apply upgrade, mark current-player runtime state, apply hub target states, queue cinematic, rebuild run modifiers, show reward, check dependent unlocks without save, request immediate save, then notify data changed.

Ninth implementation slice complete:

- Moved `UpgradeProgressSaveService` from the bottom of `UpgradeManager.cs` into `UpgradeProgressSaveService.cs`.
- Moved `UpgradeRuntimeLifecycleService` from the bottom of `UpgradeManager.cs` into `UpgradeRuntimeLifecycleService.cs`.
- Kept type names, visibility, constructor signatures, callback wiring, singleton/persistent `UpgradeManager` behavior, public entry points, serialized fields, and runtime policy unchanged.
- Added matching Unity `.meta` files for the new helper source files so their asset GUIDs stay stable after import.
- `Assembly-CSharp.csproj` had not refreshed to include the two new helper files during Codex verification, so MSBuild was intentionally not treated as valid coverage for this file-split slice.

Tenth implementation slice complete:

- Added `UpgradeManagerLifetimeService.cs`.
- Moved singleton claim/release, `GlobalUIRoot.AdoptService(...)`, and persistent-root adoption out of the `UpgradeManager` MonoBehaviour body.
- Moved UI active-state toggle selection into `UpgradeUiOpenFlow.Toggle(...)`, leaving `UpgradeManager.ToggleUI()` as the compatibility entry point.
- Changed `OnDataChanged` and `OnUIClosed` from public `Action` fields to public events, and routed UI-close notification through `UpgradeManager.NotifyUIClosed()`.
- Preserved `UpgradeManager.Instance`, public purchase/status/query/UI methods, serialized fields, save timing, purchase ordering, UI open fade/input blocker behavior, and subscriber call sites.

Verification refresh:

- The generated `Assembly-CSharp.csproj` now includes the extracted Upgrade helper files, including `UpgradeProgressSaveService.cs`, `UpgradeRuntimeLifecycleService.cs`, and `UpgradeManagerLifetimeService.cs`.
- Visual Studio MSBuild errors-only verification passed for `Assembly-CSharp.csproj` on 2026-05-16. Existing project warnings remain, but no Upgrade helper missing-type errors remain in the generated project file.

Remaining debt:

- `UpgradeManager` still owns purchase completion callback wiring, public `OnDataChanged`/`OnUIClosed` event surface, and public compatibility UI open/close entry points.
- `UpgradeEffectApplier` still owns actual purchased effect reapplication, run-start effect application, and immediate target-state application; `UpgradeRuntimeEffectService` only owns the handoff and player/save-data guard flow.
- Upgrade UI flow helper must continue to preserve the dialogue handoff and `GameFlowInputBlocker` rules recorded in `Docs/ErrorLog.md` and `Docs/StructureMemory/UIFlowInputBlocking.md`.
- Any further split should be scoped around callback wiring, public event/API ownership, or `UpgradeEffectApplier` effect-application ownership rather than purchase/run-start/lifecycle policy placement.
