---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-30
---

# Dialogue NPC Affection Structure

## Purpose

Map dialogue, NPC features, affection, merchant, upgrade, and boss dialogue scripts.

## Current Structure

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
| Affection Runtime / UI | 7 | Affection manager, progress/reward processing, UI, gradient border, gain screen effect, and base effect. |
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

- `Assets/LeeJunMo/Script/Dialogue/DialogueService.cs`
- `Assets/LeeJunMo/Script/Dialogue/UI/DialogueView.cs`
- `Assets/LeeJunMo/Script/Dialogue/UI/DialogueTextAnimationProfileSO.cs`
- `Assets/LeeJunMo/Script/Dialogue/UI/DialogueTextAnimationUtility.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubble.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubbleComponent.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Upgrade/UpgradeFeature.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/NPCFeature/Merchant/MerchantNPC.cs`
- `Assets/LeeJunMo/Script/Dialogue/Affection/AffectionManager.cs`
- `Assets/LeeJunMo/Script/Dialogue/Affection/AffectionUI.cs`
- `Assets/LeeJunMo/Script/Dialogue/Affection/AffectionRewardProcessor.cs`
- `Assets/LeeJunMo/Script/UIStructure/RewardDisplayService.cs`
- `Assets/LeeJunMo/Script/Editor/NpcCustomizationHub/NpcCustomizationHubWindow.cs`
- `Assets/LeeJunMo/Script/Editor/DialogueTextAnimationTunerWindow.cs`

## Ownership And Lifecycle

- Dialogue playback and its input block should be owned by `DialogueService`.
- NPC features that open stack UI after dialogue should wait for dialogue blockers to release before opening their UI.
- Merchant stock and upgrade progress are run/save-policy sensitive; do not treat UI state as the source of truth.
- Affection should contribute modifiers/rewards without bypassing base progression reward rules.
- Affection gain presentation gates dialogue continuation, so interrupted DOTween sequences must still invoke their pending completion callback exactly once.
- Affection reward popups are part of the dialogue flow: after affection gain presentation, show the reward UI through `RewardDisplayService.ShowFlowOwnedReward(...)`, keep the dialogue continuation as the reward close callback, and resume dialogue only after the reward UI closes.
- Boss reward affection effects author their own additive fields and convert them into `BossRewardModifierAggregate` at runtime; they should not depend on a separate modifier ScriptableObject asset.
- Dialogue line rhythm is owned by `DialogueView` plus `DialogueTextAnimationUtility`: `DialogueController` resolves line-level `anim` Ink tags, while inline `[pause=seconds]` and scoped motion/TextAnimating tags are stripped before TMP display and applied through unscaled reveal delays plus TMP vertex offsets/scales.
- TextAnimating values are tuned through `DialogueTextAnimationProfileSO`. `DialogueView.textAnimationProfileOverride` is optional; the default profile is loaded from `Assets/LeeJunMo/Datas/Resources/Dialogue/DefaultDialogueTextAnimationProfile.asset`, with code defaults as fallback.
- The Text Animation Tuner preview should mirror the default DialoguePanel rendering context. It resolves `GlobalUIRoot.prefab` `DialogueView.dialogueText`, renders through a hidden world-space `Canvas + TextMeshProUGUI` into a `RenderTexture` with a dedicated preview camera, and copies the source font, rect, wrapping, spacing, alignment, and color before applying `DialogueTextAnimationUtility`. If the source text rect has a zero or invalid size, preview-only sizing resolves width/height from the parent `DialogueTextCon` container before falling back to `1720 x 250`.
- Drunk/slurred dialogue can use `[rand_size=min,max]...[/rand_size]` to give each visible character a stable pseudo-random scale inside the configured clamp range, and `[slowshake]...[/slowshake]` for low-speed per-character shake.
- Dialogue background Effect switching is owned by `DialogueController` through the line-level `effect` Ink tag. `DialogueView.ApplyDialogueEffectTheme(...)` changes only the DialogueEffect AnimatorOverrideController and leaves textbox/speaker colors under the speaker theme.
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
| Boss dialogue sequence | `BossEncounterDirector` is the current encounter dialogue/camera/combat-start path; `BossTalkManager` remains a legacy bridge with similar sequence responsibilities. | Watch as legacy/current bridge. Do not create a separate backlog entry until prefab migration or duplicate sequence behavior becomes an active task. |
| Run-internal special NPCs | Construction and same-scene teleport NPCs use `RunSpecialNpcInteractor`, `SpeechBubbleComponent`, and local authored choices rather than `DialogueController`, Ink, portraits, or `DialogueView`. | Keep this flow separate from the existing Ink dialogue stack. Use `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md` for implementation details. |
| NPC editor authoring | `NpcCustomizationHubWindow` is the shared editor surface for existing `NPCData` profile/dialogue/presentation/affection asset edits, Ink template creation, usage scans, and validation. | V1 mutates only `NPCData` and selected `NPCDatabase` assets. Scene/prefab usage and RunSpecial NPC data remain read-only until a later authoring-risk review. |

