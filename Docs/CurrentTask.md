---
status: active
authority: current-task
category: weapon-gas
last_reviewed: 2026-05-25
---

# Current Task

## Goal

Polish the `Flowering` weapon prototype after first play validation.

## Requested Work

- Make normal `Flowering` attacks use the existing `SwordCombo2D` three-hit combo flow.
- Keep Bloom-state attacks separate from combo state and make them a three-hit flurry.
- Make Bloom flurry hitbox/effect variants choose randomly without repeating the immediately previous variant.
- Fix Bloom dash slash scheduling, hit detection, and red `SlashHit.prefab` visibility.
- Replace the hard world-rendered Bloom screen border with the affection-style UI gradient border.

## Scope Notes

- User current instruction supersedes the older runtime debug item grant task.
- Damage numbers remain temporary and can be tuned later.
- `GlobalUIRoot.prefab` is not modified in this slice; the Flowering border stays runtime-created for v1.
- Existing unrelated worktree changes must not be reverted.
- Unity batchmode must not run while Unity Editor processes are open.

## Done Criteria

- Normal `Flowering` attack selects `AD_FloweringAttack_Base` and runs a 1-2-3 SwordCombo-style combo.
- Bloom `Flowering` attack selects `AD_FloweringAttack_Bloom` and runs a non-combo three-hit flurry.
- Bloom dash creates three delayed slash hitboxes and three red slash marks.
- Dash slash hit checks keep wall line-of-sight blocking but do not drop hits because the resolved target root uses a different layer than the collider.
- Bloom screen border uses `AffectionGradientBorderGraphic` and `M_UIAffectionGradientBorder.mat`.
- Bloom cleanup removes temporary overlay, outline, dash hitboxes, and slash effects.

## Verification Plan

- Run `rg` checks for Flowering, dash augment, SwordCombo, and affection border references.
- Confirm generated `Assembly-CSharp.csproj` includes changed runtime scripts.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` when generated projects include the relevant source files.
- Run `git diff --check` for touched tracked files.
- Run a trailing-whitespace scan for touched source/assets/docs, including untracked new assets.
- Check for Unity Editor processes and do not run Unity batchmode if the Editor is open.

## Remaining Risks

- Unity Editor import/compile must still confirm ScriptableObject asset type changes and new serialized fields.
- Manual Play Mode validation is required for attack feel, UI border layering, dash slash timing, and wall line-of-sight.
