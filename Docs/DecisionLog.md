---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-05-28
---

# Decision Log

## 2026-05-28 - Normal Boss Routes Are The Default Reward Path

Decision:
The run route catalog uses three normal boss RouteSets (`ShadowCorridorBossRouteSet`, `Drunken_Dragon_Spili_CorridorBossRouteSet`, and `SlimeRouteSet`) followed by `DemonkingRouteSet` as the single final RouteSet. Direct boss-scene Play uses the full catalog route order deterministically and points `currentStageIndex` at the scene being tested, instead of creating a one-stage plan.

Reason:
Normal bosses are repeated route content and should keep the ordinary chest plus portal reward path. Final-route behavior is the exceptional terminal/final-boss policy. A one-stage direct Play plan made normal boss scenes look like the last stage and obscured reward diagnosis.

Implications:
- `RunRouteCatalog.asset` should keep `normalStageCount = 3`, `allowDuplicateNormalRoutes = false`, the three normal RouteSets, and `DemonkingRouteSet` as final.
- Runtime hub-start route generation still randomizes the normal route order according to the existing catalog policy.
- Editor direct Play uses catalog order only for deterministic local testing and route-context debugging.
- `BossRewardRevealSceneValidatorWindow` validates and can reapply the route catalog defaults alongside the target scene reveal setup.

## 2026-05-28 - Reward Gate Reveal Isolates Global Vision Masks

Decision:
When a boss reward portal reveal runs in a scene with `GlobalVisionMaskRoot`, `BossRewardObjectRevealPresentation` can temporarily narrow active global vision `SpriteMask` ranges to the dark overlay renderer's sorting range. This keeps the boss gimmick overlay masked while preventing the reward portal renderer from being clipped by the player vision mask during its local gate reveal.

Reason:
Unity `SpriteMask` interaction is sorting-range based. The reward gate reveal uses `VisibleInsideMask` on portal renderers for the local reveal mask, so the same renderers can unintentionally react to the scene's global player vision mask.

Implications:
- The target-scene apply tool enables `isolateGlobalVisionMasksDuringReveal` for portal reveal components.
- The reveal component defaults this option to enabled, but only applies it when the reveal actually uses a SpriteMask or masked renderers.
- Global vision mask ranges are restored when reveal completes, is stopped, or the reveal component is disabled.
- `HeoMinSeok_Boss` needs Play Mode review because it combines the reward portal reveal with the boss gimmick `GlobalVisionMaskRoot`.

## 2026-05-28 - Global Ending Outro View Owns Runtime Closed Startup

Decision:
`EndingOutroView` snaps itself hidden during runtime startup when its authored root is active, while `Show(...)` marks intentional playback activation so inactive authored views can still be enabled and played normally.

Reason:
The ending outro view can be authored under persistent `GlobalUIRoot` without a colocated `EndingOutroPlayer`. If the shared `IntroOverlay` is left active for editing, relying only on `EndingOutroPlayer.hideViewOnAwake` lets the fullscreen overlay appear as soon as Play Mode starts.

Implications:
- Fullscreen ending outro UI can remain authored in the global UI prefab without requiring the root to be manually inactive after every edit.
- Real outro playback still activates inactive roots through `EndingOutroView.Show(...)`.
- This is a runtime state guard only; scene/prefab authoring should still keep presentation roots inactive when practical.

## 2026-05-28 - Boss Reward Reveal Ownership Is Split By Object

Decision:
Normal boss reward chests own their simple dust reveal directly through `TreasureChest.PlayRewardReveal()`. Reward exit portals own the mask, rise, dust loop/burst, collider lock, global vision mask isolation, camera shake, and local tremble through the optional `BossRewardObjectRevealPresentation` scene component. If no portal reveal component is present, the portal still activates immediately as before.

Reason:
Chest reveal is only a one-shot dust particle and does not need the portal reveal component's mask, movement, collider lock, camera shake, or global vision mask isolation fields. Keeping chest dust on `TreasureChest` and portal reveal on a portal scene component keeps each object's presentation API proportional to its behavior.

Implications:
- `BossBattleEndHandler` and `BossEncounterEndDirector` invoke `TreasureChest.PlayRewardReveal()` only after existing chest initialization succeeds.
- `BossRewardSpawnService` activates the referenced chest before boss loot generation so presentation visibility is not blocked by a later loot-content exception.
- `BossBattleEndHandler` and `BossEncounterEndDirector` invoke `BossRewardObjectRevealPresentation.PlayReveal()` only after portal visibility restoration succeeds.
- Portal reveal sub-effects share the same reveal timeline: mask/collider lock spans the reveal, loop dust and local/root loop shake run during the reveal, burst dust and completion camera shake run on reveal completion.
- Final-route chest suppression and terminal `BossDefeatEndingSequence` reward suppression stay unchanged.
- Unity scene authoring must wire TreasureChest reward dust fields for chest dust, and portal reveal root, SpriteRenderers, particles, and colliders for the gate reveal.

## 2026-05-28 - Target Boss Reward Scenes Use Editor Apply Tool

Decision:
`HeoMinSeok_Boss`, `HeoMinSeok_Boss_Dragon_Spili`, and `SangHyup_Boss_SlimeQueen` should use `BossEncounterEndDirector` as the canonical active reward owner and use the Unity Editor apply tool for first-pass reveal wiring. The tool uses Editor scene APIs, not handwritten YAML, and disables a duplicate `BossBattleEndHandler` only when it references the same chest and portal as the director.

Reason:
The target scenes already carry both director and legacy handler components referencing the same authored chest and portal. A repeatable Editor apply path keeps the scene-instance reveal setup consistent while avoiding duplicate reward ownership and avoiding shared prefab changes.

Implications:
- Run `Tools/Validation/Apply Boss Reward Reveal Target Scene Setup` after Unity recompiles the Editor script, then review and save the scene changes.
- If an enabled `BossBattleEndHandler` references different objects, the tool leaves it enabled and reports it for manual review.
- Generated mask/dust helpers are first-pass authoring defaults; visual timing, sorting, and positions still require Play Mode review.

## 2026-05-28 - Terminal Outro Uses Live GlobalUIRoot View Resolution

Decision:
The terminal boss ending outro does not trust a stale scene-instance `EndingOutroView` reference at playback time. `EndingOutroPlayer` first reuses its assigned view when it is alive and ready, then resolves a ready view under the active `GlobalUIRoot`, and only then falls back to a single unambiguous scene view.

Reason:
Playable scenes each author a `GlobalUIRoot` for direct scene testing, but at runtime the root is persistent and duplicates destroy themselves. A boss scene reached from Hub or Corridor can therefore lose serialized references to UI children on its duplicate root even though the persistent root still has the authored outro view.

Implications:
- Cross-scene terminal endings can play the same authored outro UI whether the boss scene is launched directly or reached from another scene.
- The outro player still uses authored UI only; it does not create Canvas, TMP, Image, or presentation hierarchy at runtime.
- Ending outro player control lock spans the outro and accepted TitleScene transition startup so gameplay input does not resume behind the fullscreen panel.

## 2026-05-28 - Boss Choice Failure Effect Requires Add-Affection Sibling

Decision:
Boss dialogue choice failure presentation is automatic only when the current choice set contains at least one `add_aff` choice and the selected choice does not carry `add_aff`. Explicit `choice_fail`, `aff_fail`, and `fail_aff` tags still force the failure effect.

Reason:
Some boss choice sets are weak story branches with no affection reward. Treating every non-`add_aff` boss choice result as failure made both sides of simple branches look like failed answers.

Implications:
- Ink authors can make a success/failure pair by putting `# add_aff: 1` in the success choice body and leaving the opposite choice without `add_aff`.
- Choice sets with no `add_aff` tags do not show the failure effect unless an explicit failure tag is authored.
- The runtime previews the first result tags of the current choice set from a cloned Ink story state so body-level `add_aff` tags are detected without advancing the real story.

## 2026-05-28 - Dialogue Hides Non-Dialogue UI Through Fade Suppression

Decision:
`DialogueService` owns the common non-dialogue UI suppression boundary for Ink dialogue and exposes owner-token suppression for outer flows that need the same hidden UI window before or after Dialogue playback. The suppressed layers are `GameplayHUD`, `Popup`, `Hover`, `Prompt`, `Reward`, `DamagePopup`, and `BossHUD`; `Dialogue`, `GameOver`, and `Loading` are never suppressed by this path.

Reason:
Dialogue should stay readable without gameplay HUD or boss HP noise, but instant `SetActive` toggles made the transition abrupt and let the terminal boss ending briefly restore HUD between death SpeechBubble, Dialogue, and outro. Owner-token suppression lets the terminal death flow hold the UI hidden across multiple presentation beats while normal Dialogue still restores the original UI state when it ends.

Implications:
- Active UI roots fade out through `CanvasGroup` before deactivation and fade back in to their captured alpha when the final suppression owner releases.
- Terminal boss death acquires suppression before the death SpeechBubble and releases it without restore only after a TitleScene transition has started, so HUD does not reappear behind the ending outro or transition fade.
- If a terminal handoff fails or is interrupted before scene transition starts, the same suppression owner is released with restore so gameplay UI returns.
- Flow-owned reward presentation may reactivate the authored `RewardCanvas` and non-raycasting `HoverCanvas` through `DialogueService`'s captured-layer temporary visibility boundary while Dialogue is still waiting for a reward callback; the Reward panel and its item detail hover are part of that dialogue reward flow, not ambient HUD noise.

## 2026-05-28 - DarkLord Hub Intro Uses Completion And Seen Gates

Decision:
The first Hub introduction after `DarkLord_Tutorial` is gated by `darklord_tutorial_forced_defeat_completed` and `hub_intro_after_darklord_seen`, with editor-only test bypass routed through the same `HubIntroProgressGate` helper used by both the Hub fall presentation and the Hub intro sequence.

Reason:
The sequence is both a first-Hub introduction and the continuation of the forced-loss tutorial boss flow. Requiring the DarkLord completion flag prevents normal first Hub entry from accidentally playing the post-defeat version, while the seen flag keeps it one-shot per profile.

Implications:
- `TutorialBossEncounterSequence` records the forced-defeat completion before fake GameOver returns to Hub.
- `PlayerHubSpawnPresentation2D` and `HubIntroAfterDarkLordSequence` must use matching completion/seen ids and matching editor-bypass settings for direct Hub testing.
- The Hub introduction remains scene-authored through serialized SpeechBubble, camera focus, and Dialogue references; scene/prefab wiring and Unity import validation are required before playtest sign-off.

## 2026-05-27 - DarkLord Tutorial Scene Authoring Uses Editor Menu

Decision:
`DarkLord_Tutorial` scene placement and serialized wiring for the tutorial boss presentation is performed through `Tools/Tutorial/DarkLord Tutorial/Apply Default Authoring To Active Scene`, with validation through the paired `Validate Active Scene` menu.

Reason:
The scene needs multiple authored objects, focus targets, laser origins, tutorial HP UI, Ink references, and disabled real-boss-start flags, but direct scene YAML edits are too risky for Unity object references. A scoped Editor menu keeps placement repeatable while leaving final scene save/review under Unity Editor control.

Implications:
- The menu only mutates the active `DarkLord_Tutorial` scene outside Play Mode and marks the scene dirty for the user to save.
- Compiled Ink `.json` assets are still produced by the Unity Ink import pipeline; the menu warns/validation reports when those generated TextAssets are not ready yet.
- Scene View review remains required for camera framing, HP safe-area placement, and laser angle fine tuning after the automated pass.

## 2026-05-27 - Tutorial Boss Dialogue Stays Outside Letterbox

Decision:
`TutorialBossEncounterSequence` keeps cinematic letterbox bars off while Ink dialogue is visible, and uses the bars only for non-dialogue presentation beats such as scripted lasers and the collapse/return-to-player moment.

Reason:
The authored Dialogue UI can occupy the same vertical screen space as the letterbox bars. Keeping dialogue outside the letterbox avoids hiding speaker names, choices, or dialogue body text while preserving cinematic framing during laser/failure presentation.

Implications:
- Tutorial boss Ink should use `# speaker: ...` tags for speaker names and should not include visible speaker prefixes such as `마왕:` in line text.
- A hidden or temporary speaker can be shown through a string speaker tag such as `# speaker: ???`.
- Player input and targetability protection still spans the full sequence, including both dialogue and laser sections.

## 2026-05-27 - Tutorial Chest Open Handoff Uses Chest UI Success Event

Decision:
Tutorial chest-open continuation is driven by `TreasureChest` UI-open success events and a scene-authored `TutorialChestOpenedTrigger` bridge.

Reason:
The combat tutorial flow needs to continue after the player actually opens the reward chest, but `TutorialSceneSequenceDirector.NotifyChestOpened()` previously had no reliable source event. Emitting from `TreasureChest` only after `ChestUIManager.OpenChest(...)` succeeds keeps the tutorial handoff aligned with the visible chest UI instead of the interaction request or world open prelude.

Implications:
- `TreasureChest.OpenedUi` can report repeated successful UI opens, while `FirstOpenedUi` reports only the first successful UI open.
- `TutorialChestOpenedTrigger` defaults to first-open-only and one-shot so tutorial steps do not replay when the same chest UI is reopened.
- Scene authoring should wire `TutorialSceneSequenceDirector.OnMonstersCleared` to the chest-open tutorial prompt, and `TutorialChestOpenedTrigger.OnChestOpened` to `TutorialSceneSequenceDirector.NotifyChestOpened()` or the next tutorial step.

