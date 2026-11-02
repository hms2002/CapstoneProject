---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-11-02
---

# Dialogue NPC Affection Structure

## Purpose

Map dialogue, NPC features, affection, merchant, upgrade, and boss dialogue scripts.

## Authored world nameplates

- `Runtime/UI/HUD/WorldPrompt/NpcNameplatePresenter.cs` drives `Prefabs/UI/NpcNameplate.prefab`; there is no runtime hierarchy creation. Instances belong to the NPC/stand scene or prefab and are destroyed with it.
- NPC names and optional role icons come from `NPCData`; the encyclopedia uses authored fallbackName/fallbackIcon because it is not an NPC. Book icon = 도감, smith = 대장장이, store = 상인. 서기관 and other NPCs without an icon keep centered text with no blank icon space.
- `NameplateAnchor` owns the nameplate prefab instance at zero local offset. It is authored at the previous interaction anchor position. The separate `InteractUIAnchor` sits 0.8 world units above it and is referenced by the presenter’s interactionAnchor; WorldInteractionPromptController projects that transform directly. NPCs without an existing dedicated anchor receive both anchors. Both positions can be edited independently in Unity.
- The world canvas follows its authored anchor with no additional world offset, keeps upright text and compensates parent scale. Name text is 40 points at 0.01 world scale, matching the interaction prompt; role icons are 40×40 UI units. Horizontal layout preserves centered names when icons are absent. It fades over 0.2 unscaled seconds while DialoguePlayback, a visible cinematic letterbox, or the player Talking state is active.
- `CinematicLetterboxOverlay.IsAnyVisible` tracks PlayIn through PlayOut/Dispose and resets on subsystem registration. This projects presentation state; it does not own dialogue/gameplay state.
- 17 friendly NPC instances plus the encyclopedia stand are authored. Combat boss nameplates are not introduced. Existing installers that rebuild entire NPC objects may require preserving/reapplying the shared nameplate instance.

## Current Structure

### Ordinary NPC run conversations

