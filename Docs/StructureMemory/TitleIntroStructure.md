---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-25
---

# Title Intro Structure

## Purpose

Map the title new-slot intro flow so future title/profile launch work can start without rediscovering the ownership, data, and authoring boundary.

This is a fast structure map, not a final Architecture or Contract document.

## Current Structure

- `TitleMenuController` remains the profile launch coordinator for the title screen.
- Empty-slot `StartNewRun` launches can route through `TitleIntroPlayer` before `TitleProfileLaunchService.PrepareLaunch(...)`.
- Existing-slot `ContinueRun` launches bypass the intro and keep the direct prepare/load path.
- `TitleIntroSequenceSO` owns reusable intro data: ordered slides, image sprite, Korean script text, typing speed, intro entry fade duration, post-text waits, normal image fade duration, first-slide image fade-in duration, Space-hold skip duration, and skip hold fill color.
- `TitleIntroPlayer` owns only runtime intro playback state: coroutine lifetime, intro overlay fade-in, typewriter reveal, input polling, per-frame advance input consumption, slide fade timing, hold-to-skip progress, and completion callback.
- `TitleIntroView` owns only serialized UI projection: root `CanvasGroup` alpha, fullscreen slide image, TMP script text, skip prompt root, optional skip key glyph/label projection, optional skip fill color projection, and shared hold-fill button view with hold-fill image fallback.
- Final intro completion and skip call back into `TitleMenuController`, which uses the existing `SceneTransitionCoordinator.TryLoadScene(...)` path through `LoadScene(...)`. The shared transition service owns the black fade-out over the intro screen, the scene load, and the next-scene fade-in.
- `TitleScene` should own a scene-root authored `SceneFadeTransitionService` so title-origin transition durations are Inspector-tunable instead of depending on runtime fallback defaults.
- `TitleIntroAuthoringTool` is editor-only support for importing `Intro.zip`, creating the default sequence asset, and wiring an inactive `IntroOverlay` in the active `TitleScene`.

## Key Files

- `Assets/LeeJunMo/Script/SceneManagement/TitleMenuController.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleIntroSequenceSO.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleIntroPlayer.cs`
- `Assets/LeeJunMo/Script/SceneManagement/TitleIntroView.cs`
- `Assets/LeeJunMo/Script/Editor/TitleIntroAuthoringTool.cs`
- `Assets/LeeJunMo/Datas/Intro/IntroSequence_Default.asset` (created by the editor menu after importing or locating images)
- `Assets/LeeJunMo/Datas/Intro/Images/` (created by the editor menu when `Intro.zip` is imported)

## Runtime Flow

- The player opens the profile slot panel from the title menu.
- `TitleProfileSlotService.TryCreateLaunchRequest(...)` decides whether the selected slot is `StartNewRun` or `ContinueRun`.
- If the request is `StartNewRun` and `playIntroForNewProfile` is enabled, `TitleMenuController` disables title interaction and starts `TitleIntroPlayer`.
- The intro launch path leaves the profile slot panel active behind the intro overlay instead of starting `TitleProfileSlotPanelUI.CloseUI()`, so the intro overlay root fade is the only start-button-to-intro fade owner.
- `TitleIntroPlayer` shows the serialized view, fades the intro overlay root in with `introStartFadeDuration`, starts the current slide image fade-in and slide text typing in the same phase, waits according to one-line or multi-line text, then fades the old image out before the next slide while keeping the completed text visible.
- The first slide image uses `initialImageFadeDuration` for its image fade-in. Later slide image fade-ins and all slide image fade-outs use `imageFadeDuration`.
- Slide text is not cleared by image fade-out cleanup. The next slide replaces the text only when its own typing phase starts.
- Click or short Space release consumes one advance step at most. During the intro overlay root fade-in and first slide image fade-in, advance input is consumed but ignored so the authored slow-start timing cannot be skipped. After the first slide image fade-in completes, typing-phase advance completes the current text and still enters the normal post-text wait. A later distinct advance during post-text wait or image fade cancels only that current wait/fade phase.
- Holding Space fills the serialized `HoldFillButtonView` or fallback progress image from left to right and skips the whole intro at the configured threshold.
- When playback completes or skips, the player invokes the completion callback.
- The callback prepares the profile launch with `TitleProfileLaunchService.PrepareLaunch(...)` and loads `launchResult.TargetSceneName`.
- The title intro launch path asks `TitleIntroPlayer` to keep the intro view visible after playback completion so the title menu does not flash before `SceneFadeTransitionService` fades to black.
- If the title scene starts the post-intro transition with a runtime fallback or title-authored fade overlay, `SceneFadeTransitionService` keeps that active overlay through next-scene fade-in and defers promotion of the loaded scene's authored fade service until the transition session ends. The pending authored overlay is hidden while deferred so it cannot cover the active fade-in.

## Authoring And Editor Support

