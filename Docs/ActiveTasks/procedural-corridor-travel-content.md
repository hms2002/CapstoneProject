---
status: complete
mode: implementation
risk: high
target: content-integration
---

# Procedural Corridor Travel Content

## Goal

Complete the Corridor-owned half of data-driven Lobby↔Corridor→Boss travel and the planned one-way inter-Corridor links for the three normal boss themes. Keep the final DemonKing route on its fixed, combat-free `DemonkingCorridor` rest scene.

## Completed Scope

- Three active `LobbyGate` trigger slots in the normal-themed Start rooms.
- Three active `BossGate` interaction slots using the shared data-driven portal prefab.
- Six active normal-theme directional connection assets and one reusable travel presentation profile.
- Both directions of every normal-theme Lobby↔Corridor connection share the authored right-to-left black wipe profile; returning to HUB does not fall back to the default portal transition.
- Scene-local builder bindings and explicit per-Corridor `PreserveDuringRun` policy/state IDs.
- Each normal-theme layout and supported generated-object state remains stable while the run is active, including Corridor↔Corridor and Corridor↔HUB travel; a new or ended run clears it.
- Arrival-only destination endpoints in the three normal authored Boss scenes.
- Three one-way normal Boss→HUB connections. Each post-clear portal returns to its matching HUB gate with the shared wipe profile and `RunAction.None`.
- Normal Boss exits no longer use `ScenePortal`/`PortalRouteManager` sequential routing; their encounter directors own explicit theme RouteSets so rewards and per-run defeat gates remain data-driven.
- Arrival trigger reverse-trip suppression.
- Idempotent focused installer and full-installer chaining.
- Three active, positioned `ProtoTypeHub` A-side gate objects with configured endpoint Ids, connections, automatic triggers, `2x2` trigger colliders, and local departure/arrival anchors.
- Restored `DemonkingRouteSet` to the fixed `DemonkingCorridor`; the scene has zero authored combat spawns and retains its existing `CorridorToBoss` portal.
- Rebound the existing interactive HUB `ScenePortal` to `DemonkingHubRouteCatalog`, which contains zero normal stages and starts directly at the fixed DemonKing rest Corridor without restoring a `LobbyGate_demon_king` trigger.
- Disabled the retired `ProceduralDemonkingCorridor` Build Settings entry and both directions of its old HUB/corridor and corridor/boss connection assets while preserving those assets for reference.
- One-way `Shadow → Slime` and `Dragon → Slime` connections. Reverse directions are disabled in connection data, and Slime owns arrival-only endpoints with no interaction or trigger adapter.
- Four guaranteed Event rooms: one departure room in each source theme and two distinct arrival rooms in Slime. Existing Slime NPC guaranteed rooms remain registered.
- A dedicated pipe travel medium and presentation profile reuse the DrainPipe sprite/collider contract and the existing Slime water-arrival VFX without retaining the boss DrainPipe damage/gimmick behavior.
- A designer-facing travel binding editor connects a saved Room/Slot to a scene-local Builder, Connection A/B side, direction policy, and presentation profile without auto-saving the gameplay scene.
- Dynamic Map Preview distinguishes `EI`, `ET`, and `EA` travel slots alongside the existing object markers.

## Intentionally Pending

- Designer sign-off and final tuning for the three positioned gates in `ProtoTypeHub`.
- End-to-end Play Mode traversal and presentation tuning.

## Verification

- Unity 6000.4.2f1 compilation returned code 0.
- `ProceduralCorridorTravelInstaller.Install` returned code 0 and verified the three active generated layouts/endpoints.
- The same installer generated and verified both one-way links, all four guaranteed Event rooms, source interaction endpoints, destination arrival-only endpoints, and the two Slime scene bindings.
- Shadow, Dragon, and Slime Lobby connection assets reference `LobbyToCorridorWipeTravel.asset` in both A→B and B→A directions while preserving their distinct run actions.
- Shadow, Dragon, and Slime scene generators each serialize a unique state ID and `PreserveDuringRun`; the focused installer rejects another policy.
- `Validate Final DemonKing Rest Corridor Route` verifies the final RouteSet, fixed Build Settings scene, zero combat spawns, existing boss portal, retired procedural scene disablement, and both retired connection assets.
- `DemonKingHubPortalInstaller.Validate` and `DemonKingHubPortalPlayModeTests` verify that the unique HUB start portal resolves `DemonkingCorridor` as its first destination.
- All connection assets retain a valid `SceneConnectionSO` script GUID.
- `ProtoTypeHub.unity` contains active Shadow, Dragon, and Slime trigger gates plus the existing interactive portal bound to the DemonKing-only route catalog; no automatic DemonKing trigger gate exists.
- `InstallNormalBossHubReturns` compiled and completed with return code 0, then verified all three one-way Boss→HUB assets, matching HUB endpoint IDs, shared wipe profiles, explicit encounter RouteSets, data-driven exit interactables, and removal of the legacy `ScenePortal` components.
- `ProceduralPlayModeTests` passed 15/15 after the migration, including fixed DemonKing routing, run-state preservation, travel endpoint behavior, and arrival suppression coverage.