- `NPCData.affectionDialogues` opts an ordinary NPC into the run dialogue loop. Each entry's `affectionLevel` is the minimum unlock threshold, and `startPath` identifies its Ink route. Optional `requiredThemeId` restricts an entry to the committed active route's `StableThemeId`, read through `RunRoutePlayback`; blank/missing values retain unrestricted behavior. A required theme with no active plan/current stage is ineligible. One shared predicate gates both conversation/indicator availability and candidate selection; filtering does not roll random numbers. `greetingPaths` and `fullBackpackGreetingPaths` contain short, choice-free routes. Unconfigured NPCs and all `isBoss` NPCs retain their previous dialogue paths. Existing fields/IDs are not renamed.
- `NpcAffectionDialogue` reads saved/pending/current affection, the highest authored reward threshold and the existing session consumption list. It owns no gameplay state. `CanStartConversation` checks the active-run/NPC-ID allowance and any unlocked candidate, including at maximum affection. `TrySelect` uniformly selects among distinct unlocked paths, so duplicate entries do not bias the pool. `CanOffer` is the narrower gain/indicator gate: it also requires affection below the maximum. Outside the run allowance, select greetings, preferring full-backpack reactions when all actual backpack slots are occupied.
- `DialogueTrigger` routes player interactions through `DialoguePlayback.TryStartDialogueSequence`, preserving dialogue presentation/cleanup ownership. Opted-in NPCs have no automatic profile-once room introduction. A completed choice conversation consumes the NPC ID even without a gain, including at maximum affection; subsequent talks use greetings. Below the cap, positive gain still consumes immediately through `AffectionManager`, preventing another reward if subsequent presentation is interrupted. Before gain/completion an interrupted conversation remains retryable. Per-trigger generation invalidation on run start/end and disable rejects late completion callbacks.
- `NpcAffectionBubble` animates only a prefab-authored SpriteRenderer, hides during dialogue/cinematics, and projects `CanOffer` gain eligibility. At maximum affection it stays off even though the first run choice conversation remains available. It follows an independently authored anchor, compensating parent scale/mirroring. There is no runtime indicator hierarchy creation, new manager, or Animator requirement.
- `NpcAffectionBubbleEditor` provides an animated Scene view preview and Undo-supported position handle. Select `AffectionBubble` in `RunMerchantGroup.prefab`, drag its anchor and save the prefab. `framesPerSecond` and `worldSize` are Inspector settings.
- Sasha retains eight authored conversations but only four reviewed paths are active in `MSShopNpc.affectionDialogues`: `affection_0_a`, `affection_0_b`, `affection_0_c` at minimum 0, and `affection_1_c` at minimum 1. Unfinished `affection_1_a`, `affection_1_b`, `affection_2_a`, `affection_2_b` are absent from that list and the setup authoring defaults; their Ink/JSON/translations remain intact for later edits/reactivation. Spiri-goods `affection_0_b` still requires theme `dragon`. Eligible pool sizes at affection 0/1/2/3/4 are 3/4/4/4/4 in dragon and 2/3/3/3/3 elsewhere or without route context. Existing minimum unlocks remain cumulative, so reviewed +1 routes remain available through reward tier 4. Changing areas never resets the NPC/run allowance. Three ordinary greetings and two full-backpack greetings remain. Four cumulative permanent rewards stay unchanged: one 10–20% special offer, defeat/time-out gold retention up to 500, Rare/Epic relic weights 1.10/1.15, then 5% global discount. Maximum affection is 4; cap conversations grant no further affection/rewards.
- Merchant portrait library `Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib` keeps its original GUID/local reference and `Face/Normal` label. `Art/Sprites/NPCs/merchant/merchant.png` is the supplied replacement default; `merchant_surprised.png` supplies `Face/Surprised`, and `merchant_happy.png` supplies `Face/Happy`. All three are 1254x1254 single sprites with matching point filtering, 100 PPU and transparent backgrounds. Positive responses in `affection_0_a`, `affection_0_b`, `affection_0_c` and `affection_1_c` set Happy; other positive responses retain Normal. These are authored branch expressions, including when capped choices grant no numeric gain. All eight authored `choice_fail` branches set Surprised; every conversation/greeting opening resets Normal. The existing `PortraitActor`, portrait prefab geometry and tag parser are unchanged.
- `affection_1_c` has exactly two opening speech windows, then two choices and one response window per branch. The first click advances from the greeting to the pricing explanation; choices appear after the explanation. Its six localization keys join the existing CSV/five-table pipeline.
- Spiri-goods `affection_0_b` and unilateral-partnership `affection_0_c` each use two opening windows and one response window per branch. Existing keys `.0`–`.4` retain their IDs with replacement text; `.5` is the second opening. The partnership's joke is that Sasha's reserved share is stock for the hero to buy; accepting a look uses Happy/+1 below cap, asking for it free uses Surprised/no gain. Retained merchant Ink has 51 localized keys across five locales, including currently inactive drafts. `UpdateSpiriDialogueBatch` uses the shared conversation migration and focused production-code checks for theme eligibility, run allowance, tag parsing and localized content. See [Spiri scope](../../ActiveTasks/sasha-spiri-dialogue.md) and [content session](../../SessionLogs/2026-11-02.md).
- `NpcAffectionDialogueAuthoring` compiles merchant Ink, authors the supplied 96x32 sheet into three 32x32 sprites, and writes the run merchant prefab via Unity APIs. The source PNG is preserved. Animation is 4 fps by default. New dialogue lines use the existing five-language GameText CSV/tables and loc tags.
- `MerchantAffectionRewardsAuthoring.ConfigureConversations` is shared by setup and the scoped conversation migration; the latter compiles Ink/imports merchant text without resetting rewards or touching the placement prefab. Both this authoring list and `MSShopNpc.affectionDialogues` must be updated when reactivating revised drafts, so a later setup does not undo the candidate selection. The isolated `UpdatePortraitsAndDiscountBatch` additionally imports the supplied PNGs and edits the existing library through the installed SpriteLibrary source factory, retaining its identity. `NpcAffectionDialogueValidation` exercises real routing/session/affection code with an in-memory profile and imported assets, including cumulative pools, cap/allowance, both new conversations' speech windows and all branches through the actual portrait prefab/tag parser. See the [portrait slice](../../ActiveTasks/sasha-discount-portraits.md), [cumulative-pool scope](../../ActiveTasks/npc-dialogue-cumulative-pool.md) and [session evidence](../../SessionLogs/2026-11-02.md).

- `DialoguePlayback.SetUpperPanelHiddenForCameraDialogue` forwards through DialogueService/DialogueController to DialogueView. It hides only the authored `dialogueUpperFrameGroup` across an entire camera-directed dialogue section and preserves the story/session across Hub segments. It does not block advancing spoken lines; GrandHall's existing feature callback wait still blocks advancement during actual movement. Releasing it uses the existing upper-frame opening presentation only if UI is still visible and not closing. Hub and GrandHall sequence cleanup clear the temporary state.

| Area | Count | Responsibility |
| --- | ---: | --- |
| Dialogue / NPC / Affection | 84 | Dialogue service/controller, Ink runtime references, dialogue UI, text animation tuning, boss dialogue, NPC data/database/features, merchant/upgrade features, affection state/rewards/UI. |

### Breakdown

