# NPC Room Introduction

## Purpose

Automatically focus and start the existing NPC interaction on first entry to a generated NPC room. Completion is durable profile progress, not a per-run room flag.

## Flow

- `DungeonRoomBuilder.TryBuildRoomDiscoveryTriggers` attaches `NpcRoomIntroduction` and supplies generated objects belonging to that room.
- `RoomType.Shop` rooms are excluded from automatic introductions; manual shop interaction and room discovery remain unchanged.
- `DungeonRoomDiscoveryTrigger2D` forwards player-body entry and exit. This does not change room discovery or return-portal notifications.
- `NpcRoomIntroduction` collects `INpcRoomIntroductionSource` components, waits for an idle player, and skips completed keys.
- `DialogueTrigger` uses its existing Ink, participants, and feature controller. The introduction captures a `GameplayCameraFocusPlayback` session before starting the dialogue. Input protection is released before the dialogue system acquires its own input ownership.
- `RunSpecialNpcInteractor` starts the same flow as manual interaction. Its existing focus/letterbox sequence owns the camera; no second focus session is applied when that option is enabled.
- Normal completion appends a key to `GameData.completedNpcRoomIntroductions` and requests an immediate save through the existing coordinator.

## Identity And Lifecycle

- Ink NPCs: `npc:{NPCData.id}`. Boss NPCs are excluded.
- Current run-special NPCs: `run-npc:{DialogueFeatureKind numeric value}`. Construction and teleport each have one introduction per profile. If multiple distinct NPCs later share a feature kind, introduce an explicit stable NPC key before enabling separate introductions.
- Different corridor themes share completion when they use the same NPC identity.
- Existing saves without the additive field are treated as having no completed introductions. No existing save fields were renamed.
- `DialoguePresentationOptions.OnEnded` reports success only after normal closing; rejected starts, choice UI failures, controller disable, and aborted starts report failure.
- Run-special flow interruption reports failure. Disabling the room introduction invalidates late callbacks and restores any camera/input ownership it acquired.
- Save writes verify the profile object has not changed while the dialogue was running.
- No new singleton, scene bootstrap, authored prefab component, or asmdef is required. Static HUB NPCs are not automatically affected.

## Extension And Verification

New NPC interaction implementations can implement `INpcRoomIntroductionSource`; generated room scanning then includes them without generator-specific NPC branches. Failed/unavailable interactions remain available for a future room re-entry rather than being marked complete.

`NpcRoomIntroductionPlayModeTests` verifies completion and JSON round-trip, repeat suppression, interrupted retry, immediate exit, disable/late completion, and profile isolation (five tests). These use a controlled NPC source; actual camera framing and Ink/choice interaction still require a manual playtest.

This is a feature structure map, not a replacement for Dialogue Architecture or the profile save ownership contract.
