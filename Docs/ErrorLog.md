---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-05-23
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

## 2026-05-23 - Run-Special Teleport Waited Behind Paused FixedUpdate

Context:
A run-special same-scene teleport NPC could play the screen fade but fail to move the player.

Cause:
`RunSpecialNpcInteractor` pauses `Time.timeScale` during dialogue. `RunSameSceneTeleportNpcFeature` uses `MovementMotor2D.WarpTo(...)`, and that motor applies the queued warp in `FixedUpdate`. The teleport feature also waited on `WaitForFixedUpdate`, so executing it inside the paused dialogue window could leave the warp unapplied.

Fix:
Same-scene teleport opts into post-presentation execution. The interactor closes speech/letterbox/HUD presentation first, restores the run timer pause and `Time.timeScale`, keeps the input blocker/player talking state, then runs the teleport fade/warp/fade sequence.

Prevention:
Run-special features that depend on physics, `FixedUpdate`, or scaled game time must not execute inside the paused dialogue window. Add an explicit post-presentation execution policy for those features instead of relying on unscaled UI presentation timing.

## 2026-05-22 - PageCover Animator Overrode Reverse Sampled Disappear

Context:
The encyclopedia content disappear transition was implemented by sampling `ContentAppear` from the end of the clip back to the start, but the observed result still looked like the clip was playing forward.

Cause:
`PageCover` could carry an Animator with `ContentAppear` as its default state. Activating the PageCover object allowed that Animator to advance normally and overwrite the sprite value that `AnimationClip.SampleAnimation(...)` had just sampled in reverse.

Fix:
`EncyclopediaBookPresentation` now disables the optional `pageCoverAnimator` while it manually samples `contentAppearClip`, then restores the Animator enabled state after sampling or when the active presentation routine is stopped.

Prevention:
When a UI transition is manually sampled from an `AnimationClip`, disable any Animator on the sampled object for the sampling window. Otherwise the Animator and manual sampler both write the same properties and the visible result can follow the Animator instead of the requested sample time.

## 2026-05-22 - Runtime Self-Repair Hid Encyclopedia Authoring Gaps

Context:
The encyclopedia UI repeatedly looked wired enough to show partial data, but DimPanel/book animation, tab transitions, and item detail ownership kept breaking or becoming unclear.

Cause:
Runtime code and broad auto-wiring paths tried to find missing children or add missing components such as presentation helpers. That made missing prefab references look like a working runtime fallback and preserved legacy presenters on the active hierarchy longer than intended.

Fix:
Keep the active runtime path to serialized references: `EncyclopediaScreen -> EncyclopediaItemTab -> EncyclopediaItemLeftPage / EncyclopediaItemRightPage`. Runtime now logs missing required references instead of adding presenter/presentation components or silently rebuilding fallback bindings. The GlobalUIRoot wiring tool removes active legacy presenters and is the only repair path for the current prefab.

Prevention:
Do not solve authored UI wiring gaps with runtime `AddComponent`, broad `GetComponentInChildren` fallback during `Awake`/`OpenUI`, or duplicate legacy presenters. Add or fix the reference in Unity/Editor tooling, then let runtime validation warn when a required field is still missing.

## 2026-05-22 - OnValidate Auto-Wiring Dirtied Encyclopedia Authoring

Context:
After the encyclopedia Inspector/import issue, trying to save or close the Unity Editor kept producing new unsaved changes.

Cause:
Several encyclopedia presenter components used `OnValidate()` to call `ResolveReferences()`, and `EncyclopediaScreen.OnValidate()` also scheduled a delayed auto-wire that called `EditorUtility.SetDirty(this)`. Reference resolution writes serialized component fields and can add editor-time helper components, so Unity could mark the scene or prefab dirty again immediately after saving.

Fix:
Remove edit-mode auto-wiring from `OnValidate()`. Keep the same authoring convenience only in `Reset()` and the explicit `Auto Wire References` context menu so it runs when a component is added or when the author intentionally requests it.

Prevention:
Do not use `OnValidate()` for serialized-reference discovery, component adding, `SetDirty`, or delayed edit-mode mutation. Use explicit editor commands or one-time `Reset()` initialization for prefab/scene authoring helpers.