## 2026-05-27 - Boss Terminal Ending Replaces Reward Portal Flow Per Boss

Decision:
Boss defeat ending is an explicit scene-authored terminal flow on the selected boss: death speech bubble, Ink dialogue, ending outro, `RunEndReason.Victory`, then `TitleScene`. It reuses the boss death presentation handoff but does not run normal reward-ready, chest, or portal activation for that selected flow.

Reason:
The ending is story/session completion, not a normal battle-end reward beat. Making it opt-in per boss avoids changing ordinary boss reward and portal behavior while still letting a final/story boss move directly from defeat presentation into ending content.

Implications:
- `BossDefeatEndingSequence` is authored only on bosses that should end the run.
- `EndingOutroSequenceSO`, `EndingOutroView`, and `EndingOutroPlayer` stay separate from TitleIntro so title launch timing remains isolated.
- Terminal death presentation removes the letterbox before post-speech Dialogue starts, but keeps the camera focused on the boss through terminal Dialogue and outro. The camera is restored only if the terminal handoff fails or is interrupted before completion.
- Terminal ending scene load uses terminal-flow fade-out and TitleScene fade-in durations owned by `BossDefeatEndingSequence`, leaving ordinary scene transition timings unchanged.
- Normal bosses without this sequence continue through `BossBattleEndHandler` / `BossEncounterEndDirector` reward and portal handling.

## 2026-05-27 - Combat Tutorial Intro Owns Temporary Presentation State

Decision:
The attack/skill combat tutorial intro is owned by a scene-authored `TutorialCombatIntroSequence` triggered after `TutorialDoorClosedTrigger`, while the tutorial-only HP safety is owned by `TutorialPlayerHealthAutoRecover`.

Reason:
The design needs a temporary presentation window after the door fully closes: focus the camera on the monster/chest composition, prevent player control and enemy target acquisition, show the attack/skill tutorial, return the camera to the player, then restore normal combat. Keeping HP auto-recovery scene-local avoids changing shared damage/death rules for a tutorial exception.

Implications:
- `DoorObject` remains generic and only reports close-presentation completion.
- `TutorialCombatIntroSequence` uses existing player protection, targetability blocking, cinematic letterbox, and gameplay camera ownership patterns instead of adding a manager.
- Combat tutorial letterbox bars are enabled by default, while global UI fading is opt-in so the attack/skill tutorial panel is not accidentally hidden by the prompt/dialogue layer fade.
- Scene authoring must provide the monster/chest camera focus marker, attack/skill `TutorialInfoTrigger`, and optional gameplay-released UnityEvents.
- `TutorialPlayerHealthAutoRecover` should be present only in the tutorial scene that requires instant HP restoration; its death-return suspension is a scene-local safety guard, not a global combat rule.

## 2026-05-27 - Room Enemy Navigation Uses Active Spawn Room Registration

Decision:
Room-level enemy navigation is driven by the currently active `MonsterSpawnRoomGroup`, reads its alive spawn/lock-registered monsters, and renders arrows through a scene-level world `SpriteRenderer` overlay using the existing kill-lock arrow prefab.

Reason:
The guidance should match room/chest lock semantics instead of scanning every `Enemy` in the scene. Keeping the arrows as a world overlay avoids adding UGUI hierarchy and lets the existing chest arrow visual stay consistent while changing only the placement rule to camera viewport edges.

Implications:
- `RoomEncounterEntryTrigger2D -> MonsterSpawnRoomGroup` remains the active-room signal.
- Spawn-registered roots and Slime split descendants count because they enter through the room group's registration path.
- General direct summons remain excluded unless they explicitly use the same registration path.
- Scene authoring must provide one `RoomEnemyNavigationOverlay` and assign `KillLockMonsterNavigationArrow.prefab`; the validation menu can create/wire the opened scene instance through UnityEditor APIs.

## 2026-05-27 - Tutorial Boss Portal Uses Direct Scene Portal

Decision:
Tutorial-only fixed scene travel, such as `TutorialCorridor` to `DarkLord_Tutorial`, uses `TutorialScenePortal` instead of adding a tutorial-specific `TransitionType` to the run route system. The direct portal still captures current player runtime state and prepares a minimal destination transition context so spawned tutorial scenes can restore inventory/loadout state.

Reason:
`TransitionType` currently expresses run-route semantics resolved by `PortalRouteManager` and `RunRouteCatalogSO`. The tutorial boss handoff is a fixed authored scene jump, not a run-progress route transition, so adding a global transition enum would mix tutorial-specific routing into the normal run plan.

Implications:
- `ScenePortal` remains owned by normal run route travel.
- `TutorialScenePortal` loads a serialized target scene through `SceneTransitionCoordinator` with direct `SceneManager.LoadScene(...)` fallback, without using `PortalRouteManager`.
- `TutorialScenePortal` preserves the current player runtime state by default; enable `resetPlayerRuntimeStateOnTravel` only for a tutorial jump that intentionally discards the current loadout.
- Fixed tutorial destination scenes must be present in BuildSettings.
- If tutorial scene travel later needs run progress, reward, or route-plan behavior, promote it into the route system deliberately instead of expanding the direct portal.

## 2026-05-27 - New Profiles Enter Tutorial Corridor

Decision:
Empty-slot `StartNewRun` launches use `TitleProfileSlotService.newProfileTargetSceneName`, defaulting to `TutorialCorridor`, while existing-slot `ContinueRun` keeps using `targetSceneName`.

Reason:
The title intro is still a presentation gate and should not hard-code its own destination, but a first-time profile now needs to enter the tutorial scene after the intro instead of going straight to the hub. Keeping the split inside `TitleProfileSlotService` preserves the existing launch request path while allowing continue/default launch behavior to remain separate.

Implications:
- `TitleIntroPlayer` still does not own scene selection.
- `TutorialCorridor` must remain in BuildSettings for player builds.
- TitleScene Inspector can clear or change `newProfileTargetSceneName` if the first-time profile route changes later.
- Tutorial scene startup beats should be owned by scene-authored tutorial components, not title UI.

## 2026-05-26 - Demon King Threshold Patterns Own Temporary Damage Rules

Decision:
Demon King threshold set pieces apply their exceptional damage rules only while the set piece needs them: the 50% WallBounceRush owns a temporary `State.Status.StaggerImmune` guard, and the 10% FinalDesperation owns `State.Status.StaggerImmune` for the full final-pattern lifetime while its HP clamp only lasts until the center move finishes and the attack loop begins. FinalDesperation is the terminal threshold pattern and suppresses the 50% WallBounceRush when HP is already at or below the final threshold. If the final threshold is crossed during Groggy, FinalDesperation immediately ends the Groggy effect/tag and starts through a forced pattern reservation instead of waiting for Groggy recovery.

Reason:
The 50% rush and 10% final pattern should not be interrupted or weakened by stagger buildup during their scripted threshold set pieces, and the 10% phase should not be skipped by a large hit, delayed behind a lower-priority threshold set piece, or delayed by an already-active Groggy state. FinalDesperation also needs a center-position reset followed by a no-damage map-edge knockback before the attack loop starts. Once FinalDesperation attacks begin, normal damage and death rules should resume so the fight can end cleanly, but stagger buildup remains suppressed for the final loop.

Implications:
- Future Demon King threshold patterns should acquire and release their own temporary tags or HP guards instead of changing global combat damage behavior.
- FinalDesperation entry validation must include both the center-move HP clamp release point and the final-pattern `StaggerImmune` release path.
- Marking FinalDesperation started also consumes/suppresses the 50% pattern so a direct drop to 10% cannot play WallBounceRush first.
- FinalDesperation force-start must bypass normal selection gates and clear active Groggy state so the reactive Groggy FSM transition cannot reclaim control for the remaining Groggy duration.
- Scene/Inspector tuning can change threshold ratios, but the clamp lifetime remains code-owned unless the phase design changes.

## 2026-05-26 - Flowering Bloom Locks Active Weapon Changes

Decision:
While `Flowering` Skill1 Bloom activation is running, active weapon changes are rejected by `WeaponInventory2D`. The lock starts before the cut-in and is released only after Bloom active duration, reveal-out, cancellation, or scene cleanup finishes.

Reason:
Bloom owns player-root runtime state, cut-in presentation, weapon reveal state, dash slash augment, HUD duration projection, and a long-lived ability coroutine. Swapping or replacing the active weapon mid-flow can force weapon cleanup and leave the Bloom presentation/runtime lifecycle out of sequence.

Implications:
- Swap input, direct equip, active drop/unequip/destroy, active slot replacement, and active slot inventory swap all respect the Flowering lock.
- Offhand/non-active slot changes can still proceed because they do not tear down the active Flowering runtime.
- The lock is runtime-only and adds no serialized fields.

## 2026-05-26 - Tutorial Default Weapon Is Scene-Local

Decision:
Tutorial scenes that must force a basic starting weapon use a scene-authored `TutorialDefaultWeaponBootstrap` instead of changing global player spawn, save restore, or normal run loadout behavior.

Reason:
The tutorial needs a deterministic starting weapon, but that rule should not leak into normal corridor, hub, or boss entry. Running through `WeaponInventory2D.TrySetWeaponSlot(...)` and `Equip(...)` keeps weapon cleanup, ability ownership, stats, presentation, and inventory UI events on the existing weapon inventory path.

Implications:
- Tutorial scene authoring assigns the default `WeaponDefinition` directly on the bootstrap component.
- Other weapon slots can be cleared by the bootstrap when the tutorial must start with only the default weapon.
- The bootstrap changes the live runtime inventory for that session; if tutorial completion should transition into a different loadout policy, that later transition must reset or rebuild run state explicitly.

## 2026-05-26 - Weapon Detail SFX Stay In Logic Data

Decision:
Weapon-specific sub-timing sounds for Flowering and Lightning Spear are authored on the weapon logic data that owns the timing, while `AbilityDefinition` audio fields remain the broad cast/commit/end phase slots.

Reason:
BloomSlash, cut-in reveal, dash slash, MarkRush, recovered spear, and MarkRain timings are not shared ability lifecycle phases. Keeping those `SoundRef` slots beside the timing data lets designers tune them without adding a new manager or overloading generic `AbilityDefinition` audio.

Implications:
- Weapon logic should route these detail sounds through `AbilityAudioRouter.PlayOneShotAtPosition(...)` so world-positioned effects use the same playback context shape.
- Empty `SoundRef` values remain no-op defaults; adding the slots must not change existing sound behavior until keys are authored.
- Basic attack step sounds and common `AbilityDefinition` audio slots remain separate and can intentionally overlap if both are configured.

## 2026-05-26 - Common Demo Relics Stay Data-Centered

Decision:
Pre-demo common relic expansion is implemented as data-centered `RelicDefinition` / `RelicLogic` assets with small reusable runtime logic only for generic timed-event and health-ratio stat modifiers. Boss-target-specific and critical-kill relics stay out of this slice.

Reason:
The June 2 demo needs many stable, drop-ready relics without widening combat damage routing, boss identity checks, or kill-event payload contracts. Keeping the v1 relics at `maxLevel = 1`, `dropLevel = 1`, and default-unlocked reduces balance and verification load.

Implications:
- `결투자의 인장` remains deferred until boss target classification and damage calculation routing are reviewed.
- `처형자의 동전` remains deferred until `KillConfirmed` or an equivalent event carries critical-hit context.
- New pre-demo common relics should prefer existing stat attributes and shared `RelicLogic` assets over one-off code.

## 2026-05-26 - Tutorial Boss Encounter Uses Presentation-Only Failure

Decision:
Tutorial boss encounter support uses scene-authored presentation scripts for lasers, HP loss, collapse, and fake game-over. The scripted lasers reduce `TutorialPresentationHpView` only, and fake game-over calls `GameOverPresentationController.TryShow(...)` with `EndRunOnReturn = false`.

Reason:
The tutorial needs to teach a forced failure beat without mutating real player HP, triggering real death components, or ending the active run. Keeping the sequence presentation-only lets the same authored UI and game-over presentation be reused while avoiding save/runtime side effects.

Implications:
- Tutorial boss Ink uses a dedicated `NPCData` plus explicit first/second Ink TextAssets on `TutorialBossEncounterSequence`; it does not use `BossDialogueRunner` encounter progress.
- Hit, collapse, sound, and VFX timing are exposed as UnityEvents for scene authoring.
- Real damage/death/run-end systems should not be connected to the tutorial laser flow unless a later design explicitly changes the tutorial from scripted presentation to real combat.

## 2026-05-26 - Flowering Bloom Cut-in Reveal Uses Weapon Animation Event

Decision:
`Flowering` Skill1 Bloom cut-in now plays the weapon Animator `Skill1` trigger during the fade-in cut-in and starts the weapon reveal from the `Event.Anim.Flowering.WeaponReveal` animation event, following the Lightning Spear Skill2 pattern of data-owned trigger and event tag timing.

Reason:
The Bloom reveal should line up with the authored weapon swing animation instead of starting immediately from cut-in code. Lightning Spear Skill2 already uses an authored weapon animation event to time gameplay/presentation work, and matching that shape keeps the timing editable in the animation clip.

