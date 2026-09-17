---
status: active
authority: structure-memory
category: feature-map
last_reviewed: 2026-09-18
---

# Potion Rewards

## Purpose

Map the potion-use reward triggers and potion-only healing override. This is a reconstruction aid, not an Architecture/Contract replacement.

## Current content

| Content | Data / behavior |
| --- | --- |
| 벌컥벌컥 | `Reward_PotionGrowth`: each successful potion consumption after acquisition adds 2 percentage points of attack-speed Percent modifier; no content-specific cap. |
| 야호! | `Reward_PotionCheer`: move speed +30%, attack speed +15% for 15 scaled seconds; repeat consumption refreshes, never stacks potency. |
| 회복의 팬턴트 | `RD_RecoveryPendant`: rare, max level 1, recovery potion restores 2 instead of 1. User-requested spelling retained. |

Both cards use Earth_Card_3 (instant-type artwork), but their gameplay lifetime is Persistent because they listen for future potion use. Neither card permits repeat selection. The authored heal potion reference, not an ID substring or any generic heal event, identifies eligible consumables.

## Key files and ownership

- `Assets/_Project/Runtime/Features/Items/Consumables/PlayerConsumableInventory.cs`: owns slots, successful consumption event, and player-local source-token healing overrides. `ConsumableUsed` fires after the slot is cleared, before `OnChanged`; acquisition, swapping, restore and failed use emit nothing.
- `Assets/_Project/Runtime/Features/Items/Consumables/ConsumableDefinition.cs`: reads the inventory's resolved amount before the existing AttributeSet heal. Never mutates shared SO data or general healing.
- `Assets/_Project/Runtime/Features/Items/Relics/RelicLogic_PotionRecovery.cs`: registers one minimum recovery amount keyed by the relic token; both normal equip and restore attach register it. Unequip and restore detach remove it. Repeated attach replaces the same entry.
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/Effects/PotionGrowthLevelRewardEffectSO.cs`: stores consumed count in existing `LevelRewardEffectState.json`, reapplies one modifier from that count, owns event unsubscribe and modifier cleanup.
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/Effects/PotionCheerLevelRewardEffectSO.cs`: stores a scaled session-time expiry in effect JSON. Rebuild uses the original deadline, not a fresh 15 seconds. Owns timer, two modifiers and StatusHandle together.
- `Assets/_Project/Runtime/UI/Inventory/Consumables/ConsumableDetailView.cs`: projects effective recovery when a player-local override differs from base; no gameplay ownership.
- `Assets/_Project/Data/Progression/Leveling/Rewards/`: the two definitions/effects, `Statuses/SHD_PotionCheer`, and catalog entries.
- `Assets/_Project/Data/Items/Relics/Definitions/RD_RecoveryPendant.asset`, `Strategies/RelicLogic_RecoveryPendant.asset`, and `Data/Items/ItemDatabase.asset`: relic logic, identity and default unlocked loot registration.
- `Assets/_Project/Art/Sprites/UI/StatusIcons/potion_red.png`, `Art/Sprites/Items/RelicIcon/potion1.png`: current supplied icons, Point filtering, 32 PPU, uncompressed single sprites. The earlier `heal.png` remains available for reuse by another relic.

## Lifecycle and pitfalls

- `RunLevelRewards` disposes live handles on player unregister, rebuild, run start/end. The existing run lifecycle clears selection/effect JSON; no profile-save or serialized DTO schema changes were made.
- Growth increments only after acquisition; prior drinks are not counted. Growth and Cheer use separate modifier sources, so either cleanup preserves the other's effect.
- Cheer uses `Time.time`: pause freezes the timer and scene changes do not reset it. The expiry is for the existing in-memory run session, not a new cross-application run-resume format.
- The status hub only projects the active buff. The `potion_red` icon has no stack/duration numbers; remaining time is shown in the tooltip. Card description occurrences of 야호! are blue, title is not.
- Full-health use remains unsuccessful and does not consume a potion or trigger bonuses. Healing remains capped by the existing AttributeSet maximum-health rules.
- The relic affects only `CD_HealPotion`, not generic healing or another ConsumableDefinition, even one with the same target attribute. Future potion types need explicit authoring decisions.
- No new manager, singleton, runtime UI hierarchy, scene/prefab edits, persistent enum changes, or existing GUID replacements.

## Verification / extension

- `Assets/_Project/Tests/PlayMode/Procedural/PotionRewardPlayModeTests.cs` has seven tests for consumed-event ordering/failure, potion-specific healing and detach, growth counting/restore/reset, Cheer refresh/expiry/cleanup, and catalog/artwork wiring.
- Gameplay, UI and test-assembly MSBuild compilation passed. New sources were included using an external temporary targets file; Unity-generated csproj files were not edited.
- Unity batch PlayMode execution did not reach tests: sandbox launch failed Package Manager IPC; normal-environment retry exited 198, `No valid Unity Editor license found` / missing `com.unity.editor.headless`. Actual import/play/visual tests remain unverified.
- In a licensed Editor run the `PotionRewardPlayModeTests` fixture, then verify card artwork/blue wording, buff icon hover, relic equip/drop, room transitions and a fresh run.
- If more consumable effects need complex recovery policies, revisit a dedicated domain policy boundary rather than spreading potion checks through generic healing. No such extra framework is introduced here.
