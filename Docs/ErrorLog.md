---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-05-25
---

# Error Log

This file records recurring implementation errors, their causes, and prevention rules.

## Template

```md
## YYYY-MM-DD - Short error name

Context:

Cause:

Fix:

Prevention:
```

## Active Entries

## 2026-05-25 - P2 Drain Completion Was Treated As Permanent Blocking

Context:
After a phase-two Slime Queen sank into a broken drain and resurfaced, the same drain could no longer receive hit damage.

Cause:
The phrase "block/close the drain" was implemented as a permanent `DrainPipe.isBlocked` state. `TryApplyDamage(...)` returned false while blocked, so the drain became unusable instead of returning to its original cork state.

Fix:
`DrainPipe` no longer has a permanent blocked state for P2 boss drain completion. After the 4-second drain sequence, it restores the boss, resets the pipe to unbroken cork visual state, clears the hit count to `0`, and remains damageable.

Prevention:
For this mechanic, "close/restore the drain" means reset to the initial damageable drain state. Do not model P2 drain completion as a permanent disable unless the design explicitly changes.

## 2026-05-25 - Slime Queen Groggy Gauge Was Not Wired

Context:
`SlimeQueen`, `SlimeQueenP2Short`, and `SlimeQueenP2Long` did not expose the same groggy/stagger gauge behavior as the other bosses.

Cause:
The Slime Queen prefabs did not author a `StaggerGaugeSystem`, and the Slime Queen phase-two HUD special case explicitly hid the shared groggy bar while showing dual health bars. That meant phase-one had no stagger target component for `CombatDamageAction`, and phase-two had no visible groggy gauge even after the combat component existed.

Fix:
`SlimeQueenBossBase` now ensures a runtime `StaggerGaugeSystem` for every Slime Queen variant and wires it to the existing stagger attributes plus a 3-second status-only Groggy effect. Phase-two Slime Queen HUD handling now lives in `SlimeQueenPhaseTwoHudSource`, which projects Short/Long as separate health and groggy channels. `BossGroggyBarUI` can render the two groggy channels, using a logged runtime fallback until authored dual references are wired.

Prevention:
When adding a new boss or special multi-body HUD path, verify both sides of groggy support: the combat target must have a configured `StaggerGaugeSystem`, and the HUD path must provide an `IBossHudSource` snapshot with explicit groggy channels instead of hiding the groggy view or adding concrete boss branches to `BossHudController`.

## 2026-05-25 - Drain Control Lock Stopped Drain Pull

Context:
P2 Slime Queens stopped as soon as they entered the open drain suction radius instead of being pulled into the drain.

Cause:
The drain acquisition path called `BeginDrainControlLock()`, which set the shared pit-fall runtime lock. `SlimeQueenBossBase.Update()` responds to that lock by calling `movementMotor.StopAllMotion()` every frame, and `MovementMotor2D.FixedUpdate()` can also overwrite direct Rigidbody velocity with zero. This fought the `DrainPipe` suction velocity.

Fix:
`SlimeQueenPhaseTwoBase.Update()` now returns immediately while drain-locked, before the shared pit-fall stop path runs. `DrainPipe` creates a drain context at acquisition time, disables the boss `MovementMotor2D` for the suction/submerged lifecycle, pulls the boss by direct `Rigidbody2D.MovePosition`, and restores the motor when the boss leaves the drain.

Prevention:
When an environmental gimmick owns a forced movement phase, do not rely on Rigidbody velocity while the target's normal movement motor is still active. Explicitly transfer movement ownership for the whole forced phase and restore it in the same lifecycle context.

## 2026-05-25 - Drain Boss Suction Reused Pawn Radius

Context:
Slime Queen P2 bosses could become unable to act immediately after spawning when a drain was already open, and the first fix changed P2 drain acquisition to direct trigger contact only.

Cause:
P2 boss drain acquisition originally reused `DrainPipe.suctionRadius`, which is intentionally broad for Pawn slime cleanup. That radius was too large for phase-two boss spawn safety, while the trigger-only workaround removed the planned "enter drain suction range and get pulled in" behavior.

Fix:
`DrainPipe` now has a separate Inspector field, `phaseTwoBossSuctionRadius`, for P2 Slime Queen acquisition. Pawn slime suction still uses `suctionRadius`; P2Short/P2Long acquisition uses the new radius, then the existing drain lock/submerge/restore flow. Selected `DrainPipe` gizmos draw both ranges separately.