Implications:
- `FloweringBloomData` owns the cut-in animation trigger, reveal event tag, timeout, and fallback delay.
- The cut-in path temporarily runs the weapon Animator with unscaled time because the Bloom cut-in pauses combat time.
- The cut-in path uses `WeaponAimPresentationSettings` with `FacingSideOnly` so the Skill1 weapon motion ignores aim angle and only mirrors left/right. `FacingSideOnly` locks the cast-time side instead of following live aim changes during the animation.
- The cut-in path acquires a `GameFlowInputBlocker` before pausing combat time so unrelated UI freeze owners, such as pause menu entry, cannot stack on top of the Bloom cut-in and restore a stale `Time.timeScale = 0`.
- The final shake timing also spawns the cut-in completion particle at the player visual center, using the same cleanup/lifetime fallback path as other Flowering cut-in particles.
- The reveal waits on a Flowering-specific animation event tag so the previous SwordSkill2 event reference no longer drives Flowering reveal timing.

## 2026-05-25 - Flowering Bloom Attack Uses Single World-Lingering Slash Activations

Decision:
`Flowering` Bloom attack stays separate from normal combo state, but each `AD_FloweringAttack_Bloom` activation now creates exactly one hitbox-owned BloomSlash visual in world space. Rapid repeated activations provide the Bloom attack sequence; the ability no longer auto-spawns a multi-hit flurry inside one activation.

Reason:
Bloom attack should feel like a consistent rapid slash sequence where each slash remains visible long enough to read as a world-positioned slash mark. Bundling several hits inside one activation made timing harder to tune and made attached hitbox visuals visually follow the player instead of lingering at the strike location.

Implications:
- `AbilityLogic_FloweringAttack` stores the last Bloom hitbox variant index on the `AbilitySpec`, so immediate visual repeat avoidance works across repeated activations.
- BloomSlash prefabs are spawned as independent world objects, not player children; their colliders expire after `activeTime`, while their visuals remain until the authored animation clip finishes.
- `FloweringAttackData` no longer serializes Bloom flurry hit count or interval fields. Attack cadence is tuned through `nextAttackDelay` / `recoveryDuration`.

## 2026-05-25 - Flowering Normal Attack Uses Dedicated LightningSpear-Style Combo

Decision:
`Flowering` normal attack now uses dedicated `FloweringBaseAttackData` and a `Flowering` attack logic dispatcher modeled after the active `LightningSpearAttackData` combo schema. It no longer runs through the shared `SwordCombo2DData` / `AL_SwordCombo2D` path.

Reason:
The active Lightning Spear baseline attack is weapon-specific data and logic with nested `combo.steps[].attackPrefab` authoring, and its slash visual is authored inside the hitbox prefab's `VisualRoot/Render` hierarchy. Matching that shape for Flowering keeps the weapon's basic attack data local, makes step hitbox/visual authoring clearer, and avoids growing the legacy/sample `SwordCombo2D` path for Flowering-only needs.

Implications:
- `AD_FloweringAttack_Base` points to the existing import-stable `AL_FloweringAttack` asset, while `ALData_FloweringAttack_Base` serializes as `FloweringBaseAttackData`; runtime dispatch routes that data to the base attack runner.
- Flowering normal attack remains a three-hit combo with the existing step timing, damage, lunge, hit-event, and hit cue values. Its 1/2/1 slash visuals are owned by `Hitbox_Flowering_BasicAttack1/2/3` prefabs rather than spawned as separate step effect prefabs.
- Bloom attack remains a separate rapid single-slash branch in `AbilityLogic_FloweringAttack` and does not read/write normal combo state.
- `SwordCombo2DData.attackEffectPrefab` stays available for other weapons and compatibility, but Flowering normal attack no longer depends on it.

## 2026-05-25 - Flowering Kill Extension Requires Dedicated Relic

Decision:
`Flowering` Bloom kill duration extension is gated by a dedicated relic-granted gameplay tag. The weapon runtime still owns the actual `+1s` duration change, but it only applies when the relic tag is present.

Reason:
The Notion spec treats kill extension as the Flowering-specific relic effect, not as the weapon's baseline Bloom behavior. Keeping the duration mutation in `FloweringRuntimeState` avoids moving weapon state ownership into relic logic while still making the effect relic-conditional.

Implications:
- `RelicLogic_FloweringBloomExtension_Managed` grants/removes only the Flowering relic tag on equip/unequip/restore.
- `FloweringBloomData.killExtensionRequiredTag` is the data gate checked before `FloweringRuntimeData.ExtendBloom(...)`.
- Bloom status HUD and Skill1 active-duration HUD are runtime projections of `FloweringRuntimeData`; HUD views do not own Bloom gameplay state.

## 2026-05-25 - Flowering Splits Normal Combo From Bloom Attack

Decision:
This historical prototype decision established that `Flowering` normal and Bloom attacks stay separate. Its earlier `SwordCombo2D` and Bloom flurry implementation details are superseded by the dedicated normal attack and single Bloom slash activation decisions above.

Reason:
The design needs a stable three-hit baseline attack before Bloom, while Bloom should feel like a separate rapid state attack that can randomize hitbox-owned visual variants and avoid immediate visual repetition.

Implications:
- `AD_FloweringAttack_Base` and `AD_FloweringAttack_Bloom` continue to be selected separately by Flowering runtime state.
- `AD_FloweringAttack_Bloom` remains on `AbilityLogic_FloweringAttack`, but now owns one world-positioned slash activation at a time.
- Delayed Bloom dash slashes are owned by Bloom runtime state cleanup, not by the short-lived dash ability token.
- Bloom screen border presentation now uses the affection gradient UI graphic/material path for v1 instead of world `SpriteRenderer` border strips.

## 2026-05-25 - TitleScene Authors Shared Fade Service

Decision:
`TitleScene` should have an authored scene-root `SceneFadeTransitionService` for title-origin transitions, instead of relying on the runtime fallback overlay for the intro-to-gameplay load.

Reason:
The fade service is shared transition infrastructure, and the title intro needs the same Inspector-tunable fade-out/load/fade-in timing as gameplay scenes. A scene-root authored service can survive the scene load long enough to finish the fade-in, then yield to the loaded `GlobalUIRoot` authored service after the active transition session ends.

Implications:
- The title fade service should be authored as a root object, not as a child under the title canvas, so it is not destroyed before post-load fade-in completes.
- Loaded authored fade services must defer replacement while any existing fade service is actively transitioning.
- Runtime fallback remains an emergency path when no authored service exists, not the preferred title transition structure.
- Title fade overlay wiring should happen through Unity authoring tools or Inspector review, not direct scene YAML edits.

## 2026-05-24 - Prewarm Trace Uses Per-Tester Files

Decision:
Editor prewarm trace capture writes to tester/machine-specific JSON files under `Assets/LeeJunMo/Datas/Loading/`, while the recommendation tool aggregates all `PrewarmTrace_*.json` files plus the legacy `PrewarmTrace.json`.

Reason:
A single shared tracked trace file creates source-control conflicts when multiple testers run play sessions. Per-tester files let each tester commit their own results independently while preserving aggregate recommendation quality.

Implications:
- `PrewarmTraceRuntime` should not write new editor sessions back into the legacy shared `PrewarmTrace.json`.
- `PrewarmRecommendationWindow` is responsible for merging trace histories and deduplicating sessions by `sessionId`.
- If trace cleanup is needed, remove or archive individual tester files rather than collapsing all results into one generated JSON.

## 2026-05-23 - GlobalUIRoot Owns Persistent UI Instance Selection

Decision:
`GlobalUIRoot` remains the source of truth for persistent global UI ownership. Child global panels such as `SettingsPanelUI` and `KeyBindingPanelUI` may cache or find instances, but they must not destroy an existing persistent representative to replace it with a scene-local duplicate.

Reason:
Scene-local panels can be loaded under duplicate scene UI roots that are destroyed during scene transitions. If a child panel replaces the persistent singleton before that duplicate root is removed, the project can lose both the original persistent UI and the scene-local replacement.

Implications:
- `UIManager` continues to resolve settings/keybinding panels through `EnsureInstance()`.
- Scene-local child panels destroy themselves when another valid non-title instance already exists.
- Pad/keyboard selected-state and EventSystem selected-object behavior remain separate from persistent ownership.

## 2026-05-23 - Shared Hold Buttons Own Hold Input

Decision:
Reusable hold-confirm buttons should let `HoldActionButton` own pointer/keyboard hold timing and progress. Feature screens such as `ChestScreen` should consume hold events for domain behavior instead of recalculating the same hold input and progress.

Reason:
When a feature screen and a shared hold button both drive progress, authored fill visuals can be reset or bypassed by the wrong owner. Keeping hold input in `HoldActionButton` preserves reusable button behavior across chest reroll, tutorial panels, and title intro skip.

Implications:
- `HoldActionButton` supports authored pointer hold and optional keyboard hold.
- `HoldFillButtonView` remains visual projection only.
- Feature screens remain responsible for feature eligibility, side effects, and presentation sequences after hold events.

## 2026-05-24 - Tutorial Pages Reuse One Authored Layout

Decision:
Tutorial explanation UI uses one authored `TutorialInfoPanel` layout that swaps page image/body/title data at runtime. Page navigation belongs to the panel, while final confirmation remains owned by the authored `HoldActionButton` and is enabled only on the last page.

Reason:
The current tutorial plan has no variant layouts. Reusing one panel keeps scene/prefab authoring simple and avoids creating separate UI objects for each tutorial page while still letting designers author page content per trigger.

Implications:
- `TutorialInfoTrigger.pages` is the only tutorial content entry point.
- Prev/Next buttons are authored UI objects; `TutorialInfoPanel` only binds their click events and hides invalid directions with `SetActive(false)`.
- A/D page keys are panel-local navigation shortcuts while the panel is open.
- The Space hold-confirm button should not complete the tutorial until the final page is displayed.
- If future tutorials need different layout structure, add a separate presenter/prefab variant instead of overloading `TutorialInfoPage` or reintroducing top-level fallback content fields.

## 2026-05-23 - Title New-Slot Intro Reuses Profile Launch Target

Decision:
The title intro is a title-scene-local presentation step that runs only before empty-slot `StartNewRun` launches. It does not own or override the destination scene; after completion or skip, launch continues through the existing `TitleProfileSlotService.targetSceneName` / `TitleProfileLaunchService` path.

Reason:
The current project has no dedicated tutorial scene in BuildSettings, and the profile launch target already owns where a selected slot should enter gameplay. Keeping the intro as a presentation gate avoids introducing a second target configuration or a title-specific scene-loading branch.

Implications:
- Existing-slot `ContinueRun` launches bypass the intro and keep the current direct prepare/load path.
- Empty-slot intro completion and skip must call back into the existing profile launch preparation before scene load.
- Future changes to the post-intro destination should edit the profile slot target scene setting, not add an intro-only target override.
- Intro UI remains scene-authored in `TitleScene`; runtime code drives serialized references and does not create the intro UI hierarchy.

## 2026-05-25 - P2 Drain Completion Restores The Drain

Decision:
When a phase-two Slime Queen finishes the drain sink sequence, `DrainPipe` restores to its initial damageable cork state instead of becoming permanently blocked.

Reason:
The design intent is a temporary 4-second boss groggy/control-loss mechanic. The drain should be usable again after being re-opened by hits, not removed from future play.

Implications:
- `DrainPipe` resets `currentHitCount` to `0` after the P2 boss exits.
- The drain visual returns to the cork color/state.
- Existing captured Pawn slime targets are cleaned up during restore so disabled suction targets do not remain.
- Future P2 drain changes should avoid permanent disable/blocked flags unless a separate design request asks for one-time drains.

## 2026-05-25 - Boss HUD Uses One Slot Per Boss

Decision:
`BossHudController` manages registered bosses as separate HUD slots. A multi-body boss phase displays one slot per active boss body instead of projecting multiple bodies into one dual health/groggy bar.

Reason:
The slot model is easier to reason about than a second dual-channel rendering path inside `BossHealthBarUI` and `BossGroggyBarUI`. It keeps the common HUD controller boss-agnostic without requiring source adapters for every multi-body boss shape.

Implications:
- Bosses call `RegisterBoss`, `MarkBossDefeated`, and `UnregisterBoss` through the common HUD registration API.
- Slime Queen phase two Short/Long bodies register as independent slots.
- `BossHealthBarUI` and `BossGroggyBarUI` remain single-channel views.
- `IBossSplitHealthPresentation` is still available for split labels on a single boss slot, but dual boss rendering is not a supported HUD path.

## 2026-05-23 - Run-Special Unavailable Responses Belong To Choices

Decision:
Run-special NPC unavailable response lines belong to the selected `RunSpecialNpcChoiceDefinition`, not to a top-level dialogue branch, when the failure is discovered at choice execution time.

Reason:
Different choices and future feature actions can fail for different reasons. Keeping the failure line with the choice lets authors control "what the NPC says after this selected option cannot execute" without forcing every feature to add another initial branch.

Implications:
- `RunSpecialNpcInteractor` must check `CanExecute(...)` before success response lines and before `Execute(...)`.
- Unavailable choices may remain visible only when the choice has `unavailableResponseLines` and the feature explicitly allows showing that unavailable state.
- Construction insufficient-funds feedback is a payment-choice unavailable response, while construction pending/completed remain feature branch states.

## 2026-05-23 - Same-Scene Teleport Splits Appearance And Landing Points

