# Alarm bell interaction range

- QA: interacting near an event-room return portal activated the bell. No room-wide F-key handler was found. AlarmBellInteractable relied on tracker membership without rechecking physical proximity; stale candidates could therefore activate remotely. The exact QA scene/seed was not reproduced.
- CanInteract now requires an active bell, enabled own collider, same-scene player and player position inside that collider. Both candidate selection and OnPlayerInteract reuse this check. Encounter rules, portal priority, prefab radius (1.2) and scene data are unchanged.
- Added Bell_RejectsStaleSensorCandidateOutsideItsOwnTrigger: inside eligibility, stale tracker membership after moving away, remote request rejection, return to range and disabled trigger.
- Scoped diff check passed. Build verification was blocked by unrelated WeaponExclusiveRelics unresolved-type errors; the source exists but generated-project inclusion needs checking. Unity PlayMode tests were not executed.
- Doc impact: SessionLog only. No Architecture/Contracts changes.
