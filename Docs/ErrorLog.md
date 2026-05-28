---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-05-28
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

## 2026-05-28 - Run Route Catalog Made Normal Bosses Look Like Final Context

Context:
Normal reward bosses should run as the default route flow three times before the single final boss, but direct boss-scene testing made normal bosses appear to use final-route behavior or an incomplete route context.

Cause:
`RunRouteCatalog.asset` had only one normal stage and only the Dragon Spili route in `normalRouteSets`, while `SceneDomainCoordinator` seeded direct scene Play with only the current RouteSet. A single-stage development plan makes the current boss both first and last, so final-route checks and boss-exit routing can be misleading during direct scene tests.

Fix:
Restore `RunRouteCatalog.asset` to three normal RouteSets (`Shadow`, `Dragon Spili`, `Slime Queen`) plus `DemonkingRouteSet` as final. `PortalRouteManager.SeedDevelopmentPlan(...)` now builds the deterministic full catalog plan for direct Play and sets `currentStageIndex` to the tested RouteSet. `BossEncounterEndDirector` debug logging now reports RouteSet, `IsFinalRouteSet`, active-plan state, stage index, and catalog.

Prevention:
Treat NormalRoute as the default boss reward path and FinalRoute as the special one. When testing a boss scene directly, verify the route context with `BossEncounterEndDirector.logDebug` before diagnosing reward suppression. Keep `RunRouteCatalog.normalStageCount`, normal RouteSet list, and final RouteSet covered by the boss reward reveal validator.

## 2026-05-28 - Boss Reward Portal Reveal Sub-Effects Used Split Timing

Context:
The boss reward portal reveal looked unsynchronized: SpriteMask reveal, dust particles, portal root tremble, and camera shake did not feel tied to the same rise timing.

Cause:
Portal reveal sub-effects were triggered from separate moments in the routine. Loop and burst dust both played at reveal startup, root tremble used global time for its oscillation phase, and scene ParticleSystems authored under the moving reveal root could inherit the lowered root motion instead of staying at the authored dust output point.

Fix:
`BossRewardObjectRevealPresentation` now uses one reveal timeline. The mask and collider lock span the reveal. Loop dust starts with the reveal and stops at completion, burst dust plays only at completion, loop camera shake and root tremble use the reveal normalized envelope, and root tremble uses reveal elapsed time for its phase. Particle prefab spawn poses are resolved from the final reveal-root position, and scene loop ParticleSystems under the moving reveal root are temporarily detached/restored during playback.

Prevention:
When adding portal reveal polish, route it through the reveal timeline instead of firing it from independent setup or completion code. Effects that should live during the rise should use reveal normalized progress; effects that should punctuate arrival should run after the final reveal progress is applied. Do not let ground dust inherit the lowered gate root unless the intended effect is attached to the moving gate itself.

## 2026-05-28 - Boss Reward Reveal Used Mask And Particle References From The Wrong Scope

Context:
In `HeoMinSeok_Boss`, the reward chest did not visibly activate after boss defeat, and the reward portal's mask/particle reveal did not play as expected.

Cause:
The chest reveal was dust-only but still had `maskedRenderers` serialized from component reset, with no `revealMask`. Runtime reveal therefore set the chest renderer to `VisibleInsideMask` without a local mask, making it appear invisible. The portal reveal mask was parented under the same portal root used as `revealRoot`, so the mask moved with the portal and could not clip a bottom-up reveal. The dust arrays also referenced prefab asset ParticleSystem components instead of scene instances.

Fix:
Chest reward dust now belongs to `TreasureChest.PlayRewardReveal()` with one serialized dust ParticleSystem plus spawn anchor/offset. `BossRewardObjectRevealPresentation` remains for portal gate reveal behavior only: mask interaction, root rise, particles, collider lock, global mask isolation, camera shake, and local tremble. The boss reward reveal apply tool now removes stale chest reveal components, serializes `TreasureChest` reward dust fields, clears serialized gate masks for runtime generation, and validation reports stale chest reveal components or portal mask wiring mistakes.

Prevention:
For reward reveal authoring, do not put `BossRewardObjectRevealPresentation` on reward chests. Configure chest dust on `TreasureChest`. Keep mask/masked-renderer wiring only on portal gate reveals. Gate masks must stay outside the transform that moves during reveal, or leave the mask field empty so runtime can generate one from the target renderer. Particle fields may reference either scene ParticleSystem instances or prefab ParticleSystem assets; prefab references need a spawn anchor/offset that represents the intended reveal dust position.

## 2026-05-28 - Dialogue Boss Silhouette Ignored First Face Tag

Context:
`DarkLord_Tutorial` authored the first Ink line with `# face:Tutorial`, but the boss dialogue silhouette still appeared with the `Normal` portrait shape.

Cause:
The Dialogue opening sequence played the boss portrait/silhouette intro before the first Ink line was consumed. The `face` tag was processed only after the intro completed and the first line began, so the silhouette setup had already chosen the `Normal` sprite.

Fix:
`DialogueController` now previews the first playable Ink line through a cloned `Story` state, extracts the matching `face` tag without advancing the real story, and passes that label through `DialoguePresentationSequencer` / `CinematicDirector` to the portrait intro. `PortraitController.SetupSilhouetteMode(...)` can now set the silhouette sprite from that label.

Prevention:
When a presentation intro depends on Ink tags from the first line, preview the tag state before opening presentation instead of waiting for normal line playback. For boss silhouette variants, author the first playable line with a valid `face` label and verify that the assigned `SpriteLibraryAsset` contains the same label.

## 2026-05-28 - Global SpriteMask Clipped Reward Portal Reveal

Context:
`HeoMinSeok_Boss` uses `GlobalVisionMaskRoot` for its boss gimmick. The reward portal also uses a local `SpriteMask` reveal so the gate can rise from below after boss defeat.

Cause:
Unity `SpriteRenderer.maskInteraction` responds to every `SpriteMask` whose sorting range includes the renderer; it does not target one specific mask. When the reward portal renderer is set to `VisibleInsideMask` for the local reveal, the global player vision mask can also clip it.