- Runtime code does not create `Canvas`, `EventSystem`, `Button`, `TMP_Text`, `Image`, or intro hierarchy objects for this feature.
- The intro overlay should be authored under the `TitleScene` canvas and left inactive outside playback.
- Skip prompt icon/text and hold fill color visuals are authored by default. `TitleIntroView.autoApplySkipKeyGlyphOnShow` and `TitleIntroView.autoApplySkipFillColorOnShow` can be enabled only when the scene should let runtime replace those authored prompt values on show.
- The editor menu `Tools/Title Intro/Import Intro Zip And Create Default Sequence` extracts supported images into `Assets/LeeJunMo/Datas/Intro/Images/`, imports them as single sprites, and creates or refreshes `IntroSequence_Default.asset`.
- The editor menu `Tools/Title Intro/Create Default Sequence From Images Folder` can rebuild the default sequence from existing images in the intro images folder.
- The editor menu `Tools/Title Intro/Wire Active TitleScene Intro Overlay` creates or repairs the inactive `IntroOverlay` in the currently open scene, adds `TitleIntroView`, adds `TitleIntroPlayer` to the `TitleMenuController` GameObject, and assigns serialized references.
- The editor menu `Tools/Title Intro/Wire Active TitleScene Fade Service` creates or repairs a scene-root `TitleSceneFadeTransitionService` with a full-screen black overlay and wires its `SceneFadeTransitionService` references. Review `fadeOutDuration` and `fadeInDuration` in the Inspector after running it.
- The zip/image order is currently filename-sorted after import, so image names should preserve the intended Notion document order.

## Extension Entry Points

- Change intro text, waits, intro entry fade duration, first-slide image fade-in duration, normal image fade duration, typing speed, and reusable skip hold fill color on `TitleIntroSequenceSO`; change prompt art and fill shape/layering in the authored overlay graphics. Leave the auto fill color option disabled when the scene should preserve a custom authored fill color.
- Add or remove slides by editing the sequence asset.
- Change layout, fonts, hold-button layers, or prompt visuals in the authored `TitleScene` overlay, not in runtime code. Leave `autoApplySkipKeyGlyphOnShow` disabled when using fully custom prompt art/text.
- Disable the intro slice by turning off `TitleMenuController.playIntroForNewProfile`.
- Change the post-intro target by editing the existing profile slot service target scene field, not by adding an intro-only override.
- Keep post-intro fade timing on the authored `TitleSceneFadeTransitionService` `SceneFadeTransitionService.fadeOutDuration` and `fadeInDuration`; do not add intro-specific scene transition durations unless a future task changes the shared transition contract.

## Known Pitfalls

- Do not hard-code a tutorial scene in the intro player. This flow intentionally uses the profile launch target.
- Do not direct-edit `TitleScene` YAML for intro references unless the team explicitly accepts that serialized-reference risk.
- Do not add a new manager, singleton, or `DontDestroyOnLoad` object for the intro; title scene ownership is sufficient.
- Do not call `ApplySkipKeyGlyph(...)` or runtime fill-color projection from the normal show path unless the serialized opt-in is enabled; otherwise authored prompt art/text/color can be overwritten.
- Do not tie text lifetime to image fade-out. Text stays visible during old-image fade-out and changes only when the next slide begins typing.
- Do not add a blank pre-intro wait. The current slow-start policy is a serialized intro overlay root fade-in followed by the separately serialized first-slide image fade-in.
- Do not let click or short Space advance short-circuit the intro overlay root fade-in or first slide image fade-in. Hold-to-skip remains the only early exit during that initial timing window.
- The intro entry fade assumes the authored `IntroOverlay` root has a `CanvasGroup` and covers the title UI with an opaque or intentionally designed background.
- Do not close the profile slot panel with its own fade when the intro starts; that creates a second visual fade under the intro overlay.
- Do not let one physical advance input cross coroutine phase boundaries. Typing completion, post-text wait cancellation, and fade cancellation are separate steps.
- Do not hide the intro view before starting the scene transition on empty-slot intro completion. The existing black transition overlay should cover the intro screen first to avoid a title menu/slot panel flash.
- Do not place the title fade service under the title canvas if it must own a scene load transition. It should be a scene-root object so it can survive long enough for `FadeInAsync()` to complete.
- Do not let the loaded gameplay scene's authored fade service replace an active runtime fallback or title-authored fade service before fade-in. That destroys or hides the overlay the coordinator still needs for next-scene reveal. Also do not leave the deferred authored overlay visible, because its prefab-authored `FadeImage` can be active and alpha 1 before the service initializes.
- If `Intro.zip` is unavailable locally, the default sequence asset and image references cannot be completed until the zip is selected or images are placed in the intro images folder.
- New scripts may not appear in generated `.csproj` files until Unity imports them; source-only verification does not equal Unity compile success.

## Verification Notes

- Manual Unity play validation is still required for overlay layout, image aspect/framing, text placement, Space hold fill, skip behavior, empty-slot intro routing, and continue-slot bypass.
- Unity batchmode must not be run while Unity Editor processes are open.

## Promotion Candidate

Candidate for future `Docs/Architecture/` or `Docs/Contracts/` promotion if title/profile launch flows gain more scene-local presentation steps.
