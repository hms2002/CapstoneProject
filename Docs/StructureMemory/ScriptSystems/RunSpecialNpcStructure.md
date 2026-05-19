---
status: proposed
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-19
---

# Run Special NPC Structure

## Purpose

Map the planned structure for run-internal special NPCs that use in-world speech bubbles and local interaction choices instead of the existing Ink portrait dialogue stack.

This is a planning structure map only. It does not describe implemented code yet, and it does not override `Docs/Architecture/DialogueArchitecture.md`, `Docs/Architecture/RuntimeSaveArchitecture.md`, or project contracts.

## Proposed Structure

| Area | Proposed responsibility |
| --- | --- |
| Interaction entry | Use the normal `InteractableBase` / `IPlayerInteractor` path so prompts and interaction gating stay consistent with other world objects. |
| Speech bubble dialogue | Use `SpeechBubbleComponent`-driven in-world lines near the NPC or player, not `DialogueController`, Ink, portrait UI, or `DialogueView` choices. |
| Choice UI | Add a separate run-special-NPC choice presenter around the speech bubble/world position. It should support click and number-key selection, and should guard against advance-key mashing selecting a choice accidentally. |
| Flow controller | A small flow owner should sequence lines, choices, feature execution, input blocking, and cleanup. UI projects the flow state; it should not own progression state. |
| Feature modules | Construction, same-scene teleport, affection gates, and currency payment should be feature modules attached to the shared flow rather than separate one-off NPC controllers. |

## Planned Flows

### Construction / Permanent Shortcut NPC

- First interaction explains that the path is blocked or missing.
- One choice exits; the payment choice spends the configured magic-stone cost.
- After payment, construction state records the start point and remaining run completions.
- While construction is pending, the NPC reports the remaining count.
- When complete, the target path opens as a permanent shortcut through the existing map/shortcut progression path.

Default planning choice:
Use `DoorObject` / `ShortcutProgressService`-style permanent shortcut state for the durable unlock. If the visual map must change, prefer scene-authored blocked/open object sets over direct runtime `Tilemap.SetTile(...)` edits for the first implementation slice.

### Same-Scene Teleport NPC

- Gate access by authored conditions such as required boss affection.
- If the condition fails, show only speech bubble explanation lines.
- If the condition passes, show a confirmation choice.
- On confirm, fade out, block control, move the player to an authored same-scene destination, play any landing presentation, then restore control and enemy detection.

Default planning choice:
This is not a `ScenePortal` transition. It should move the current player inside the same loaded scene and use existing fade/control/warp support where practical.

## Ownership And Lifecycle

- The flow owner owns the active conversation state, input-blocking window, choice gating, and cleanup.
- Speech bubble and choice UI own only presentation and input relay.
- Currency spending stays with `CurrencyManager`.
- Affection checks stay with `AffectionManager`.
- Durable path unlock state stays with map/shortcut save ownership, not UI.
- Same-scene teleport should use the current player runtime object. It should not create a new player or run scene transition capture/restore.
- If player movement uses `MovementMotor2D.WarpTo(...)`, the flow should account for physics update timing and clear stale motion as needed.

## Extension Entry Points

- Add shared run-special-NPC data for authored line sequences, choices, costs, gates, and feature references.
- Add feature modules for construction progress, permanent shortcut opening, same-scene teleport, and future local run NPC actions.
- Add authored scene/prefab references for speech bubble anchors, choice anchors, teleport destinations, blocked/open path objects, and target doors/shortcuts.
- Add validation later if these NPCs become common: missing destination, missing speech bubble, missing target shortcut, invalid cost, and missing gate target.

## Known Pitfalls

- Do not route these NPCs through `DialogueController` unless the design explicitly changes to Ink portrait dialogue. Their UI and flow are intentionally different.
- Do not let the speech bubble or choice UI become the source of truth for construction progress, unlock state, currency, or affection.
- Do not silently mutate tilemaps as the first solution for path opening. Runtime tile edits are harder to author, review, save, and validate than toggling authored blocked/open objects or opening a durable `DoorObject`.
- Same-scene teleport needs explicit player control blocking, prompt hiding, fade cleanup, and camera-follow sanity checks.
- Prefab/scene references are likely: changing the eventual flow MonoBehaviours, serialized data, or UI prefabs needs Unity reference review and Editor verification.
- Save-schema changes for construction progress require migration/default review; prefer an existing map/shortcut progress shape when possible.

## Promotion Candidate

Not yet. Keep this in StructureMemory until a first implementation proves the stable boundaries. If the flow becomes a recurring content pipeline, promote stable authoring rules into `Docs/Architecture/` or `Docs/Contracts/` after explicit approval.