Prevention:
Do not reuse a broad mob cleanup radius for a boss gimmick acquisition rule when spawn safety and gimmick proximity need different tuning. Split the serialized authoring fields and show both authoring ranges in gizmos.

## 2026-05-22 - Detached Telegraph Cleanup Was Owned Only By Caster Coroutine

Context:
SlimeQueenP2Short body inflate could leave its circular warning blinking in the scene when the boss died during the warning window.

Cause:
The warning was created as a detached `AttackTelegraphView`, while its timed destroy coroutine ran on the caster's `AttackTelegraphService`. If the caster was destroyed before the coroutine completed, the detached warning had no remaining owner-held cleanup path.

Fix:
Body inflate warning views are now retained by the Slime Queen body-inflate host and cleared from ability `finally`, pattern end/abort, disable, and destroy paths.

Prevention:
Detached telegraph or presentation views must either be self-owned for their lifetime or retained by the gameplay owner that can clear them during cancel, death, disable, and destroy cleanup. Do not rely only on a coroutine hosted by an object that may die before the detached view.

## 2026-05-19 - Affection Reward UI Waited Behind Dialogue Blocker

Context:
Dialogue could stop after affection gain when the affection change unlocked a reward.

Cause:
`AffectionRewardProcessor` passed the dialogue continuation callback into `RewardDisplayService.ShowReward(...)`. During dialogue, `DialogueService` owns an external UI input blocker, so `RewardDisplayService` could not open `RewardDisplayUI` and left the request queued. Dialogue was waiting for the reward close callback, while the reward UI was waiting for dialogue's blocker to release.

Fix:
When an affection reward is earned while external UI input is blocked, the reward display request is queued without the dialogue continuation callback and the dialogue callback is invoked immediately. `RewardDisplayService` now retries queued reward presentation once UI opening is allowed again.

Prevention:
Do not make a UI popup close callback the only continuation path while the popup itself is blocked by the current flow's input blocker. Queue the popup separately or use an explicit owner handoff.

## 2026-05-19 - Affection Tween Kill Skipped Dialogue Continuation

Context:
Dialogue could stop advancing after the affection gain presentation.

Cause:
`AffectionUI.PlayGainAnimation(...)` forwarded the dialogue continuation only from the DOTween sequence `OnComplete`. If the UI was disabled, destroyed, or a new affection animation killed the existing sequence before completion, DOTween did not run that completion callback and the dialogue tag flow stayed waiting.

Fix:
`AffectionUI` now tracks the active gain sequence and pending continuation callback. `OnComplete`, `OnKill`, `OnDisable`, and replacement animation paths all converge through a single completion helper that snaps the UI to the final affection value and invokes the pending callback once.

Prevention:
Dialogue-blocking presentation code must not rely only on tween `OnComplete`. Any presentation that gates Ink/dialogue continuation needs an interruption path that invokes the continuation exactly once.

## 2026-05-19 - World Drop Sprite Hidden By Mask Interaction

Context:
Weapon replacement created the dropped weapon object correctly, and its SpriteRenderer, sprite, material, and color alpha looked valid, but the dropped weapon sprite was not visible in the world.

Cause:
`DroppedWeapon.prefab` had its active weapon image SpriteRenderer authored with `SpriteMaskInteraction.VisibleInsideMask` and the Default sorting layer. Without a matching SpriteMask, the renderer can be fully hidden even when the component is enabled and alpha is 1. Default sorting also made the drop less consistent with other world pickups.

Fix:
`DroppedWeapon.prefab` now disables the unused root SpriteRenderer, clears SpriteMask interaction on the actual `WeaponImage` renderer, and moves that renderer to the Entity sorting layer used by generic world pickups.

Prevention:
For world pickup and drop prefabs, check `SpriteRenderer.maskInteraction` and sorting layer before assuming material, sprite assignment, or alpha is the visibility cause. World pickup visuals should normally use no SpriteMask interaction unless a visible SpriteMask is intentionally authored with the prefab.

## 2026-05-19 - Runtime SampleAnimation Did Not Drive Laser Clip Playback

Context:
DemonKing EgoSword laser VFX spawned, but the visible SpriteRenderers stayed on their initial prefab sprites instead of playing the authored Start/Idle/End clips.

