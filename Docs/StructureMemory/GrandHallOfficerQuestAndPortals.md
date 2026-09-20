# Grand Hall officer objective and cleared portals

Current structure map; not a technical contract.

## State and ownership

GamePlayData owns officerQuestStarted and officerQuestCompletionPresented for the current run. RunOfficerQuestProgress counts distinct slime/dragon/shadow IDs from defeatedBossIds; it does not increment a second counter or use permanent boss clears. RunProgressCoordinator marks the objective started when an active run enters Grand Hall, including scene-load and run-start ordering. RunSessionLifecycleService clears both flags on start, end and development reset.

A third officer kill makes the objective complete but visible. Completion presentation can only be acknowledged in Grand Hall. The presentation flag prevents replay when returning again or recreating the HUD; interruption before completion leaves it pending. The runtime run DTO preserves this across existing serialization flows, without adding a permanent profile quest or new manager.

## HUD

QuestHudView projects a new authored OfficerObjective TMP row above the existing changing mainDescription in GlobalUIRoot. Content is white with existing BlackOutline; heading remains orange. The scene-dependent guidance logic remains in ResolveMainQuestText. Subquest display remains below the main objectives.

The officer row shows 마왕성 간부를 찾아 토벌하자 (n/3), adding · 완료 at three. In Grand Hall, after scene transition and main entrance finish, a 0.35-second hold is followed by a 12-pixel/0.12-second upward bounce and existing leftward exit duration. Completion acknowledgement occurs only after the exit. Remaining main guidance and subquest heading/rows then move up with the existing reflow duration. UI disable kills motion and restores authored positions; leaving Grand Hall during the removal cancels it without acknowledging completion.

## Portal visuals

GrandHallClearedPortalView is authored on ScenePortal.prefab with references to ScenePortal, SpriteRenderer, Portal_Disable.png and PortalParticle root. It applies only to an active run's Grand Hall portal when RunRoutePlayback.GetTravelBlockWarning returns BossAlreadyDefeatedThisRun, the same decision used by ScenePortal interaction. It does not infer the destination from NormalRouteSets: current officer catalogs use a single FinalRouteSet. The enabled RequiredBossClearScenePortalAccessRule also disables the visual while its current-run officer requirements fail; other travel blockers do not mark a portal cleared. Defeated portals swap sprite, stop/clear child particles and deactivate the particle root; returning to uncleared state restores the originally authored sprite and particle activation. Final-boss and other scenes retain their original appearance. Existing travel rejection remains owned by route/travel services.

## Player portal guidance (Hub and Grand Hall)

- [PlayerPortalGuidanceView](../../Assets/_Project/Runtime/Presentation/Loot/PlayerPortalGuidanceView.cs) drives a scene-owned marker around the registered player. The view only reads equipment, introduction and route state; it does not grant weapons, start travel or save progress. Player registration/unregistration is paired with enable/disable, and disable hides both renderers and releases cached player references.
- [PlayerPortalGuidance.prefab](../../Assets/_Project/Prefabs/Map/Navigation/PlayerPortalGuidance.prefab) authors the existing `Portal.png` and `KillLockNavigatorArrow.png` as two initially hidden renderers. It matches chest guidance: radius 1.2, anchor Y 0.3, arrow offset 0.35, portal width 0.42 and arrow width 0.32. Only the arrow rotates. The marker follows in world space while remaining owned by its scene, so scene unloading removes it without persistent-player children or runtime visual creation.
- `ProtoTypeHub.unity` contains one instance targeting the authored `Portal_To_GrandHall` transform. Its equipped-weapon gate matches `HubWeaponDepartureGuide`: opening a grave or spawning its drop is insufficient; `WeaponInventory2D.HasEquippedWeapon` must be true. An already-equipped arrival also qualifies, and losing the equipped weapon hides guidance. This portal uses `SceneTravelInteractable`, not `ScenePortal`.
- `Grand Hall.unity` contains three instances targeting the same dragon, shadow and slime portal transforms as the scribe introduction. The final-boss portal is excluded. `GrandHallScribeSequence.IsPortalGuidanceReady` requires the current slot's completed introduction and a finished entry flow, including camera restoration and return greeting. A saved completion flag alone cannot expose the markers during the cinematic. Cleared officer routes hide their marker using the existing `BossAlreadyDefeatedThisRun` warning.
- Both gates suppress markers during non-idle player interaction, dialogue, death, pause, scene transition, inactive target or inactive owning scene. No new progression state or travel rule is introduced. This local presentation map is not an Architecture/Contracts promotion candidate.
- [PortalGuidanceRegression](../../Tools/Validation/PortalGuidanceRegression.cs) exercises the equipment and introduction gates, direction/rotation, suppression, cleanup, rebind and slot replacement in an isolated Unity Editor fixture. Actual scene rendering and first-entry timing still require a main-project playtest.

## Damage popup font

DamagePopupUI_World.prefab now binds Galmuri9 SDF BlackOutline and its embedded material. Existing damage/critical/element/player/evade text colors and motion continue to come from the existing popup profile and service.

## Verification and extension points

Regression fixtures in CombatFeelLootQuestPlayModeTests cover unique officer counts, completion deferred until Grand Hall, serialized acknowledgement/new-run reset, and portal sprite/particle restoration. Unity visual verification remains required for actual loading timing, particle sorting, fonts and HUD spacing. Adding more officer types requires updating the objective IDs and count requirement together. No Architecture/Contracts or Presentation HTML update is currently identified.


## Final corridor gate correction (2026-09-13)

RequiredBossClearScenePortalAccessRule.AreRequirementsMet requires an active run and all authored IDs in RunSessionStore.Data.defeatedBossIds. Permanent profile clears remain available for other progression systems but cannot unlock this portal. GrandHallClearedPortalView uses the same property without invoking denial events, and restores sprite/particles when requirements pass. Existing route defeat blocking still disables already-cleared destinations. Corridor_demon_king_Boss enables A-to-B travel with its original BossNotDefeatedThisRun gate; reverse direction stays disabled. Production and fixture build passed; Unity travel/rendering remains unverified.

### Guidance proximity (2026-09-21)
- PlayerPortalGuidanceView scales its player anchor, icon radius and arrow offset linearly inside 4 world units. Both renderers hide while the portal is in the player's interaction tracker overlap set and return on exit if route/equipment gates still pass. IsInInteractionRange reads existing sensor membership, independently of which nearby object is selected.