Fix:
`BossRewardObjectRevealPresentation` now has `isolateGlobalVisionMasksDuringReveal`, defaulted on for reward reveals but only applied when a local reveal mask or masked renderers exist. While the gate reveal is active, it stores active `GlobalVisionMaskController` child mask ranges, narrows those masks to the dark overlay renderer's sorting range, and restores the original ranges on completion, stop, or disable. The target-scene apply tool enables this option for portal reveal components, and validation reports it as required in global vision mask scenes.

Prevention:
For any future scene that combines a local reward gate SpriteMask reveal with `GlobalVisionMaskRoot`, keep global vision mask isolation enabled and verify both the dark overlay cutout and the portal reveal in Play Mode. Do not assume `VisibleInsideMask` means "inside only my local reveal mask."

## 2026-05-28 - Global Ending IntroOverlay Appeared On Play Start

Context:
The `IntroOverlay` authored under `GlobalUIRoot` for the ending outro could appear immediately when entering Play Mode if the prefab or scene instance was left active for authoring.

Cause:
`GlobalUIRoot` carries the authored `EndingOutroView`, but not necessarily an `EndingOutroPlayer` on the same active object. The player-owned `hideViewOnAwake` path therefore does not cover a globally authored view object that starts active.

Fix:
`EndingOutroView.Awake()` now snaps the view hidden when it is active due to scene/prefab authoring. `EndingOutroView.Show(...)` marks its own activation so the first real outro playback can activate an inactive authored view without the `Awake()` startup hide immediately turning it off again.

Prevention:
Presentation view roots that can live under persistent global UI should own a safe runtime-closed startup state, even if a separate player component also hides them. Do not rely only on scene/prefab inactive authoring for fullscreen overlays that may be toggled on during editing.

## 2026-05-28 - Ink Rich Text Hex Color Split Into Tag

Context:
DarkLord tutorial Script 1 tried to color the hidden `???` laugh line purple with TMP rich text, but Dialogue showed literal `<color=` instead of colored text.

Cause:
Ink treats `#` inside a line as the start of an Ink tag. The source line used `<color=#A855F7>...`, so the compiled JSON split the visible text at `^<color=` and stored `A855F7>...` as a tag instead of body text. The Dialogue typewriter also used `DOText`, which can expose partial rich-text tags while typing.

Fix:
Use a named TMP color tag (`<color=purple>...`) for the tutorial line and update the generated TextAsset JSON to match. `DialogueView` now sets the full rich text on TMP first and animates `maxVisibleCharacters`, so rich-text tags are never revealed as partial body text during typing.

Prevention:
Do not put raw `#RRGGBB` rich-text color values directly in Ink body text unless the `#` is confirmed to survive Ink compilation. Prefer named TMP colors or verify the generated JSON contains a single visible text token with the full rich-text tag.

## 2026-05-28 - Dialogue Reward Opened Under Suppressed Reward And Hover Canvases

Context:
Completing an affection reward during Dialogue could log `Coroutine couldn't be started because the game object 'RewardPanel' is inactive!` from `RewardDisplayUI.OpenUI()`. After the reward canvas was restored, hovering a reward slot could also log the same coroutine error for `ItemDetailPanel` because `HoverCanvas` was still inactive.

Cause:
Dialogue non-dialogue UI suppression hides both `GlobalCanvasLayer.Reward` and `GlobalCanvasLayer.Hover` while Dialogue is playing. Affection rewards are flow-owned UI that can intentionally open before Dialogue fully exits, so `UIManager` accepted the reward panel while its parent `RewardCanvas` was still inactive from suppression. Reward slots then use the normal inventory hover path, which needs `HoverCanvas` to be active before `ItemDetailPanel` starts its open presentation coroutine.

Fix:
`DialogueService` now lets a flow mark a captured suppressed layer as temporarily visible so an in-progress suppression fade will not immediately hide it again. `RewardDisplayUI.OpenUI()` uses that boundary to reactivate the authored Reward and Hover canvas roots before enabling the reward panel. Reward is restored as interactable/raycasting, while Hover is restored visible but non-raycasting. When the reward popup closes while Dialogue is still playing, the reward owner hides both canvas roots again so the dialogue suppression window remains intact.

Prevention:
When an overlay can be opened as part of the same flow that suppressed its canvas layer, the overlay owner must restore every authored canvas root it needs before starting coroutines or UI animation. Do not assume `UIManager.PushUI()` activates inactive parent canvas roots, and remember that reward item slots depend on the shared Hover layer.

## 2026-05-28 - Terminal Outro View Destroyed With Duplicate GlobalUIRoot

Context:
Entering the DemonKing boss scene from Hub or Corridor could complete the death SpeechBubble and Dialogue handoff but skip the ending outro.

Cause:
The DemonKing scene's `EndingOutroPlayer` could hold a serialized `EndingOutroView` reference from that scene's `GlobalUIRoot` prefab instance. When the scene was reached from another playable scene, a persistent `GlobalUIRoot` already existed, so the destination scene's duplicate `GlobalUIRoot` destroyed itself and invalidated the serialized outro view reference before playback.

Fix:
`EndingOutroPlayer` now resolves a live `EndingOutroView` from the current `GlobalUIRoot` before playback when its serialized view is missing or not ready, and `BossDefeatEndingSequence` revalidates the playable outro player immediately before starting the outro.

Prevention:
For presentation UI authored under persistent global roots, validate cross-scene entry, not only direct scene play. Any terminal flow that references global UI children should re-resolve live `GlobalUIRoot` objects after scene load and duplicate-root cleanup instead of trusting stale scene-instance references.

## 2026-05-28 - Tutorial Direct Portal Dropped Weapon Runtime State

Context:
Using the tutorial gate from `TutorialCorridor` into `DarkLord_Tutorial` could leave the spawned player without the tutorial weapon.

Cause:
`TutorialScenePortal` intentionally bypassed the normal `ScenePortalTravelService` route plan, but that also bypassed the player runtime-state capture used by normal scene portals. `DarkLord_Tutorial` then spawned a fresh player prefab and the restore bootstrapper had no pending weapon inventory state to apply. A later source fix added preserve flags, but existing scene instances did not serialize those new fields, so they could still deserialize as `false` and skip capture. After capture was restored, the tutorial default weapon still could not be restored because `Weapon.ApprenticeHeroSword` was assigned in the scene bootstrap but missing from `ItemDatabase.allWeapons`, so `PlayerRuntimeResolverBridge.ResolveWeapon(...)` could not map the captured id back to a `WeaponDefinition`.