Decision:
Run-special same-scene teleport uses two authored anchors: `appearancePoint` for where the player becomes visible after fade-out, and `landingPoint` for the final playable position. The old `destination` serialized reference migrates to `landingPoint`.

Reason:
The teleport arrival presentation needs a separate visual start position and final control-restoration position. A single destination can move the player, but it cannot express "appear here, then land here" without overloading one transform.

Implications:
- `landingPoint` is required for teleport availability.
- `appearancePoint` is optional; if it is empty, the feature uses `landingPoint` as the appearance point and preserves the old one-anchor behavior.
- Player protection and targetability blocking release after the appearance-to-landing movement finishes.

## 2026-05-23 - Same-Scene Teleport Uses Player Targetability Gate

Decision:
During run-special same-scene teleport execution, the player is protected by both `PlayerCinematicProtection` and `PlayerTargetabilityBlocker`. Enemies keep their normal perception flow, but `Enemy.CanPerceiveTarget(...)` treats a blocked player target as not perceivable.

Reason:
Teleport arrival presentation should not make nearby enemies chase or attack while the player is still in a cinematic/control-locked state. Invulnerability already prevents damage, but it should not also become the meaning for enemy recognition.

Implications:
- `PlayerTargetabilityBlocker` is a player-owned owner-token gate and can be reused by future player-centered presentation flows.
- Enemy recognition resumes when the teleport feature releases the gate, currently after its fade/warp/arrival movement execution finishes.
- Boss combat activation and general enemy objects are not disabled by this gate; only player targetability is filtered.

## 2026-05-23 - Same-Scene Teleport Executes After Run-Special Presentation Close

Decision:
Only same-scene teleport run-special features execute after speech/letterbox/HUD presentation closes and gameplay time is restored. Construction and other default run-special features continue to execute inside the paused dialogue flow.

Reason:
`RunSameSceneTeleportNpcFeature` relies on `MovementMotor2D.WarpTo(...)`, which applies the warp in `FixedUpdate`, then waits for `WaitForFixedUpdate`. Running that while `RunSpecialNpcInteractor` holds `Time.timeScale = 0` can fade the screen without ever applying the warp.

Implications:
- `RunSpecialNpcFeatureBase.ExecuteAfterRunSpecialPresentationClose` defaults to `false`.
- `RunSameSceneTeleportNpcFeature` overrides that policy to `true`.
- `RunSpecialNpcInteractor` restores run timer pause and `Time.timeScale` before executing post-presentation features, while keeping input/player locks until the feature finishes.

## 2026-05-23 - Run-Special NPC Dialogue Data Lives In DialogueSetSO

Decision:
Run-special NPC authored lines and choices live in `RunSpecialNpcDialogueSetSO`. `RunSpecialNpcInteractor` reads that SO and asks its `primaryFeature` for the current `RunSpecialNpcDialogueBranchKey`; feature-specific branch selection and text formatting stay on the feature implementation.

Reason:
Construction and teleport NPCs need different branch states, but the authored line/choice data should be reusable content rather than extra scene components. Keeping SO data separate from scene feature references also avoids putting scene component references inside reusable assets.

Implications:
- `RunSpecialNpcInteractor` owns presentation flow and generic choice-action execution only.
- `RunConstructionNpcFeature` returns construction branch keys, including `ConstructionInsufficientFunds`, and resolves the `N일` token.
- `RunSameSceneTeleportNpcFeature` returns teleport branch keys.
- `RunSpecialNpcDialogueSetSO` uses feature-kind-specific branch groups, shown through a custom Inspector.
- Line breaks authored inside one `RunSpecialNpcLine` text field are interpreted as separate speech-bubble lines at playback time. Visual wrapping inside one line should rely on the speech bubble layout instead of hard line breaks.
- Choice execution is represented by action enum values such as `ExecutePrimaryFeature`, not direct scene component references in the SO.
- The earlier provider-component decision is superseded for runtime. Provider components are migration-only compatibility until scene/prefab data is moved to SO assets.

## 2026-05-23 - Run-Special NPC Dialogue Branching Lives In Providers

Decision:
Run-special NPC line and choice branching belongs to feature-specific `RunSpecialNpcDialogueProviderBase` components. `RunSpecialNpcInteractor` remains the presentation flow owner that executes provider-built branches, but it should not contain construction-specific, teleport-specific, or future feature-specific branch rules.

Reason:
Construction and teleport NPCs split dialogue on different state axes: construction progress, completion, remaining run count, affection lock, destination availability, and feature execution eligibility. Keeping those decisions inside the interactor made generic presentation code depend on one feature's serialized field names and token replacement rules.

Implications:
- Construction dialogue state lives in `RunConstructionNpcDialogueProvider`.
- Same-scene teleport dialogue state lives in `RunSameSceneTeleportNpcDialogueProvider`.
- `RunSpecialNpcInteractor` asks for a `RunSpecialNpcBranch`, plays lines, shows choices, then executes the selected feature.
- Feature-specific line text formatting, such as the construction `N일` remaining-run token, belongs to the provider that understands the feature.
- Moving old interactor-authored fields to provider components requires Unity Editor migration; `[FormerlySerializedAs]` is not enough because the data moves across components.

## 2026-05-23 - Encyclopedia Relic Body Uses Logic Tooltip

Decision:
The encyclopedia Relic RightPage displays `RelicLogic.BuildTooltip(...).effectText` as the relic body text. It does not display `RelicDefinition.description` in the current Item slice.

Reason:
The inventory hover `ItemDetailPanel` and the encyclopedia should expose the same relic information. Relic logic assets are the source of truth for effect text, formatting tokens, bullet lines, glossary links, and per-level preview output; the serialized relic description can drift from the actual drop tooltip.

Implications:
- `EncyclopediaItemRightPage` writes formatted relic effect text into the authored body slot used by `StoryText`.
- `RelicDefinition.description` remains available for item data, but is not used by the encyclopedia Relic tab body in this slice.
- A level `1 / 1` relic still shows Prev/Next preview guides as disabled authored controls instead of hiding the guide objects.

## 2026-05-22 - Encyclopedia Content Disappear Reuses ContentAppear Clip

Decision:
The encyclopedia book content transition uses the authored `ContentAppear` animation clip as the single content-cover source. Appear samples the clip forward. Disappear samples the same clip backward through `AnimationClip.SampleAnimation`; it does not require a separate `DisAppear` clip and does not use negative Animator speed.

Reason:
The content cover is a presentation overlay, not gameplay state. Keeping one authored clip reduces prefab wiring and avoids Animator reverse-play edge cases while still allowing a visible hide-before-swap and reveal-after-swap sequence.

Implications:
- `EncyclopediaBookPresentation` depends on `pageCoverImage` and `contentAppearClip`; `pageCoverAnimator` is optional.
- If `pageCoverAnimator` exists on the same object, it must not drive the cover while `SampleAnimation` is sampling, because its default state can replay `ContentAppear` forward and override the reverse disappear frames.
- Item sub-tab transitions should sequence content disappear, page turn, content swap, and content appear through the book presentation.
- Slot selection remains an immediate RightPage bind in this slice.

## 2026-05-22 - Encyclopedia Item Detail Uses Dedicated Presenter

Decision:
The encyclopedia Item tab uses `EncyclopediaItemRightPage` as the sole active RightPage item presenter. It does not use `ItemDetailPanel.Instance`, hover-canvas adoption, drag/drop, inventory tooltip behavior, or the older `EncyclopediaDetailPanel` component on the active Item layout.

Reason:
Runtime inventory item detail UI owns hover positioning and canvas migration behavior that conflicts with a scene-authored book page. The encyclopedia needs stable serialized RightPage references that project selected item data without owning gameplay state.

Implications:
- Item details for `Weapon`, `Relic`, and `Consumable` read from `ItemDatabase` and bind through `EncyclopediaItemRightPage`.
- Weapon ability rows are pooled from `Panel_AbilityBlock_Encyclopedia.prefab` / `WeaponAbilityBlockView`, not from `WeaponDetailViewV2`.
- `ItemDetailPanel` under `RightPage` is a layout/ScrollRect/content host only. Presenter ownership should stay on `RightPage` through `EncyclopediaItemRightPage`.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` removes duplicate child `EncyclopediaItemRightPage` presenters and legacy `EncyclopediaDetailPanel` components under RightPage.
- Boss affection/reward detail remains out of this presenter and should use a future boss-specific detail component.
- Inspector wiring should target `EncyclopediaItemRightPage` authored header/detail roots and the encyclopedia ability-block prefab, not runtime hover detail components.

## 2026-05-22 - Encyclopedia Item Tab Owns Item Page State

Decision:
`EncyclopediaScreen` owns popup stack behavior, open/close presentation, and top-level tab entry. `EncyclopediaItemTab` owns Item sub-tab state, page index, selected entry, and item data binding. `EncyclopediaItemLeftPage` and `EncyclopediaItemRightPage` own the authored left/right page references for the Item tab.

Reason:
Keeping every Item control and selection field on `EncyclopediaScreen` made the Inspector hard to understand and would not scale when Monster and Boss get their own tab-specific layouts. Splitting the Item tab keeps screen-level presentation separate from item-specific content behavior.

Implications:
- `EncyclopediaScreen.OpenUI()` calls `EncyclopediaItemTab.ShowDefault()` after the book-open presentation completes.
- `OpenWeaponSubTab`, `OpenRelicSubTab`, `OpenConsumableSubTab`, and page-button methods remain on the screen only as button-friendly forwarding methods.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` are obsolete/migration fallback components, not the preferred new authoring target.
- Monster/Boss work should add their own tab/presenter components instead of expanding Item tab state back into `EncyclopediaScreen`.

## 2026-05-22 - Encyclopedia Tome Open Uses Authored Animator Sequence

Decision:
Tome interaction opens the encyclopedia through a `DimPanel` fade, a separate book motion-root drop, and the child book Animator `BookOpen` state. Close uses `BookClose`, LeftPage sub-tabs use `BookLeftPage`, and future RightPage main-tab transitions use `BookRightPage`.

Reason:
The book asset already exposes authored animation states, and the layout should remain scene-authored. Moving the parent motion root keeps screen-level drop timing separate from the child book Animator's page/book clips.

Implications:
- `EncyclopediaBookPresentation` owns sequencing and content visibility around the Animator states.
- `EncyclopediaScreen` delays list/detail binding until the open presentation completes.
- Unity authoring must wire `DimPanel`, `BookMotionRoot`, the book Animator, and optional content roots instead of relying on runtime hierarchy creation.

## 2026-05-05 - Use Docs as Project Memory Vault

Decision:
Use `Docs/` as both the Obsidian vault and the official project Markdown memory store.

Reason:
Codex and the developer should read the same source-of-truth documents. Keeping a separate vault would create drift between human notes and agent context.

Implications:
- `Docs/.obsidian/` remains local and ignored.
- Markdown documents in `Docs/` are project assets.
- Obsidian is the editing/navigation UI, not a separate authority layer.

## 2026-05-05 - Use AGENTS.md for Codex Project Instructions

Decision:
Place project-specific Codex instructions in the repository root `AGENTS.md`.

Reason:
Codex uses `AGENTS.md` as durable project guidance. This keeps execution rules close to the code and separate from runtime configuration.

Implications:
- `.codex/config.toml` is reserved for future MCP, sandbox, profile, and approval settings.
- Project behavior rules live in `AGENTS.md`.

## 2026-05-05 - Treat Reviews, Notes, and Handoffs as Reference-Only