| Area | Count | Responsibility |
| --- | ---: | --- |
| Upgrade Feature | 17 | Upgrade manager, feature, progress service, database, node/effect base, tree UI/editor, tooltip/slot UI, runtime accumulator, and lake presentation. |
| Dialogue UI | 15 | Dialogue view/theme, portrait/cinematic/text animation, text animation profile/utility, slide/fade, sequencer, choice input/glyph/highlight, and failure effect UI. |
| Dialogue Core | 12 | Dialogue service/controller, variables/session/tag handling, trigger, participant registry, runtime resolver, audio info, story segment, emotes, and knot selector. |
| Merchant Feature | 10 | Merchant NPC, activation cinematic, run state, refresh, purchase, shop definition/policy/roll/slot, and world item detail presenter. |
| Upgrade Effects | 9 | Upgrade effect ScriptableObjects for unlocks, shop/chest/grave modifiers, run-start rewards, attributes, and empty effects. |
| Affection Runtime / UI | 7 | Affection manager, progress/reward processing, gameplay presentation contract, UI, gradient border, gain screen effect, and base effect. |
| Boss Dialogue / Encounter Dialogue | 5 | Boss dialogue runner, progress store, encounter director/dialogue, and boss talk manager. |
| Affection Effects | 3 | Unlock item, info-only, and boss run modifier affection effects. |
| NPC Data / Manager | 3 | NPC database, data, and manager. |
| NPC Feature Core | 2 | NPC feature controller and feature interface. |

### Upgrade Feature Hotspots

| Parent path | Hotspot | Count | Responsibility |
| --- | --- | ---: | --- |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Runtime Services | 5 | Upgrade manager, progress service, effect applier, runtime target accumulator, and run modifier service. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade UI / Presentation | 5 | Upgrade tree UI, tooltip, slot UI, lake surface image, and ripple graphic. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Data / SO | 3 | Upgrade database, node ScriptableObject, and base upgrade effect ScriptableObject. |
| Dialogue / NPC / Affection > Upgrade Feature | Feature Entry / Requests | 2 | Upgrade feature entry and cinematic request. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Editor | 1 | Upgrade tree editor. |
| Dialogue / NPC / Affection > Upgrade Feature | Upgrade Other | 1 | Upgrade lake presentation support. |

## Key Files

- `Assets/_Project/Runtime/UI/Dialogue/DialogueService.cs`
- `Assets/_Project/Runtime/UI/Dialogue/DialogueView.cs`
- `Assets/_Project/Runtime/UI/Dialogue/EmoteController.cs`
- `Assets/LeeJunMo/Script/Dialogue/UI/DialogueTextAnimationProfileSO.cs`
- `Assets/LeeJunMo/Script/Dialogue/UI/DialogueTextAnimationUtility.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubble.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubbleComponent.cs`
- `Assets/_Project/Runtime/Features/Progression/Upgrades/UpgradeFeature.cs`
- `Assets/_Project/Runtime/Features/Dialogue/NPC/Merchant/MerchantNPC.cs`
- `Assets/_Project/Runtime/Features/Dialogue/Affection/AffectionManager.cs`
- `Assets/_Project/Runtime/Features/Dialogue/Affection/IAffectionPresentationView.cs`
- `Assets/_Project/Runtime/UI/Dialogue/Affection/AffectionUI.cs`
- `Assets/_Project/Runtime/UI/Dialogue/Affection/AffectionGainScreenEffect.cs`
- `Assets/_Project/Runtime/Presentation/Common/AffectionGradientBorderGraphic.cs`
- `Assets/_Project/Runtime/Features/Dialogue/Affection/AffectionRewardProcessor.cs`
- `Assets/_Project/Runtime/Features/Dialogue/BossEncounterDialogue.cs`
- `Assets/_Project/Runtime/Features/Dialogue/BossDialogueProgressStore.cs`
- `Assets/_Project/Runtime/Features/Dialogue/BossTalkManager.cs`
- `Assets/_Project/Runtime/Core/Save/GameData.cs`
- `Assets/_Project/Runtime/UI/Chest/RewardDisplayService.cs`
- `Assets/LeeJunMo/Script/Editor/NpcCustomizationHub/NpcCustomizationHubWindow.cs`
- `Assets/LeeJunMo/Script/Editor/DialogueTextAnimationTunerWindow.cs`
- `Assets/LeeJunMo/Datas/Inks/AnimatedVariants/SlimeQueenIntroDialogue_MeltaNotion_Animated.ink`
- `Assets/LeeJunMo/Datas/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_SlimeQueenBoss.spriteLib`
- `Assets/LeeJunMo/Datas/Dialogue/NPC/SlimeQueenBossNpc.asset`

## Ownership And Lifecycle