Fix:
`TutorialScenePortal` now captures the current player's `PlayerRuntimeState` through `PlayerRuntimeCaptureBridge`, stores it in `GamePlayDataManager`, and prepares a minimal pending transition context before starting the direct scene load. If load acceptance fails, it restores the previously pending state/context. The runtime now preserves by default without relying on newly added serialized true values; only the explicit inverse `resetPlayerRuntimeStateOnTravel` option disables capture. `WD_ApprenticeHeroSword` is also registered in `ItemDatabase.allWeapons` so the captured tutorial weapon id can be resolved during destination restore.

Prevention:
When adding direct tutorial scene travel, check whether the destination depends on spawned-player inventory, relics, consumables, or ability runtime state. Direct scene travel must either capture runtime state explicitly or deliberately reset the destination loadout. Avoid adding positive serialized bool defaults to existing scene components when a stale false value would break critical runtime preservation. Any weapon that can be saved/restored by id must be present in the active `ItemDatabase.allWeapons`, even if it is tutorial-only and not default-unlocked.

## 2026-05-28 - Hub Intro Draft Ink Opened Empty Dialogue

Context:
The DarkLord-to-Hub intro reached the Dialogue UI, but the dialogue body was empty during the temporary MSUpgradeNpc Hub intro test.

Cause:
The temporary Hub intro JSON stored each line under a named Ink knot such as `HUB_INTRO_JUNK`, while `HubIntroAfterDarkLordSequence` had blank `dialogueStartPath` fields in the saved Hub scene. Ink therefore started at the generated root, found only `done`, and opened/closes the Dialogue presentation without yielding visible text.

Fix:
The Hub intro authoring tool now wires the temporary draft start paths and validates that draft JSON assets use their matching knot. The temporary JSON files also mirror their line at root so already-saved blank start paths can still show placeholder text after Unity imports the updated TextAssets.

Prevention:
When creating temporary Ink JSON by hand, either put playable content at root or wire the exact `startPath` used by the runtime caller. Validation must cover the asset/start-path pair, not just that a TextAsset reference exists.

## 2026-05-28 - DemonKing Final Threshold Waited For Groggy Exit

Context:
Dropping DemonKing to the 10% FinalDesperation threshold while he was already Groggy left him in the invulnerable-looking Groggy state until the Groggy duration ended, then started the 10% pattern.

Cause:
The health gate reserved FinalDesperation, but the active `State.Status.Groggy` tag still drove the boss FSM back into `BossGroggyState` on the next reactive transition. The forced start also reused normal pattern evaluation, which can still be affected by stale blackboard HP/selection gates.

Fix:
FinalDesperation now marks the final phase started, ends active Groggy effects/tags immediately, and reserves the final pattern through a forced pattern reservation that bypasses normal selection gates at execution start.

Prevention:
Terminal threshold patterns that must preempt reactive states should explicitly clear or override the reactive state tag and should not depend on ordinary AI pattern selection/evaluation gates.

## 2026-05-28 - DarkLord Tutorial Relied On Scene-Only HUD And Camera Authoring

Context:
Entering `DarkLord_Tutorial` from `TutorialScene` could still show the normal Gameplay/Boss HUD and could jump straight to the boss focus instead of first showing the spawned player.

Cause:
The previous fix deactivated HUD objects in the `DarkLord_Tutorial` scene instance, but the actual `GlobalUIRoot` can persist from the previous scene through `DontDestroyOnLoad`. Scene YAML authoring for inactive HUD roots therefore did not cover runtime entry. The sequence also started the first boss focus immediately after player registration, so scene transition fade-in could hide the player-facing starting beat.

Fix:
`TutorialBossEncounterSequence` now snapshots and hides the runtime `GlobalUIRoot` Gameplay/Boss HUD canvas roots and default HUD component roots at sequence start, restores that snapshot when the scene unloads or the sequence is canceled, waits for the active transition fade to finish, frames the player for the initial beat, then moves to the boss focus. The tutorial fake game-over request also carries a tutorial-only return button label.

Prevention:
For tutorial scenes reached through runtime scene transitions, validate both scene-authored objects and persistent UI/service state. Do not assume inactive objects saved in the destination scene will affect an already-loaded `DontDestroyOnLoad` UI root, and do not start camera focus beats until the transition fade is no longer active.

## 2026-05-28 - Scene Fade Image Rendered Behind Ending Outro

Context:
The DemonKing terminal ending outro reached its final slide and waited for the configured slow title transition duration, but the visible black fade did not appear before `TitleScene` loaded.

Cause:
The ending outro was authored as a later child under the same `FadeInOutCanvas` that owns the shared `SceneFadeTransitionService` `FadeImage`. The fade coroutine was increasing the fade image alpha, but Unity rendered that image behind the outro because of sibling order.

Fix:
`SceneFadeTransitionService` now moves the configured overlay root to the last sibling whenever it is activated, so authored UI inserted into the same fade canvas cannot sit above the black transition image.

Prevention:
For shared transition overlays, validate both canvas sorting and sibling order. A correct fade duration and alpha curve do not prove the fade is visible if the overlay image is below scene-authored presentation UI.

## 2026-05-28 - Scene Fade Canvas Rendered Behind Destination Scene UI

Context:
The DemonKing terminal ending could load `TitleScene`, but the requested post-load TitleScene fade-in did not visibly play.

Cause:
The active fade image sibling order was corrected inside its own `FadeInOutCanvas`, but the destination scene can load new canvases after the transition starts. The persistent `GlobalUIRoot` fade canvas uses normal overlay sorting, so the old active fade image can continue changing alpha behind newly loaded TitleScene UI.

Fix:
`SceneFadeTransitionService` now elevates the active overlay's parent canvas to the maximum sorting order while a fade is running, repeats that elevation during fade frames after scene load, and restores the saved canvas sorting state when the transition or overlay fade session ends.

Prevention:
For scene-load fade-in bugs, validate the active transition canvas against destination-scene canvases, not only the fade image alpha and sibling order. A `DontDestroyOnLoad` overlay must be re-promoted after `LoadSceneAsync` because the destination scene can introduce newer or higher-sorted canvases.