Cause:
The VFX tried to use code-driven `AnimationClip.SampleAnimation(...)` instead of real Animator components. The prefab had no Animator Controller bound to the Start/Body renderers, and later edits also left a stale manual sampling call in the Idle phase.

Fix:
`DemonKingEgoLaserVfx` now controls playback by calling `Animator.Play(...)` for `Start`, `Idle`, and `End` states. The VFX prefab has separate Animator components on the Start and Body SpriteRenderer objects, and those Animators reference dedicated Start/Body controllers using the authored AnimationClip assets. Idle clips are marked looping.

Prevention:
For sprite clips authored as Unity AnimationClips, prefer an Animator Controller on the GameObject that owns the animated SpriteRenderer. Keep gameplay timing code-driven when needed, but let Animator own frame binding and clip playback.

## 2026-05-19 - Transparent Beam Sprite Bounds Made Laser Look Tiny

Context:
DemonKing EgoSword laser VFX used a sliced 64px-high sprite sheet where the visible beam occupied only a small strip inside a mostly transparent frame.

Cause:
The first runtime VFX implementation sized the tiled `SpriteRenderer` directly to gameplay `laserWidth`. That scaled the full transparent sprite rect down to the hitbox width, so the actual visible beam pixels became much thinner than the intended attack width and made the Body segment look missing.

Fix:
`DemonKingEgoLaserVfx` now separates ray length from visual thickness. Body uses `SpriteRenderer.size.x` for tile length, keeps source sprite height for `size.y`, and applies Y transform scale from a serialized `sourceBeamHeightUnits` value. Start uses the same visual scale. The VFX drives the authored AnimationClip assets through Animator Controllers instead of manually stepping sprite arrays.

Prevention:
For beam/ribbon sprites with transparent padding, do not treat the sprite rect height as the visible beam height. Author or serialize the visible beam height separately, and scale only the visual axis while preserving tile size on the repeat axis.

## 2026-05-18 - Attached Target VFX Used Root Transform Instead Of Visual Bounds

Context:
Electric electrocute status particles and discharge snap/trail points needed to appear around the monster body center and scale across small mobs, large mobs, and scaled dummy targets.

Cause:
The presentation context fell back to `target.transform.position`, and attached visuals kept a fixed world scale. Several enemy roots are authored at the base or have collider/sprite offsets, so root position and fixed scale did not match the visible monster sprite.

Fix:
`SpawnedPresentationHook` now has opt-in sprite-bounds anchor and uniform scale modes. Electric electrocute status particles use `SpriteRenderer.bounds.center` and `SpriteRenderer.bounds.size`, and Electric discharge visual points use the same sprite-bounds center.

Prevention:
Target-attached body VFX should not assume the target root transform is the visual body center. For presentation that must cover the rendered unit, use an explicit visual anchor policy such as sprite bounds, and leave root-position spawning as the compatibility default.

## 2026-05-18 - Manual WhileActive Presentation Had No Release Handle

Context:
Electric electrocute needed a looping particle visual that attaches to each debuffed monster and stays alive until the status ends.

Cause:
`GameplayEffect.presentationWhileActive` previously forwarded spawned visuals through one-shot presentation playback only. Looping ParticleSystems with auto-detected lifetime could auto-release too early, while `ManualRelease` visuals had no retained handle for the effect removal path to release.

Fix:
`GameplayEffectPresentationRouter` now stores handles for ManualRelease while-active visuals, spawns them through `PresentationSpawnService.SpawnPersistent(...)`, and releases them from `RemoveWhileActive(...)`. Auto-release while-active visuals still use the existing merged presentation path.

Prevention:
Any sustained `GameplayEffect` visual with `ManualRelease` must have an owner-held handle and an explicit release call on phase/effect exit. Do not rely on one-shot auto lifetime for looping status particles.

## 2026-05-18 - Destroy Cleanup Recreated Runtime Service

Context:
Stopping Play or closing a scene logged `Cannot set the parent of the GameObject 'PresentationAssetProvider' while its new parent '[RuntimeServices]' is being destroyed`, followed by a leaked `PresentationAssetProvider` scene object warning.

Cause:
`PresentationPreloadService.OnDestroy()` tried to release active manifests without creating a provider, but `ApplyManifest(...)` and `ApplyRouteSetManifest(...)` fell back to `PresentationAssetProvider.CurrentProvider`. That property creates a provider when none exists, so cleanup code could spawn a new runtime service while `[RuntimeServices]` was already being destroyed.