- Dialogue playback and its input block should be owned by `DialogueService`.
- NPC features that open stack UI after dialogue should wait for dialogue blockers to release before opening their UI.
- Merchant stock and upgrade progress are run/save-policy sensitive; do not treat UI state as the source of truth.
- Positive `AffectionManager.AddAffection` requests consume one allowance per NPC in `GamePlayData.affectionGainNpcIds`, through `RunSessionStore.TryConsumeAffectionGain`. For opted-in ordinary NPCs, active-run eligibility and maximum affection are checked first; completed failed conversations also consume through `DialogueTrigger`. Legacy/unconfigured NPCs and bosses retain the hub-preparation-plus-run positive-gain policy. StartRun preserves the list, active EndRun and development reset clear it. Blocked requests complete their callback without gain animation/reward. Negative changes do not restore the allowance; `SetAffection` remains an absolute state-setting API.
- Sasha (`MSShopNpc`, id 1001) uses `MerchantDialogue_Animated.ink`: choice 1 carries `add_aff: 1`, choice 2 explicitly carries `choice_fail` because automatic failure inference is boss-only. `MerchantAffectionRewardEffect` supplies the first three data-driven benefits; the existing `ShopAffectionDiscountEffect` supplies tier 4 at 5%. Both rebuild modifiers; aggregation derives unlocked effects from permanent affection plus pending run deltas, without compounding on repeated rebuilds.
- `ShopRunModifierDelta.affectionDiscountRate` remains separate from legacy upgrade discounts. Hub shops combine those global rates additively; gold shops retain only affection benefits and preserve their exclusion of legacy upgrade unlock/slot/refresh/discount settings. `MerchantOfferPricing` assigns one special offer per stock and multiplies the stock original price by global and special discount factors, rounding only the final price. `undiscountedPrice`, `specialDiscountPercent` and `specialOfferAssigned` preserve repricing/reentry/sold state. `ShopSlot` and purchases both consume `entry.price`; authored original-price and special-badge labels project the stock state.
- `MerchantAffectionRewardsAuthoring` writes four reward assets, NPC reward/candidate bindings, two price-label prefabs and five-language rows through Unity APIs. The original NPC setup menu delegates reward configuration to it. Reapplying either menu resets merchant tuning to the approved initial defaults; change reward assets directly for balance work. See [merchant reward scope](../../ActiveTasks/merchant-affection-rewards.md) and [gold/shop ownership](../RunGoldAndShop.md).
- Affection should contribute modifiers/rewards without bypassing base progression reward rules.
- Affection gain presentation gates dialogue continuation, so interrupted DOTween sequences must still invoke their pending completion callback exactly once. Gameplay `AffectionManager` should call only `IAffectionPresentationView`; concrete `AffectionUI` and `AffectionGainScreenEffect` live under UI, while shared gradient drawing lives under Presentation/Common.
- Affection reward popups are part of the dialogue flow: after affection gain presentation, show the reward UI through `RewardDisplayService.ShowFlowOwnedReward(...)`, keep the dialogue continuation as the reward close callback, and resume dialogue only after the reward UI closes.
- Boss reward affection effects author their own additive fields and convert them into `BossRewardModifierAggregate` at runtime; they should not depend on a separate modifier ScriptableObject asset.
- `BossDialogueProgressStore` owns durable per-boss dialogue progress. Its cumulative victory/defeat totals are distinct from `BossDialogueRecord.lastEncounterOutcome`, which records only the most recently completed encounter for `LastBossEncounterOutcome` dialogue rules. `BossTalkManager` updates that outcome only after an active boss encounter ends in player victory or `RunEndReason.Defeat`; a run time-out remains a separate `LastRunEndReason` condition.
- `BossDialogueRunner` runs a selected conditional encounter segment before the NPC primary Ink. An empty candidate set is intentional for a first encounter: it preserves the authored primary first-intro dialogue. Conditional authoring must therefore gate all re-encounter entries with `HasMetBoss` and leave no unconditional fallback rule.
- A boss's primary Ink may also use an `IDialogueStartKnotSelector` for re-encounter-only body dialogue. The selector must return `null` before the first completed encounter so the primary Ink root remains the authored first-intro; on later encounters it may choose from explicit body-knot candidates. `DemonKingDialogueStartKnotSelector` is the current example and keeps its candidate list serialized on the boss-scene component for additive authoring.
- Chloe uses `ShadowBossDialogueStartKnotSelector` on the dialogue runner in `Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity`. It preserves the primary Ink root until the first completed encounter, then chooses uniformly from `repeatStartKnots`; its current candidates are `CHLOE_SILENT_WARDEN_PRACTICE` and `CHLOE_MONTHLY_PRISONER_REPORT`. The root and repeat body knots live together in `ShadowBossIntroDialogue_Animated.ink`, separately from the conditional greeting Ink. Add future repeat bodies to that primary Ink and the scene selector's candidate list. `ShadowBossDialogueAuthoring` compiles the primary Ink, links this scene component and imports the CSV through the existing localization importer.
- Dialogue line rhythm is owned by `DialogueView` plus `DialogueTextAnimationUtility`: `DialogueController` resolves line-level `anim` Ink tags, while inline `[pause=seconds]` and scoped motion/TextAnimating tags are stripped before TMP display and applied through unscaled reveal delays plus TMP vertex offsets/scales.
- TextAnimating values are tuned through `DialogueTextAnimationProfileSO`. `DialogueView.textAnimationProfileOverride` is optional; the default profile is loaded from `Assets/LeeJunMo/Datas/Resources/Dialogue/DefaultDialogueTextAnimationProfile.asset`, with code defaults as fallback.
- The Text Animation Tuner preview should mirror the default DialoguePanel rendering context. It resolves `GlobalUIRoot.prefab` `DialogueView.dialogueText`, renders through a hidden world-space `Canvas + TextMeshProUGUI` into a `RenderTexture` with a dedicated preview camera, and copies the source font, rect, wrapping, spacing, alignment, and color before applying `DialogueTextAnimationUtility`. If the source text rect has a zero or invalid size, preview-only sizing resolves width/height from the parent `DialogueTextCon` container before falling back to `1720 x 250`.
- Drunk/slurred dialogue can use `[rand_size=min,max]...[/rand_size]` to give each visible character a stable pseudo-random scale inside the configured clamp range, and `[slowshake]...[/slowshake]` for low-speed per-character shake.
- Dialogue portrait emote playback is presentation-owned under UI/Dialogue. `DialogueTagHandler` raises emote tag requests, while UI `DialogueController` / `PortraitController` resolve the target portrait and play `EmoteController`.
- Dialogue background Effect switching is owned by `DialogueController` through the line-level `effect` Ink tag. `DialogueView.ApplyDialogueEffectTheme(...)` changes only the DialogueEffect AnimatorOverrideController; it does not change the active speaker accent color. Boss prelude activation keeps the authored Intro-to-Loop animation and fades the animated child Image from transparent to its authored alpha after the DimPanel fade. Closing preserves the reverse-order contract: portrait outro, upper/lower frame exit, then DialogueEffect Image FadeOut; only after that final fade does the view reset the Animator to hidden Idle and deactivate the effect root.
- `DialogueThemeSO.accentColor` is the single NPC-authored UI color. `DialogueView` projects it only to the speaker-name text, the selected choice root background, and the three ContinueIcon images. TextBox/SpeakerFrame visuals, choice text, unselected choices, and KeyGlyph images keep their authored colors.
- `Choice0.prefab` owns the selected background on its root `Image`. Its `HorizontalLayoutGroup` lays out `KeyGlyph` before the choice text, and `ContentSizeFitter` expands the same root/background with the preferred content width. `DialogueChoiceHighlightPresentation` toggles/themes that root background and presents the whole choice row through the root CanvasGroup/RectTransform; it does not independently move or recolor the KeyGlyph.
- `DialogueUpperFrame` and the lower `TextBoxGroup` enter in parallel without an alpha transition. `DialogueUpperFrame` moves down from above while the lower group moves up from below; both return to those off-screen positions during the closing frame stage. The lower presentation targets the entire `TextBoxGroup`, so its sibling `DialogueTextCon`, `SpeakerFrame` (speaker name plus boss `AffectionUI`), and inactive/cleared `DialogueChoices` share the exact same closing motion. Boss `AffectionUI` is snapped active with this container rather than applying a second positional tween. The authored open/close durations for both upper and lower frames are `0.5s` / `0.4s`.
- Opening presentation order is frame entrance, portrait silhouette intro, first-line header, then body text. The first-line-only header reveals `SpeakerName` one TMP character per beat while boss `AffectionUI` scales all five authored segment roots from left to right on the same beat; body text reveal starts only after both tracks finish. Closing reverses the presentation boundary by completing the portrait outro before retracting both frames. Later speaker changes keep the normal immediate name update. Skip/disable paths snap the name and segment scales to their final state.
- Gameplay HUD enters dialogue in two phases: all gameplay HUD roots participate in the initial non-dialogue UI FadeOut, then health, consumables, run timer, level, and magic-stone currency FadeIn when both dialogue frames complete. The FadeIn shares the portrait-intro start beat. Status, weapon, swap, inventory-guide, and Boss HUD remain suppressed. `DialogueService` skips this re-entry when another suppression owner exists or the Gameplay HUD canvas is inactive, preserving terminal and Tutorial Boss full-HUD flows. `GlobalUIRoot.prefab` authors `DialogueCanvas (50) < GameplayHUDCanvas (75) < PopupCanvas (100)`, so the re-entered HUD renders over the dialogue composition while popups retain priority.
- `DialogueChoices` is active only while a choice set exists. `DialogueView` rebuilds its vertical layout, freezes the layout group during motion, then reveals each `Choice0` from below with a short stagger. Choice input and raycasts require both the controller input gate and completion of the reveal sequence. A committed choice locks input immediately; unselected rows fade quickly while the selected row fades and moves upward before the Ink callback is invoked once.
- Dialogue impact shake is owned by `DialogueController` through the line-level `CameraShake` Ink tag. `DialogueView` applies TextBoxGroup panel shake, DOShake-like per-character TMP impact offsets, dialogue text inertia, and the existing `CameraShakeService`; the camera part respects the screen-shake setting while UI motion still plays. Low/Middle/High strengths plus a global intensity multiplier are tuned in `DialogueTextAnimationProfileSO`.
- SpeechBubble reveal timing is opt-in through animated APIs on `SpeechBubbleComponent`; existing `Speak(...)` callers keep the legacy behavior unless they explicitly route through the animated path.