## 2026-05-28 - Deferred Fade Replacement Promoted A Destroyed Owner

Context:
After the DemonKing terminal ending loaded `TitleScene`, Unity threw a `MissingReferenceException` from `SceneFadeTransitionService.PromotePendingReplacementIfAvailable()` when `EndTransitionSession()` tried to destroy the old fade service owner.

Cause:
The scene-load coordinator can still hold a `SceneFadeTransitionService` reference after Unity has destroyed that service object during `LoadSceneMode.Single`. The deferred replacement path then promoted the TitleScene service but accessed `gameObject` on the destroyed old owner. In the same state, the post-load fade-in can be skipped because the old owner's overlay references are destroyed.

Fix:
`SceneFadeTransitionService` now destroys service owners through a helper that first respects Unity's destroyed-object null check. `SceneTransitionCoordinator` also re-resolves a replacement fade service after the scene load if the original service has been destroyed, begins a recovered transition session, snaps it to black, and then runs the configured fade-in on the replacement.

Prevention:
After `LoadSceneAsync`, do not assume a cached `UnityEngine.Object` service reference is still alive. Re-check Unity null semantics before post-load fade work, and recover through the destination scene's authored service when the original transition owner was destroyed.

## 2026-05-28 - Tutorial Presentation HP Canvas Used Stale Authoring

Context:
`DarkLord_Tutorial` had a valid `TutorialPresentationHpView`, heart slots, and laser references, but the fake HP UI did not appear during the laser sequence.

Cause:
The saved scene had an older generated `TutorialPresentationHpCanvas` setup: the HP references were wired, but the canvas/root transform and sorting authoring were not normalized and the sequence only restored CanvasGroup alpha. During the laser beat, the cinematic letterbox canvas can also draw above presentation UI unless the tutorial HP canvas is explicitly sorted as an overlay.

Fix:
The DarkLord authoring tool now normalizes the tutorial HP canvas RectTransform to full-screen overlay bounds with `localScale = Vector3.one`, keeps the canvas GameObject active, enables max overlay sorting, and validates that the HP canvas is renderable. `TutorialBossEncounterSequence` also ensures the referenced presentation HP canvas is active, enabled, and sorted before showing it at the laser timing.

Prevention:
For temporary scene-authored UI that is hidden with CanvasGroup alpha, keep the Canvas and root GameObjects active and validate transform/sorting separately from visibility alpha. A passing serialized reference check is not enough to prove UI can render above a cinematic overlay.

## 2026-05-28 - Tutorial Boss Camera Followed Real Boss Director

Context:
`DarkLord_Tutorial` should move the gameplay camera to the authored tutorial boss focus before Script 1 and again before Script 2, but the camera could fail to move when the sequence was wired through the real boss `CameraPresentationDirector` / `BossCam` path.

Cause:
The tutorial scene reused a real boss camera presentation path even though the tutorial boss is a stripped presentation scene object. That made the sequence depend on real encounter camera authoring that may be missing, disabled, or no longer aligned with the tutorial focus markers.

Fix:
The DarkLord tutorial authoring menu now disables `TutorialBossEncounterSequence.useCameraPresentationDirector` and wires the sequence to the explicit `BossFocusTarget` / `PlayerFocusTarget` gameplay camera path. Validation reports if the real boss camera director path is enabled.

Prevention:
Scene-local tutorial presentations should use their own authored focus targets unless the target scene intentionally owns a complete real boss camera setup. Do not treat a present `CameraPresentationDirector` as sufficient for tutorial camera authoring.

## 2026-05-27 - Cinematic Protection Did Not Block Dash Tags

Context:
`DarkLord_Tutorial` should never allow player movement, dash, attack, skill, or aim input, but the player could still move or dash after the tutorial boss sequence validation passed.

Cause:
`PlayerCinematicProtection` disabled several input producer behaviours, but movement and ability systems can still read cached movement sources or activation tags. The shared UI control tag set also did not include `State.Move.Dash.Blocked`, so dash activation could survive when only UI-style control blocking was active.

Fix:
Make `PlayerCinematicProtection` apply the existing UI control block tag set and explicitly add `State.Move.Dash.Blocked` for the protected window. `TutorialBossEncounterSequence` now keeps the player protection held after completion for `DarkLord_Tutorial` because that scene has no intended controllable player timing.

Prevention:
Cinematic player locks must block both input behaviours and gameplay tags. Do not treat disabled input components as sufficient when `MovementMotor2D`, cached intent sources, or GAS activation can still consume state after another system restores player interactor state.

## 2026-05-27 - Tutorial Boss Sequence Locked Before Player Registration

Context:
`DarkLord_Tutorial` validation passed with `lockPlayerControls = true`, but the player could still move during the laser presentation.

Cause:
`TutorialBossEncounterSequence` acquired `PlayerCinematicProtection` immediately at `Start()`. In the tutorial scene, `playOnStart` can run before the player is registered in `PlayerRuntimeRegistry`, so the sequence had no player transform and skipped the control/targetability lock while validation only checked serialized booleans. A later scene fade or player spawn path could also restore the current player to an interactive state unless the sequence held a `SceneFadeTransitionService` player-unlock blocker and maintained the lock after registration.

Fix:
Wait for a resolved player transform before acquiring the sequence state, subscribe to `PlayerRuntimeRegistry.PlayerRegistered` / `PlayerUnregistered`, hold `SceneFadeTransitionService.SetPlayerUnlockBlocked(...)`, and maintain the current registered player lock during the sequence. Keep the acquired player protection for the full sequence, including dialogue, laser, collapse, and fake game-over setup.

Prevention:
For play-on-start tutorial or cinematic flows that depend on spawned player components, validate serialized intent but also make runtime acquisition wait for player registration before starting the protected flow. If a scene fade or spawner can restore player state, the cinematic owner must hold the transition unlock blocker and reapply the lock to the currently registered player.

## 2026-05-27 - Event-Driven Tutorial Info Trigger Required Collider

Context:
Combat tutorial info should be opened by the door-close sequence through `TutorialInfoTrigger.FireNow()`, not by player collider entry.