## 2026-05-22 - Duplicate Encyclopedia Item Detail Presenters

Context:
The authored encyclopedia `RightPage` had `EncyclopediaItemRightPage` on `RightPage` while the child `ItemDetailPanel` still carried the older `EncyclopediaDetailPanel`. Both exposed similar icon/title/story/stat/detail fields in the Inspector.

Cause:
The migration from the first fixed detail presenter to the `EncyclopediaItemTab -> EncyclopediaItemRightPage` structure left the legacy component on the active hierarchy as a fallback. That made it unclear which component owned item detail binding and allowed future wiring to target the wrong presenter.

Fix:
Make `EncyclopediaItemRightPage` the sole active Item RightPage presenter. The GlobalUIRoot wiring tool now keeps/adds the presenter on `RightPage`, removes duplicate child `EncyclopediaItemRightPage` components, and removes legacy `EncyclopediaDetailPanel` components under RightPage. The V1 builder no longer adds `EncyclopediaDetailPanel` for generated encyclopedia screens.

Prevention:
Do not keep fallback presenters on the same active authored UI hierarchy once the replacement presenter owns the flow. If a migration component must remain in source, hide it from Add Component and remove it from the current prefab/scene through UnityEditor API or Inspector work.

## 2026-05-22 - Corrupt Prefab Import Masqueraded As Inspector GUIStyle Failure

Context:
Unity repeatedly logged `Unable to use a named GUIStyle without a current skin` and `UnityEditor.EditorStyles.get_toolbarButtonRight()` while the Inspector was redrawing after the encyclopedia UI prefab work.

Cause:
The visible Console stack was a Unity Inspector cascade. `Editor.log` showed the earlier source error: `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaScreen.prefab` failed import with `Problem detected while importing the Prefab file`, `Broken text PPtr`, and many `Transform child can't be loaded` messages. Static YAML inspection found 153 local `fileID` references in that prefab that had no matching object definitions, and those 153 IDs matched the broken PPtr IDs in `Editor.log`.

Fix:
Do not keep the corrupt prefab under `Assets` while the Editor imports. Preserve it outside the Unity import tree for inspection, then regenerate the screen prefab through `EncyclopediaV1AssetBuilder` or rebuild it in Unity with authored references. Do not try to repair this class of damage with broad YAML regex patches.

Prevention:
When an all-Unity-internal Inspector GUIStyle stack persists after Library/layout reset, inspect `Editor.log` for the first import/serialization error before the GUIStyle cascade. For prefab YAML, run a local-reference scan that verifies every non-external `{fileID: ...}` has a corresponding `--- !u! ... &fileID` document before trusting the prefab.

## 2026-05-20 - Broad Regex Corrupted Unity Prefab YAML

Context:
The encyclopedia book presentation needed Animator components and clip/controller references on generated prefabs while Unity Editor was open, so Unity batchmode could not be run.

Cause:
A manual text replacement used a broad regex across Unity prefab YAML documents. It matched beyond the intended MonoBehaviour block, inserted Animator/BookPresentation data into unrelated objects, and left malformed fields such as duplicated serialized values and broken document separators.

Fix:
Stop manual YAML patching for this change and regenerate the affected prefabs through `EncyclopediaV1AssetBuilder` in Unity Editor or batchmode when the Editor is closed. The builder now creates the AnimatorController and AnimationClip assets through UnityEditor APIs.

Prevention:
Do not use cross-document regex replacements on Unity prefab/scene YAML. For Animator, AnimationClip, nested UI, TMP, and cross-prefab references, use UnityEditor APIs or a document-bound parser with explicit fileID ownership checks, then validate import in Unity.

## 2026-05-20 - TMP Font Asset Changed Without Shared Material

Context:
The encyclopedia prefab appeared to ignore the requested Galmuri9 font on existing screen text, especially RightPage detail text, even after the builder assigned a font asset for new TMP objects.

Cause:
Existing prefab TMP components still serialized the default TMP font asset and shared material GUIDs. The earlier correction only handled null/new font references and did not replace already-assigned default TMP font/material references. The slot prefab also had Galmuri9 `m_fontAsset` but no serialized Galmuri9 `m_sharedMaterial`.

