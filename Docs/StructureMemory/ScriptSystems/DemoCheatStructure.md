---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-09-10
---

# Demo Cheat Structure

## Purpose

Map the editor-only cheats used during Play Mode reviews. Existing hotkeys are restored without enabling cheats in player builds.

This is structure memory only. It does not override project architecture or contracts.

## Current Structure

| Area | Current responsibility |
| --- | --- |
| Settings | `DemoCheatSettingsSO` keeps existing serialized fields and the Resources path. `EnableDemoCheats` always returns false outside the Editor. |
| Hotkey entry | `DemoCheatHotkeyController` and its existing bootstrap are compiled only with `UNITY_EDITOR`. The controller owns keyboard polling, notifications, and cleanup. |
| Cheat execution | `DemoCheatService` and `DemoCheatResult` are compiled only with `UNITY_EDITOR`. Effects use existing currency, player, movement, portal, attribute, cooldown, and camera APIs. |
| Map zoom authoring | `DemoCheatMapZoomBounds` remains a serializable scene-authored bounds marker. Num+ uses active-scene bounds before the settings fallback. |

## Key Files

- `Assets/_Project/Runtime/Features/Cheats/DemoCheatHotkeyController.cs`
- `Assets/_Project/Runtime/Features/Cheats/DemoCheatService.cs`
- `Assets/_Project/Runtime/Features/Cheats/DemoCheatSettingsSO.cs`
- `Assets/_Project/Runtime/Features/Cheats/DemoCheatMapZoomBounds.cs`
- `Assets/_Project/Resources/DemoCheatSettings.asset`

## Current Asset Hotkeys

Focus the Game view during Play Mode. These keys come from the settings asset, not the C# defaults.

| Key | Effect |
| --- | --- |
| Num0 | Show the current cheat guide |
| Num1 | Warp to nearest portal in Hub/corridor/hallway scenes |
| Num2 / Num3 | Attack +100 / -30 |
| Num4 | Magic stones +100 |
| Num5 | Toggle manual invulnerability |
| Num6 | Cycle through spawned RunSpecial NPCs |
| Num7 | Refill health |
| Num9 | Reset owned weapon cooldowns |
| Num/ | Slime Queen Melta affection +1 |
| Num- | Select the next portal's boss destination: F1 Shadow, F2 Dragon, F3 Slime, F4 Demon King, F5/Escape cancel |
| Num+ | Toggle map zoom |
| PageDown | Release automatic invulnerability |

Automatic low-health invulnerability is OFF by default in both the settings asset and new settings instances. It remains available through the existing Inspector option. Manual invulnerability is independent.

## Ownership And Lifecycle

- `DemoCheatHotkeyController` owns input polling and calls into `DemoCheatService`; it restores map zoom immediately when a scene transition begins.
- Disabling the controller or `Enable Demo Cheats` releases tracked manual/automatic invulnerability, restores map zoom, and cancels pending boss selection.
- `DemoCheatService` owns a map zoom session through the Core `GameplayCameraMapZoomPlayback` contract. Presentation owns the concrete camera snapshot and restoration.
- Map zoom animation uses unscaled time and `SmoothStep`, so it can run independently of gameplay time scale.
- The zoom fit formula is `max(mapHeight / 2, mapWidth / (2 * cameraAspect)) + padding`.
- `DemoCheatMapZoomBounds` owns only scene authoring data and gizmo visualization. It does not own gameplay camera state.

## Extension Entry Points

- Add or tune global fallback values in `DemoCheatSettings.asset`.
- Add one enabled `DemoCheatMapZoomBounds` object per map scene for scene-specific Num+ framing.
- Attach a `BoxCollider2D` to the same object and enable `preferBoxCollider` when level designers want to resize bounds with Unity collider handles; otherwise use the component's manual `size`.
- Add future demo shortcuts through `DemoCheatSettingsSO`, `DemoCheatHotkeyController`, and `DemoCheatService` rather than creating a new singleton.

## Known Pitfalls

- Scene or prefab wiring is still required for per-map zoom. This source change does not place `DemoCheatMapZoomBounds` in any scene.
- If multiple enabled `DemoCheatMapZoomBounds` components exist in the active scene, the service warns and uses a stable first candidate. Keep one active marker per map scene for predictable review behavior.
- `UNITY_EDITOR` guards exclude cheat input and execution from both Development and release player builds. The settings and bounds types remain serializable; their presence does not enable cheat execution.
- Warp cheats require an existing portal/NPC. Boss selection overrides an existing portal once; it does not create one or bypass its interaction conditions.
- Currency and affection cheats use existing persistence APIs. Editor-only execution does not make these changes temporary.
- Manual Play Mode validation is required for active Cinemachine rigs because camera follow restoration depends on the scene's current camera setup.

## Promotion Candidate

Not yet. Keep this as StructureMemory for the editor review workflow.
