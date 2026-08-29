---
status: complete
mode: implementation
risk: high
target: content-integration
---

# Procedural Corridor Travel Content

## Goal

Complete the Corridor-owned half of data-driven Lobby↔Corridor→Boss travel for all four boss themes without changing the authored Lobby layout.

## Completed Scope

- Four stable `LobbyGate` trigger slots in themed Start rooms.
- Four stable `BossGate` interaction slots using the shared data-driven portal prefab.
- Eight directional connection assets and one reusable travel presentation profile.
- Scene-local builder bindings and explicit per-Corridor regenerate policy/state IDs.
- Arrival-only destination endpoints in the four authored Boss scenes.
- Arrival trigger reverse-trip suppression.
- Idempotent focused installer and full-installer chaining.

## Intentionally Pending

- Placement of four complete gates in `ProtoTypeHub`.
- Binding a selected Lobby gate to the matching route catalog/stage context.
- Pipe topology between different Corridor themes.
- End-to-end Play Mode traversal and presentation tuning.

## Verification

- Unity 6000.4.2f1 compilation returned code 0.
- `ProceduralCorridorTravelInstaller.Install` returned code 0 and verified all four generated layouts/endpoints.
- All connection assets retain a valid `SceneConnectionSO` script GUID.
- `ProtoTypeHub.unity` was not modified.
