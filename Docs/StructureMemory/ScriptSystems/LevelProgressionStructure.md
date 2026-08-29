---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-08-18
---

# Level Progression Structure

## Purpose

Map the run-owned player level/EXP flow, common enemy death notification, EXP reward source, and authored pickup/UI extension boundaries.

## Last Verified

- 2026-08-11
- Unity 6000.4.2f1 batch compilation completed successfully.
- 2026-08-17: `Gameplay.csproj` and `UI.csproj` compiled with zero errors; the new presenter was force-included in the UI compile because Unity had not refreshed the generated project file.
- 2026-08-18: `Gameplay.csproj` compiled with zero errors after the distributed EXP pickup and boss-stage reward integration.

## Source Of Truth

- Runtime ownership follows `Docs/Architecture/RuntimeServiceOwnershipArchitecture.md`.
- Run state ownership follows `Docs/Architecture/RuntimeSaveArchitecture.md`.
- This document is a reconstruction aid, not an Architecture or Contract replacement.

## Current Structure

1. `Enemy.Die()` sets `isDead`, runs the inherited death hook, and emits `DeathStarted` once.
2. `ExperienceRewardSource` subscribes without subclassing `Enemy`, resolves the final rounded EXP value, and splits it across a limited number of authored `ExperiencePickup2D` instances only during an active run.
3. `ExperiencePickup2D` waits using scaled time, homes toward `PlayerRuntimeRegistry`, and grants EXP on player contact.
4. `BossEncounterEndDirector` grants boss EXP once per cleared encounter. It reads the current run stage index for the 120/150/180 normal-stage values and skips the final route set.
5. `RunLevelProgression` is the gameplay facade over `RunSessionStore.Data.levelProgression` and emits read-model events for future authored UI presenters.
6. `LevelProgressionCalculator` performs overflow, multi-level, pending-reward, and max-level calculations without Unity object dependencies.
7. `RunSessionLifecycleService` resets level state at run start, run end, and development reset.
8. `LevelRewardDefinitionSO` composes one card from stable IDs, display data, and one or more `LevelRewardEffectSO` assets.
9. `RunLevelRewards` stores only reward/effect state in `GamePlayData`, disposes live handles when the player leaves, and reapplies persistent effects when a new player is registered.
10. `OneSwordOathLevelRewardEffectSO` seals weapon slot index 1 without deleting its contents, applies an attack modifier, and scopes cooldown reduction to abilities granted by the weapon currently in slot index 0.
11. `RunLevelRewardOffers` rolls up to three eligible cards from a persisted run seed, stores the active IDs and reroll usage, and consumes only candidates from that stored offer.
12. `LevelRewardSessionController` handles manual R-key opening, dialogue/UI/combat eligibility, shared pause/input lock, and consecutive pending selections. Authored UI only projects its events and calls its public commands.
13. Eight reusable effect SO types cover kill healing, level-scaled max health, basic-attack proc damage, future-level full restore, low-time haste, kill cooldown reduction, random relic upgrade, and soul-heart grant.
14. `GlobalUIRoot/GameplayHUDCanvas/LeftUpperUIGroup/LevelHUD` contains the authored HUD visuals and `LevelHudPresenter`. The presenter projects level/EXP and reward-open eligibility through independent display paths.
15. `Data/Progression/Leveling/Rewards` contains nine authored reward definitions, their nine effect configurations, and one catalog. Display icons remain intentionally empty so the selection Presenter can apply one shared fallback icon.
16. `GlobalUIRoot/RewardCanvas/LevelRewardSelectionRoot` contains the inactive authored selection window with a dim input blocker, title, three centered cards, and close/reroll controls. The rough visual uses only Image, Button, TMP, layout, Presenter, and card-view components.
17. `LevelRewardSelectionPresenter` projects session events into one to three authored card slots, handles fixed card/reroll inputs, and opens through the session-owned UI-stack path. `LevelRewardCardView` binds one authored card visual without creating UI at runtime.

## Key Files