Decision:
`Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only unless promoted into `Contracts` or `Architecture`.

Reason:
Older reviews and handoff notes are valuable context, but they can conflict with current architecture and contracts.

Implications:
- Current implementation decisions should prefer `Contracts` and `Architecture`.
- Review documents can explain why a decision exists, but should not override active rules.

## 2026-05-06 - Update CurrentTask on Active Work Changes

Decision:
Update `Docs/CurrentTask.md` whenever the active implementation task changes.

Reason:
The file was being read but not updated, so it no longer represented the actual task in progress.

Implications:
- `CurrentTask.md` should hold the current goal, scope, and done criteria.
- Detailed implementation notes belong in `Docs/SessionLogs/`.
- Durable design choices still belong in `Docs/DecisionLog.md`.

## 2026-05-06 - Shop v2 Uses Definition-Driven Runtime Policy

Decision:
Use `ShopDefinitionSO` as the source of truth for merchant shop settings, and combine it with `RunModifierService.ShopModifiers` through a shop policy layer.

Reason:
Upgrade effects now modify shop availability, slot count, discounts, and refresh count. Keeping those policies inside `MerchantNPC` would make scene presentation, stock state, and upgrade logic too tightly coupled.

Implications:
- Merchant stock remains run/session scoped in `GamePlayData.merchantStates`.
- Existing stock is preserved when discounts change.
- Existing slots are preserved when slot count expands; only newly opened slots are rolled.
- Scene and prefab references must be manually wired to a `ShopDefinitionSO`.

## 2026-05-10 - Keep Lightning Spear Basic Attack Weapon-Specific

Decision:
Do not keep the temporary `WeaponComboAttack2D` shared layer. Lightning Spear basic attack owns its combo data and execution logic, while `SwordCombo2D` remains a legacy/sample ability.

Reason:
The shared layer only had one active consumer and made it unclear whether Sword combo behavior had become project-wide weapon policy.

Implications:
- Lightning Spear attack tuning stays in `LightningSpearAttackData`.
- New weapon combo logic should not depend on the removed shared runner by default.
- `SwordCombo2D` assets and logic remain untouched unless a separate task targets them.

## 2026-05-14 - Boss Rewards Use Additive Modifier Aggregates

Decision:
Keep `StageLootTable` and `LootManager` as the base boss reward source, and let Affection, upgrades, and future systems contribute only additive boss reward modifier aggregates.

Reason:
Affection should change extra boss rewards without bypassing stage progression reward rules. Keeping base rewards and modifiers separate prevents NPC Affection assets from becoming an alternate reward table system.

Implications:
- Boss chest base loot comes from `LootManager.GenerateBossChestLoot(...)`, which reads boss-specific count and rarity defaults from `StageLootTable`.
- Boss magic stone base count still comes from `LootManager.GetBossMagicStoneCount()`.
- Boss field heal base count comes from `StageLootTable`, while extra field heals remain additive modifiers.
- Boss-specific extra items, extra magic stones, extra field heals, and boss chest count deltas are additive modifiers.
- The former `BossDrop` adapter was temporary and has since been deleted; dedicated reward and portal components are the supported path.

## 2026-05-14 - Treat Markdown as Structure Memory

Decision:
Use project Markdown not only as task history, but as structure memory that helps future agents quickly understand previously changed systems before editing them again.

Reason:
Session logs that only say what was done are not enough when a later task starts in a different part of the codebase. Future work is faster and uses fewer tokens when the log also records ownership, lifecycle, key files, verification, and the next document or source file to read.

Implications:
- Material architecture, ownership, runtime state, lifecycle, shared service, interface, asmdef, MonoBehaviour, ScriptableObject, and prefab-facing changes should leave a concise task entry in `Docs/SessionLogs/YYYY-MM-DD.md`, and a feature-level `Docs/StructureMemory/` entry when future context reconstruction needs a stable system map.
- Durable decisions still belong in `Docs/DecisionLog.md`.
- `Docs/Architecture/` and `Docs/Contracts/` remain source-of-truth documents and should only be rewritten with explicit approval.
- Small implementation edits do not need new memory documents unless they change how future work should understand the system.

## 2026-05-14 - GameFlowInputBlocker Owns Flow Input Blocks

Decision:
Use `GameFlowInputBlocker` as the reusable lifecycle wrapper for temporary game-flow input blocks, while `UIManager` remains the central policy owner.

Reason:
Chest first-open sequences, dialogue playback, upgrade open fades, reward open presentations, and future authored flows all need the same temporary block behavior without each system directly manipulating `UIManager` internals.

Implications:
- New flow code should acquire/release `GameFlowInputBlocker` instead of calling `UIManager.SetExternalUiInputBlocked(...)` directly.
- `UIManager.TryPushUIForExternalBlockOwner(...)` is the owner exception path for a flow that must open its own stack UI while the block is active.
- The blocker is for stack-outside flow gaps and presentation windows; opened stack UI screens still express time freeze/control locks through `IStackableUI.GameplayLockProfile`.
- `GameFlowInputBlocker` must release from normal completion and from `OnDisable`/`OnDestroy` cleanup paths so interrupted flows do not leave controls locked.

## 2026-05-14 - Use Feature-Level StructureMemory and RefactorBacklog

Decision:
Add `Docs/StructureMemory/` for feature-level structure maps and `Docs/RefactorBacklog/` for feature-level structural debt tracking.

Reason:
Date-based session logs are useful for recent work, but they scatter system context over time. Future agents need a faster way to understand current structure and known refactor candidates before editing related systems.

Implications:
- `StructureMemory` is a fast context map, not a source-of-truth replacement for `Architecture` or `Contracts`.
- `RefactorBacklog` tracks intentional structural debt with target shape, risks, and refactor triggers; it is not a generic TODO list.
- Small edits usually update only `SessionLogs`; reusable structure, known debt, durable decisions, and recurring mistakes update the narrower matching memory document.
- Stable structure can be proposed for promotion from `StructureMemory` to `Architecture` or `Contracts`, but those official documents still require explicit approval before editing.

## 2026-05-15 - Prototype Runtime UI Creation Is Temporary

Decision:
Runtime creation of UI or presentation objects is acceptable for first-pass prototyping, debug fallback, or emergency fallback, but production-facing UI and presentation should be promoted to scene- or prefab-authored objects before build-facing use.

Reason:
Runtime-created UI is useful for quickly checking feel and avoiding blocked iteration. Keeping it as the final structure hides sorting, raycast, scaling, animation, and serialized-reference choices in code, making Unity authoring, inspection, and prefab/scene verification harder.

Implications:
- Gameplay-facing UI should normally live under scene/prefab-authored objects, often through `GlobalUIRoot` canvas layers or explicit serialized references.
- Runtime-created `Canvas`, `EventSystem`, button, TMP, image, overlay, and hierarchy fallbacks must be clearly treated as prototype/debug/fallback paths, not silent production defaults.
- Any runtime-created UI that remains beyond prototype should have explicit owner, cleanup, and a prefab/scene migration follow-up.
- Pooled gameplay presentation objects may still be instantiated from authored prefabs; the prefab is the authored unit, while the runtime service owns spawning and cleanup.
- Existing runtime fallback presentation debt is tracked through `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md`.

## 2026-05-15 - Use ContentAuthoring Guides for Repeatable Content Pipelines

Decision:
Manage repeatable combat-content production through a `Docs/Guides/ContentAuthoring/` hub and focused type-level pipeline guides.

Reason:
Weapons, mobs, bosses, relics, consumables, and reward links will keep growing. A single structure map is useful for code review, but content creators need a production-order guide that starts from authored data and ends at loot, inventory, presentation, save, and verification.

Implications:
- `ContentAuthoring` guides are production entry points, not replacements for `Contracts` or `Architecture`.
- Stable rules still belong in `Docs/Contracts/` or `Docs/Architecture/` after explicit approval.
- Structure-specific context remains in `Docs/StructureMemory/`, and concrete structural debt remains in `Docs/RefactorBacklog/`.
- New content pipelines should link to source-of-truth documents instead of copying their full rules.

## 2026-05-15 - Treat TitleScene as App And Gameplay Session Boundary

Decision:
Use `Docs/Architecture/SceneDomainBootstrapArchitecture.md` as the source-of-truth document for title-to-game scene bootstrap ownership. `TitleScene` is both the app entry scene and the gameplay session boundary.

Reason:
Title, profile selection, return-to-title, gameplay runtime presentation, camera rig behavior, and run-session cleanup were implemented across several services without a single documented boundary.

Implications:
- App-scope services may exist on title, but gameplay-scope runtime presentation should not treat title as active gameplay.
- Title-local UI/camera presentation stays scene-authored unless an explicit fallback/prototype path is documented.
- Return-to-title changes must preserve run/session cleanup before or during the title scene transition.
- Code refactors in this area should start from `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md`.

## 2026-05-15 - Use Source-Structure Verification For Unity Script File Splits

Decision:
For source-only Unity script file splits, proceed with helper `.cs` file extraction when behavior and APIs are unchanged, even if Unity-generated `.csproj` files have not refreshed yet.

Reason:
Unity Editor import/compile is the reliable final check for new `Assets/` scripts, while generated `.csproj` files can lag behind the filesystem and block otherwise safe source-only refactors.

Implications:
- Do not manually edit generated `.csproj` files.
- Verify moved helpers by source structure: duplicate definitions, original helper removal, call-site references, namespace/global-scope compatibility, and whitespace.
- Run MSBuild only when generated project files include the relevant source files.
- Do not claim Unity compile or MSBuild success for new files unless that verification actually ran.

## 2026-05-15 - Element Build-Up Applies From ElementOffenseSource

Decision:
Applied elemental build-up is resolved from the attacker's `ElementOffenseSource` through `ElementBuildUpResolver`, not from per-hit `DamagePayloadConfig.elementFormulas` or `elementBuildUps` payloads.

Reason:
The combat application path already used attacker-wide elemental build-up, while legacy per-hit fields made weapon tuning intent ambiguous.

Implications:
- `DamagePayloadConfig.elementFormulas`, `CombatDamageSnapshot.ElementBuildUps`, and `elementBuildUps` / `elementInputs` API parameters are compatibility debt until serialized data and call sites are migrated.
- Future per-hit elemental tuning must be introduced as a named merge/override policy instead of reviving the old implicit payload path.
- Elemental weapon tuning should verify attacker `ElementOffenseSource`, stat provider wiring, and `ElementBuildUpFormulaProfile` rather than per-hit payload formulas.

## 2026-05-16 - RouteSets Reference Boss Special Reward Presets

Decision:
Let `CorridorBossRouteSetSO` reference only an optional `BossSpecialRewardPresetSO` for boss-specific special loot candidates. Authored chest/portal objects live in boss scenes, and reward amount changes remain runtime modifier overlays.

Reason:
Route sets already compose the corridor/boss scene, BGM, and loading context for a run stage, so linking a boss-specific special loot preset there is acceptable. Putting common chest, magic stone, portal prefabs, portal offsets, placement policy, magic stone bonuses, field-heal bonuses, or chest-count deltas on route data mixes content composition with shared prefab catalogs and runtime reward effects.

Implications:
- `CorridorBossRouteSetSO` may reference `BossSpecialRewardPresetSO`, but should not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- `BossSpecialRewardPresetSO` owns only boss-specific special loot candidates.
- Upgrade, Affection, and future runtime effects should keep contributing `BossRewardModifierAggregate` overlays instead of mutating preset SO assets.
- Scene/prefab-owned `TreasureChest` and `ScenePortal` objects provide chest and portal positions through their own transforms.
- `BossBattleEndDefinitionSO`, `BossRewardProfileSO`, `BossBattleEndPrefabCatalogSO`, and `BossBattleEndAnchors` are deleted; route special reward preset and scene `BossBattleEndHandler` are the supported authoring path.

## 2026-05-16 - Remove BossDrop Legacy Adapter

Decision:
Delete the `BossDrop` legacy adapter instead of keeping it as a prefab-safe fallback.

Reason:
Boss battle-end ownership is now split across route-linked special reward presets, scene `BossBattleEndHandler`, and run-progress coordination. Keeping `BossDrop` would preserve a second reward/portal authoring path and hide missing RouteSet or handler wiring.

Implications:
- Serialized scene/prefab references to the old `BossDrop` script GUID must be removed during the migration.
- `BossRewardFallbackService` must not read `BossDrop` fields.
- Unity Editor verification must run the boss battle-end validator and play-check boss death reward/portal behavior because deleted `BossDrop` field values are no longer available as fallback data.

## 2026-05-16 - Remove BossRewardModifierSO Authoring Layer

Decision:
Do not keep a separate `BossRewardModifierSO` asset layer. Boss-specific special loot candidates can live in a RouteSet-linked `BossSpecialRewardPresetSO`, while Affection, upgrades, and future runtime effects expose their own fields and project them into `BossRewardModifierAggregate` at runtime.

Reason:
The modifier SO duplicated boss reward authoring responsibility and made runtime-changing effects look like mutable asset state. Runtime effects need additive overlays without mutating ScriptableObject reward presets.

Implications:
- `BossRewardModifierAggregate` remains the runtime value type for combining boss reward deltas.
- Affection and upgrade effects should author their own relevant values directly, then emit aggregates during run modifier rebuild.
- Existing assets that referenced `BossRewardModifierSO` need migration to direct effect fields or RouteSet special reward preset data.
- New boss-specific base special loot candidates should be configured through route-linked special reward presets, not modifier assets.

## 2026-05-16 - StageLootTable Owns Boss Base Reward Defaults

Decision:
Keep boss base reward defaults in `StageLootTable`: boss weapon count profile, boss relic count profile, boss Common/Rare/Epic relic rarity weights, boss magic stone count, and boss field heal base count.

Reason:
These values are stage progression defaults, not boss prefab wiring and not RouteSet composition. RouteSets should only identify the optional boss-specific special loot preset for the selected boss route, while runtime systems such as Affection and Upgrade should add overlays through `BossRewardModifierAggregate` without mutating ScriptableObjects.

Implications:
- Normal chest rewards use normal chest profiles; boss chests use the boss-specific generation path.
- RouteSet `BossSpecialRewardPresetSO` entries are only additional boss-specific special loot candidates.
- `BossRewardModifierAggregate` remains the only runtime overlay path for boss magic stone bonuses, field heal bonuses, boss chest count deltas, and runtime bonus loot.
- Boss reward base values should be reviewed in the StageLootTable Inspector, not on boss prefabs or RouteSet assets.

## 2026-05-16 - Keep Scene-Facing Compatibility Facades For P2 Closure

Decision:
Keep `UpgradeManager`, `GamePlayDataManager`, and `ScenePortalTravelService.TryTravel(...)` as the public/scene-facing compatibility surfaces while moving more ownership into helpers.

Reason:
These names and entry points are already referenced by scenes, prefabs, runtime bootstrap, portal interaction, upgrade UI flows, and save/run lifecycle code. Renaming them would create serialized reference and static call-site risk without improving the current player-facing behavior.

Implications:
- `UpgradeManager` remains the public upgrade facade, but data-change/save/UI-close notification ownership goes through `UpgradeNotificationService`.
- `GamePlayDataManager` remains the scene-facing run/session holder, but volatile timer, pending transition, pending player state, pending reward, affection, and shortcut mutations go through `RunSessionStateService`.
- `ScenePortalTravelService.TryTravel(...)` remains the static portal entry point, but route resolution, transition lock, manager resolution, and execution handoff live behind `ScenePortalTravelCoordinator` and existing planner/executor helpers.
- Future refactors should not reopen MonoBehaviour or static entry naming unless a scene/prefab migration pass is explicitly planned.

## 2026-05-16 - Treat Representative GlobalUIRoot As Build-Facing Presentation Root

Decision:
Use `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab` as the representative build-facing runtime presentation root for loading, cursor, status HUD, and Boss HUD authored-reference validation. Keep the display letterbox runtime-generated by `GamePresentationController`.

Reason:
Runtime presentation fallbacks are useful as emergency/debug paths, but build-facing UI structure needs a single prefab target that can be inspected, auto-fixed, validated, and reused by scene setup.

Implications:
- Other GlobalUIRoot variants are warning/follow-up targets unless they are promoted to build-facing roots.
- `MouseCursorService` must prefer authored references before creating fallback presentation hierarchy.
- `GamePresentationController` owns display letterbox runtime hierarchy because the overlay is resolution/window-mode policy rather than prefab layout.
- `Tools/Validation/Scene Setup Validator` should report missing representative prefab references and offers an auto-fix path for cursor authoring.
- Unity Editor validation/play checks remain required because code validation cannot prove canvas order, input behavior, or visual layout.

## 2026-05-16 - Keep Shared ScenePortal Prefab Semantic-Neutral

Decision:
Keep the shared `ScenePortal.prefab` at `TransitionType.None` with no `RunRouteCatalogSO` reference. Hub start portals must carry `HubToRunStart` and the run route catalog on their scene instance, while boss exit portals rely on the active `PortalRouteManager` plan to resolve `BossToCorridor` or `ReturnToHubAfterRun`.

Reason:
The same portal prefab can be reused by hub, corridor, and authored boss exit portal instances. Letting the prefab carry `HubToRunStart` made boss exit portals behave like run-start portals and bypassed the intended active route semantics.

Implications:
- Boss battle-end no longer reads `BossBattleEndPrefabCatalogSO.portalPrefab`; authored boss exit portals should use semantic-neutral `ScenePortal` instances.
- Hub start portals are the only supported owners of `RunRouteCatalogSO` references.
- Boss exit portal placement is authored by the portal object's own transform.
- Runtime boss portal creation is not supported; missing `portalObj` is an authoring error.

## 2026-05-17 - Boss BattleEnd Chest And Portal Are Authored Activation Objects

Decision:
Boss battle-end `TreasureChest` and `ScenePortal` objects are authored in the scene and toggled inactive/active by a scene-authored `BossBattleEndHandler`. Runtime chest/portal prefab creation, anchor-driven portal movement, boss-position fallback, and boss-prefab-owned battle-end placement are not supported.

Reason:
The required behavior is simple: after boss death, a chest and a portal appear at predetermined positions and the portal routes through the active `RunRouteCatalogSO` plan. Authoring the objects directly makes their transforms the source of truth and avoids code-side spawn offsets, captured positions, or boss-child transform ambiguity. The handler belongs with the scene's battle-end placement setup, not with the reusable boss actor prefab.

Implications:
- `BossBattleEndHandler` references the `BossControllerBase`, authored `TreasureChest`, and authored exit portal.
- `BossBattleEndHandler` initializes the authored chest with generated boss loot and activates it.
- Boss magic stones and field heals remain physical runtime drops. Their counts come from `StageLootTable` plus `BossRewardModifierAggregate`, and their drop origin is the boss death position rather than the authored chest/portal position or a separate anchor.
- `BossBattleEndHandler` activates the authored exit portal without moving it.
- `BossRewardSpawner`, `BossExitPortalActivator`, `BossBattleEndAnchors`, and `BossBattleEndPrefabCatalogSO` are deleted; new authoring should not add replacement spawn/anchor components for this flow.
- Boss exit portals should not carry `RunRouteCatalogSO`; hub start portals remain the route catalog owner.

## 2026-05-17 - Keep Markdown As Source And Use Presentation HTML As Human Dashboard

Decision:
Keep Markdown documents as the project documentation source of truth, and use `Docs/Presentation/` HTML/CSS/JS only as human-readable navigation dashboards.

Reason:
Markdown is cheaper for Codex context, easier to diff, and better for source edits. HTML is better for human scanning but adds presentation markup and should not duplicate full source document content.

Implications:
- Codex should continue reading Markdown documents first.
- Presentation HTML should stay thin and source-linked rather than copying full Markdown bodies.
- Routine dashboard updates should usually touch `_shared/docs-data.js`; HTML shells, shared CSS, and render JS should change rarely.
- If duplicated content drifts, update the Markdown source first and regenerate or adjust the presentation view from that source.
- Presentation HTML may be updated only when requested or approved by the authorized HTML maintainer `nadoman354`.
- When Markdown changes may make Presentation HTML stale, Codex should report the affected HTML page and required summary/diagram update instead of editing it automatically.
- `refactor-board.html` may exist as an approved thin overview page, but it must remain a risk/trigger board that links back to `RefactorBacklog`.
- `session-summary.html` is not kept as a Presentation entry by default because session logs remain Markdown-first history rather than a regular human decision surface.
- `authoring-guide.html` may be more detailed than the other Presentation pages when it works as a production handbook, but it should still link back to Markdown source documents instead of copying full pipeline bodies.
- `architecture-overview.html` should prefer focused UML-like ownership and runtime-flow diagrams over exhaustive component listings.
- Mermaid diagrams may be rendered from CDN for human-readable pages; if CDN rendering is unavailable, the Mermaid source text should remain readable rather than blocking the document.

## 2026-05-17 - Shop Slots Are Prefab-Instantiated From Typed Anchors

Decision:
Prefer authored slot anchor transforms plus a `ShopSlot` prefab reference for merchant shop layouts. Each anchor can specify the stock type filter (`Any`, `Weapon`, `Relic`, or `Consumable`) used by shop stock rolling.

Reason:
Scene-copied shop slot objects made layout maintenance repetitive and mixed visual placement with live slot behavior. Anchors keep scene authoring focused on position, while the prefab keeps slot presentation/interaction changes centralized.

Implications:
- `MerchantNPC` may instantiate or reuse `ShopSlot` instances under configured anchors during play.
- Existing child `ShopSlot` objects remain a compatibility fallback when prefab-slot authoring is not configured.
- `ShopDefinitionSO` still owns visible slot count, stock weights, and max weapon/consumable caps.
- Typed slot layouts must keep `ShopDefinitionSO.MaxWeaponSlots` and `MaxConsumableSlots` aligned with the intended display.

## 2026-05-17 - Enemy Player Targeting Uses Canonical Player Root

Decision:
For enemies targeting the `Player` tag, resolve the target transform through `PlayerRuntimeRegistry`/`PlayerInteractor2D` and map player-owned child colliders or directly assigned child transforms back to the player root.

Reason:
Player-attached relic/effect objects such as orbiting feathers can have colliders near the player. If boss encounter or combat movement targets those child objects, the boss appears to move even when the actual player is stationary.

Implications:
- Shared `Enemy` target acquisition should not use a player-owned child collider transform as the movement target.
- Explicit boss/enemy target assignment should normalize player-owned child transforms before the blackboard or movement code reads target position.
- Future player-attached objects should still avoid unintended `Player` tags, hurtboxes, or body-blocking colliders.
- Collision and damage target authoring remain separate from movement targeting and should be reviewed independently when new attached objects are added.

## 2026-05-17 - Final Boss Route Skips Chest Activation

Decision:
When `BossRewardContext.IsFinalRouteSet` is true, `BossBattleEndHandler` keeps the authored `TreasureChest` inactive, still spawns boss physical drops, marks rewards handled, and lets portal activation continue through the existing path.

Reason:
The final route ends the run sequence, so a post-boss reward chest is not needed. Physical drops are not chest presentation and should keep behaving like other boss death drops. Keeping portal handling unchanged preserves the existing final-route exit flow.

Implications:
- Final boss scenes can keep the common `BossBattleEndHandler` wiring without spawning/showing a chest.
- Final boss physical drops such as magic stones and field-heal pickups still spawn from the boss death position.
- Do not add a final-boss chest fallback unless final-route reward policy changes.
- Non-final boss reward behavior remains unchanged.

## 2026-05-17 - Room And Chest Locks Count Registered Combatants Plus Slime Splits

Decision:
Room and chest monster-kill locks count spawn-registered enemy roots and Slime split descendants that inherit a registered parent's lock context. General direct summons are excluded from lock conditions. Transform or phase changes on the same tracked GameObject remain the same enemy. Death presentation remains lock-active until the tracked root GameObject is destroyed.

Reason:
Lock state should follow explicit room/chest participation instead of every enemy instantiation. Slime splitting is the intended exception because the split children replace the parent's combat presence and must keep doors/chests locked while alive.

Implications:
- `RoomDoorMonsterKillLock` and `ChestMonsterKillLock` continue using tracked root GameObject lifetime.
- Slime split code must register valid split children into the same lock context as the parent.
- Boss/local summons and other direct `Instantiate(...)` enemies do not affect room/chest clear unless a future design explicitly registers them.
- No-loot or gimmick enemies count only when they enter through the same spawn registration or Slime split inheritance path.

## 2026-05-18 - Electric Trigger Splits Electrocute Status From Discharge Logic

Decision:
Electric gauge completion applies a dedicated instant trigger effect that refreshes the electrocute duration status, deals secondary electric damage, and then performs the discharge chain. The electrocute status effect itself remains status-only.

Reason:
Status refresh and chain execution have different responsibilities. Keeping discharge in the trigger effect lets each electric gauge completion and chain reapplication deal damage exactly once without making every generic status refresh recursively start another chain.

Implications:
- `GE_ElectricShockTrigger` is the Electric gauge trigger effect; `GE_ElectrocutedStatus` only grants `State.Status.Electrocuted`.
- Discharge is a single nearest-neighbor chain: every step scans around the current unit, chooses the nearest already-electrocuted unit, and visits each unit once per discharge event.
- Electric damage is applied through `GE_Damage_Spec` with `Data.Damage` SetByCaller so it stays inside GAS damage handling and does not re-enter element build-up.
- Electric chain visuals are driven from the final ordered chain point list; the current authored prefab renders SpriteRenderer segments with `ElectricParticleTrail` without changing the gameplay chain logic.

## 2026-05-19 - Closed Doors Block Mob Player Perception

Decision:
General mob player perception treats a closed `DoorObject` on the enemy-target sight line as an immediate perception blocker.

Reason:
Room doors should prevent monsters from detecting or continuing to act on a player beyond the closed door, even if the distance-based detection range still contains the player.

Implications:
- Shared `Enemy` perception checks line of sight for closed `DoorObject` colliders.
- Target acquisition, chase detection/movement, common mob attack continuation, and Dead's Skeleton self-destruct checks use the shared perception rule.
- Opening the door restores normal distance-based detection without clearing the cached player target.
- The rule depends on door colliders being authored so physics raycasts hit the closed `DoorObject`.

## 2026-05-19 - Run Special NPCs Use Speech Bubble Flow

Decision:
Run-internal special NPCs such as construction and same-scene teleport NPCs should use a separate speech-bubble interaction flow instead of extending the existing `DialogueController` / Ink / portrait dialogue stack.

Reason:
These NPCs need in-world `SpeechBubble` UI, local world choices, same-scene movement, construction progress, and shortcut unlock behavior. That interaction shape differs from the existing visual-novel dialogue UI and should not overload the Ink dialogue path.

Implications:
- `DialogueController`, `DialogueView`, Ink start paths, and portrait presentation remain the existing visual-novel/boss dialogue path.
- Run special NPCs should start from `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md`.
- Teleport NPCs are planned as same-scene player movement, not `ScenePortal` scene transitions.
- Construction NPC path opening should prefer durable shortcut/map progress and scene-authored blocked/open objects before direct runtime tilemap mutation.

## 2026-05-20 - Run Special NPC Choice UI Stays Authored

Decision:
The run-special NPC source slice provides a `RunSpecialNpcChoicePresenter` that drives serialized `CanvasGroup`, `Button[]`, and `TMP_Text[]` references, but it does not create the choice UI hierarchy at runtime.

Reason:
The project UI contract prefers scene/prefab-authored UI. This keeps the new speech-bubble flow usable from code while leaving visual layout, world-space canvas placement, button styling, and references as explicit Unity authoring work.

Implications:
- `RunSpecialNpcInteractor` owns flow state; the presenter only projects labels and relays input.
- Missing choice presenter references are authoring errors, not a reason to instantiate buttons or canvases at runtime.
- Scene/prefab review is required before content can use the flow in play.

## 2026-05-20 - Run Special NPC Choices Use Screen-Space World Anchors

Decision:
Run-special NPC choices should use an authored screen-space presenter under `GlobalUIRoot > DialogueCanvas`, positioned from the player world coordinate through `RunSpecialNpcChoiceAnchorFollower`.

Reason:
The choices need to feel attached to the in-world conversation, but they still need shared UI raycast, scaling, input guard, and canvas ordering. Screen-space overlay with a world-anchor follower keeps the UI in the global canvas while avoiding per-NPC world-space Canvas sorting and camera setup.

Implications:
- `RunSpecialNpcInteractor` owns when the choice panel is shown and which player transform it follows.
- `RunSpecialNpcChoiceAnchorFollower` owns only coordinate projection and canvas clamping.
- The presenter remains authored UI; it does not create buttons at runtime.
- Scene references to prefab-owned presenters should be validated in Unity after prefab import.

## 2026-05-20 - Construction Progress Uses Run-Special Save Data

Decision:
Construction NPC payment/progress is stored in `GameData.runSpecialNpcData.constructionRecords`, with run-active starts staged in `GamePlayData.pendingRunSpecialNpcConstructionStarts` and committed through `RunSessionProgressCommitPolicy`.

Reason:
Construction progress is neither UI state nor a pure door unlock. It needs a durable start point so "required run completions after payment" can be computed before opening the permanent shortcut.

Implications:
- `CurrencyManager` still owns magic-stone spending.
- `DoorObject` / `ShortcutProgressService` still own the final permanent path unlock.
- Existing profiles rely on `GameDataManager` normalization to initialize the new save data safely.

## 2026-05-20 - Encyclopedia V1 Uses Public Catalog Before Discovery Save

Decision:
The first encyclopedia implementation uses `EncyclopediaCatalogSO` as authored display data and treats every registered weapon, monster, and boss entry as visible. Player-specific discovery IDs and save schema changes are deferred to a later release task.

Reason:
The feature does not yet have a built encyclopedia screen, content validation, or hub opening flow. Building and reviewing that visible v1 first reduces the risk of coupling UI/presentation work to unvalidated save events.

Implications:
- The catalog owns display content, not player discovery state.
- `GameData` and encounter/acquisition event hooks are intentionally unchanged in v1.
- Release discovery should add a save-owned discovered ID set and record from weapon acquisition, monster encounter, and boss encounter/defeat events.
- The encyclopedia screen and interactable should stay prefab/scene-authored; editor-only builders may generate temporary authoring assets, but runtime must not create the UI hierarchy.

## 2026-05-20 - Encyclopedia Book Presentation Is Optional Authored Sprite Playback

Decision:
The encyclopedia can experiment with UI book open/close and page-cover reveal presentation, but the runtime still only drives authored `Image`, `CanvasGroup`, `SpriteRenderer`, and sprite-array references. Missing book presentation references fall back to the existing immediate/pixel-reveal behavior instead of creating UI at runtime.

Reason:
The updated planning note prefers a simpler already-open UI, but the current requester explicitly asked to try the book unfolding and page-appearance feel. Keeping the animation optional and authored preserves the UI stack/input policy while allowing visual iteration.

Implications:
- UI book close is handled through `ICloseRequestHandler` so the screen remains on the popup stack until the close presentation finishes.
- Category/item data still updates immediately behind a visual cover; item selection remains unanimated.
- EarthTome field-book sheets are treated as 48x40 frame strips by editor import support.
- If the experiment is rejected, the screen can clear `EncyclopediaBookPresentation` references and continue using the prior reveal fallback.

## 2026-05-20 - Encyclopedia Book Presentation Uses Animator Clips

Decision:
The encyclopedia UI book, page content-appear cover, and EarthTome world book presentation should be driven by authored `AnimatorController` states and `AnimationClip` assets, not by runtime sprite-frame stepping coroutines.

Reason:
The book/page presentation is authored visual rhythm. Letting an `Animator` own sprite frame binding makes the state visible and replaceable in Unity, matches the project's presentation authoring direction, and avoids hiding clip structure inside runtime code.

Implications:
- `EncyclopediaBookPresentation` only requests states such as `Open`, `Close`, `Opened`, `Closed`, and `ContentAppear`; it uses clip lengths only to sequence callbacks.
- `BookWorldSpriteSequencePresentation` keeps its legacy class name for serialized reference compatibility, but it now requests EarthTome Animator states such as `ClosedIdle`, `OpenedIdle`, `Open`, and `Close`.
- `EncyclopediaV1AssetBuilder` is responsible for creating/wiring the temporary `.anim` and `.controller` assets through UnityEditor APIs.
- Manual prefab YAML patching is not an acceptable substitute for UnityEditor API generation when adding Animator components or serialized clip/controller references.

## 2026-05-20 - Encyclopedia Entry List Uses Prefab-Instantiated Slots

Decision:
The encyclopedia entry list uses an authored `EncyclopediaEntrySlot.prefab` instantiated and pooled under an authored `entryGridRoot`. Serialized `entrySlots` remain only as a migration fallback for old generated prefabs.

Reason:
The fixed 48-slot vertical list was hard to style, capped visible entries, and produced solid rectangular rows that competed with the book art. A prefab slot gives the UI a single editable holder/highlight/icon structure while keeping runtime creation limited to approved authored instances.

Implications:
- Runtime encyclopedia code may instantiate `EncyclopediaEntrySlot.prefab` but must not create raw `Button`, `Image`, `TMP_Text`, `Canvas`, or arbitrary UI hierarchy.
- `EncyclopediaScreen.prefab` must wire both `entryGridRoot` and `entrySlotPrefab` to use the new grid path.
- `EncyclopediaEntryButton` should resolve named `Icon`, `IndexText`, and `TitleText` children before fallback searches, so the holder background is not mistaken for entry art.
- Final visual layout can be tuned by editing the slot prefab and grid root in Unity without changing the runtime list population policy.

## 2026-05-22 - Encyclopedia Detail Uses Fixed Right-Page Presenter

Decision:
The encyclopedia right-page detail layout uses dedicated fixed presenters instead of reusing inventory `ItemDetailPanel`. This entry originally introduced `EncyclopediaDetailPanel`; the current Item tab presenter is now `EncyclopediaItemRightPage`, and `EncyclopediaDetailPanel` is retained only as a migration fallback.

Reason:
`ItemDetailPanel` is a hover UI singleton that adopts itself to the hover canvas and hides itself during `Awake()`. Reusing it inside the book page would fight the authored RightPage layout and inventory hover lifecycle.

Implications:
- `EncyclopediaItemRightPage` is the preferred Item RightPage wiring target.
- `EncyclopediaDetailPanel` may still exist on older layouts while serialized references migrate.
- RightPage presenters must not create arbitrary runtime UI hierarchy and must not invoke inventory hover controllers, drag/drop, or item detail popup behavior.

## 2026-05-22 - Encyclopedia Current Slice Is Item First

Decision:
The current encyclopedia implementation slice is limited to the Item tab, with `Weapon`, `Relic`, and `Consumable` sub-tabs active. Monster pages, boss pages, monster theme tabs, and boss-specific tabs/details are deferred.

Reason:
The full encyclopedia UI would take too long to finish in one pass, and the user is actively authoring the item-side layout first. Keeping the slice narrow prevents unfinished Monster/Boss UI from blocking item iteration.

Implications:
- `EncyclopediaScreen` keeps Monster/Boss code paths but exposes them as disabled serialized category toggles by default.
- Current scene/prefab authoring should wire and validate the Item tab, including weapon/relic/consumable layouts.
- Deferred category work is tracked in `Docs/RefactorBacklog/EncyclopediaDeferredCategories.md`.

## 2026-05-22 - Encyclopedia UI Uses Layout-Scoped View Components

Decision:
Split encyclopedia UI driving into layout-scoped tab/page components while keeping `EncyclopediaScreen` as the popup-stack and presentation owner.

Reason:
The item encyclopedia layout is now authored directly in Unity. Keeping every title, tab, page button, slot grid, and detail reference on `EncyclopediaScreen` makes the Inspector hard to reason about and couples layout editing to screen-level state.

Implications:
- `EncyclopediaScreen` owns open/close policy, main-tab entry, and presentation sequencing.
- `EncyclopediaItemTab` owns Item sub-tab, page, selection, and item data binding.
- `EncyclopediaItemLeftPage` owns authored Item LeftPage controls such as title, item sub-tab buttons, pagination labels, and list notices.
- `EncyclopediaItemRightPage` owns authored Item RightPage header/detail roots and weapon ability block pooling.
- `EncyclopediaEntryGridView` owns the slot pool and page binding under the authored grid root.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` remain migration fallbacks only.
- View components relay user input upward and project state downward; they should not own catalog selection or gameplay state.

