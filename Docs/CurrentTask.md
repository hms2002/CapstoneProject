---
status: active
authority: current-task
category: workflow
last_reviewed: 2026-05-08
---

# Current Task

## Goal

Polish the Lightning Spear MarkRush and recovered spear shot presentation: make MarkRush DashTrail visible, split RecoverShot into a fixed world trail, and keep recovered spear projectile gameplay separate from that trail.

## References

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Architecture/GameplayAbilityWeaponArchitecture.md`
- `Docs/Architecture/CombatArchitecture.md`
- `Docs/Contracts/WeaponCleanupContract.md`
- `Docs/Contracts/PresentationAuthoringContract.md`

## In Scope

- Wire `LightningDashTrail`, `LightningSpearRecoverSpawn`, `LightningSpearRecoverDespawn`, and `LightningSpearRecoverShot` into the Lightning Spear VFX flow.
- Split `RecoverShot` into a fixed world-space trail effect spawned at release time, not a child visual on the moving projectile.
- Predict the RecoverShot trail endpoint from projectile speed/lifetime and wall raycast.
- Support short-distance start-anchored SpriteMask crop rendering and long-distance stretch rendering for Lightning Spear trails.
- Keep the recovered spear stock prefab in a `BodyVisual` plus `EffectVisual` structure.
- Reveal/hide the recovered spear body from animation events when authored, with fallback timing when events are missing.
- Make recovered spears follow the owner with `SmoothDamp`, while snapping on large warp-like distance changes.
- Place recovered spears on the player's back side using `WeaponPresentationRig2D.CurrentSideSign`, not aim rotation.
- Play `LightningSpearRecoverShot` as a shot trail VFX when recovered spears are fired by no-mark Q.
- Use the same start-anchored mask crop rule for MarkRush `LightningDashTrail`.
- Add a Skill2 MarkDrop landing impact prefab that uses its visual as the landing hitbox.
- Make Skill2 MarkDrop placement player-radius based instead of room-bounds based.
- Wire Lightning Spear skill icons so inventory/default UI shows the no-mark Q icon while the combat HUD can temporarily show the MarkRush icon only when a selectable mark is under the cursor.

## Out of Scope

- Changing Unity scenes.
- Adding recovered spear gameplay persistence across weapon swap or scene transition.
- Turning wall-stuck recovered spear shots back into gameplay marks.
- Adding a recovered spear-specific Animator Controller.
- Replacing the actual projectile movement/damage/wall-stuck behavior.
- Running Unity batchmode compile.

## Done Criteria

- MarkRush creates a visible dash trail between start and destination, cropping from the start point on short distances instead of 9-slice scaling.
- Recovered spear stock spawn/despawn clips can reveal/hide `BodyVisual` by event or fallback.
- Recovered spears are positioned behind the player for right/left facing and move smoothly when the side changes.
- No-mark Q keeps the sweep behavior, despawns stocked recovered spears, then fires projectiles with a fixed `RecoverShot` trail.
- Recovered spear shots still pierce monsters, stop on wall, and do not become gameplay marks.
- Skill2 MarkDrop spawns a `LightningMarkDropImpact` hitbox/effect at the spear landing activation frame.
- Skill2 MarkDrop candidates stay within the player-centered combat radius even in large rooms.
- Inventory/detail UI shows Skill1 as the no-mark Q icon because it still only displays two skill slots.
- Combat HUD switches Skill1 to the MarkRush icon only while Q is ready and the cursor has an actually selectable mark.
- Owner disable, weapon swap, and runtime destruction clean up stock actors, transient despawn actors, projectiles, and coroutines.
- Verification reports static analysis only; Unity Editor import/compile remains a manual check.
