# Ordinary NPC Run Affection Dialogue

- Mode: Implementation, approved in chat on 2026-11-01.
- Status: Completed on 2026-11-01; Unity native 226/226 and final generated-project MSBuild passed. Full manual Play Mode walkthrough and Player build remain unexecuted.
- Scope: Merchant Sasha first; reusable ordinary-NPC routing and authored animated indicator. Bosses/commanders retain existing behavior.
- Approved assets: merchant NPCData/dialogue/rewards, run merchant prefab, supplied 96x32 indicator sheet sliced into three 32x32 frames, and scene-position editor tooling. No new manager or bootstrap changes.

## Behavior

The cumulative-pool and maximum-affection behavior below is updated by the user-approved [2026-11-02 slice](npc-dialogue-cumulative-pool.md). Original verification counts in this receipt refer to the earlier implementation.

- Only an active run offers an affection conversation, once per NPC ID. Both completed success and completed failure consume the opportunity; later encounters use short choice-free greetings.
- Success grants affection through the existing manager/reward flow. A granted gain stays consumed if the remaining dialogue is interrupted. Interruption before gain/completion permits retry. Late callbacks from an ended run cannot consume a new run's opportunity.
- Defeat/victory and other existing active-run end paths clear the session list. Permanent affection and rewards follow the existing commit path.
- Random selection among distinct paths whose minimum affection threshold is at or below the current amount. Full-backpack greetings precede ordinary greetings after conversation consumption or in the hub.
- Maximum affection is the highest authored reward threshold; the gain indicator stops there, while one choice conversation per active run remains available without increasing affection or granting rewards.
- Run merchant conversation starts on player interaction, replacing the automatic profile-once introduction for opted-in NPCs.

## Temporary Content

This initial content configuration is superseded by [permanent merchant rewards](merchant-affection-rewards.md): four tiers, eight candidate bindings and maximum affection four. The loop/indicator behavior in this task remains in use.

- Sasha levels 1/2/3 reuse the existing 20% discount effect: cumulative 20%/40%/60%. This is placeholder balance requested by the user, not final reward design.
- Two affection conversations per starting level 0/1/2; three ordinary greetings and two full-backpack greetings.
- Existing localization pipeline covers all five languages. New non-Korean lines are drafts pending language review.
- Sheet animation defaults to 4 frames/second, at 0.8 world units; position, size and speed are authored on the prefab. No Animator/controller changes.

## Verification

- Unity native regression, compiled Ink/JSON consistency, imported asset/prefab inspection and MSBuild where generated source coverage permits.
- Inspect rendered merchant/indicator frames and check Markdown links/paths and diff whitespace.
- Full manual gameplay and Player build must be reported separately if unexecuted.
- Final preservation/localization audit confirms all 190 active merchant locale values, prior prefab objects and prior locale entries; changed-document links and whitespace pass. See [session evidence](../SessionLogs/2026-11-01.md).

## Related Memory

- [Dialogue NPC Affection Structure](../StructureMemory/ScriptSystems/DialogueNpcAffectionStructure.md)
- [Scene Runtime Save Structure](../StructureMemory/ScriptSystems/SceneRuntimeSaveStructure.md)
- [Session log](../SessionLogs/2026-11-01.md)