## Boundary Review

| Boundary | Current Shape | Review Outcome |
| --- | --- | --- |
| Dialogue playback and flow block | `DialogueService` owns dialogue playback state, `GameFlowInputBlocker`, and run timer pause while dialogue is active. | Good current owner. Feature UI should continue to wait for dialogue playback and external UI blocks to release before opening. |
| NPC feature handoff | `INPCFeature.Execute(onComplete)` supports both blocking dialogue tags and features that exit dialogue before opening UI. | Keep this visible. Future features should distinguish dialogue-continuing behavior from dialogue-exit-then-open-UI behavior. |
| Upgrade runtime | `UpgradeManager` owns upgrade public compatibility entry points and public UI/data-change event surface. Purchase transaction policy delegates to `UpgradePurchaseService`, purchase success ordering delegates to `UpgradePurchaseCompletionService`, runtime effect reapply/run-start/hub-target handoff delegates to `UpgradeRuntimeEffectService`, run-start eligibility/timing delegates to `UpgradeRunStartEffectPolicy`, UI open fade/input-blocker/toggle execution delegates to `UpgradeUiOpenFlow`, scene/run/player lifecycle subscription plus run-start guard state delegates to `UpgradeRuntimeLifecycleService`, unlock-check save execution delegates to `UpgradeProgressSaveService`, notification/save dispatch delegates to `UpgradeNotificationService`, and singleton/persistent root adoption delegates to `UpgradeManagerLifetimeService`. | P2 target is a compatibility facade, not a rename. Keep new behavior out of the MonoBehaviour body and reopen only for effect-application ownership or planned scene/prefab migration. |
| Run modifiers | `RunModifierService` and its delta/snapshot/aggregation/rebuild/provider helpers live under `Assets/LeeJunMo/Script/Progression/RunModifiers/`. It aggregates upgrade and affection contributors for grave, chest, shop, and boss reward modifiers. Reward-facing consumers use `RunRewardModifierSnapshot`, aggregation traversal/calculation runs through `RunModifierAggregationService`, upgrade node loading/merge policy runs through `RunModifierUpgradeNodeProvider`, and rebuild execution orchestration runs through `RunModifierRebuildService`. | System-wide progression/run modifier owner, not upgrade-only. The current singleton/public API surface remains as the compatibility facade. |
| Merchant policy | `MerchantNPC` combines `ShopDefinitionSO`, `RunModifierService.ShopModifiers`, `MerchantShopPolicy`, and `MerchantRunStateService`. | Boundary is acceptable. Continue treating stock as run/session state and shop definition as authored policy. |
| Boss dialogue sequence | `BossEncounterDirector` is the current encounter dialogue/camera/combat-start path; `BossTalkManager` remains a legacy bridge with similar sequence responsibilities. Its legacy camera references are generic `Component` fields and are forwarded to the Core `ICameraPresentationSettingsReceiver` contract, while the concrete Cinemachine handling stays in Presentation. | Watch as legacy/current bridge. Do not create a separate backlog entry until prefab migration or duplicate sequence behavior becomes an active task. |
| Run-internal special NPCs | Construction and same-scene teleport NPCs use `RunSpecialNpcInteractor`, `SpeechBubbleComponent`, and local authored choices rather than `DialogueController`, Ink, portraits, or `DialogueView`. | Keep this flow separate from the existing Ink dialogue stack. Use `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md` for implementation details. |
| NPC editor authoring | `NpcCustomizationHubWindow` is the shared editor surface for existing `NPCData` profile/dialogue/presentation/affection asset edits, Ink template creation, usage scans, and validation. | V1 mutates only `NPCData` and selected `NPCDatabase` assets. Scene/prefab usage and RunSpecial NPC data remain read-only until a later authoring-risk review. |