Fix:
Replace both `m_fontAsset` and `m_sharedMaterial` references in encyclopedia prefabs with `Galmuri9 SDF.asset` and update the editor builder to assign `fontSharedMaterial = fontAsset.material` when creating TMP text.

Prevention:
When changing TMP font in prefabs or builders, verify both `m_fontAsset` and `m_sharedMaterial` GUIDs. Do not treat font replacement as complete when only null references or only `fontAsset` assignments were updated.

## 2026-05-20 - DemonKing EgoSword Dropped Loop Survived Boss Battle End

Context:
DemonKing could die while EgoSword was planted in the arena. The sword stayed visible and continued running its independent dropped VerticalStrike/CrossLaser pattern loop after the boss fight ended.

Cause:
The dropped sword cadence was owned by `EgoSwordActor`, but DemonKing death/battle-end cleanup only stopped the boss pattern runtime. `EgoSwordActor` also checked only `owner != null` and `state == Fixed`, so a dead or combat-inactive owner still allowed the side loop and active subpattern ability to continue.

Fix:
`DemonKingController` now sends an explicit battle-end cleanup to EgoSword from death and destroy paths. `EgoSwordActor` cancels active dropped subpattern tokens, stops the dropped loop, clears mask/aura/attached one-shot VFX, switches back to held state, and deactivates the sword until the next `AttachToOwner()` reactivates it. Dropped loop and subpattern cancellation checks now require `owner.IsCombatActive` and `!owner.IsDead`.

Prevention:
Boss-owned side actors with independent timers must have an owner death/battle-end cleanup entry point and must include owner death/combat-active checks in wait loops, activation gates, and active ability cancellation checks. Do not rely on the boss main pattern abort path to stop side actor coroutines.

## 2026-05-20 - Inactive Tilemaps Leaked Into Runtime Queries

Context:
Construction sites now switch between inactive `BlockedState` / `OpenState` tilemap roots. Existing safety and drop code used `FindObjectsOfType<Tilemap>(true)`, so inactive open-ground or blocked-wall tilemaps could be discovered before their construction state was active.

Cause:
The search intentionally included inactive objects but did not filter `isActiveAndEnabled` and `activeInHierarchy` before treating a tilemap as runtime ground or hazard data.

Fix:
Filter inactive tilemaps in runtime tilemap scanners. Construction site modules also refresh safety tilemap caches after toggling state and register completed open-ground tilemaps with scene pathfinders.

Prevention:
When a system uses inactive scene authoring roots, any runtime scan that passes `includeInactive: true` must immediately filter inactive components before using gameplay data from those objects.

## 2026-05-20 - DemonKing Inspector Used Stale Witch GAS Assets

Context:
DemonKing battle logic was using GAS at runtime, but the DemonKing scene Inspector still showed Witch `AbilityDefinition` references in `Phase Data` and `AbilitySystem.initialAbilities`.

Cause:
DemonKing patterns had been implemented as runtime-created `AbilityDefinition` / `AbilityLogic` instances inside `DemonKingController.ConfigureRuntimePatternsIfNeeded()`. That made play mode work through runtime registration, but left copied Witch serialized references visible in the scene and made the Inspector an unreliable source of truth.

Fix:
Create persistent `AD_DemonKing_*` and `AL_DemonKing_*` assets, wire the DemonKing scene phase list to those assets, clear `AbilitySystem.initialAbilities`, and turn off `configureRuntimePatternsOnStart` for the scene. `DemonKingController` now binds special pattern roles from authored phase abilities when runtime generation is disabled, while keeping runtime generation as a fallback when no phases are authored.

Prevention:
For boss patterns that should be inspected or tuned in Unity, create persistent AbilityDefinition/AbilityLogic assets and put them in `BossPhaseConfig`. Do not leave copied boss ability references in a scene just because a runtime fallback currently overwrites them. Keep `AbilitySystem.initialAbilities` empty for boss phase abilities unless a separate non-phase ability must be granted at startup.