Fix:
Make `PresentationPreloadService.ReleaseAllActiveManifests(...)` pass a no-provider-creation mode into manifest apply helpers. Cleanup can still clear active manifest references and record completed operations, but it no longer creates `PresentationAssetProvider` during destruction.

Prevention:
`OnDestroy`, scene-close, and application-quit cleanup paths must use non-creating service lookups. Avoid properties or helpers named like `Current...` if they have `EnsureInstance()` fallback behavior.

## 2026-05-18 - Editor Preview Mutated Prefab Asset

Context:
Upgrade UI lake preview restoration ran during assembly reload and play-mode state changes, then Unity Inspector repeatedly failed with `EditorStyles.toolbarButtonRight` errors.

Cause:
`Resources.FindObjectsOfTypeAll<UpgradeTreeUI>()` returned `UpgradeTreeUI` components that live in Prefab Assets. The editor preview restore path called `UpgradeLakePresentation.Initialize(...)`, which attempted to create and parent a generated `LakeSurface` child under a Prefab Asset transform.

Fix:
Skip persistent Prefab Asset objects, unloaded scenes, and Prefab Stage objects in the upgrade lake editor preview loop and in `UpgradeTreeUI` editor preview/restore methods. Keep automatic material restoration no-create/no-initialize so assembly reload and play-mode transitions do not call `UpgradeLakePresentation.Initialize(...)`. Disable the automatic edit-mode lake preview callbacks by default; lake preview refresh is now a manual Inspector button action. Add defensive `UpgradeLakePresentation` guards so generated lake surface or ripple layers are not created under persistent Prefab Asset transforms.

Prevention:
Editor preview/update loops that use `Resources.FindObjectsOfTypeAll` must filter out persistent assets, unloaded scene objects, and Prefab Stage contents before mutating transforms, components, materials, or generated children. Automatic cleanup/restoration handlers for assembly reload, play-mode transitions, and `OnDisable` should restore only existing state; they must not create helper components or generated children.

## 2026-05-06 - CurrentTask Drift

Context:
Feature implementation continued while `Docs/CurrentTask.md` still described the old project memory system task.

Cause:
The document was treated as required reading but not as an actively maintained task contract.

Fix:
Update `Docs/CurrentTask.md` at the start of each active task change, and keep detailed progress in `Docs/SessionLogs/`.

Prevention:
Before implementation, confirm `CurrentTask.md` matches the user's current requested work. If it does not, update it first.

## 2026-05-14 - Chest World/UI Presentation Timing Conflation

Context:
While fixing chest first-open input blocking, the world `TreasureChest` open presentation lifetime was used to extend the wait before the chest UI opened.

Cause:
The GameObject open presentation and the chest UI first-open reveal were treated as one timing chain, even though the UI reveal timing is authored separately.

Fix:
Restore `TreasureChest` open-to-UI timing to its existing animator/fallback behavior, and keep input blocking inside the chest UI reveal presentation.

Prevention:
Do not use world object particle/effect lifetime to time chest UI reveal opening. When the issue is "UI reveal blocking", start and end blockers from the UI presentation owner.

## 2026-05-14 - Chest First-Open Blocker Gap

Context:
The chest UI reveal was blocked correctly, but the gap between `TreasureChest` GameObject interaction/open prelude and the later UI reveal still allowed V inventory and ESC pause input.

Cause:
The GameObject open presentation and UI reveal were kept separate for timing, but input blocking was also scoped only to the UI reveal owner.

Fix:
Acquire an external UI input blocker from `TreasureChest` immediately on first-open interaction, allow that owner to open only its intended chest UI, then hand off to the inventory/chest UI reveal blocker.

Prevention:
For first-open chest behavior, keep world and UI presentation timing separate, but treat them as one input-blocking sequence with explicit ownership handoff.

## 2026-05-14 - NPC Feature UI Opened Before Dialogue Blocker Release

Context:
After moving dialogue input blocking into `DialogueService` through `GameFlowInputBlocker`, the Upgrade NPC feature stopped opening its popup.

Cause:
`UpgradeFeature.Execute()` called `UpgradeManager.ToggleUI()` before requesting dialogue exit. The dialogue blocker was still active, so `UpgradeManager.OpenUI()` failed the `UIManager.CanOpenUI(...)` gate before its own open-presentation blocker could take ownership.

