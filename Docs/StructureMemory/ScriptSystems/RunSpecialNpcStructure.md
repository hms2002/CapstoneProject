---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-30
---

# Run Special NPC Structure

## Purpose

Map the first implementation slice for run-internal special NPCs that use in-world speech bubbles and local interaction choices instead of the existing Ink portrait dialogue stack.

This is a structure-memory map. It does not override `Docs/Architecture/DialogueArchitecture.md`, `Docs/Architecture/RuntimeSaveArchitecture.md`, or project contracts.

## Current Structure

| Area | Current responsibility |
| --- | --- |
| Interaction entry | `RunSpecialNpcInteractor` derives from `InteractableBase`, so player prompt, proximity, and interact-state gating stay aligned with other world objects. |
| Speech bubble dialogue | `RunSpecialNpcInteractor` sequences `RunSpecialNpcDialogueSetSO` branches through `SpeechBubbleComponent`. It does not call `DialogueController`, Ink, portrait UI, or `DialogueView` choices. |
| Choice UI | `RunSpecialNpcChoicePresenter` projects an authored `CanvasGroup`, `Button[]`, and `TMP_Text[]`. `RunSpecialNpcChoiceAnchorFollower` can position that screen-space panel from a player world anchor. It relays clicks and number-key selection after an input guard. If a choice button root has the existing `DialogueChoiceKeyGlyph` component and an authored `KeyGlyph` child, the presenter binds the visible `1`/`2`/`3` keyboard guide icon. It does not create UI hierarchy. A scene may explicitly enable single-choice execution without a presenter for one-action bootstrap authoring, but multi-choice NPCs still need an authored presenter. |
| Flow ownership | `RunSpecialNpcInteractor` owns the active flow, SO branch execution, input blocker, run-timer pause, `Time.timeScale` pause, line skip input, letterbox lifetime, camera focus/restore, player talking state, and cleanup. |
| Feature modules | `RunSpecialNpcFeatureBase` is the shared feature entry. Current modules are `RunConstructionNpcFeature` and `RunSameSceneTeleportNpcFeature`. Features can opt into post-presentation execution through `ExecuteAfterRunSpecialPresentationClose`. |
| Dialogue data | `RunSpecialNpcDialogueSetSO` owns authored line/choice branches. The primary `RunSpecialNpcFeatureBase` chooses the branch key and resolves feature-specific line text. The custom Inspector normalizes newly added line entries to the existing speech-bubble default duration and theme values. At playback time, line breaks inside one text field are expanded into separate speech-bubble lines. |
| Construction persistence | `GameData.runSpecialNpcData.constructionRecords` stores durable construction state. `GamePlayData.pendingRunSpecialNpcConstructionStarts` holds run-active starts until `RunSessionProgressCommitPolicy` commits them. |
| Construction site tilemaps | `ConstructionSiteTilemapModule` owns scene-authored blocked/open roots for a construction block. It toggles temporary wall tilemaps off and open ground/Door/Shortcut/Chest roots on when construction completes. `SlimeCorridor` currently has a minimal `ConstructionSite_Test_01` validation module: `BlockedState` contains the existing `BlockCollidor`, `OpenState` is an empty inactive root, and Door/Shortcut saving is intentionally disabled for this test. |

## Key Files

- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcInteractor.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcChoicePresenter.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcChoiceAnchorFollower.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcFeatureBase.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcModels.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcBranch.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcDialogueSetSO.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSpecialNpcConstructionProgress.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunConstructionNpcFeature.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/RunSameSceneTeleportNpcFeature.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Characters/Runtime/PlayerCinematicProtection.cs`
- `Assets/HeoMinSeok/_Project/Scripts/Gameplay/Characters/Runtime/PlayerTargetabilityBlocker.cs`
- `Assets/Script/Enemy/Enemy.cs`
- `Assets/LeeJunMo/Script/Camera/CameraBootstrap.cs`
- `Assets/LeeJunMo/Script/Map/Construction/ConstructionSiteTilemapModule.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubble.cs`
- `Assets/LeeJunMo/Script/SpeechBubble/SpeechBubbleComponent.cs`
- `Assets/LeeJunMo/Script/Presentation/Runtime/CinematicLetterboxOverlay.cs`
- `Assets/LeeJunMo/Script/SaveData/GameData.cs`
- `Assets/LeeJunMo/Script/SaveData/GamePlayData.cs`
- `Assets/LeeJunMo/Script/SaveData/RunSessionProgressCommitPolicy.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/Editor/RunSpecialNpcDialogueSetSOEditor.cs`
- `Assets/LeeJunMo/Script/Dialogue/NPC/RunSpecial/Editor/RunSpecialNpcDialogueSetAssetMigrationTool.cs`
- `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab`
- `Assets/Scenes/SlimeCorridor.unity`

## Implemented Flows

### Shared Speech-Bubble Flow

- Player interaction starts only while the player is `Idle` and no run-special flow is active.
- The flow hides the world prompt, sets the player to `Talking`, acquires `GameFlowInputBlocker`, pauses `RunTimeLimitSystem`, and sets `Time.timeScale` to `0` until cleanup.
- The flow plays the existing `CinematicLetterboxOverlay` and fades HUD-style global canvas layers to the configured target alpha, like the Merchant cinematic. The run-special path excludes `GlobalCanvasLayer.Dialogue` from that fade so the authored `DialogueCanvas` choice panel remains visible and clickable.
- After letterbox-in, the flow can temporarily focus `CameraBootstrap`'s gameplay `CinemachineCamera` on the NPC focus target. The default target is the assigned speech bubble transform, falling back to the NPC transform. The camera brain uses unscaled blending while dialogue has `Time.timeScale = 0`, and focus/return waits treat serialized wait seconds as minimum holds before waiting for camera settle.
- The interactor asks its primary `RunSpecialNpcFeatureBase` for a `RunSpecialNpcDialogueBranchKey`, reads that branch from `RunSpecialNpcDialogueSetSO`, then plays branch lines and branch choices. Legacy interactor-authored line/choice fields remain only as dialogue-set asset migration input.
- Run-special lines call `SpeechBubbleComponent.SpeakWithPreSizedLayout(...)`, so the bubble measures the full line first, clamps the text width, enables wrapping, and then starts the typewriter reveal inside the already-sized bubble. Normal `SpeechBubbleComponent.Speak(...)` calls keep the legacy empty-text-then-grow behavior.
- If a `RunSpecialNpcLine.Text` contains line breaks, `RunSpecialNpcInteractor` splits that text into multiple speech-bubble lines during playback. Each split segment keeps the original line's duration and theme, and blank segments are ignored. This applies to branch lines and choice response lines.
- Line skip follows the existing Dialogue input rule: left click or `InputActionId.DialogueAdvance` advances the current speech-bubble line. The first skip completes active typing; the next skip hides the line. Choice selection remains click or number-key based, so Space/DialogueAdvance does not confirm run-special choices.
- Choices are authored in the dialogue set SO. A choice can include `unavailableResponseLines`; when present and the feature allows that unavailable state to be exposed, the choice may stay visible even if its action is currently unavailable, so the selected choice can explain the failure before any feature execution.
- Before the authored choice presenter appears, the flow returns the camera to the player, waits for the unscaled minimum return window and camera settle, then shows the player choice UI. If the selected choice has NPC response lines, the flow focuses the camera back on the NPC before playing those lines.
- The authored choice presenter accepts clicks and number keys `1`-`9` after the configured guard window. It supports the current three-button panel by activating only visible choice slots and deactivating unused button objects so layout collapses correctly. For the current three-button authoring, attach `DialogueChoiceKeyGlyph` to each choice button root and place the visual `KeyGlyph` child under that button; the presenter binds the icon to the visible choice index and hides it with unused slots.
- When the presenter has `RunSpecialNpcChoiceAnchorFollower`, the interactor assigns the current player transform after the camera return and before showing choices, then clears it during flow cleanup. The current authored panel lives under `GlobalUIRoot > DialogueCanvas` and uses screen-space overlay positioning from player world coordinates.
- Most feature executions run inside the paused run-special dialogue flow. A feature that returns `ExecuteAfterRunSpecialPresentationClose = true` is deferred until after camera return, letterbox/HUD close, run-timer unpause, and `Time.timeScale` restore. The flow keeps the input blocker and player talking state until that deferred feature finishes.
- Construction-pending branching is selected by `RunConstructionNpcFeature.GetDialogueBranchKey(...)`, not by `RunSpecialNpcInteractor`.
- Construction-pending lines can use the authored text token `N일`; `RunConstructionNpcFeature.ResolveDialogueLineText(...)` replaces it at playback time with `GetRemainingRunCompletions()`, so the SlimeCorridor line `앞으로 N일 정도 남았어.` displays the current remaining run count.
- If `executeSingleChoiceWithoutPresenter` is explicitly enabled and exactly one visible choice remains, the interactor may execute that choice without showing a choice presenter. This is a compatibility path for scene authoring that has not yet added the speech-bubble choice UI.
- Cleanup hides choices, hides the active speech bubble, restores the gameplay camera target, disposes the letterbox overlay, releases the input blocker, releases run-timer pause, restores `Time.timeScale`, and restores player state.

### Construction / Permanent Shortcut NPC

- `RunConstructionNpcFeature` owns construction dialogue branch-key selection.
- If construction has started and is not complete, it returns `ConstructionPending`.
- If construction is complete, it returns `ConstructionCompleted`.
- If construction has not started, it returns `ConstructionNotStarted` whether or not the player currently has enough magic stones.
- `RunSpecialNpcDialogueSetSO` owns the line/choice data for those construction branches.
- The insufficient-funds line belongs to the payment choice's `unavailableResponseLines`, not to the initial construction branch. `RunConstructionNpcFeature.ShouldShowUnavailableChoice(...)` exposes that payment choice only for payment-shortage state. `RunSpecialNpcInteractor` checks `CanExecute(...)` before selected success response lines or `Execute(...)`; if payment is short, it plays the selected choice's unavailable response lines and stops before execute.
- `RunConstructionNpcFeature` spends `CurrencyManager` magic stones only when construction has not started.
- If the run is active, construction start is recorded in `GamePlayData.pendingRunSpecialNpcConstructionStarts` and committed with other pending run progress.
- If the run is not active, construction start writes directly to `GameData.runSpecialNpcData`.
- Completion is based on `GameData.clearCount - startedClearCount >= requiredRunCompletions`.
- When complete, the feature should call an authored `ConstructionSiteTilemapModule` when one is assigned. The module toggles `BlockedState` / `OpenState`, registers open ground tilemaps with scene pathfinders, refreshes safety tilemap caches, and opens the module-owned target `DoorObject`.
- Direct `blockedStateRoot` / `openStateRoot` and `targetDoor` fields on `RunConstructionNpcFeature` remain a compatibility fallback for older authoring.
- Permanent unlock uses `DoorObject.ForceOpen(... save: true)` and `ShortcutProgressService` rather than tilemap mutation.
- `CanExecute(...)` treats missing magic stones as unavailable before spending. The interactor checks that before selected response lines, so the success response does not play when payment cannot be made.

### Same-Scene Teleport NPC

- `RunSameSceneTeleportNpcFeature` owns teleport dialogue branch-key selection.
- If affection is required and too low, it returns `TeleportLocked`.
- If destination/player state is invalid, it returns `TeleportUnavailable`.
- If teleport is available, it returns `TeleportAvailable`.
- `RunSpecialNpcDialogueSetSO` owns the line/choice data for those teleport branches.
- `RunSameSceneTeleportNpcFeature` can require affection through `AffectionManager.GetAffection(npcId)`.
- Teleport uses an authored required `landingPoint` and an optional `appearancePoint`. Existing serialized `destination` references migrate to `landingPoint`; if `appearancePoint` is empty, the feature uses `landingPoint` for both positions.
- Optional fade uses `SceneFadeTransitionService.TryBeginOverlayFadeSession(...)`.
- Player movement prefers `MovementMotor2D.WarpTo(...)` and falls back to `Rigidbody2D`/transform position when the motor is absent.
- `RunSameSceneTeleportNpcFeature` opts into post-presentation execution so `MovementMotor2D.WarpTo(...)` and its following `WaitForFixedUpdate()` run after `Time.timeScale` has been restored.
- Current arrival order is fade out, warp to `appearancePoint`, fade in, then move from appearance to `landingPoint` over the authored arrival movement duration. With no `appearancePoint`, this collapses to the old one-position teleport.
- During teleport execution, `RunSameSceneTeleportNpcFeature` acquires `PlayerCinematicProtection` and `PlayerTargetabilityBlocker`. The current release point is after the fade/warp/arrival movement sequence finishes, so enemy recognition resumes at the same point player protection is released.
- Enemy perception stays on the normal `Enemy.CanPerceiveTarget(...)` path. That method treats a player-root or player-child candidate with an active `PlayerTargetabilityBlocker` as not perceivable, while preserving existing distance, door, and collider checks for targetable candidates.
- It is same-scene movement only. It does not call `ScenePortal` or route/scene transition APIs.

## Ownership And Lifecycle

- The flow owner owns active conversation state, dialogue-set branch execution, input-blocking window, run-timer pause, and cleanup.
- `RunSpecialNpcDialogueSetSO` owns authored line/choice data, while the primary feature owns feature-specific branch-key selection and line text formatting.
- The flow owner also owns the local `Time.timeScale` pause, letterbox overlay lifetime, and temporary camera focus state. Speech bubble and choice UI own only presentation and input relay.
- Run-special camera focus reuses `CameraBootstrap.GetPlayerCamera()`, `GetBrain()`, and `GetLegacyFollow()`. It caches Follow/LookAt/Priority, legacy follow enabled state, and `CinemachineBrain.IgnoreTimeScale`, then restores them on normal exit or cleanup.
- Run-special HUD fade reuses `CinematicLetterboxOverlay`, but passes an explicit layer list that fades `GameplayHUD`, `Popup`, `Hover`, `Prompt`, `Reward`, `DamagePopup`, and `BossHUD`. `Dialogue` is intentionally excluded because run-special choices are authored there.
- `SpeechBubble` tweens run on unscaled time so run-special dialogue can continue while `Time.timeScale` is paused.
- Pre-sized speech-bubble layout is opt-in from the run-special flow. Other speech-bubble users should continue using the default `Speak(...)` path unless they explicitly want dialogue-style fixed-width wrapping.
- Currency spending stays with `CurrencyManager`.
- Affection checks stay with `AffectionManager`.
- Teleport presentation protection is split by meaning: `PlayerCinematicProtection` owns control lock and invulnerability, while `PlayerTargetabilityBlocker` owns enemy targetability. `State.Invulnerable` should not be used as an enemy-recognition condition.
- Durable construction progress stays in `GameData.runSpecialNpcData`.
- Run-active construction starts stay pending in `GamePlayData` until run progress commit.
- Construction block visual/collision state stays in scene-authored `ConstructionSiteTilemapModule` roots. UI and NPC dialogue do not own those active states.
- Durable shortcut unlock state stays with `DoorObject` / `ShortcutProgressService`.
- Same-scene teleport uses the current player runtime object. It does not create a new player or run scene transition capture/restore.

## Extension Entry Points

- Add authored line/choice data in `RunSpecialNpcDialogueSetSO`.
- Add new feature dialogue state by extending the feature's branch-key API and the dialogue set SO/editor branch surface; do not add feature-specific branch checks to `RunSpecialNpcInteractor`.
- Add feature modules by deriving from `RunSpecialNpcFeatureBase`.
- Add presenter prefabs/scene objects with `RunSpecialNpcChoicePresenter` and serialized button/text references. Add the existing `DialogueChoiceKeyGlyph` to each button root when the button should show a keyboard shortcut guide icon.
- Add scene/prefab references for speech bubble anchors, choice anchors, teleport destinations, and construction site modules. A construction site should contain `BlockedState` with temporary wall tilemap/colliders and `OpenState` with open ground, optional wall edges, Door/Shortcut anchors, and optional Chest.
- Add validation later if these NPCs become common: missing destination, missing speech bubble, missing target shortcut, invalid cost, invalid construction id, too many choices for authored buttons, and missing gate target.

## Known Pitfalls

- Do not route these NPCs through `DialogueController` unless the design explicitly changes to Ink portrait dialogue.
- Do not add construction, teleport, or future feature branch rules back into `RunSpecialNpcInteractor`; keep those rules in feature-owned branch-key APIs.
- Do not let the speech bubble or choice UI become the source of truth for construction progress, unlock state, currency, or affection.
- Do not silently mutate main tilemaps as the first solution for path opening. Construction sites use additive authored tilemap blocks instead: inactive/active roots switch the temporary wall and open ground state.
- Inactive tilemaps are discoverable through APIs such as `FindObjectsOfType<Tilemap>(true)`. Consumers that scan tilemaps must filter inactive maps, and construction modules must refresh or register runtime consumers after state changes.
- Scene/prefab authoring is still required. The source slice does not create the speech bubble, teleport destination, target door, or blocked/open construction objects.
- `SlimeCorridor` currently wires `ConstructionNpc` through a minimal construction-site validation state: `ConstructionSite_Test_01` is assigned to `RunConstructionNpcFeature.constructionSiteModule`, `BlockedState` owns the existing `BlockCollidor`, and `OpenState` is an empty inactive root. This is enough to test blocked/open root switching, but it is not final Door/Shortcut/Chest authoring.
- `ShortcutTarget` / `targetDoor` is optional for this validation. It is only needed when completion should also open and save a real `DoorObject` shortcut anchor; leaving it unset means completion only toggles the construction-site blocked/open roots.
- Scene-level references to prefab-owned run-special UI should be reviewed in Unity after prefab import because they use stripped prefab-instance references.
- Cleanup paths must not use C# null-conditional calls on Unity component references such as `SpeechBubble`, `SpeechBubbleComponent`, choice presenter, or anchor follower. Destroyed Unity objects compare null only through Unity's overloaded null check.
- Camera focus must restore even if the flow is interrupted by disable/destroy. If another system owns the camera target while interaction is allowed, this flow may restore toward the current player, so those systems should block run-special interaction first.
- Because choices are shown after a camera return to the player, NPC response lines that should be visually framed need response text entries so the flow can focus the camera back on the NPC before speaking.
- `openingLines`, `noAvailableChoiceLines`, and `choices` remain on `RunSpecialNpcInteractor` only as legacy dialogue-set migration source fields.
- Legacy provider components are migration-only source readers. Run `Tools/RunSpecialNpc/Create Dialogue Set Asset From Selected Interactors`, inspect the generated SO, and save scene/prefab references before removing legacy fields or provider components.
- Same-scene teleport needs play-mode validation for camera follow, enemy detection, physics overlap, fade cleanup, and prompt cleanup.
- If a future same-scene teleport replaces the built-in appearance-to-landing movement with a dedicated water-emerge or landing animation, keep the `PlayerTargetabilityBlocker` release at the true end of that arrival presentation. Releasing it immediately after warp would let enemies recognize the player before control is restored.
- Because run-special interaction locally stores and restores `Time.timeScale`, avoid starting it while another system owns a time freeze unless that owner is known to block the interaction first.
- Do not execute FixedUpdate-dependent feature behavior inside the paused dialogue window. Use `ExecuteAfterRunSpecialPresentationClose` for feature actions that need physics or scaled game time, as same-scene teleport does.
- `CinematicLetterboxOverlay` still creates a runtime overlay object. Run-special NPCs reuse it as the existing letterbox implementation, but an authored overlay under `GlobalUIRoot` would be the cleaner contract target if this becomes a broad pipeline.
- If a future choice UI moves out of `GlobalCanvasLayer.Dialogue`, review the run-special faded layer list so the choice panel is not faded or made non-interactable.
- Save-schema changes require Unity import/compile and existing profile load validation.
- Remaining construction count formatting is intentionally narrow: only the `N일` token is replaced. Do not replace every `N`, because English text, IDs, or future tokens could be corrupted.
- For construction payment shortage, do not move the check into `Execute(...)` alone. The interactor must check `CanExecute(...)` before selected success response lines and execute, then play the selected choice's `unavailableResponseLines` so the player does not hear the success response before a failed payment.
- In run-special SO authoring, a text-field line break means "next speech line." Use the speech bubble's automatic wrapping for visual wrapping inside one line; do not add hard line breaks when the intended behavior is only visual text wrapping.

## Promotion Candidate

Not yet. Keep this in StructureMemory until scene/prefab authoring and play-mode validation prove the stable boundaries. If the flow becomes a recurring content pipeline, promote stable authoring rules into `Docs/Architecture/` or `Docs/Contracts/` after explicit approval.