- `Assets/_Project/Runtime/Features/Enemies/Common/Enemy.cs`
- `Assets/_Project/Runtime/Core/Progression/LevelProgressionState.cs`
- `Assets/_Project/Runtime/Core/Save/GamePlayData.cs`
- `Assets/_Project/Runtime/Infrastructure/Save/RunSessionLifecycleService.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/LevelProgressionConfigSO.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/RunLevelProgression.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/ExperienceRewardSource.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/ExperiencePickup2D.cs`
- `Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossEncounterEndDirector.cs`
- `Assets/_Project/Prefabs/Loot/ExperiencePickup_Square.prefab`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/LevelRewardEffectSO.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/LevelRewardDefinitionSO.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/LevelRewardCatalogSO.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/RunLevelRewards.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/RunLevelRewardOffers.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/LevelRewardSessionController.cs`
- `Assets/_Project/Runtime/Features/Progression/Leveling/Rewards/Effects/OneSwordOathLevelRewardEffectSO.cs`
- `Assets/_Project/Runtime/Core/Combat/CombatActivityEvents.cs`
- `Assets/_Project/Runtime/Core/Combat/CombatOutgoingDamageModifiers.cs`
- `Assets/_Project/Runtime/Features/Items/Weapons/Inventory/WeaponInventory2D.cs`
- `Assets/_Project/Runtime/Core/Abilities/AbilityCooldownController.cs`
- `Assets/_Project/Runtime/UI/HUD/LevelHudPresenter.cs`
- `Assets/_Project/Runtime/UI/Progression/Leveling/LevelRewardSelectionPresenter.cs`
- `Assets/_Project/Runtime/UI/Progression/Leveling/LevelRewardCardView.cs`
- `Assets/_Project/Runtime/UI/Common/UIManager.cs`
- `Assets/_Project/Data/Progression/Leveling/LevelProgressionConfig.asset`
- `Assets/_Project/Data/Progression/Leveling/Rewards/Catalog/LevelRewardCatalog.asset`
- `Assets/_Project/Data/Progression/Leveling/Rewards/Definitions/`
- `Assets/_Project/Data/Progression/Leveling/Rewards/Effects/`
- `Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab`

## Ownership And Lifecycle

- `GamePlayData.levelProgression` owns level, current EXP, and pending reward count for the active run.
- The same state owns selected reward IDs, per-effect JSON payloads, and whether each instant effect already ran.
- It also owns the reward random seed, offer sequence, active candidate IDs, and reroll usage; closing UI does not reroll or discard the offer.
- UI must only query `RunLevelProgression.State`, subscribe to its events, and issue commands through the facade. UI must not mutate the DTO.
- The `LevelHUD` prefab hierarchy and presenter are presentation-only. `ExperienceFill` reads current EXP progress; `RewardReadyBorder` and `LevelUpPrompt` read session-open eligibility and never derive from fill amount.
- The presenter uses `ExperienceGranted` for a short unscaled-time fill animation and `StateChanged` for immediate lifecycle/restoration synchronization. Runtime progression never waits for the animation.
- `LevelProgressionConfigSO` owns the Lv.1-to-Lv.10 requirement table. The default values match the first prototype plan.
- `ExperienceRewardSource` owns per-enemy base EXP, runtime grant/multiplier overrides, per-pickup target amount, maximum pickup count, and scatter radius.
- `ExperiencePickupDropSpawner` preserves the exact total EXP while distributing it across a deterministic golden-angle scatter. A count cap changes the amount stored by each pickup rather than losing EXP.
- Boss EXP belongs to encounter completion rather than an individual boss death event so split/multi-boss encounters cannot pay more than once.
- Boss EXP uses the active run stage position, not boss identity. Normal stages 1/2/3 grant 120/150/180; `BossRewardContext.IsFinalRouteSet` grants none.
- The current 19 spawn-profile general-monster prefabs use rough prototype values of 5, 10, or 15 EXP. Boss/local summons and runtime slime splits explicitly disable their inherited EXP reward source.
- The EXP pickup is an authored gameplay prefab. No runtime UI hierarchy is created by this system.
- No new Manager, Singleton, or `DontDestroyOnLoad` object was introduced.
- Persistent live effects must return `ILevelRewardEffectHandle`; `RunLevelRewards` disposes those handles on player unregister, run start/end, and rebuild.
- Instant effects use `InstantOnce` and are not repeated during scene restoration.
- Weapon slot seals are live handle-owned policies. They do not clear or rewrite the serialized slot contents and are released during reward handle cleanup.
- Scoped cooldown multipliers affect newly started cooldown/recharge calculations for matching abilities; they do not retroactively rewrite an already-running cooldown.
- Actual HP loss emits `CombatActivityEvents.DamageApplied`; only damage involving the registered player updates the level-reward combat grace window.
- `Enemy.AnyDeathStarted` is emitted once from the same guarded common death entry point as the instance event. Kill-count effects subscribe to the global signal and persist only their narrow counters.
- The selection Presenter owns projection and input forwarding only. The session controller remains the owner of candidates, reroll usage, pending selections, pause, and input blocking.
- The Presenter owns only offer-identity presentation memory and transition timing. The session pause/input lock remains held until the full close fade completes and the Presenter allows `UIManager` to remove the stack entry.
- A revealed offer is identified by run seed, offer sequence, and reroll usage. Reopening that same offer keeps the front faces visible; rerolls and consecutive pending offers receive new reveal identities.
- `UIManager` records the external blocker owner of an owned stack UI. Fixed `Escape` can close that top UI only when no unrelated external blocker is active.
- `RewardCanvas` hosts the always-active session controller and selection Presenter; the inactive panel root remains a child presentation object. The HUD presenter references the same session controller.
- The current prototype offer owns five rerolls. Opening with R suppresses selection-window input for the opening frame so that the same R press cannot immediately consume one reroll.

## Extension Entry Points

- Add an authored `ExperienceRewardSource` to EXP-paying enemy prefabs and assign an authored `ExperiencePickup2D` prefab.
- Summon, revive, and infinite-spawn flows can call `SetGrantExperience(false)` on their spawned instance.
- Run-wide EXP modifiers can call `SetExperienceMultiplier(...)` before death.
- `LevelHudPresenter` binds to `RunLevelProgression.StateChanged` and `ExperienceGranted`; extend it instead of adding a second owner for the same authored HUD.
- `LevelRewardSessionController.CanOpenSession` is the side-effect-free HUD query. Keep `TryOpenSession(...)` as the input command and do not duplicate its combat/UI rules in presentation code.
- Replace or restyle the rough visuals under each `CardVisualMount/CardFront` and `CardBack` without moving session/gameplay ownership into the card objects. The Presenter deactivates unused slots so the horizontal layout recenters one or two candidates.
- Assign reward icons on individual definitions or one shared `fallbackIcon` on `LevelRewardSelectionPresenter`; otherwise the card icon Image is intentionally hidden at runtime.
- UI reads `Candidates`, `RerollsUsed`, and `PendingRewardCount`, then calls `TrySelectCandidate(...)` or `TryReroll(...)`. Normal close requests must finish the authored OFF presentation before calling `CloseSession()`; UI must not call `RunLevelRewards.TrySelect(...)` directly.
- The selection-window close shortcut is fixed to `Escape`. It is not exposed through key mapping and does not resolve a rebinding-dependent glyph.
- If the reward window implements `IStackableUI`, open it through `TryPushSessionUI(...)` so it can coexist with the session-owned external input block.
- New effects should derive from `LevelRewardEffectSO`, keep scene references out of JSON state, and return a cleanup handle for live subscriptions/modifiers.
- Kill counters and similar state can serialize a narrow payload into `LevelRewardEffectState.json`.
- UI can query `WeaponInventory2D.IsSlotAccessible(...)` and subscribe to `OnSlotAccessChanged` to render a sealed slot without owning the policy.

## Known Pitfalls

- `KillConfirmed` is attacker-side and does not cover environmental/direct deaths; EXP must use `Enemy.DeathStarted`.
- A pickup without `LevelProgressionConfigSO` logs a warning and destroys itself on contact.
- General-monster EXP values are rough prototype tuning and still need playtest balancing.
- Pickup count is capped at 30. Large rewards preserve their total by increasing the amount carried by each Square, so visual count is no longer one-to-one above the cap.
- New summon, revive, split, or infinite-spawn paths must explicitly call `SetGrantExperience(false)` when repeated farming is not intended.
- Reward catalogs must be registered before selected IDs can be reapplied after a domain reload or fresh app start.
- The active `LevelRewardSessionController` now references `LevelRewardCatalog.asset`; reward availability still depends on an active run, registered player, pending choice, and the existing combat/UI eligibility rules.
- Several effect display names were absent from the source plan and currently use provisional names. Their stable `rewardId`/`effectId` values are persistence keys and must not be renamed casually even if visible names change.
- The selection UI is a rough first-pass layout. Final sprites, card art, borders, responsive polish, and a shared fallback icon remain unassigned.
- `RewardCanvasRaycastGate` must continue recognizing the active level-reward Presenter; otherwise keyboard input works but authored card/control Buttons cannot receive pointer input.
- The green border is never an EXP-full indicator. It represents current level-up selection eligibility, including a valid candidate, dialogue/UI/pause state, the three-second damage grace window, and enemy recognition.
- The level-up sound reference is intentionally unassigned until a project sound key is authored.
- Combat eligibility should be playtested against every custom enemy subclass. `Mob` uses detection state and `BossControllerBase` uses combat-active state; a future non-Mob `Enemy` that recognizes the player must override `IsRecognizingPlayer`.
- `Apply(...)` implementations must not leave partial mutations when they throw; validate authoring and eligibility before selection.
- Do not release the level-reward session pause/input lock at the start of a visual close. Normal gameplay resumes only after the blocker and presentation roots are fully transparent and inactive.
- The current PlayMode test asmdef does not reference `Core`; do not change the asmdef without explicit approval.

## Recent Structural Changes

- 2026-08-11: Added the common death event and the initial run-owned level/EXP pipeline.
- 2026-08-11: Added stable reward/effect definitions, run-owned selection state, instant-once tracking, and persistent handle rebuild/cleanup.
- 2026-08-11: Added the first concrete reward, `한 자루의 맹세`, plus reusable slot-seal and ability-scoped cooldown policies.
- 2026-08-11: Added eight planned effect implementations, persisted deterministic offers/rerolls, common combat activity signals, enemy recognition checks, and the R-key selection session controller.
- 2026-08-16: Added the authored `LevelHUD` shell under the shared upper-left HUD group without coupling it to the existing health, consumable, or status presenters.
- 2026-08-17: Added `LevelHudPresenter`, authored Lv.1-Lv.10 requirement data, smooth bottom-up EXP projection, Lv.10 full-fill behavior, and a side-effect-free reward-open eligibility query.
- 2026-08-17: Authored and organized the nine planned reward definitions/effect configurations plus their catalog; icons remain Presenter-owned fallback presentation.
- 2026-08-17: Added the inactive three-slot level-reward selection shell under `RewardCanvas`; one-to-three-card behavior remains Presenter-owned and no runtime UI hierarchy is created.
- 2026-08-17: Added the selection Presenter/card projection scripts and owner-aware fixed-ESC stack closing; prefab component/reference wiring remains authored UI work.
- 2026-08-18: Wired the session, catalog, HUD, Presenter, cards, and controls into `GlobalUIRoot`, set the prototype reroll budget to five, added a rough 1920x1080 layout, and enabled RewardCanvas pointer input for the new modal.
- 2026-08-18: Added the green Square EXP pickup prefab, exact-total distributed drops for 18 general-monster prefabs, explicit no-EXP handling for boss summons/runtime slime splits, and stage-position boss EXP with final-route exclusion.
- 2026-08-18: Connected the previously omitted Dragon-stage `TreasureMonster` prefab at 10 EXP, bringing all 19 current spawn-profile general monsters onto the common EXP reward path.
- 2026-08-18: Added staggered overshoot card entry, first-view front/back flips, card-only reroll/consecutive-offer replacement, and deferred full-window close fading to the authored selection UI.

## Recovery Notes

- If EXP is not spawned, check active-run state, `grantExperience`, positive `baseExperience`, and pickup prefab assignment.
- If a pickup reaches the player but EXP does not change, check its progression config and player registration.
- If level state survives a new run, inspect all three reset calls in `RunSessionLifecycleService`.
- If a selected effect does not return after a scene transition, confirm its catalog was registered and its definition/effect IDs still match the stored IDs.
- If an instant reward repeats, confirm its effect uses `InstantOnce` and that `instantApplied` is preserved in `GamePlayData`.
- If R does not open the selection, check active-run/pending state, input backend registration, blocking dialogue/UI/pause state, the three-second player damage grace window, and active enemy recognition.
- If reopening changes candidates, inspect `activeRewardOffer` serialization and ensure UI closes via `CloseSession()` instead of mutating progression state.

## Promotion Candidate

- Promote the external `Enemy.DeathStarted` notification ordering to a Combat contract if more death rewards begin depending on it.
- Promote the independent EXP-progress and reward-readiness projection boundary if additional HUDs begin consuming the same read model.