Fix:
Request dialogue exit first, then wait until dialogue playback and external UI input blockers are released before opening Upgrade UI.

Prevention:
NPC features that open stack UI after dialogue should not open the UI while dialogue is still the active game-flow blocker. End or hand off the dialogue flow first, then open the feature UI.

## 2026-05-16 - Shared Portal Prefab Carried Start-Run Semantics

Context:
After boss battle-end portals began using the common `BossBattleEndPrefabCatalogSO.portalPrefab`, runtime-spawned boss exit portals could not move to the next corridor.

Cause:
The shared `ScenePortal.prefab` carried `TransitionType.HubToRunStart` and a `RunRouteCatalogSO`. Boss exit portal instances inherited that start-run semantic instead of letting `PortalRouteManager` resolve `BossToCorridor` or `ReturnToHubAfterRun` from the active route.

Fix:
Keep shared portal prefabs semantic-neutral (`TransitionType.None`, no catalog), and put `HubToRunStart` plus `RunRouteCatalogSO` only on hub start portal scene instances. Boss battle-end now uses authored portal instances rather than catalog portal prefab creation.

Prevention:
Do not put route-start, boss-exit, or scene-specific transition semantics on a shared portal prefab that is used by multiple travel contexts. Author the semantic at the scene instance or through the active route resolver.

## 2026-05-17 - Boss BattleEnd Position Was Solved In Code Instead Of Authoring

Context:
Boss reward chest and exit portal placement needed fixed final positions after boss death.

Cause:
The implementation tried to solve fixed placement through runtime chest/portal spawning and anchor world-position capture. That kept placement policy in code and still depended on where helper anchors lived.

Fix:
Make chest and portal authored inactive scene objects. `BossBattleEndHandler` initializes/activates the authored chest and activates the authored portal. The object transforms are the placement source of truth.

Prevention:
For boss battle-end chest/portal placement, do not add spawn offsets, boss-position fallback, anchor capture, runtime prefab creation, or boss-prefab-owned placement components. Author the final objects in Unity and wire a scene `BossBattleEndHandler`.

## 2026-05-17 - Boss Physical Drops Were Removed With Placement Cleanup

Context:
While simplifying boss BattleEnd so chest and portal use authored inactive scene objects, the runtime magic stone and field-heal pickup spawning path was also removed.

Cause:
The cleanup treated every BattleEnd-created object as the same placement problem. Chest and portal are fixed authored objects, but magic stones and field heals are variable-count physical drops driven by `StageLootTable` and runtime modifiers.

Fix:
Keep chest and portal activation-only, but spawn boss magic stones and field heals as runtime physical pickups from the boss death position.

Prevention:
When removing runtime placement helpers, separate fixed authored result objects from variable-count physical rewards. Do not move magic stones or field heals into chest loot, and do not use authored chest/portal placement as the pickup origin unless the reward policy explicitly changes.

Follow-up:
Final-route chest suppression must follow the same split. Skipping the authored final-boss chest must not skip boss physical drops; only the chest activation/contents path is suppressed.

## 2026-05-17 - Player-Attached Objects Were Treated As Player Targets

Context:
Boss movement could react to a player-owned orbiting feather object even when the actual player was stationary.

Cause:
Shared enemy target acquisition and direct target assignment accepted child transforms as movement targets. Player-attached relic/effect colliders could therefore influence the target center used by boss movement.

Fix:
For `Player` targets, shared `Enemy` target acquisition and target assignment now resolve through `PlayerRuntimeRegistry`/`PlayerInteractor2D` and map player-owned child colliders/transforms back to the player root.

Prevention:
Player-attached objects should not carry unintended `Player` tags, hurtboxes, or body-blocking colliders. Movement targeting, damage targeting, and physics collision authoring must be reviewed separately for new player-attached relic/effect objects.

## 2026-05-17 - Shop Layout Duplicated Live Slot Objects

Context:
Shop maintenance was difficult because multiple scene `ShopSlot` objects were copied into layouts directly.

Cause:
The scene layout owned both slot positions and live slot behavior instances. A presentation change to `ShopSlot` could require duplicated scene updates.

Fix:
`MerchantNPC` can now use authored slot anchors plus a `ShopSlot` prefab reference, instantiate/reuse slots at runtime, and apply per-anchor stock filters.

Prevention:
For new shop layouts, author anchor transforms and slot filters instead of duplicating live `ShopSlot` objects. Keep the slot prefab as the central presentation/interaction source.