Cause:
`TutorialInfoTrigger` still had `[RequireComponent(typeof(Collider2D))]` even though its public `Fire`, `FireNow`, and `FireAfterDelay` methods already support event-driven timing. This forced unnecessary collider authoring on sequence-owned tutorial prompts.

Fix:
Remove the `Collider2D` requirement from `TutorialInfoTrigger`. Collider setup remains optional and is used only for `OnTriggerEnter2D` activation.

Prevention:
For components that support both trigger-collider activation and direct event/code activation, do not use `RequireComponent` for the optional activation path. Document which activation mode needs scene collider authoring.

## 2026-05-27 - Boss Encounter Aim Block Did Not Freeze Presentation Facing

Context:
Boss encounter intros needed the player body and weapon presentation to stop turning left/right from Aim while the encounter camera/dialogue sequence was running.

Cause:
Blocking or disabling `PlayerAim2D` stopped live aim updates, but `PlayerAnimatorController2D` and `WeaponPresentationRig2D` still read cached `AimDirection` / `MouseWorld` every frame. The first legacy `BossTalkManager` fix also did not cover scenes driven by `BossEncounterDirector`.

Fix:
Player body facing and weapon presentation now expose owner-token cinematic locks, and boss encounter owners acquire/release those locks for modern, legacy, and tutorial boss encounter flows.

Prevention:
For cinematic or boss flows that must freeze facing, block both the input producer and the aim-driven presentation consumers. Do not assume disabling `PlayerAim2D` prevents all visible Aim-following behavior.

## 2026-05-26 - Accepted Scene Transition Could Still Abort Before Load

Context:
Title intro Space-hold skip could appear to complete but intermittently remain on the title scene.

Cause:
`SceneTransitionCoordinator.TryLoadScene(...)` returned `true` as soon as it started the transition coroutine. If `SceneFadeTransitionService.TryBeginTransitionSession()` then failed inside that coroutine, the coordinator cleared its routine and exited without loading the requested scene, leaving the caller with no retry path.

Fix:
The coordinator now logs the fade-session begin failure and falls back to direct `SceneManager.LoadScene(...)` for the accepted target scene.

Prevention:
Scene transition APIs that report request acceptance must either complete the scene load or provide an explicit fallback/logged failure path after asynchronous setup begins. Do not let a post-acceptance fade/session setup failure silently end the transition.

## 2026-05-26 - Manually Generated Unity Empty Lists Swallowed Following Fields

Context:
The timed common relics `승리의 깃발`, `추격자의 발톱`, and `붉은 계약` showed tooltip output like `[[ ]] [0]` even though the logic assets had visible `attribute`, `displayNameOverride`, and `value` lines in text.

Cause:
The manually generated Unity YAML wrote empty lists as:

```yaml
passiveEntries:
  []
```

Unity did not deserialize the following root fields reliably from that shape, so the ScriptableObject loaded default values for the timed buff fields.

Fix:
Use inline empty-list serialization for manually generated Unity assets:

```yaml
passiveEntries: []
```

Prevention:
When creating or rewriting Unity `.asset` files outside the Editor, verify empty lists, field indentation, and root-field deserialization with `rg` checks. Prefer matching Unity's existing inline `field: []` style for empty serialized arrays/lists.

## 2026-05-26 - Global DamagePayloadConfig Shadowed UnityGAS Type

Context:
`ApprenticeHeroSwordHitConfig` failed compilation with `CS1503` when passing its damage config into `DamageSnapshotBuilder.BuildFromBaseValues(...)`.

Cause:
The project still has both a legacy global `DamagePayloadConfig` and the current `UnityGAS.DamagePayloadConfig`. In a global-namespace source file, unqualified `DamagePayloadConfig` resolved to the legacy global type even though `using UnityGAS;` was present.

Fix:
Apprentice Hero Sword damage config now explicitly uses `UnityGAS.DamagePayloadConfig`.

Prevention:
New weapon damage data that feeds `DamageSnapshotBuilder` should spell `UnityGAS.DamagePayloadConfig` explicitly until the legacy global compatibility type is removed.

## 2026-05-26 - Overlapping Time Freeze Restored Stale Zero

Context:
`Flowering` Bloom cut-in pauses combat time while playing unscaled presentation. If the pause menu or another freeze-owning UI opened during that cut-in, the screen could remain frozen after the presentation ended.

Cause:
Both Bloom cut-in and stack UI freeze paths stored `Time.timeScale` locally and restored their cached value later. When UI opened while Bloom had already set `Time.timeScale = 0`, UI cached `0`; Bloom then restored gameplay to `1`, and closing the UI restored the stale cached `0` back over the resumed value.

Fix:
Bloom cut-in now acquires a `GameFlowInputBlocker` before setting `Time.timeScale = 0`, blocking unrelated pause/new UI entry during the protected cut-in window and releasing it in cleanup. `UIManager` also avoids writing a stale cached zero over a later non-zero restore when a freeze overlap has already been resolved by another owner.

Prevention:
Do not allow independent time-freeze owners to enter freely inside a cinematic/presentation freeze window. Use `GameFlowInputBlocker` for protected flow windows, and when restoring a UI-owned freeze that cached `0`, avoid overwriting a later non-zero `Time.timeScale` restored by the original freeze owner.

## 2026-05-26 - Paused Cut-in Blocked Weapon Animation Event Timing

Context:
`Flowering` Bloom Skill1 should play its weapon animation during the cut-in fade and use an animation event to start the weapon reveal.

Cause:
Bloom cut-in presentation pauses combat by setting `Time.timeScale` to `0`. A normal Animator update mode does not advance under scaled time while paused, so weapon animation events can fail to fire or fire only after time resumes. The existing Skill1 clip also pointed its event at the old SwordSkill2 hit tag, so even a fired event would not identify Flowering reveal timing clearly.

Fix:
The Bloom cut-in path plays the `FloweringBloomData.cutInAnimationTrigger` on the weapon animation channel, temporarily switches the weapon Animator to unscaled time for the cut-in, and restores the previous update mode afterward. The Skill1 clip now sends `Event.Anim.Flowering.WeaponReveal`, and the reveal coroutine waits for that tag with a timeout before starting the SpriteMask reveal.

Prevention:
When presentation pauses `Time.timeScale` but still depends on Animator events, explicitly decide whether that Animator must run on unscaled time for the paused section. Use feature-specific gameplay event tags for animation timing instead of reusing another weapon's hit/event tag.

