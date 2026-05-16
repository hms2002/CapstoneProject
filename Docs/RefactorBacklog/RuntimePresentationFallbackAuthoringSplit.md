---
status: proposed
authority: refactor-backlog
category: refactor-item
last_reviewed: 2026-05-15
---

# Runtime Presentation Fallback Authoring Split

## Status

proposed

## Current Problem

Several production-facing UI and presentation support paths create presentation hierarchy at runtime instead of driving authored scene or prefab objects.

The main pressure points are:

- `LoadingOverlayController` can create a runtime loading canvas and `LoadingOverlayView` fallback.
- `MouseCursorService` creates its own cursor canvas and image hierarchy.
- `CinematicLetterboxOverlay` creates cinematic bars and temporarily adds canvas groups to `GlobalUIRoot` layers.
- `GamePresentationController` creates display letterbox overlay hierarchy at runtime.
- `StatusHudPresenter`, `StatusHudEntryView`, and `StatusHudTooltipView` can create status HUD roots, entry widgets, tooltip images, layout groups, and TMP text hierarchy from code.
- `BossHealthBarUI` can create fallback dual-health and split-health presentation hierarchy when authored references are missing.

Runtime creation is useful during first implementation and feel checks, but these paths are easy to leave in the build path as hidden UI structure.

Dynamic tooltip content, dynamic status entries, and pooled presentation prefabs are acceptable. The debt is full visual template construction in code when a prefab or scene-authored object should own the base layout, sorting, raycast, font, spacing, and animation choices.

## Why It Exists

- First-pass UI and presentation work needs fast iteration before the final visual layout is known.
- Loading and cursor behavior need safe fallback behavior when a scene is missing authored references.
- Some overlays are cross-scene presentation helpers, so creating them from code was the quickest way to avoid per-scene setup.
- Status HUD and tooltip content changes every run, so it was convenient to let runtime code guarantee a minimal template even when no prefab is authored.
- Slime Queen phase-two boss HUD needed a quick fallback for dual/split health visuals before the final authored HUD shape was stable.
- The current structure works, but it makes sorting, raycast, canvas scaling, and visual inspection harder to review in Unity.

## Target Shape

- Production-facing UI and presentation objects should be scene- or prefab-authored and reviewed in Unity.
- Runtime code should primarily find, bind, and drive authored references rather than constructing full UI hierarchy.
- `GlobalUIRoot` canvas layers should be the default home for global UI overlays when possible.
- Runtime-created fallback should be marked as prototype/debug/emergency fallback and kept behind explicit paths.
- Pooled gameplay presentation may still instantiate authored prefabs; the authored prefab remains the visual contract.
- Tooltip and status HUD presenters should be allowed to fill dynamic data, but the base tooltip/entry visual template should come from prefab or scene authoring when build-facing.

## Risks

- Moving fallback UI into prefabs or scenes touches serialized references, canvas layer order, raycast gates, and UI scaling behavior.
- Removing fallback too early can break boot/loading/cursor behavior in scenes that are not fully authored yet.
- Keeping both authored and fallback paths without clear priority can create duplicate overlays.
- Letterbox and loading overlays are input- and visibility-sensitive, so migration needs manual Unity review.
- Status HUD and tooltip migration touches `GlobalUIRoot` layers, hover routing, TMP layout, pointer behavior, and existing status definition assets.
- Boss health fallback migration touches serialized Boss HUD references, Slime Queen phase-two display behavior, health animation timing, and split-health labels.

## Refactor Trigger

- Editing loading overlay, cursor presentation, cinematic letterbox, display letterbox, or other global overlay visuals.
- UI sorting, raycast, input blocking, or CanvasScaler conflicts involving runtime-created overlays.
- Preparing build-facing UI polish where authored prefab/scene review is required.
- Adding another runtime-created canvas or global overlay fallback.
- Repeated need to tune runtime-created UI values that should be inspector-authored.
- Editing Status HUD tooltip/entry visuals or Boss HUD dual/split health visuals.

## Related Documents

- `Docs/DecisionLog.md`
- `Docs/StructureMemory/ScriptSystems/LoadingPresentationStructure.md`
- `Docs/Contracts/PresentationAuthoringContract.md`
- `Docs/Architecture/LoadingScopes.md`
- `Assets/LeeJunMo/Script/Loading/Runtime/LoadingOverlayController.cs`
- `Assets/LeeJunMo/Script/UIStructure/MouseCursorService.cs`
- `Assets/LeeJunMo/Script/Presentation/Runtime/CinematicLetterboxOverlay.cs`
- `Assets/LeeJunMo/Script/Settings/GamePresentationController.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/Status/StatusHudPresenter.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/Status/StatusHudEntryView.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/Status/StatusHudTooltipView.cs`
- `Assets/HeoMinSeok/_Project/Scripts/UI/HUD/BossHealthBarUI.cs`
- `Docs/StructureMemory/ScriptSystems/InventoryAndChestUIStructure.md`

## Next Refactor Step

When one of the runtime-created presentation paths is next edited, first decide whether it is still a prototype/debug fallback. If it is build-facing, move the visual hierarchy into an authored prefab or scene object and keep runtime code as the owner that drives references and cleanup.