## Extension Entry Points

- Add dialogue behavior through Dialogue Core and Dialogue UI buckets.
- Add new dialogue reveal timing through `DialogueAnimType`, `DialogueTextAnimationUtility`, and controller-owned Ink tags. Use scoped motion tags such as `[shake]`, `[tremble]`, `[punch]`, `[wave]`, `[float]`, `[rand_size=95,110]`, and `[slowshake]` for word-level emphasis instead of shaking a whole line by default.
- Tune existing TextAnimating values in `Tools/Dialogue/Text Animation Tuner`; the tool keeps the preview window on the left, profile inspector on the right, and bottom controls split into `Input | Playback Controls | Tag Presets`. Preview/profile, top/bottom, and bottom pane widths are draggable and stored in EditorPrefs. The preview uses the default `GlobalUIRoot.prefab` DialoguePanel text settings unless a manual `TextMeshProUGUI` Preview Source is assigned, and preview font changes require `Override Preview Font`. Use the profile pane's per-tag reset buttons when only one inline tag or one `CameraShake` preset should return to code defaults; `Camera All` resets the shared camera-shake multiplier too.
- Add background Effect changes with `# effect: <target>`, where `<target>` is `default`, `speaker`, or an NPC id such as `1005`.
- Add full-line impact shake with `# CameraShake: Low|Middle|High`; the same tag drives panel motion, DOShake-like per-character TMP impact offsets, text inertia, and camera shake. Keep `Middle` as the only medium-strength spelling and validate new Ink through the NPC Customization Hub.
- Add broad dialogue timing passes as additive Ink/JSON copies under `Assets/LeeJunMo/Datas/Inks/AnimatedVariants/` first; do not replace original TextAsset references until the owning scene/tool is intentionally rewired.
- Slime Queen's Notion-authored variant follows this additive rule: Notion source id `1004` is normalized to project NPC id `3001`, the new SpriteLibrary is assigned to `SlimeQueenBossNpc.asset`, and the new Melta Ink JSON remains inactive until `primaryInk` is explicitly reassigned. The Slime Queen SpriteLibrary keeps `default`, `Normal`, and `Idle` aliased to the Idle portrait so the existing placeholder `SlimeQueenBossIntro` face tag still resolves.
- Review and edit existing NPCData assets through `Tools/NPC/NPC Customization Hub` when changing profile, Ink JSON references, dialogue theme, sprite library, emote offset, affection rewards, or NPCDatabase membership.
- Add NPC actions through NPC Feature Core and feature-specific folders.
- Add run-internal speech-bubble NPC behavior through [Run Special NPC Structure](./RunSpecialNpcStructure.md), not the Ink portrait dialogue buckets.
- Add run-special NPC feature behavior by deriving from `RunSpecialNpcFeatureBase`; do not attach it to `NPCFeatureController` unless the design intentionally moves back into the portrait dialogue stack.
- Add upgrade behavior through Upgrade Feature and Upgrade Effects, keeping authored data in ScriptableObjects.
- Add affection reward behavior through Affection Effects and additive runtime modifier patterns.