## 2026-05-22 - Encyclopedia Item Data Reads ItemDatabase Directly

Decision:
The item/weapon side of the encyclopedia reads `ItemDatabase.allWeapons` directly instead of requiring `EncyclopediaCatalogSO` as a copied weapon list.

Reason:
Weapon, relic, and consumable source data already exists in item definition/database assets. A separate catalog for those entries creates duplicate maintenance and makes the encyclopedia look like another source of truth.

Implications:
- `EncyclopediaItemTab` uses `ItemDatabase` for weapon/relic/consumable counts, names, icons, and detail binding.
- `EncyclopediaCatalogSO` remains only as a legacy fallback and temporary Monster/Boss display-data path until those pages get their own source/provider shape.
- `EncyclopediaInteractable` should no longer require a catalog for the item page to open.
- Relic/Consumable subtabs also read existing item data sources directly rather than adding copied catalog entries.

## 2026-05-20 - DemonKing EgoSword Subpatterns Stay In GAS

Decision:
EgoSword dropped VerticalStrike and CrossLaser are DemonKing-owned GAS abilities, but they run through `ParallelIndependent` AbilityDefinitions triggered by EgoSword's independent dropped-pattern runner instead of the main `BossPhaseConfig` pattern timer.

Reason:
The sword's dropped behavior is still part of DemonKing's pattern kit, so it should remain inspectable and cancellable through GAS. Its cadence is intentionally separate from the boss FSM's main pattern selection, so putting it in phase selection would couple two timers that should stay independent.