## 2026-05-26 - Weapon Reveal Replayed After Cut-in Fade

Context:
`Flowering` Bloom weapon reveal should play once during the cut-in fade. In Play Mode it could play once during the fade, then play a second time after the fade when active Bloom state began.

Cause:
`FloweringBloomPresentationController.PlayCutIn(...)` and `BeginActiveBloom(...)` both called `ApplyWeaponBloomSprite(true)`. The first fix made active apply idempotent, but `FloweringRuntimeState.BeginBloom(...)` still called `EndBloom()` before active state setup. That `EndBloom()` released the presentation and restored the weapon sprite immediately after the cut-in reveal completed, so active Bloom entered from an inactive sprite and started reveal again.

Fix:
The active weapon sprite apply path returns early when the Bloom sprite is already applied or a weapon reveal coroutine is still running. `FloweringRuntimeState` now preserves the cut-in presentation across the immediate `BeginBloom(...)` setup reset, while normal Bloom end, cancellation, weapon swap, and owner disable still release presentation state. Reveal coroutine handles are also cleared on early exits, so a failed/empty reveal attempt does not permanently block a later valid apply.

Prevention:
Presentation entry points that can be called from both transition setup and steady-state activation must be idempotent. Do not run full presentation release between a transition reveal and the steady-state that consumes that revealed visual state. Guard effect/reveal starts against already-running coroutines and already-applied visual state before starting a new coroutine.

## 2026-05-25 - Hitbox-Owned Slash Visual Followed Owner

Context:
`Flowering` Bloom attack should leave each BloomSlash visual at the strike position like a short-lived slash mark while its collider turns off early.

Cause:
The BloomSlash hitbox prefabs owned their visuals correctly, but `MeleeHitboxActor.attachToOwnerOnSetup` was enabled. `Setup(...)` parented each hitbox instance to the player while preserving world position, so the lingering visual continued to follow player movement after spawn.

Fix:
Bloom attack now forces `overrideAttachToOwnerOnSetup = true` and `attachToOwnerOnSetup = false` in its spawn context, and the BloomSlash prefabs are authored with `attachToOwnerOnSetup` disabled. The collider still expires through `activeTime`; the visual remains only for its authored animation clip lifetime.

Prevention:
For hitbox-owned slash marks or lingering attack visuals, do not enable owner attachment unless the design explicitly wants the visual to move with the attacker. Prefer world-parented hitbox instances with short collider lifetime and clip-length visual lifetime for strike marks.

## 2026-05-25 - Runtime AttackBase Component Needed Concrete Collider First

Context:
`Flowering` Bloom dash logged a `NullReferenceException` every time dash tried to spawn a slash hitbox.

Cause:
`FloweringDashSlashHitboxActor` derives from `AttackBase`, whose base attribute requires `Collider2D`. `Collider2D` is abstract, so adding the hitbox actor first to an empty runtime GameObject can fail Unity's automatic required-component creation and return a null component before `Setup(...)` is called.

Fix:
The dash slash spawn path now adds a concrete `BoxCollider2D` before adding `FloweringDashSlashHitboxActor`, checks the returned actor before setup, and the actor explicitly declares `RequireComponent(typeof(BoxCollider2D))`.

Prevention:
Runtime-created `AttackBase` subclasses should declare and add their concrete collider type before adding the behavior component. Do not rely on a base `RequireComponent(typeof(Collider2D))` to create a valid collider on an empty runtime GameObject.

## 2026-05-25 - Detached Dash Slash Hitbox Scanned Too Narrowly

Context:
`Flowering` Bloom dash slash marks could appear while the intended slash damage did not apply.

Cause:
The detached dash slash hitbox only scanned once at spawn time and filtered `OverlapBoxAll` by damage layers before resolving `CombatHurtbox2D`. That made the hit path more fragile than the existing melee hitbox path: fast dash timing, trigger update timing, or collider/root layer differences could cause a valid target to be missed.

Fix:
The dash slash hitbox now scans during its active lifetime and accepts a target when either the hit collider layer or resolved damage target root layer matches the authored damage mask. Wall line-of-sight blocking is still checked before damage is applied.

Prevention:
Detached short-lived hitboxes should either reuse the shared melee hitbox actor or match its target resolution tolerance. Avoid spawn-frame-only scans for effects that are meant to remain active across multiple frames.

## 2026-05-25 - Delayed Dash Augment Was Tied To Dash Ability Token

Context:
`Flowering` Bloom dash should create three delayed slash hitboxes and three red slash marks after the dash starts.

Cause:
The delayed slash coroutine checked the dash ability spec cancellation token between slash spawns. A short dash ability can complete or cancel before the delayed visual/hit sequence finishes, so later slash marks and hitboxes can be skipped even though Bloom is still active.

Fix:
Dash slash scheduling now follows Bloom runtime state lifetime and weapon cleanup. Bloom end, weapon swap, owner disable, and transient runtime reset still stop remaining coroutines and destroy temporary hitboxes/effects.

Prevention:
Delayed weapon augment effects that intentionally outlive the triggering global ability should be cancelled by the owning weapon runtime state, not by the trigger ability token, unless the design explicitly says the delayed effect should disappear when that trigger ends.

## 2026-05-25 - Active Fade Service Was Replaced During Scene Load

Context:
Title intro completion should fade to black, load the gameplay scene, then fade the next scene in through the shared `SceneTransitionCoordinator` flow.

Cause:
When the title scene had no authored `SceneFadeTransitionService`, the transition began with a runtime fallback overlay. Loading the next scene awakened an authored `SceneFadeTransitionService` under `GlobalUIRoot`, and the existing singleton replacement policy destroyed the fallback while the coordinator still held it for `FadeInAsync()`. The same trap also applies to any active title-authored transition owner that must survive long enough to finish the fade-in before yielding to the loaded scene's authored service.

Fix:
`SceneFadeTransitionService` now defers replacing an active transition owner with a loaded authored service until `EndTransitionSession()`. The pending authored overlay is reset transparent/inactive while it waits, so it cannot cover the active fade-in. The active owner remains alive for fade-out, load, post-load settle, and fade-in, then promotes the authored service after the transition ends.