## 2026-05-20 - EgoSword Dropped Subpatterns Bypassed GAS

Context:
EgoSword dropped VerticalStrike and CrossLaser were intended to be DemonKing patterns with a separate cadence from the main boss pattern timer, but they were implemented as direct `EgoSwordActor` coroutines with no authored AbilityDefinition/AbilityLogic assets.

Cause:
The separate dropped-sword timer was treated as a reason to keep the attacks outside GAS, instead of splitting "timing owner" from "pattern execution owner". That made the attacks less inspectable and bypassed the AbilitySystem cancellation/presentation lifecycle used by DemonKing patterns.

Fix:
Create persistent `AD_DemonKing_EgoSwordVerticalStrike` / `AD_DemonKing_EgoSwordCrossLaser` and matching AL assets, register them on `DemonKingController` as `ParallelIndependent` subpattern abilities, and make `EgoSwordActor` request GAS activation from its independent dropped-pattern loop.

Prevention:
When a boss-owned attack has an independent cadence, keep the cadence runner separate but still execute the attack through GAS. Use `ParallelIndependent` instant abilities for side-patterns that must run while the main boss FSM pattern queue may be busy.

## 2026-05-20 - Behavior Tree Base Was Not Serializable

Context:
After script reload, Unity logged `The type Assembly-CSharp ActivateGASAbilityAction is being serialized by [SerializeReference], but its parent type Assembly-CSharp AIAbilityBridgeActionBase is missing the [Serializable] attribute`, followed immediately by repeated Inspector `Unable to use a named GUIStyle without a current skin` and `EditorStyles.toolbarButtonRight` errors.

Cause:
Concrete Unity Behavior nodes were marked `[Serializable]`, but the shared abstract `AIAbilityBridgeActionBase` and `AIAbilityBridgeConditionBase` classes in the same SerializeReference inheritance path were not. The Unity serializer warning occurred during Inspector rebuild after domain reload, then Unity's internal `PropertyEditor` tried to initialize named GUIStyles before a current skin was available.

Fix:
Mark the shared Behavior Tree action/condition base classes `[Serializable]`. They do not add serialized fields, so this only satisfies Unity's serialization chain requirement.

Prevention:
For Unity Behavior nodes that expose `[SerializeReference]` / `BlackboardVariable` fields, keep the full node inheritance chain serializable, including abstract project base classes. When an Inspector `EditorStyles` stack is all Unity-internal, check the first Console or `Editor.log` message before the GUIStyle cascade.

## 2026-05-19 - Pattern State Animation Was Treated As Spawned VFX

Context:
DemonKing PierceCombo and EgoSword VerticalStrike VFX were visible, but their ownership did not match the authored effect intent.

Cause:
`DemonKingStab` was scaled across the whole PierceCombo path as if it represented the hitbox lane, even though the intended visual is an effect attached to the boss during the lunge. `EgoSwordAttackAura` was also built and spawned as a child VFX prefab even though it is the EgoSword's own attack state animation.

Fix:
PierceCombo now attaches `DemonKingStab` to the boss transform and suppresses the path-covering attack primitive for that pattern. `EgoSwordActor` plays `EgoSwordAttackAura` through its own SpriteRenderer/Animator as `Start` then `Idle`, and the generated aura prefab was removed.

Prevention:
Before wiring new boss sprites, decide whether the asset represents an external hit VFX, a hitbox/telegraph visualization, or the actor's own state animation. State animation should be driven by the owning actor's Animator, while hitbox geometry remains separate gameplay logic.

## 2026-05-19 - Animator Import And State Validation Hid DemonKing VFX

Context:
Non-laser DemonKing/DarkLord VFX prefabs existed and were loaded through `Resources/DemonKing/Vfx`, but Explosion, Impact, Stab, Slash, GroggyRelease, and EgoSword attack visuals did not appear in play tests.

Cause:
The generated AnimatorController YAML had `m_TimeParameter:` and the next Unity document separator on the same line, e.g. `m_TimeParameter:--- !u!...`. Unity can fail to import or play those malformed controllers, leaving Animator-driven sprite VFX invisible even though the prefab path is correct.