## Known Pitfalls

- Upgrade UI previously failed when opened before dialogue blocker release; check `Docs/ErrorLog.md`.
- Merchant stock is session/run scoped and should preserve existing state when modifiers change.
- `UpgradeManager` is a compatibility facade. Purchase transaction policy has been split to `UpgradePurchaseService`, purchase success ordering has been split to `UpgradePurchaseCompletionService`, runtime effect handoff has been split to `UpgradeRuntimeEffectService`, run-start eligibility/timing has been split to `UpgradeRunStartEffectPolicy`, upgrade UI open fade/input-blocker/toggle execution has been split to `UpgradeUiOpenFlow`, lifecycle subscription/run-start guard state has been split to `UpgradeRuntimeLifecycleService`, unlock-check save execution has been split to `UpgradeProgressSaveService`, data-change/save/UI-close notification dispatch has been split to `UpgradeNotificationService`, and singleton/persistent root adoption has been split to `UpgradeManagerLifetimeService`.
- `RunModifierService` should not be treated as an upgrade-only service when reviewing loot, boss rewards, graves, chests, or merchant policy; reward consumers should prefer `RunRewardModifierSnapshot` over direct individual modifier property reads.
- `RunModifierService`, `RunModifierDeltas`, `RunRewardModifierSnapshot`, `RunModifierAggregationService`, `RunModifierUpgradeNodeProvider`, and `RunModifierRebuildService` live in the dedicated progression/run modifier layer.
- Boss dialogue has both current and legacy sequence drivers; check scene/prefab references before removing either path.
- `anim` Ink tags are presentation metadata. Keep them ignored by `DialogueTagHandler` so they do not become unknown tag warnings or gameplay blockers.
- `effect` Ink tags are presentation metadata. Keep them ignored by `DialogueTagHandler`; `DialogueController` applies them after `speaker` handling and before the line is typed.
- `CameraShake` Ink tags are presentation metadata. Keep them ignored by `DialogueTagHandler`; invalid values should be caught by editor validation, while runtime silently treats unsupported values as no shake.
- `[pause=seconds]` markers and scoped motion/TextAnimating tags are stripped from displayed TMP text. Keep any future range effects in the same parser family instead of overloading Ink line tags or gameplay tags.
- `[rand_size=min,max]` values are clamped by the active `DialogueTextAnimationProfileSO`; the default is still 80%-120%, and authoring should normally stay around 95%-110% for drunk pitch variation.
- Preview exaggeration can happen if tooling uses world `TextMeshPro` or arbitrary camera scale instead of the runtime `TextMeshProUGUI`/Canvas context; keep future preview changes tied to the default DialoguePanel source or an explicit manual preview source.
- `GlobalUIRoot.prefab` can author `DialogueText` with height `0` while relying on `DialogueTextCon` for layout. Editor previews must not copy that zero height literally without resolving an effective preview size.
- Do not copy `fontSharedMaterials` arrays from an authored TMP source into a freshly created preview TMP object; copy the font and primary shared material, then let TMP build submesh materials for the preview text.
- `PreviewRenderUtility` may not be enough for hidden UGUI CanvasRenderer previews; use a dedicated preview camera and `RenderTexture` when the source/effective rect diagnostics are valid but the preview still renders black.
- Keep Text Animation Tuner diagnostic fields out of the normal Input pane once the effective-size fallback is stable; use internal logic and source selection rather than exposing size debug controls during normal tuning.
- Animated Ink variants are not automatically used by existing scenes just because the files exist; Unity import and explicit TextAsset reassignment are still required.
- Notion dialogue source ids can differ from project `NPCData.id`; normalize `# speaker` and `# face` tags to the project id before compiling or the runtime portrait lookup can miss the intended NPCData.
- Ink `# face: npcId: label` uses the NPC id to pick `NPCData`, but portrait sprite lookup uses the runtime SpriteLibrary category `Face` and the authored label. Editor validation should not treat the NPC id as a SpriteLibrary category.
- Affection presentation must not rely only on DOTween `OnComplete`; disable, destroy, or replacement-animation paths also need to release the dialogue continuation.
- Do not tween choice-row `RectTransform` positions while the parent `LayoutGroup` is actively rebuilding them. Rebuild first, capture each authored layout position, disable the layout group for the transition, and restore it during choice cleanup.
- Affection reward display can deadlock if it uses the normal reward opening path while a dialogue or boss encounter external blocker is active. Use the flow-owned reward path for dialogue-gated affection rewards, and keep the missing-view fallback so dialogue can continue if the authored reward UI is absent.
- ScriptableObject changes need asset migration/reference review.

