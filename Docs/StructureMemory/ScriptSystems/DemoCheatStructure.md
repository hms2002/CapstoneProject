---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-29
---

# Demo Cheat Structure

## Purpose

Map the current demo cheat runtime, especially the F-key shortcuts used during reviews and Play Mode demonstration.

This is structure memory only. It does not override project architecture or contracts.

## Current Structure

| Area | Current responsibility |
| --- | --- |
| Settings | `DemoCheatSettingsSO` is the Resources-loaded settings asset for enabling demo cheats, assigning hotkeys, numeric cheat values, and global fallback map zoom bounds. |
| Hotkey entry | `DemoCheatHotkeyController` owns keyboard polling, notification display, and transition-time cleanup. It reuses the existing demo cheat bootstrap object and does not add a new manager. |
| Cheat execution | `DemoCheatService` applies effects through existing runtime APIs such as currency, player registry, movement motor, portals, attributes, weapon inventory, ability cooldowns, and camera bootstrap. |
| Map zoom authoring | `DemoCheatMapZoomBounds` is an optional scene-authored bounds marker. If one enabled marker exists in the active scene, F5 uses that center/size before the global `DemoCheatSettingsSO` fallback values. |

## Key Files

- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Cheats/DemoCheatHotkeyController.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Cheats/DemoCheatService.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Cheats/DemoCheatSettingsSO.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Cheats/DemoCheatMapZoomBounds.cs`
- `Assets/Resources/DemoCheatSettings.asset`

## Ownership And Lifecycle

- `DemoCheatHotkeyController` owns input polling and calls into `DemoCheatService`; it also restores active map zoom immediately when scene transition begins or the controller is disabled.
- `DemoCheatService` owns active map zoom state. It captures Cinemachine Follow, LookAt, Priority, orthographic size, and camera pose before zooming, then restores them on F5 toggle-back or cleanup.
- Map zoom animation uses unscaled time and `SmoothStep`, so it can run independently of gameplay time scale.
- The F5 fit formula is `max(mapHeight / 2, mapWidth / (2 * cameraAspect)) + padding`.
- `DemoCheatMapZoomBounds` owns only scene authoring data and gizmo visualization. It does not own gameplay camera state.

## Extension Entry Points

- Add or tune global fallback values in `DemoCheatSettings.asset`.
- Add one enabled `DemoCheatMapZoomBounds` object per map scene for scene-specific F5 framing.
- Attach a `BoxCollider2D` to the same object and enable `preferBoxCollider` when level designers want to resize bounds with Unity collider handles; otherwise use the component's manual `size`.
- Add future demo shortcuts through `DemoCheatSettingsSO`, `DemoCheatHotkeyController`, and `DemoCheatService` rather than creating a new singleton.

## Known Pitfalls

- Scene or prefab wiring is still required for per-map zoom. This source change does not place `DemoCheatMapZoomBounds` in any scene.
- If multiple enabled `DemoCheatMapZoomBounds` components exist in the active scene, the service warns and uses a stable first candidate. Keep one active marker per map scene for predictable review behavior.
- `DemoCheatMapZoomBounds.cs` is a new MonoBehaviour file, so Unity must refresh generated project files and import/compile before local MSBuild covers it.
- The `DemoCheatSettingsSO` schema contains serialized map zoom fallback fields; review the asset in Unity after imports.
- Manual Play Mode validation is required for active Cinemachine rigs because camera follow restoration depends on the scene's current camera setup.

## Promotion Candidate

Not yet. Keep this as StructureMemory until demo cheat behavior becomes a durable production-facing contract.