A follow-up failure came from runtime validation: `Animator.HasState(...)` was checked with only the short state name hash such as `Play`. Unity state lookup can require the layer-qualified hash such as `Base Layer.Play`, so valid controllers were treated as invalid, the authored VFX object was destroyed, and the primitive fallback ran instead. With domain reload disabled, a prior failed `Resources.Load(...)` could also leave a cached `null` prefab until play mode restarted.

Another follow-up was that manually authored `.anim` / `.controller` files could appear empty in the Unity Inspector even when their text YAML looked populated. This made the runtime path keep falling back because the Editor import result, not the raw YAML text, is what matters.

Fix:
Repair the controller YAML so `m_TimeParameter:` is its own blank scalar line and each `--- !u!...` separator starts a new YAML document. Runtime validation now resolves both short and layer-qualified Animator state hashes before rejecting a controller. DemonKing VFX prefab cache is cleared through `SubsystemRegistration`, and failed `Resources.Load(...)` results are not cached.

Add an Editor-only DemonKing VFX asset builder that creates or repairs AnimationClips, AnimatorControllers, and prefabs through UnityEditor APIs from the original DarkLord sprite sheets. It runs only when the imported VFX assets are missing or invalid, and it can also be triggered manually from `Tools/DemonKing/Rebuild Pattern VFX Assets`.

Prevention:
When manually editing Unity `.controller` YAML, run a text check for `:--- !u!` or `m_TimeParameter:---` before play testing. For sprite-sheet VFX, keep playback through AnimatorController states, but validate Animator states with the same layer-qualified names Unity uses internally. Prefer UnityEditor API generation for `.anim` and `.controller` assets if the Inspector shows empty curves/states. Do not cache missing `Resources` prefabs as durable failures during iteration.

## 2026-05-19 - Affection Reward UI Waited Behind Dialogue Blocker

Context:
Dialogue could stop after affection gain when the affection change unlocked a reward.

Cause:
`AffectionRewardProcessor` passed the dialogue continuation callback into `RewardDisplayService.ShowReward(...)`. During dialogue, `DialogueService` owns an external UI input blocker, so `RewardDisplayService` could not open `RewardDisplayUI` and left the request queued. Dialogue was waiting for the reward close callback, while the reward UI was waiting for dialogue's blocker to release.

Fix:
`RewardDisplayService` now has a flow-owned reward request path that can open `RewardDisplayUI` as part of the active dialogue/encounter flow while unrelated UI openings remain blocked. `AffectionRewardProcessor` keeps the dialogue continuation callback attached to the reward close callback, so dialogue resumes only after the player closes the reward UI. If the reward view is missing, the service logs a warning and invokes the callback as a fallback so dialogue does not remain stuck.

Prevention:
Do not detach a dialogue continuation from the reward popup just to avoid an input-blocker deadlock. If the popup is part of the current flow, use an explicit flow-owned UI handoff; if the view is missing, invoke a fallback continuation with a warning.

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

## 2026-05-20 - Disable Cleanup Started Presentation Coroutine On Inactive Object

Context:
Exiting play mode or disabling the encyclopedia stand logged `Coroutine couldn't be started because the the game object 'EncyclopediaStand' is inactive!`.

Cause:
`EncyclopediaInteractable.OnDisable()` reused the normal unhighlight path, which asked `BookWorldSpriteSequencePresentation.PlayClose()` to start the close coroutine after Unity had already made the object inactive.

Fix:
Disable cleanup now clears the outline and calls `SnapClosed()`. The book sprite presentation also guards public play methods so inactive calls apply a static sprite instead of starting a coroutine.

Prevention:
`OnDisable()` cleanup should stop/snap authored presentation state and release references; it should not replay highlight, close, fade, or idle animations that depend on `StartCoroutine()` on the disabled object.

## 2026-05-20 - Speech Bubble Cleanup Used C# Null-Conditional On Destroyed Unity Object

Context:
Disabling a run-special NPC flow logged `MissingReferenceException: The object of type 'SpeechBubble' has been destroyed but you are still trying to access it`, from `SpeechBubbleComponent.HideActive()` into `SpeechBubble.StopActiveTweens()`.