## Refactor Candidates

- `Docs/RefactorBacklog/UpgradeRuntimeBoundarySplit.md` records the resolved P2 compatibility facade choice for upgrade runtime. Reopen it only for effect-application ownership or an explicit scene/prefab migration.
- `Docs/RefactorBacklog/RunModifierAggregationBoundarySplit.md` records the resolved move from Upgrade-owned service semantics to a progression/run modifier aggregator with Upgrade and Affection as contributors.

## Promotion Candidate

Stable dialogue rules already live in `Docs/Architecture/DialogueArchitecture.md`. Keep feature-specific structure here until a rule is stable enough to promote.


### Retained lower frame between directed dialogue segments

SetLowerPanelRetainedBetweenDialogues is opt-in presentation state forwarded by DialoguePlayback/Service/Controller to DialogueView. HideUI can finish a session while leaving textBoxGroup visible; the next ShowUI consumes that retained frame without another opening animation. Releasing during a new session does not hide its retained frame before deferred opening; releasing between sessions closes the retained panel. If retention is canceled during closing, FinishHide clears the retained root. This does not alter story input or upper-frame/portrait policies.


### Retained-frame theme lifetime

ResetTheme preserves currentTheme and visible accent colors while lowerPanelRetained is true. Effect cleanup still runs. The next ApplyTheme replaces that theme normally; releasing retention between sessions closes the frame, clears retention and resets its theme. Actual final dialogue closing also resets normally. Upper-frame camera hiding uses PlayGroupClose with an optional completion callback, enabling GrandHall to wait for its authored exit animation.

### Common cinematic HUD fade (2026-09-16)

CinematicLetterboxOverlay always captures GameplayHUD and BossHUD and fades them to alpha 0 in every PlayIn overload. Custom layer lists and captureGlobalUiLayers=false now control only non-HUD layers; dialogue and tutorial prompt panels can remain visible independently. Duplicate canvas references are captured once. PlayOut restores the captured original alpha and interaction flags; Dispose retains immediate cancellation cleanup. Existing unscaled-time fade duration follows the caller's letterbox duration. The implementation reuses the existing overlay/CanvasGroup lifecycle; no new runtime UI hierarchy was introduced.

GrandHall's custom list previously omitted GameplayHUD, causing missing pre-dialogue HUD fade. HubIntroAfterDarkLordSequence no longer disables the HUD Canvas before PlayIn when useLetterbox is true, allowing the common fade to render. The existing no-letterbox immediate-hide option is retained. Letterbox completion still precedes Dialogue ownership; dialogue panel/camera-cue rules are unchanged. Already-disabled tutorial-specific HUD roots remain disabled rather than being forcibly activated.

### Grand Hall training dummy entry visibility

`GrandHallScribeSequence.trainingDummy` references the scene-authored `TrainingDummy_GrandHall_Center` instance. The prefab remains authored active. Awake hides it only when loaded profile data has not completed `grandhall_scribe_intro_seen`; Start repeats that check after save readiness, before waiting for player/dialogue/transition readiness. Intro completion and sequence cleanup do not reactivate the dummy during that visit. Normal officer-boss exits reload Grand Hall after stage one, restoring the authored active instance; the saved intro flag prevents hiding on that return and on later runs, including runs starting at zero defeated officers. This depends on the existing one-way Hub entry and boss-return scene flow; revisiting Grand Hall before a boss clear would also use the seen-intro behavior. No new progress flag or run lifecycle owner is introduced. Move the existing instance Transform to adjust placement.