Prevention:
Do not replace or destroy a fade service while `IsTransitionActive` is true. Scene-loaded authored services should wait until the current transition owner has completed fade-in before taking over singleton ownership, and any deferred overlay must be visually hidden while pending. Title-origin transitions should prefer a scene-root authored fade service over runtime fallback so transition duration can be tuned in the Inspector.

## 2026-05-25 - Intro Entry Fade Overlapped Slot Panel Close Fade

Context:
Starting a new empty profile slot should show one start-button-to-intro fade before the first intro image appears.

Cause:
`TitleMenuController.BeginIntroLaunch(...)` started `TitleIntroPlayer` and then called `TitleProfileSlotPanelUI.CloseUI()`. The profile slot panel's own fade-out could run under the intro overlay root fade, making the start fade appear to reset and run twice even with no additional input.

Fix:
The intro launch path no longer starts the profile slot panel close animation after intro playback begins. The panel can remain active behind the intro overlay because the overlay blocks raycasts and covers the title UI until scene transition.

Prevention:
For title intro entry, keep exactly one visual fade owner. Do not run panel close fades underneath the intro overlay start fade unless the transition is explicitly sequenced after the overlay has fully covered the UI.

## 2026-05-24 - Intro Entry Fade Was Conflated With First Image Fade

Context:
The title intro slow-start request needed two separately tunable beats: the intro overlay FadeIn immediately after pressing `시작하기`, and the first slide image FadeIn after the overlay is visible.

Cause:
The first pass interpreted "intro starts slowly" as only the first slide image FadeIn duration, so the start-button-to-intro overlay FadeIn had no separate data field.

Fix:
`TitleIntroSequenceSO` now separates `introStartFadeDuration` from `initialImageFadeDuration`. `TitleIntroPlayer` fades the authored intro overlay root first, then starts first-slide image FadeIn together with text typing.

Prevention:
For title/popup presentation timing, distinguish container/root visibility transitions from content-specific media transitions. Do not use a slide image fade field to represent the screen or overlay entry fade.

## 2026-05-23 - Intro Text Was Coupled To Image Fade

Context:
The title intro design keeps slide text visible while images fade in or out. Text typing starts together with the new image fade-in, and completed text remains visible during old-image fade-out.

Cause:
`TitleIntroPlayer` treated image fade-in, text typing, wait, image fade-out, and text clear as separate sequential steps. That made text disappear as part of image transition cleanup instead of letting text lifetime follow the script timing.

Fix:
`TitleIntroPlayer` now runs text typing and image fade-in as one input-polled phase, and image fade-out no longer clears the completed text. The next slide replaces text only when its own typing starts.

Prevention:
For staged narrative UI, define text lifetime separately from media transition lifetime. Do not clear narrative text from image fade cleanup unless the design explicitly says the text should fade or disappear with the image.

## 2026-05-23 - Scene-Local UI Replaced Persistent Global Panel

Context:
After moving from lobby to corridor, pause still opened but the settings panel could fail to open. The global UI root was intended to persist as the UI source of truth.

Cause:
`SettingsPanelUI` and `KeyBindingPanelUI` had their own singleton replacement policy. A scene-local panel could destroy the existing persistent instance, then later be destroyed together with a duplicate scene-local `GlobalUIRoot`, leaving no usable global panel.

Fix:
The child panels now destroy only themselves when another valid instance already exists. `EnsureInstance()` remains a lookup/search path, while persistent ownership stays with `GlobalUIRoot` and `UIManager`.

Prevention:
Do not let global UI child panels delete an existing persistent representative to win a static instance slot. Scene-local duplicates should defer to the root/UI manager ownership policy, and selected-button/EventSystem visual policy should be handled separately.

## 2026-05-23 - Intro Skip Prompt Overwrote Authored Visuals

Context:
The title intro skip prompt should be scene-authored so its icon, text, fill color, and custom art can be tuned in Unity. `TitleIntroView.Show()` still forced the skip prompt glyph/label to Space at runtime.

Cause:
The view treated skip prompt display as runtime-owned presentation instead of only projecting the authored root/fill state. This made custom prompt visuals fragile because show-time setup could overwrite authored icon/text/color references.

Fix:
`TitleIntroView` now keeps authored skip prompt icon/text/fill color untouched by default. Runtime glyph and fill color application are available only through serialized opt-ins or explicit projection calls.

Prevention:
For scene-authored UI prompts, runtime view code should activate/reset/progress the authored objects without replacing their icon/text/color content unless an explicit serialized opt-in says the prompt value is data-driven.

## 2026-05-23 - Intro Advance Input Skipped Multiple Phases

Context:
Title intro text advance should complete the currently typing text, then still wait for the configured post-text delay before image fade-out. A click or short Space release during typing could immediately push the intro into the fade-out phase.

Cause:
`TitleIntroPlayer` polled raw advance input separately in typing, wait, and fade coroutines. When a typing coroutine consumed an advance input and ended, the next coroutine could poll the same frame's input event again and treat it as a wait/fade skip.

Fix:
`TitleIntroPlayer` now records the frame that consumed an advance input. Later phases ignore click or short Space advance events already consumed in that frame, while Space hold skip remains independent.

Prevention:
For staged presentation flows, consume physical input events at the flow-owner level before crossing coroutine/state boundaries. One click or key release should not be able to complete typing and skip the following wait/fade phase in the same frame.

## 2026-05-23 - Locked Slot Art Was Written To Item Icon

Context:
Inventory locked-slot presentation needed `InventorySlotLock` to appear as the slot background, with the item icon layer empty. The locked slot still changed the serialized `icon` / `itemImage` sprite to `InventorySlotLock`.

Cause:
`ItemSlotUI.RefreshLockedSlot()` reused the item icon rendering path through `ItemDisplayIconUtility.ApplyRaw(icon, lockedSlotSprite, ...)`, so the lock art was treated as an item icon instead of slot background presentation.

Fix:
`ItemSlotUI` now writes `lockedSlotSprite` only to the serialized `backgroundImage` reference. Locked slots clear item icon/level content first, and missing `backgroundImage` wiring logs a warning instead of falling back to the icon image.

