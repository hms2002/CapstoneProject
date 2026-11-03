# Cumulative Ordinary NPC Dialogue Pool

- Mode: Implementation, approved in chat on 2026-11-02.
- Status: Completed; full manual Play Mode and Player build remain unexecuted.
- Goal: Keep earlier conversations eligible as affection grows; at maximum affection retain one choice conversation per NPC/run with no gain or indicator. Add Sasha's approved unilateral-partnership conversation to the base pool.
- Intent: Players can encounter previous/unseen character conversations across later runs rather than losing them on level-up.

## Approved Behavior

- Existing `NpcAffectionDialogueEntry.affectionLevel` becomes a minimum unlock threshold; preserve its serialized name/schema and existing NPC IDs/paths.
- Select uniformly among distinct unlocked paths. Sasha has three base paths, two unlocked at 1, two at 2; remove the redundant level-3 copies of level-2 paths.
- All configured ordinary NPCs retain the active-run, NPC-ID-based one-conversation allowance. Completed positive/negative choices consume it; subsequent encounters use greetings. The same rule applies at maximum affection.
- At maximum, choice dialogue remains available but positive gain requests do not change affection or grant rewards. The authored bubble still indicates gain availability and remains off at maximum. Hub interactions remain greetings; boss/unconfigured NPC behavior stays unchanged.
- Base conversation `affection_0_c`: Sasha claims that the hero entering the front lets her leave through the back; caring for her safety grants +1, refusing to buy grants no change. Five localized strings use the existing CSV/tables/Ink pipeline and one speech line per response.

## Allowed / Protected

- Allowed: selector, existing merchant authoring/validation helpers, merchant Ink/compiled JSON and candidate list, narrow localization/Ink inventory entries, current documentation/memory. Update the existing Notion merchant reference to the final behavior/content within its prior authorized scope.
- Preserve: all reward values/assets, user-adjusted RunMerchantGroup/bubble position, other NPC/boss content, scenes, existing GUIDs, serialized field names, enums, asmdefs, ProjectSettings and bootstrap ownership. No new manager, runtime UI creation or save schema.
- Captured preimages/protected hashes: `outputs/npc-dialogue-pool-2026-11-02/`. Keep the user's running Editor and real save slots untouched; use an isolated short-path native validation workspace if needed.

## Success / Verification

- Verify unlocked pool reachability at 0/1/2/3/4, lower-stage availability after gains, duplicate-path neutrality, future-path exclusion, NPC-ID/reentry allowance, cap-positive continuation without gain/reward and hidden bubble, complete/interrupt/end/new-run behavior, and existing boss/greeting behavior.
- Verify the partnership Ink branches, line counts, all active merchant loc keys against source/JSON/CSV/five tables, old records preserved and scoped candidate migration.
- Run covered generated-project MSBuild and native Unity checks where possible. Explicitly report full manual Play Mode/Player build if unexecuted.
- Doc Impact: SessionLog + existing DialogueNpcAffectionStructure/SceneRuntimeSaveStructure + DecisionLog + ErrorLog (observed cosmetic StringTable serialization drift); update task routing and the existing Notion page. No Architecture/Contracts/Presentation HTML edits.

## Completed Evidence

- Unity 6000.4.2f1 compiled/imported and executed the final production assemblies with an in-memory profile: 337/337 native checks, exit 0. Includes 256 seeded selections at each of five affection amounts, duplicate-path neutrality, reachability/exclusion, capped choices/continuation/reward count/bubble, reentry/run reset/late callbacks, boss compatibility, both partnership branches and all 215 active merchant translation values.
- Preservation/copyback audit: 216 checks, including protected scenes/rewards/settings/bubble placement, old localization IDs/records and CSV values, source parity and exact scoped candidate migration. Cosmetic Unity YAML quoting/escape-case drift on unrelated relic names was restored after semantic comparison.
- Existing-source MSBuild passed; final source build and complete evidence live in [outputs](../../outputs/npc-dialogue-pool-2026-11-02/). No new C# files, asmdef, metadata, manager or runtime UI hierarchy were introduced. The disposable validation project is removed after retaining evidence.
- The existing Notion merchant reference is updated with cumulative pools, cap behavior and the new conversation, preserving unrelated content and authored table widths.

## Follow-up

- [Sasha discount dialogue and portraits](sasha-discount-portraits.md) adds `affection_1_c` and two supplied expressions. Current pools are 3/6/8/8/8; the seven-path counts and 337-check result above remain the historical receipt for this slice.
