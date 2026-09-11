---
status: active
authority: structure-memory
last_reviewed: 2026-09-11
---

# Scene Music Requests

## Purpose And Ownership

Scene and encounter authors choose music explicitly. The existing Infrastructure `SoundManager` alone owns playback, same-track handling and fading. No additional manager, persistent root, temporary-music stack or automatic previous-track restoration is introduced.

| File under `Assets/_Project/` | Responsibility |
| --- | --- |
| `Runtime/Infrastructure/Audio/SceneBgmRequester.cs` | Request the authored entry track when its scene starts/becomes active. |
| `Runtime/Core/Audio/SoundPlaybackUtility.cs` | Forward requests to the registered backend; scene-bound play/stop requires a valid, loaded, currently active source scene. |
| `Runtime/Features/Dialogue/BossEncounterDirector.cs`, `BossTalkManager.cs` | Request `bossCombatBgm` when their existing encounter flow begins combat. |
| `Runtime/UI/GameOver/GameOverPresentationController.cs` | Request GameOver music using the dying player's scene, not the persistent UI's scene. |
| `Editor/Tools/Audio/SceneBgmAuthoringUtility.cs` | Validate authored scene requests and catalog keys without saving scenes; contains the explicit one-time migration entry point. |
| `Tests/PlayMode/Procedural/SceneBgmRequestPlayModeTests.cs` | Cover request boundaries, scene lifecycle, death-return handoff and configured catalog keys. |

## Authoring

- Each gameplay/title scene has one enabled `SceneBgmRequester` on its own root named `SceneBgm`. Set `Scene Music` to the intended audio catalog key. An empty key explicitly requests silence.
- Do not place it under a manager root that may migrate to DontDestroyOnLoad, or under generated room content that may be cleared.
- Set `Boss Combat Bgm` on the existing `BossEncounterDirector` or legacy `BossTalkManager`. An empty encounter key makes no request, leaving current music unchanged.
- GameOver continues using the existing UI's `gameOverBgm` setting. Boss-death presentations use the same scene-bound stop boundary.
- Use `Tools/Audio/Validate Scene BGM` after adding/changing scenes. It checks enabled build scenes plus the currently disabled procedural DemonKing scene. It permits explicit silent scenes and unset encounter music.
- `MigrateProjectScenes` is the completed one-time conversion, not the ongoing authoring workflow: it rereads legacy route settings and resaves assets. Do not rerun it to validate or after individually tuning scene music.

## Lifecycle

`Start` runs after sceneLoaded bootstrap/title cleanup and before ordinary gameplay Start callbacks. Additively loaded inactive scenes cannot replace active music; activation makes their requester submit its setting. The requester does not poll or reapply music every frame, so later encounter/GameOver requests remain in effect.

Disable/unload unsubscribes without stopping music. Thus an old scene's cleanup cannot silence the destination. Scene-bound late play/stop requests from invalid, unloaded or inactive scenes are rejected. Existing unscoped audio APIs remain for application-level compatibility; the migrated scene/encounter paths use the scoped APIs.

## Current Setup And Pitfalls

- ProtoTypeHub and Grand Hall use `bgm.hub`; Title uses `TitleSceneBGM`.
- Existing corridor/boss-entry assignments preserve `bgm.shadow_corridor` for all four routes. Current boss combat assignments preserve `bgm.boss.shadow`; distinct per-theme tracks were not invented.
- TutorialCorridor, DarkLord_Tutorial and SangHyup_Hallway have explicit silent entry settings. Set a key on their requester if music is desired.
- Legacy route/catalog BGM fields remain serialized for compatibility and migration input, but no longer control live playback. Changing them does not change scene music.
- Batch `SaveScene` can reserialize unrelated Tilemap/default component data. Review the complete asset diff; this migration retained only BGM additions and was validated by reopening all 18 scenes afterward.

## Documentation Follow-Up

This map supplements, not replaces, source-of-truth docs. The removed route BGM service rows in `RuntimeServiceOwnershipArchitecture.md` and `SceneDomainBootstrapArchitecture.md` need an approved architecture update. Legacy route music fields and the one-time migration can be retired in a separately approved serialization/tool cleanup. No Presentation HTML was changed.