Prevention:
Do not use `icon`, `itemImage`, or item display helpers for locked-slot frame art. Locked slot visuals belong to the authored slot background layer; item icon images should only display actual item icons or remain empty.

## 2026-05-23 - Legacy Button Interactable Blocked HoldActionButton

Context:
Chest reroll prefab wiring had `HoldActionButton`, `HoldFillButtonView`, and `RerollHoldProgress` connected correctly, but Space hold still did not start reliably.

Cause:
`HoldActionButton` was intended to own hold availability, but `CanUse()` still checked the optional companion Unity `Button.IsInteractable()`. A legacy `Button` with `interactable = false` could therefore block the shared hold input even when `ChestScreen` set `HoldActionButton` itself usable.

Fix:
`HoldActionButton.CanUse()` now uses its own `interactable` state as the hold gate. The optional Unity `Button` can still mirror visual/interactable state for compatibility, but it no longer decides whether a hold can start.

Prevention:
For hold-only controls, do not reintroduce Unity `Button.IsInteractable()` as an input gate. Feature screens should call `HoldActionButton.SetInteractable(...)`, and Unity `Button` should remain optional legacy compatibility only.

## 2026-05-23 - UI Submit Did Not Start Hold Action

Context:
Chest reroll Space input changed the selected Unity `Button` to its pressed color, but the shared hold action did not start.

Cause:
Unity UI Submit can drive `Button` pressed visuals independently from a custom hold component. `HoldActionButton` only relied on its own key polling, so a selected button could visually react to Space without routing that Submit event into the hold flow.

Fix:
`HoldActionButton` now implements `ISubmitHandler` and bridges selected UI Submit input into the same keyboard hold path, while still verifying that the Submit action's active control matches the configured `holdKey`.

Prevention:
For hold buttons that can be selected by the EventSystem, bridge UI Submit into the hold component instead of assuming `Button` pressed visuals imply the hold action has started.

## 2026-05-23 - Keyboard Hold Missed The Usable Window

Context:
Chest reroll needed Space hold to work through `HoldActionButton`, but pressing Space could fail to start the hold.

Cause:
`HoldActionButton` only started keyboard hold on the key-down frame. If Space was already held while the chest reveal was finishing, or while the reroll button was still non-interactable, the original key-down frame was missed and the hold never started after the button became usable.

Fix:
`HoldActionButton` can now start keyboard hold while the configured key is already pressed and the button becomes usable. Keyboard completion blocks restart until the key is released, so one long press cannot repeatedly complete the action.

Prevention:
For keyboard hold buttons inside animated or gated UI, support "key already held when usable" behavior and separately block repeated completion until release.

## 2026-05-23 - Filled Image And Clip Width Double-Applied Hold Progress

Context:
Chest reroll hold fill was authored with a filled `Image` and the same `RectTransform` assigned as the clip/fill root.

Cause:
`HoldFillButtonView` drove both `Image.fillAmount` and the same rect width by the same progress value. This effectively applied progress twice, so early hold progress could be too small to notice.

Fix:
`HoldFillButtonView` now skips clip-width resizing when the fill `Image` is already a filled Image on the same `RectTransform`. It also reapplies progress on enable and avoids deactivating its own GameObject when the clip root is accidentally assigned to the owner.

Prevention:
For hold-fill buttons, either drive a filled Image or drive a separate clip-root width. Do not double-drive the same fill geometry with both mechanisms.

## 2026-05-23 - Duplicate Hold Progress Owners Hid Authored Fill UI

Context:
Chest reroll was being tested with the shared hold-fill button components, but the fill was not visibly progressing.

Cause:
`ChestScreen` still owned direct mouse/Space hold detection and progress calculation while an authored `HoldActionButton`/`HoldFillButtonView` could also be present on the same reroll button. This left two possible owners for the same progress state and made it easy for the shared fill view to be unconnected or reset by the legacy path.

Fix:
`HoldActionButton` now owns pointer/keyboard hold timing and progress, while `ChestScreen` subscribes to hold events and only owns reroll eligibility, chest close/shake/open presentation, and refresh execution.

Prevention:
Do not let feature screens and shared hold-button components both calculate the same hold progress. If a `HoldActionButton` is authored for a button, feature code should consume its events instead of duplicating pointer/key hold detection.

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
`SlimeQueenBossBase` now ensures a runtime `StaggerGaugeSystem` for every Slime Queen variant and wires it to the existing stagger attributes plus a 3-second status-only Groggy effect. Phase-two Slime Queen Short/Long bodies now use the same boss HUD registration path as other bosses and appear as separate HUD slots.

Prevention:
When adding a new boss or special multi-body HUD path, verify both sides of groggy support: the combat target must have a configured `StaggerGaugeSystem`, and each active boss body must register its own HUD slot instead of hiding the groggy view or adding concrete boss branches to `BossHudController`.

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

## 2026-05-28 - Boss Reward Chest Appeared Missing From Duplicate Scene Instance Or Loot Exception

Context:
`HeoMinSeok_Boss` reward review showed the portal path running while the chest appeared not to activate.

Cause:
`HeoMinSeok_Boss` is not a final-boss scene, so final-route chest suppression should not explain direct scene testing there. The scene contains more than one `TreasureChest` instance; the canonical `BossEncounterEndDirector.treasureChest` reference points to `TreasureChest (1)`, so watching a different chest object can look like activation failed. The reward chest activation path also generated boss chest loot before calling `SetActive(true)`, so a loot-generation exception could leave the chest inactive while the later portal path still ran.

Fix:
Keep the final-route chest skip policy separate from portal activation, but do not apply it as the default diagnosis for `HeoMinSeok_Boss` direct tests. `BossRewardSpawnService` now activates the referenced chest before boss loot generation, so a later loot exception does not keep the reward object hidden. For setup/debugging, validate the active reward owner reference and inspect the referenced scene object, not any duplicate chest instance with a similar name.

Prevention:
When a boss reward chest appears inactive, first confirm whether the tested scene should actually be final-route suppressed. For non-final direct scene tests, confirm which `TreasureChest` object is wired into `BossEncounterEndDirector` or the active `BossBattleEndHandler`, then check the Unity Console for `[BossBattleEnd] ActivateTreasureChest failed.` Remove or clearly rename duplicate authored chest instances during scene authoring so the reward owner reference is unambiguous.
