# Grand Hall scribe sequence

Current structure map, not an Architecture/Contracts document. Updated 2026-09-15.

## Purpose and ownership

`Grand Hall.unity` authors `GrandHall_Scribe` at (-26, 13.5), below the three corridor portals, and a separate `ScribeFocusCamera`. `GrandHallScribeSequence` owns presentation timing, interaction and cleanup. Existing DialoguePlayback, NPCFeatureController, speech bubble, letterbox, input blocker and player protection APIs perform their existing responsibilities. No manager or persistent presentation object was added.

## Files and entry points

- `Assets/_Project/Runtime/Presentation/Dialogue/NPC/GrandHallScribeSequence.cs`: automatic entry flow and repeat interaction. Serialized references select NPC/story, speech, input blocker, camera and four portal targets. `moveSeconds`, `closeSize`, `portalPadding`, `greetingSeconds` tune presentation.
- `Assets/_Project/Data/Dialogue/Ink/GrandHallScribe.ink` and `.json`: intro, audience and repeat knots. Source comes from the Notion scribe document, with the user's amended three-area line and return dialogue. Each `[camera cue] # feature: scribe_cue` line is consumed by the existing blocking-feature path and never displayed. Do not put a spoken line on the same blocking tag: DialogueController resumes by advancing the story.
- `ScribeNpc.asset`, `SpriteLibrary/ScribePortrait.asset`, and `Art/Sprites/NPC/Scribe/`: NPC ID 1010, Face/Normal portrait and original ZIP images. World sprite uses 32 PPU with bottom-center pivot; portrait uses 100 PPU. Both use point filtering and no texture compression.

## State flow

- Slot lifetime: TutorialProgressStore completion key `grandhall_scribe_intro_seen`. Commit after the intro's final completion cue and dialogue exit. Ordinary repeated interaction enters the repeat knot.
- Run lifetime: GamePlayData `grandHallScribeReturnStage` records completed return presentation; `grandHallAudienceGranted` unlocks the final portal. StartRun, EndRun and development reset clear both fields.
- Count uses RunOfficerQuestProgress's distinct slime/dragon/shadow boss IDs, not permanent boss unlocks.
- One/two clears: automatic world bubble, no letterbox or camera lock; acknowledge after bubble completion.
- Three clears: focus scribe, dialogue, focus final portal, set audience granted, final dialogue, return camera and release letterbox/input. Portal requirement and existing GrandHallClearedPortalView project the same granted state.
- `RequiredBossClearScenePortalAccessRule.requireGrandHallAudience` is enabled only on the Grand Hall final portal. Other portal rules retain their prior behavior.

## Camera and lifecycle

Overview is centered on the dragon portal. Orthographic size accounts for all three portal positions, aspect ratio, padding and the letterbox. World sorting and portal positions are unchanged. The authored focus camera has no follow component; it interpolates its transform/lens with unscaled time and uses CameraCinematicWaitUtility for blending. Cleanup restores priority, legacy follow state, brain time mode, letterbox-owned layer states and player protection. Disabling the sequence stops owned coroutines and requests exit for its active cinematic dialogue.

## Verification and remaining checks

MSBuild included the new script through a temporary verification targets file without editing Unity-generated csproj files. Ink compilation/playback checks confirmed 9 intro spoken lines/4 cues, 2 audience lines/2 cues, and 1 repeat line/no cues. New scene fileIDs/references and NPC ID uniqueness were checked.

Unity import/Play Mode rendering not executed. Verify fresh-slot entry, reentry, distinct slot, run reset, one/two/three boss returns, interrupted dialogue, camera framing at intended aspect ratios, portrait/bubble placement, and locked-to-active final portal timing. No Architecture/Contracts promotion or Presentation HTML update proposed.

### First-entry readiness correction

Normal `Hub_GrandHall` travel uses `runAction: None`. Entry readiness therefore requires loaded slot/player/dialogue and finished transition, not an active run. `IsCurrentSlot` protects intro continuation and its completion write; `IsCurrentRun` protects return greetings and audience permission. `CanContinueCinematic` selects slot identity for intro and active-run identity for the three-clear cinematic. This distinction applies to camera interpolation and blocking Ink callbacks as well as initial entry. Runtime verification of normal hub travel remains pending.

### Dialogue visibility during letterbox (2026-09-15)

GrandHall uses the same explicit PresentationFadedLayers as HubIntroAfterDarkLordSequence: Popup, Hover, Prompt, Reward, DamagePopup and BossHUD. Dialogue and GameplayHUD are excluded. The sequence does not acquire a second DialoguePlayback UI-suppression owner; Dialogue controls its own canvas/HUD lifecycle. Both intro and audience share this path. Never use the three-argument letterbox PlayIn here: its default list includes Dialogue and makes the dialogue canvas transparent.

### Intro panel transitions (2026-09-15)

Both first-entry and three-clear sequences finish letterbox PlayOut/Dispose before Dialogue starts, matching Hub. No dialogue cue recreates black bars. Intro cue 0 hides the upper frame before the three-portal overview; it stays hidden through both portal-facing spoken lines and cue 1. Cue 2 returns to the scribe, then restores the frame for the remaining ordinary dialogue. In the three-clear audience, cue 0 hides the frame through the final portal-facing line; cleanup clears hiding after dialogue closes. The view-only API is `SetUpperPanelHiddenForCameraDialogue`; advancing spoken lines remains enabled, while the existing blocking Ink callbacks guard camera movement. Cleanup also releases input/camera protection. Gameplay controls remain protected while dialogue and camera direction are active.


### Portrait visibility during portal guidance

GrandHall invokes DialoguePlayback.SetPortraitsHiddenForCameraDialogue at the same section boundaries as its upper-frame hiding. Intro cue 0 hides the authored portrait root before portal movement; cue 2 restores its prior active state after returning to the scribe. Audience keeps portraits hidden through the final portal-facing line, then cleanup restores root state after closing. The UI backend forwards to PortraitController, which toggles its existing PortrailFrame root without clearing actor/face/position state or closing Dialogue. No black bars are recreated; Hub keeps its existing WithoutPortraits segments.


### Upper-frame exit animation

GrandHall cue 0 waits for SetUpperPanelHiddenForCameraDialogue completion before moving. DialogueView uses the existing PlayGroupClose/UISlideFadePresentation instead of SnapGroupClosed, so the authored upper-frame exit motion is shared with normal dialogue closing. The frame remains hidden throughout portal guidance; lower-frame and portrait section policies are unchanged.