Implications:
- `DemonKingController` registers the two subpattern AbilityDefinitions separately from phase pattern entries.
- `EgoSwordActor` owns only the interval/toggle runner and visual state cleanup, then asks DemonKing's AbilitySystem to activate the subpattern ability.
- The subpattern AbilityDefinitions must remain `ParallelIndependent` and instant, otherwise they can be blocked by the boss's active main pattern execution.
- EgoSword should not grow its own runtime-created AbilitySystem for these patterns unless a future design explicitly separates sword ownership from DemonKing.

## 2026-05-20 - Hit Impact Cue Classification Lives On ALData

Decision:
Hit impact cue classification is authored on ALData at the actual hit unit, not on `AbilityDefinition`.

Reason:
Combo attacks, multi-hit patterns, and projectile/helper hit configs can mix slash, blow, or suppress-hit styles inside one AbilityDefinition. ALData already owns execution presentation intent and builds the `CombatHitPayload`, so it is the narrowest authoring layer that can describe each hit correctly.

Implications:
- Runtime transport is `ALData -> CombatHitPayload -> CombatDamageAction -> AbilityEventData -> AbilityHitCueRouter`.
- `AbilityDefinition` hit-confirmed cue lists remain for explicit reusable cues such as hit spark and camera shake.
- `HitImpactCueKind.Default`, `Slash`, and `Blow` currently resolve to `SlashHit` until a separate BlowHit cue exists; `None` suppresses the automatic impact cue.
- New combo or hit-config ALData should expose the classification at the same granularity as the hit payload it creates.

## 2026-05-20 - Runtime Split And Summoned Mobs Do Not Drop Monster Loot

Decision:
General mobs created by runtime split or boss/local summon paths suppress the normal `Mob.OnDeathStarted` monster loot request. Spawn-registered mobs from `MonsterSpawner` / `MonsterSpawnContainer` keep the existing monster drop behavior.

Reason:
Split and summon mobs are not authored baseline room population. Letting every temporary child roll the full monster loot table can multiply rewards, including field-heal pickups, beyond the intended room placement.

Implications:
- Slime split descendants can still inherit room/chest lock tracking, but they do not spawn monster loot on death.
- SlimeQueen call/drop summons and Witch retreat skeleton summons are marked no-loot immediately after instantiation.
- Boss rewards, boss physical drops, graves, chests, and direct world pickups are outside this policy.

## 2026-05-20 - Construction Sites Use Additive Tilemap State Modules

Decision:
Run construction shortcuts use scene-authored additive `ConstructionSiteTilemapModule` blocks with `BlockedState` and `OpenState` roots instead of editing the main Ground/Wall tilemaps at runtime.

Reason:
Construction completion needs a visible wall-before/path-after map change, but raw `Tilemap.SetTile` mutation would be harder to author, review, save, and validate. A block module keeps temporary wall tilemaps, open ground tilemaps, optional Door/Shortcut anchors, and optional Chest objects together as one scene-authored construction site.

Implications:
- `RunConstructionNpcFeature` should call the construction site module when assigned; its direct blocked/open root and target door fields are fallback authoring only.
- Save data stores semantic construction completion by `constructionId`, not individual tile cells.
- Tilemap consumers that scan the scene must ignore inactive tilemaps, and pathfinding must accept additive open ground tilemaps after completion.

## 2026-05-20 - Run-Special NPC Interaction Freezes Time With Unscaled Presentation

Decision:
Run-special NPC interaction pauses the run timer and `Time.timeScale`, while its speech-bubble typing, line skip, choice UI, and letterbox presentation run on unscaled time.

Reason:
These NPCs are an interaction flow rather than live combat dialogue. The player should not lose run time or be attacked through scaled gameplay while reading construction/teleport choices, but the presentation still needs to animate and accept Dialogue-style skip input.