Cause:
`SpeechBubbleComponent` stored a pooled `SpeechBubble` reference and called `activeBubble?.Hide()`. Unity destroyed objects are not real C# nulls, so the null-conditional operator still invoked `Hide()` on a destroyed `UnityEngine.Object`.

Fix:
Active bubble cleanup and line-skip access now pass through a helper that uses Unity's overloaded null check and clears destroyed references. `SpeechBubble.Hide()` also exits early when invoked on a destroyed instance, and run-special cleanup avoids null-conditional calls on Unity component references.

Prevention:
Do not use `?.` as a lifecycle guard for `UnityEngine.Object` references in teardown, pooling, or delayed callback paths. Use explicit `if (object != null)` checks so Unity fake-null destroyed objects are treated as gone before calling instance methods.

## 2026-05-22 - Pooled Ability Preview Kept Ignored Layout State

Context:
After viewing or switching a variant-capable encyclopedia weapon, later normal weapons could display two skill rows overlapped in one `Panel_AbilityBlock_Encyclopedia` area.

Cause:
The variant switch preview used a pooled ability-block instance and set its `LayoutElement.ignoreLayout` to `true` so it could animate outside the normal vertical layout. When that same instance was later reused as a normal skill row, the ignored-layout state and preview presentation state were not reset.

Fix:
`WeaponAbilityBlockView` now has a reusable pooled-state reset path that restores switch guide, preview mute, `CanvasGroup`, and `LayoutElement.ignoreLayout`. `EncyclopediaItemRightPage` calls it before reusing or hiding pooled ability blocks.

Prevention:
Any UI pooled object that temporarily opts out of parent layout, changes draw order, or changes preview alpha/color must restore those states before returning to the pool. Do not assume `SetActive(false)` resets `LayoutElement`, `CanvasGroup`, or graphic color state.

## 2026-05-22 - Encyclopedia Book Animation Was Bypassed By Missing Presentation Dependency

Context:
Tome interaction and Item sub-tab switching displayed data but did not play the expected DimPanel fade, `BookOpen`, or `BookLeftPage` animations.

Cause:
The authored `GlobalUIRoot` encyclopedia screen had no persisted `EncyclopediaBookPresentation` component/reference, so screen open and tab switching fell back to immediate content binding. The DimPanel path also assumed a `CanvasGroup`; a plain `Image`/`Graphic` DimPanel could be treated as effectively unavailable for animated fade. Generic child Animator lookup could also pick slot/button animators before the actual Book/Tome animator.

Fix:
`EncyclopediaScreen` can add the missing `EncyclopediaBookPresentation` in edit mode and at runtime as a component-only self-repair, and the safe GlobalUIRoot wire tool now adds/wires it explicitly for persistent prefab repair. `EncyclopediaBookPresentation` fades either `CanvasGroup` or `Graphic` DimPanels and prefers Book/Tome/EarthTome named animators or animators with `BookOpen`, `BookClose`, `BookLeftPage`, or `BookRightPage` states/clips.

Prevention:
Treat book presentation as a required authored dependency for animated encyclopedia screens. Do not rely on content binding to prove the presentation path is wired. When inspecting missing DimPanel behavior, check both `CanvasGroup` and plain `Image` authoring, and verify the selected Animator is the book Animator rather than a slot/button animator.

## 2026-05-22 - Encyclopedia DimPanel Leaked Because Book Was Treated As Screen Root

Context:
Play mode could start with `EncyclopediaUI` active and only `DimPanel` visible, making the game screen look black even though the book/content appeared closed.

Cause:
The implementation treated the child `Book` object as the screen active boundary. `DimPanel` is authored under `EncyclopediaUI`, so closing/deactivating `Book` did not necessarily deactivate sibling UI objects.

Fix:
`EncyclopediaScreen` now owns a serialized `screenActiveRoot` that resolves to `EncyclopediaUI`. Opening activates that root, while runtime startup close and `CloseUI()` deactivate it.

Prevention:
For authored popup roots with sibling presentation objects, define the active boundary at the popup root, not at one animated child. Dim panels should be covered by the same root lifecycle as the rest of the popup.