## 2026-05-17 - Locked Upgrade Nodes Could Not Explain Failure

Context:
Locked upgrade nodes had no click feedback for why the upgrade could not be purchased.

Cause:
`UpgradeSlotUI` disabled the button for locked nodes, so the click never reached `UpgradeManager.TryBuyUpgrade(...)` and the purchase failure reason could not be mapped to a warning popup.

Fix:
Locked upgrade buttons remain interactable while keeping the locked visual state. `UpgradeManager` maps purchase failure reasons to shared warning popup codes.

Prevention:
If a disabled-looking UI element needs to explain failure, keep a click path to the shared warning/tooltip system or add a separate explicit explanation trigger. Do not rely on `Button.interactable = false` when the user needs feedback.

## 2026-05-17 - Legacy Upgrade Panels Kept Horizontal Scrollbar State

Context:
The current upgrade panel prefab moved to overflow arrow navigation, but older scene instances still carried a serialized `ScrollRect.horizontalScrollbar` reference and a `Scrollbar Horizontal` object.

Cause:
The prefab was updated, but scene-level prefab overrides and old missing-prefab instances can preserve obsolete serialized UI references outside the representative prefab.

Fix:
`UpgradeTreeUI.ConfigureScrollRect()` now disables any legacy horizontal scrollbar before clearing the reference. `Tools/Validation/Scene Setup Validator` now checks inactive upgrade panels, reports missing overflow arrows, reports stale horizontal scrollbar references, and Auto Fix can detach/disable the stale scrollbar.

Prevention:
When replacing UI navigation authoring, validate both the representative prefab and scene instances. Do not treat a prefab-only serialized check as proof that old scene overrides were migrated.

## 2026-05-17 - Attached Particle Transform Did Not Guarantee Attached Simulation

Context:
Player heal recovery spawned `HealParticle` as a child of the player, but the visible particles stayed near the spawn position while the player moved.

Cause:
The implementation parented the spawned ParticleSystem transform but did not force attached heal playback to use local particle simulation before replaying the particle systems. For player-following presentation, Transform parenting alone is not enough if the ParticleSystem simulates emitted particles in world space.

Fix:
`PlayerHealParticlePlayback` now sets each spawned ParticleSystem `main.simulationSpace` to `ParticleSystemSimulationSpace.Local` before clearing and replaying it.

Prevention:
For VFX that must follow an owner after spawn, verify both the spawned transform parent and ParticleSystem simulation space. Parent-attached one-shot particles should set or author Local simulation unless world-trailing particles are explicitly desired.

## 2026-05-19 - Boss Encounter Presentation Did Not Own UI Input Blocking

Context:
ESC could still open pause/UI during boss encounter presentation windows, even though dialogue playback itself was blocked correctly.

Cause:
`DialogueService` owned the dialogue-only input block, while `BossEncounterDirector` and legacy `BossTalkManager` owned camera focus, transition wait, player cinematic protection, and timer pause without also owning a UI input blocker for the non-dialogue encounter windows.

Fix:
Boss encounter sequence owners now acquire a `GameFlowInputBlocker` for the full encounter presentation and release it on normal handoff, setup failure, disable, and coroutine interruption cleanup paths.

Prevention:
When a flow spans both dialogue and non-dialogue presentation, do not assume the dialogue blocker covers the whole flow. The outer flow owner must acquire its own `GameFlowInputBlocker` for camera/transition/handoff windows where unrelated ESC or UI opens must stay blocked.

## 2026-05-19 - World Text Readout Was Authored Without Canvas

Context:
The lobby training dummy damage record text did not appear during play review.

Cause:
The readout was authored as a 3D `TextMeshPro` MeshRenderer child using a `RectTransform` but no Canvas. Unity normalized the child RectTransform to the dummy center, and the text was not reliably visible as the intended above-dummy UI.

Fix:
`TrainingDummy.prefab` now owns a prefab-authored world-space Canvas with `TextMeshProUGUI`, and `TrainingDummyDamageReadout2D` targets `TMP_Text` so it can drive either UGUI or world TMP text safely.

Prevention:
For prefab-authored world UI labels, prefer a world-space Canvas plus `TextMeshProUGUI`, or use a plain Transform with 3D TMP deliberately. Do not rely on RectTransform positioning without a Canvas when the label must appear at a precise world offset.