## Extension Entry Points

- Add dialogue behavior through Dialogue Core and Dialogue UI buckets.
- Add new dialogue reveal timing through `DialogueAnimType`, `DialogueTextAnimationUtility`, and controller-owned Ink tags. Use scoped motion tags such as `[shake]`, `[tremble]`, `[punch]`, `[wave]`, `[float]`, `[rand_size=95,110]`, and `[slowshake]` for word-level emphasis instead of shaking a whole line by default.
- Tune existing TextAnimating values in `Tools/Dialogue/Text Animation Tuner`; the tool keeps the preview window on the left, profile inspector on the right, and bottom controls split into `Input | Playback Controls | Tag Presets`. Preview/profile, top/bottom, and bottom pane widths are draggable and stored in EditorPrefs. The preview uses the default `GlobalUIRoot.prefab` DialoguePanel text settings unless a manual `TextMeshProUGUI` Preview Source is assigned, and preview font changes require `Override Preview Font`. Use the profile pane's per-tag reset buttons when only one inline tag or one `CameraShake` preset should return to code defaults; `Camera All` resets the shared camera-shake multiplier too.
- Add background Effect changes with `# effect: <target>`, where `<target>` is `default`, `speaker`, or an NPC id such as `1005`.
- Add full-line impact shake with `# CameraShake: Low|Middle|High`; the same tag drives panel motion, DOShake-like per-character TMP impact offsets, text inertia, and camera shake. Keep `Middle` as the only medium-strength spelling and validate new Ink through the NPC Customization Hub.
- Add broad dialogue timing passes as additive Ink/JSON copies under `Assets/LeeJunMo/Datas/Inks/AnimatedVariants/` first; do not replace original TextAsset references until the owning scene/tool is intentionally rewired.
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
- Ink `# face: npcId: label` uses the NPC id to pick `NPCData`, but portrait sprite lookup uses the runtime SpriteLibrary category `Face` and the authored label. Editor validation should not treat the NPC id as a SpriteLibrary category.
- Affection presentation must not rely only on DOTween `OnComplete`; disable, destroy, or replacement-animation paths also need to release the dialogue continuation.
- Affection reward display can deadlock if it uses the normal reward opening path while a dialogue or boss encounter external blocker is active. Use the flow-owned reward path for dialogue-gated affection rewards, and keep the missing-view fallback so dialogue can continue if the authored reward UI is absent.
- ScriptableObject changes need asset migration/reference review.

## Refactor Candidates

- `Docs/RefactorBacklog/UpgradeRuntimeBoundarySplit.md` records the resolved P2 compatibility facade choice for upgrade runtime. Reopen it only for effect-application ownership or an explicit scene/prefab migration.
- `Docs/RefactorBacklog/RunModifierAggregationBoundarySplit.md` records the resolved move from Upgrade-owned service semantics to a progression/run modifier aggregator with Upgrade and Affection as contributors.

## Promotion Candidate

Stable dialogue rules already live in `Docs/Architecture/DialogueArchitecture.md`. Keep feature-specific structure here until a rule is stable enough to promote.