Implications:
- `RunSpecialNpcInteractor` owns the time-scale pause and restores the previous value during cleanup.
- `SpeechBubble` uses unscaled DOTween updates so bubble typing, auto-hide, fade, and scale presentation continue while `Time.timeScale` is `0`.
- Run-special line skip uses left click or `InputActionId.DialogueAdvance`; run-special choices remain click/number-key only so Space does not confirm choices.
- The existing `CinematicLetterboxOverlay` can be reused for letterbox plus HUD fade, but run-special NPCs pass an explicit faded layer list that excludes `GlobalCanvasLayer.Dialogue` so the authored `DialogueCanvas` choice panel stays visible and interactive.

## 2026-05-20 - Run-Special Speech Bubbles Pre-Size Only In Special NPC Flow

Decision:
Run-special NPC dialogue uses a dedicated pre-sized speech-bubble call that measures the full line, clamps maximum text width, enables wrapping, and then types inside the prepared bubble. General speech-bubble users keep the existing empty-text-then-grow `Speak(...)` path.

Reason:
Construction/teleport NPC lines are longer and should feel closer to dialogue UI, but globally changing `SpeechBubble` sizing would alter unrelated short world barks and cinematic bubbles.

Implications:
- `RunSpecialNpcInteractor` owns the opt-in sizing limits for this flow.
- `SpeechBubbleComponent.Speak(...)` remains the compatibility path.
- `SpeechBubbleComponent.SpeakWithPreSizedLayout(...)` is the explicit path for dialogue-style wrapping.

## 2026-05-22 - Authored GlobalUIRoot Encyclopedia Uses Scoped Wiring Tool

Decision:
The current authored `GlobalUIRoot.prefab` encyclopedia layout is repaired through `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia`, not through `EncyclopediaV1AssetBuilder` and not through direct prefab YAML string patches.

Reason:
The active encyclopedia layout is user-authored under `GlobalUIRoot/EncyclopediaUI/Book`. The V1 builder can rebuild generated shell assets and has previously been a risky path for preserving the current layout. A scoped UnityEditor API tool can wire existing components and report missing authored roots while keeping visual placement under Unity authoring control.

Implications:
- `EncyclopediaV1AssetBuilder` remains a generated-asset support tool, but it should not be used as the repair path for the current `GlobalUIRoot` layout.
- Prefab reference repairs should use `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` or direct Unity Inspector work.
- The wiring tool should not create replacement visual layout, item sub-tab buttons, or detail section roots for the current authored layout. Missing roots are authoring gaps to fix in Unity.
- After running the wiring menu, Inspector review is still required because duplicate generic names in authored UI can bind to the wrong candidate.

## 2026-05-22 - Encyclopedia Ability Switch Is Variant-Only

Decision:
`Panel_AbilityBlock_Encyclopedia` represents one displayed weapon skill. Skill1 and Skill2 are separate block instances. The switch guide and preview behavior are shown only when that specific skill has multiple tooltip variants through `IAbilityTooltipVariantProvider`.

Reason:
The switch UI represents replacement-mode skills such as LightningSpear, not the fact that a weapon has two different skill slots. Treating normal Skill1/Skill2 as one switchable panel collapses distinct abilities into a confusing single UI block.

Implications:
- Normal weapons should show two separate ability blocks when both Skill1 and Skill2 exist.
- Variant-capable skills may reserve an extra preview block for the switch animation, but that preview belongs to that one skill's variant cycle.
- `AbilityBlockContainer` must be authored or normalized as a vertical list that controls child height so pooled block instances do not overlap.

## 2026-05-22 - Encyclopedia Item RightPage Uses Type Sections

Decision:
The encyclopedia Item RightPage uses common `Icon` / `Name` / `Description` fields plus item-type section roots instead of one shared metadata/story presenter for Weapon, Relic, and Consumable.

Reason:
Weapon, Relic, and Consumable detail layouts have different information contracts. Automatically injecting ID, rarity, max level, restore amount, or target attribute into one shared text path made the UI harder to author and did not match the planned encyclopedia layout.

Implications:
- Weapon entries show story, stat text, and separate Skill1/Skill2 ability blocks. They do not show a relic-style level preview.
- Relic entries show description, preview level, and `RelicLogic.BuildTooltip(...)` effect text. They do not show weapon stats or weapon skill blocks.
- Consumable entries show only name and description in the current encyclopedia scope.
- Any additional metadata must be added as an explicit authored section decision, not as a shared fallback string.

## 2026-05-22 - EncyclopediaScreen Is Orchestration Only

Decision:
`EncyclopediaScreen` owns only stack participation, open/close requests, main-tab routing, and book/dim presentation. Item sub-tab state, page index, selection, grid binding, and detail binding belong to `EncyclopediaItemTab`, `EncyclopediaItemLeftPage`, and `EncyclopediaItemRightPage`.

Reason:
Putting Item data and page state directly on the screen made the Inspector hard to understand and created multiple competing places for button/page/detail references. The screen should be readable as the popup shell; Item behavior should be readable in the Item tab components.

Implications:
- Screen public button methods remain for Unity Button bindings, but Item-related calls forward to `EncyclopediaItemTab`.
- `EncyclopediaScreen` should not regain direct item grid/detail/page serialized fields.
- If the screen is authored active, `closeOnRuntimeAwake` snaps the presentation closed and deactivates the screen at Play startup so opening still happens only through Tome/UI stack flow.

## 2026-05-22 - Encyclopedia Item RightPage Shares Story And Preview Roots

Decision:
The Item RightPage uses one shared `StoryText` body for Weapon, Relic, and Consumable descriptions, and Weapon stat output may share the same authored root as Relic level preview.

Reason:
The current authored layout is one detail panel whose internal elements are switched on/off per item type. Splitting description fields or forcing separate stat/preview roots added confusing fallback paths and did not match the layout the user is wiring.

Implications:
- Weapon mode writes story to `StoryText`, shows stat text, and hides Relic preview guide icons even when the stat root is the same object as the Relic preview root.
- Relic mode writes description to `StoryText`, shows preview level/effect, and enables preview guides only when the relic has multiple levels.
- Consumable mode writes description to `StoryText` and keeps weapon/relic-only elements inactive.
- Legacy metadata or type-specific description text fields should remain migration-only and should not be touched by the primary binding path.

## 2026-05-22 - Encyclopedia Book Presentation Is A Required Screen Dependency

Decision:
The authored encyclopedia screen must have an `EncyclopediaBookPresentation` on the Book/screen root when Tome open/close and tab page-turn animations are expected.

Reason:
Without that component, `EncyclopediaScreen` and `EncyclopediaItemTab` can only perform immediate content binding. That makes Tome interaction and tab changes look like no animation is wired even when Animator clips exist in the child Book asset.

Implications:
- `EncyclopediaScreen` does not add the missing component at runtime. Missing presentation references produce warnings and immediate fallback behavior until the prefab is wired.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` also adds/wires the component on `GlobalUIRoot/EncyclopediaUI/Book`.
- Direct prefab YAML string patching remains avoided; persistent prefab repair should happen through Unity serialization or the scoped wire tool.

## 2026-05-22 - Encyclopedia Runtime Does Not Self-Wire Missing UI References

Decision:
The active encyclopedia runtime path does not create presenter components, canvas/button/TMP/image objects, or missing UI references during `Awake`, `OpenUI`, or item binding. Runtime code validates required serialized references, logs explicit warnings, and disables or falls back only at the feature level.

Reason:
Silent runtime self-repair made the Inspector unclear and hid the difference between the intended authored `GlobalUIRoot` layout and accidental fallback behavior. The current goal is a stable authoring contract where `EncyclopediaScreen -> EncyclopediaItemTab -> EncyclopediaItemLeftPage / EncyclopediaItemRightPage` is visible in the prefab.

Implications:
- `Reset()` and explicit `Auto Wire References` context menus remain authoring conveniences; normal runtime binding should use serialized references.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` is the persistent repair path for `GlobalUIRoot.prefab`.
- `EncyclopediaEntryGridView` may instantiate the authored slot prefab pool, and `EncyclopediaItemRightPage` may instantiate the authored ability-block prefab pool. These are approved content pools, not fallback hierarchy construction.
- Missing `screenActiveRoot`, `itemTab`, `EntryGridView`, `StoryText`, `ScrollRect`, or ability-block references should be treated as prefab/scene wiring issues instead of being hidden by broad child searches.

## 2026-05-22 - EncyclopediaUI Is The Screen Active Boundary

Decision:
The encyclopedia popup active boundary is `EncyclopediaUI`, not the child `Book` object.

Reason:
The authored layout can place `DimPanel`, `Book`, and page/presentation objects as siblings under `EncyclopediaUI`. Closing only `Book` can leave `DimPanel` active and black out the screen even though the encyclopedia content appears closed.

Implications:
- `EncyclopediaScreen.screenActiveRoot` should resolve to or be wired to `EncyclopediaUI`.
- `OpenUI()` activates `screenActiveRoot` before resolving/presenting content.
- Runtime startup close and `CloseUI()` deactivate `screenActiveRoot`.
- `Book` remains the animation target for open/close/page-turns, not the popup lifetime root.

## 2026-05-23 - Run Special NPC Camera Focus Reuses Gameplay Camera State

Decision:
Run-special NPC dialogue focuses the existing `CameraBootstrap` gameplay `CinemachineCamera` on the NPC target and restores the cached gameplay camera state after the interaction. It does not create a new manager or runtime UI/camera hierarchy.

Reason:
The requested dialogue-start camera move matches the Merchant activation cinematic pattern. Reusing the gameplay camera target swap keeps the feature source-only and avoids adding another camera ownership layer for a local NPC conversation.

Implications:
- `RunSpecialNpcInteractor` caches Follow/LookAt/Priority, legacy `CameraFollow` enabled state, and `CinemachineBrain.IgnoreTimeScale` before focusing.
- Camera blending runs with unscaled time because run-special dialogue pauses `Time.timeScale`.
- Scene authoring may assign a specific `cameraFocusTarget`; otherwise the speech-bubble transform is used, then the NPC transform.
- User choices are framed on the player: the flow returns the camera to the player before showing the authored choice panel, then refocuses the NPC only when the selected choice has NPC response lines.
- Manual play validation is required for scene-specific framing and damping.

## 2026-05-25 - Flowering Dash Augment Keeps Dash Global

Decision:
`개화` Bloom dash behavior uses a narrow `IWeaponDashAugment` opt-in hook from `AbilityLogic_Dash2D`; it does not add a dash `WeaponAbilitySlot` or route dash through weapon input selection.

Reason:
Dash is authored and executed as the global `AD_Dash`, while weapon attacks and skills use `WeaponAbilitySelector -> WeaponAbilityBridge -> AbilitySystem`. Extending weapon slots for one weapon's dash modifier would blur that boundary and risk changing all dash input behavior.

Implications:
- Bloom state lives in `FloweringRuntimeData` and a transient `FloweringRuntimeState` attached to the player while active.
- `AbilityLogic_Dash2D` only asks for an optional augment; if none is active, global dash behavior is unchanged.
- Bloom can keep dash cooldown at zero and add three delayed slash hitboxes without making dash a weapon-owned ability.
- The current Flowering presentation is runtime-created prototype presentation; if reused, move it to authored prefab/material references under the presentation contract.

## 2026-05-25 - Flowering Bloom Splits World Dim From UI Border

Decision:
Flowering Bloom uses a world `SpriteRenderer` DimPanel on `Sorting Layer = Entity`, `Order in Layer = -1` for black dimming, while the red Bloom border remains a raycast-free UI overlay using `AffectionGradientBorderGraphic`.

Reason:
The black DimPanel must darken tiles/background without covering player, monsters, effects, or HUD. The affection gradient border already gives the desired soft screen-edge quality and should stay in UI space.

Implications:
- `FloweringBloomData` owns the DimPanel sorting values, cut-in zoom values, eye-flash frames/settings, and Flowering OFF/ON weapon sprites.
- `FloweringBloomPresentationController` may create runtime-only DimPanel and eye-flash objects for the current v1 implementation and must destroy them during Bloom cleanup/weapon release.
- Camera zoom is applied to the existing `CameraBootstrap` player `CinemachineCamera` and restored from cached lens values after the cut-in.
- If Flowering cut-in presentation becomes reusable, migrate the runtime-created presentation objects into authored prefab/scene references under the presentation authoring contract.

## 2026-05-25 - Flowering Cut-in Uses SpriteMask Reveal And Player_Idle Silhouette

Decision:
Flowering weapon OFF/ON reveal uses a target sprite overlay with a runtime `SpriteMask`, and the player blackout cut-in uses the first frame of `Player_Idle.anim` as a fixed silhouette. `Eagle-eyed_YesPlayer` remains a reference image only.

Reason:
The reveal needs handle-to-blade spatial control that is easier to tune with mask position/size than material UV parameters. The blackout cut-in must match the player's actual idle silhouette while letting `Eagle-eyed_NoPlayer` remain a separate one-shot eye flash layered over the player.

Implications:
- Keep the old reveal material fields serialized on `FloweringBloomData` for compatibility, but do not use them as the runtime reveal gate.
- `FloweringBloomPresentationController` must restore the hidden `PlayerRender`, destroy the silhouette, and clear any reveal masks during Bloom cleanup/weapon release.
- If this cut-in becomes shared presentation, migrate the runtime-created silhouette/reveal objects to authored prefab references under the presentation authoring contract.
