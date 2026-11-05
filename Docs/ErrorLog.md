---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-09-14
---

# Error Log

## 2026-10-10 — native language name and title alignment escaped validation

SettingsPanelUI.ChangeLanguage refreshes the stepper immediately after SetLanguage. Reading pending CurrentLanguage for the native name placed 日本語 into the still-active Korean font and produced U+8A9E replacement squares. Display the applied SelectedLocale's native name while arrows use the pending choice. Prior render checks covered fixed StringEvents/fonts, not this dynamic value, and called SetLanguage directly instead of the actual button listener. Add dynamic caption/replacement-glyph checks and invoke real button listeners. The title width layout also reset authored y=-40 to full-sprite center; retain that offset as top80/bottom0 padding in the fixed-height panel. Width/line-count checks alone do not prove visual centering.

## 2026-10-10 — localization preloads must survive locale cache release

LocalizedDatabase.OnLocaleChanged calls ReleaseAllTables. Loading next-locale tables/fonts before setting SelectedLocale alone does not preserve their cache ownership across selection. Keep explicit acquired table/font handles through the commit, release them in finally, and project visible bindings at SelectedLocaleChanged after initialization. Render-time tests must wait for initial saved-language initialization before auditing an actual user change; do not count that startup state as a successful transition.

Batch-mode regression checks also exposed a timing trap: `ItemKeywordTooltipPlayModeTests.WaitForLocalization` uses a fixed 180-frame budget, which can expire before asynchronous initialization completes even while source-preserving native renders pass in all five locales. A failed locale teardown can leave its restore flag set and cause later teardown failures. Preserve the initial failure and any unchanged successful retry; do not suppress the failure or treat a retry as proof that startup is stable. A future fixture repair should use an appropriate elapsed-time initialization budget and explicit teardown state cleanup. See the [tooltip layout session](SessionLogs/2026-10-10.md).

## 2026-10-08 — copied static localization overwrites key-row action names

KeyMapRow's actionLabel retained a Screen Mode LocalizeStringEvent from its source row. KeyBindingRowUI.Refresh already owns the action's localized caption; asynchronous fixed-string completion can overwrite it afterward. Remove the action-label fixed event, retain font localization, and attach fixed events only to authored captions such as Reset. Review the spawned row after localization settles, not only GetActionLabel's return value.

## 2026-09-14 - Boss HP Tests Validated Orphan Profiles Instead Of Runtime Wiring

Symptom: the balance fixture reported Slime Queen HP as 500/300/300 and the Shadow Witch as 1000, but the playable Slime Queen bodies each initialized at 100 while the Shadow boss scene replaced its prefab profile with the Demon King's 2800 profile.

Cause: the test loaded standalone `AttributeInitProfileSO` assets and asserted their numbers without proving that the production prefabs or scene instances referenced those profiles. All three Slime Queen prefabs referenced only the shared groggy override, and the Shadow scene authored a prefab-instance override to the shared Demon King health profile.

Fix: bind the dedicated 550/330/330 health profiles to the three Slime Queen prefabs, remove the Shadow scene's Demon King profile override so its dedicated 1000 profile wins, and extend regression coverage to inspect production prefab references plus the Shadow scene override GUID.

Prevention: a content-value test is insufficient for serialized gameplay data. Whenever a profile or ScriptableObject is expected to drive a production prefab or scene, test both the value and the final serialized reference path; scene prefab-instance overrides must also be checked because they can supersede a correct prefab.

## 2026-09-12 - Pending Action Cues Competed With Death And Disabling Clipping Changed Warning Style

GoblinWarrior had a no-exit-time Any State death path, but action transitions preceded it and the animator bridge left unconsumed action triggers queued. Tests reproduced death not being selected immediately with pending cues. The bridge now clears its action triggers, rejects later action cues and keeps death idempotent. Goblin's death request uses its authored cue after attack cleanup; its controller and the generator prioritize death. The reported intermittent five-second corpse was not reproduced end-to-end, so this is verified transition hardening, not proof that every delayed removal had this cause.

The straight/rectangle/circle wall-clipping disable policy also removed the condition that selected the thin mesh renderer, falling back to sprite borders. Rendering mode and clipping are now independent: Rectangle/Circle keep thin mesh presentation with no wall mask; Line keeps its full authored endpoints; Sector/Ring retain their existing clipping. Tests compare mesh geometry before/after adding a wall and across explicit/inherited geometry updates, including fill progression and color.

## 2026-09-12 - Optional Chest Authoring Left A Mandatory Present Room Quota

Symptom: changing candidate-only Combat rooms from reward tag `Present` to `None` could prevent dungeon generation even though optional chest placement should not require a fixed number of reward rooms.

Cause: the shared production policy still required exactly two Normal + Present rooms and one Large + Present room. Earlier authoring advice explained candidate tags without updating those legacy hard quotas. The generator correctly rejected an unsatisfiable policy; `None` itself was not an invalid tag.

Fix: remove the Normal + Present quota and require one Large room with reward filter `Auto` (unrestricted). Keep the Large cap at one. Align installer defaults so newly installed policies do not reintroduce the old coupling. Preserve candidate selection limits, direct chests and all other hard constraints. Regression coverage uses cloned production libraries with every Combat reward tag set to None, leaving authored assets unchanged.

## 2026-09-11 - Guaranteed Event Required Both Cycle And Dead End

Symptom: runtime Shadow generation exhausted 512 topology attempts when ParcelPickup was selected, despite the static-profile seed sweep passing.

Cause: `Shadow_Event_ParcelPickup` authored `CycleDetour`, minimum start distance 2 and `requireDeadEnd=true`. A cycle detour always has at least two edges; a dead end has one. This fails guaranteed-role assignment before template backtracking. Earlier seed coverage omitted runtime-injected event guarantees.

Fix: change only this asset's mode to `FarthestFromStart`, preserving distance 2 and the dead-end requirement. Share an explicit contradiction check between the Room Piece validation/bake gate and guaranteed-template preflight. Do not automatically relax either hard condition. Cover runtime start-event plans and authored follow-up guarantees, not only the base generation profile.

Related cleanup trap: player unregistration clears weapon HUD slots, whose input getter previously called `EnsureInstance`. If the input service was already destroyed, cleanup could recreate it. Both weapon HUDs now read the bootstrapped `InputBindingService.Instance` without creation; a targeted test destroys the cached service, clears slots and checks that no replacement is spawned, while later normal bootstrap remains discoverable. The service/bootstrap lifecycle itself is unchanged.

## 2026-09-11 - Greedy Room Selection Committed Before Checking Scarce Neighbors

Symptom: a flexible node selected A even though an adjacent node could use only A, causing consecutive identical rooms despite an alternative B for the first node. Local repeat buckets only saw already assigned neighbors and never reconsidered the first choice. Required Combat reservation also pinned a randomly chosen concrete template too early.

Fix: bounded template search with precomputed domains, minimum-remaining-values ordering, forward checking and rollback; Combat quotas can redistribute across Combat nodes rather than pinning a concrete identity or tag to a provisional slot. Physical success and hard quotas are validated before accepting a candidate. The old quota filter's single-candidate/all-matching escape paths were removed, so repetition fallback cannot silently overfill a hard quota. Regression tests include scarce-neighbor selection, a square-domain contradiction requiring real rollback, provisional quota reassignment and single-candidate hard-count rejection. See [Dungeon Template Selection](StructureMemory/DungeonTemplateSelection.md).

## 2026-09-11 - Monster AI Overwrote Profile HP And Hid Initial Clamp

Symptom: common monsters and slimes did not reliably use their authored HP. Removing their legacy writes exposed current HP stuck at 100 even when MaxHealth correctly became 220-1100.

Cause: seven common AI classes and the Slime base wrote independent numeric HP during Awake/split setup. MonsterDifficultyReceiver could cache a different baseline depending on Awake order. Separately, Health-before-MaxHealth profile application clamps Health against the earlier maximum, and the affected prefabs had initialization fill disabled.

Fix: remove numeric HP initialization from those AI classes and retain only slime appearance setup. Enable the existing Health-to-MaxHealth initialization fill on the 15 common/slime/Shadow prefabs covered by the balance pass. Capture current HP before reducing MaxHealth in MonsterDifficultyReceiver; reading it afterward used the already-clamped value and incorrectly turned a full-health 900 HP tank into 225/450 under x0.5 scaling. Stage/role/elite coefficients and global AttributeSet behavior are unchanged.

Regression coverage: the real-prefab Play Mode fixture checks authored/full HP, repeated difficulty application and no healing during split setup. Other deliberately partial-health prefabs and global initialization semantics remain outside this slice.

## 2026-09-11 - Alarm Bell Coupled To Unclaimed Level Rewards

Symptom: An unused alarm bell's interaction prompt disappears when the run has unclaimed level rewards. The same check can stall wave advancement and keep encounter doors locked after all monsters die.

Cause: `AlarmBellInteractable.HasPendingLevelReward` treated pending rewards and a persisted active offer as an executing reward UI flow. Those are saved entitlements/candidate state, not evidence that the UI is open. The bell reused that predicate for both activation and coroutine waits, although rewards are selected on demand with R.

Fix: Remove the predicate and all reward-flow waits from the bell. Keep experience granting after encounter completion, without consuming rewards. Existing player interaction blocking and common pause/input-lock systems remain responsible for an actually opened UI.

Prevention: Do not use pending data or a resumable saved offer as a proxy for active UI execution. Encounter progression should depend on its own combat state, not unrelated unclaimed rewards. Do not duplicate common UI flow ownership in a world interactable.

Verification: Runtime and regression-test compilation passed. Added interaction-gate and cleared-wave/completion tests; Unity Play Mode execution remains unverified.

## 2026-09-05 - Upgrade Opening Coroutine Ran On Its Inactive Panel

Symptom: From Hub, selecting the NPC upgrade feature did not open the window. Editor.log recorded `Coroutine couldn't be started because the the game object 'UpgradeTreePanel' is inactive!` at `UpgradeUiOpenFlow.Open`.

Cause: UI backend extraction moved the opening coroutine onto `UpgradeTreeUI`, whose GameObject stays inactive until `OpenUI` is called inside that same coroutine.

Fix: `UpgradeUiOpenFlow` runs opening on the existing active `UIManager`, while the panel still owns the flow and its individual input blocker. Panel cleanup stops the routine on the actual host and releases the overlay/input lock. Without an active UI host, use the existing immediate-open path.

Prevention: A hidden UI cannot host the coroutine that first activates it. Keep coroutine execution lifetime separate from panel visibility, and do not share the host's input blocker across independently owned presentations.

Verification: UI Roslyn compilation passed; Hub Play Mode interaction remains unverified.

This file records recurring implementation errors, their causes, and prevention rules.

## Template

```md
## YYYY-MM-DD - Short error name

Context:

Cause:

Fix:

Prevention:
```

## Active Entries

## 2026-08-19 - Direct Hub Presentation Skip Was Removed One-Sidedly

Context:
Playing `ProtoTypeHub` directly was intended to provide an immediately usable run-start portal. The route bootstrap and correctly authored `HubToRunStart` portal remained present, but direct Play still ran the Hub fall/wake and Hub intro sequences, both of which can own player input and cinematic protection.

Cause:
The original `ConsumeHubSpawnPresentationSkip()` handoff was removed when Hub spawn presentation was restored for normal Hub arrivals. That change removed the development exception together with the normal-flow behavior, and the later Hub intro path had its own Editor bypass. Route readiness and presentation readiness therefore drifted into separate policies.

Fix:
Add Core `EditorDirectSceneStartContext`, mark only the initial direct-Play Hub scene from `SceneDomainCoordinator`, and make both `PlayerHubSpawnPresentation2D` and `HubIntroAfterDarkLordSequence` skip from the same marker. Clear the marker on the next scene load and compile it as a player-build no-op.

Prevention:
Treat direct Hub iteration as one development policy covering volatile run reset, player interaction normalization, Hub portal plan preparation, and all Hub arrival presentation gates. Do not encode this exception in the shared portal prefab, profile completion flags, or only one of the two arrival sequences. Verify direct Hub Play separately from Title-to-tutorial-to-Hub and run-return-to-Hub flows.

## 2026-08-18 - Inspector Rebuilt Before Unity GUI Skin Was Ready

Context:
While Unity 6.4 remained open, external C# and `GlobalUIRoot.prefab` edits triggered compilation, Domain Reload, prefab import, and Inspector redraw. The Inspector then rendered only partial component fields and repeatedly logged `UnityEditor.EditorStyles.get_toolbarButtonRight()` exceptions.

Cause:
During Domain Reload, Unity rebuilt `PropertyEditor` preview contents before a current GUI Skin was available. `PropertyEditor.Styles` static initialization failed, so the same Editor session continued throwing `TypeInitializationException` during Inspector updates. Interleaving external C# and prefab saves increased the chance of overlapping reload and Inspector rebuild work, although the uncaught failure is inside Unity Editor code.

Fix:
Restart the Unity Editor to reset the failed static initializer. If the broken Inspector layout persists, restore the default layout before considering a backed-up `UserSettings/Layouts/default-6000.dwlt` reset.

Prevention:
Follow `Docs/Guides/UnityExternalEditingWorkflow.md`: batch C# edits first, wait for compilation and Domain Reload to finish, then batch approved prefab/scene changes and perform one final Refresh/verification pass. For multi-file external edits, temporarily disable Auto Refresh when practical. Do not alternate C# and serialized-asset saves during an active reload, and do not add runtime workarounds for UnityEditor GUI style initialization.

## 2026-07-04 - Runtime Core Contained Editor-Only Menu Tool

Context:
Assembly-definition migration found `InteractableTool.cs` under `Assets/_Project/Runtime/Core/Interaction/` even though it was a UnityEditor `MenuItem` tool wrapped in `#if UNITY_EDITOR`.

Cause:
The compile guard prevented player-build compilation, but the file still lived under a runtime ownership folder. That makes runtime Core assembly boundaries ambiguous and blocks clean `Core.asmdef` migration.

Fix:
Move editor-only menu/tool scripts and their `.meta` files into `Assets/_Project/Editor/...` so Unity GUIDs are preserved while runtime folders contain only runtime-owned source.

Prevention:
Do not keep `UnityEditor` tooling in `Runtime` folders, even behind `#if UNITY_EDITOR`. For asmdef-safe structure, file location must match ownership: runtime code under Runtime assemblies, editor tooling under the Editor assembly.

## 2026-06-02 - SpeechBubble Negative Scale Flip Shifted Layout Semantics

Context:
DemonKing and EgoSword parallel speech bubbles needed tails to face the speaker during short back-and-forth dialogue.

Cause:
The first implementation flipped the whole background with negative X scale and counter-flipped the text, but did not treat tail side, layout padding, anchor position, and bounds relayout as one policy. This let the tail direction change while the text start position and bubble bounds still reflected the unflipped layout.

Fix:
Parallel speech now evaluates tail side, layout offset, world bounds, and tail pivot distance together before applying a placement. Active/single bubbles keep their position, active parallel-dialogue bubbles may only flip tail side, and parallel bubbles use a small candidate set so overlap fixes do not pull the bubble far away from its speaker root.

Prevention:
When flipping UI that owns layout children, do not only negate scale or decide tail direction as a late visual pass. Cache and restore the anchor-facing transform defaults, mirror directional padding/spacing semantics, measure bounds for each candidate state, then choose the tail side and any layout nudge as one placement decision.

## 2026-06-01 - Debris Final Contact Puff Looked Like Growing Fade

Context:
DemonKing explosion used the `Debris High` particle path. After fragments broke apart, the final disappearance could look like transparent particles growing larger instead of debris pieces fading out.

Cause:
The debris bounce emitter emitted contact puffs on final ground contact. Those puffs intentionally grow while fading, which is correct for impact dust but wrong as the final disappearance representation for debris fragments.

Fix:
`TopDownDebrisBounceEmitter2D` now keeps final fragments in the render-particle buffer and fades their alpha in place before deactivating the piece. Bounce contacts can still emit normal non-final puffs.

Prevention:
Keep final debris disappearance separate from impact-dust presentation. If a debris piece should vanish, fade the fragment itself; reserve expanding puffs for readable contact impacts.

## 2026-06-01 - Unity Null-Conditional Access Hit Destroyed Presentation Objects

Context:
Editor logs showed `MissingReferenceException` from `EndingOutroView` while stopping the ending outro during disable/scene transition cleanup. A follow-up scan found the same cleanup pattern on the title intro view and GameOver inventory inspection path.

Cause:
Presentation cleanup used C# null-conditional calls such as `view?.SetSkipFill(...)`, `outroPlayer?.HideViewImmediate()`, and `inventoryScreen?.ReleaseInspectionOnlyMode(...)`. Unity destroyed objects can still have a non-null C# reference, so `?.` can call into a destroyed `UnityEngine.Object` instead of using Unity's overloaded null comparison.

Fix:
Outro and title-intro cleanup now use explicit Unity null checks before hiding the view or resetting skip fill. The DemonKing Victory GameOver handoff checks the `EndingOutroPlayer` before hiding its view. The GameOver inventory exception checks the `InventoryScreen` before applying or releasing inspection-only mode.

Prevention:
Do not use `?.` for cleanup calls on `UnityEngine.Object` references that may be destroyed during scene unload, disable, or global UI replacement. Use `if (obj != null)` before calling methods so Unity's destroyed-object null semantics are respected.

2026-10-12 alcohol exposure follow-up: the native mug capture destroyed its
authored child before disabling the owning view. `Hide` must validate the live
root, mug and droplets separately even after caching. Editor authoring also uses
explicit Unity null checks before `AddComponent`; `GetComponent<T>() ?? ...` can
retain an Editor fake-null reference. Focused destroyed-child, disable, motion
and authored capture tests cover these paths. See
[AlcoholExposure](StructureMemory/AlcoholExposure.md).

2026-09-03 follow-up - Slime death blocked by expired telegraph handles:
The July 6 commit `0229db83` changed Knight and SlimeQueen warning references from `AttackTelegraphView` to `IAttackTelegraphHandle`. Interface null checks do not use Unity's destroyed-object comparison. `AttackTelegraphService` can destroy a detached view when its duration expires, leaving the caller's interface reference behind. Existing `Editor-prev.log` stacks confirmed `AttackTelegraphView.Release()` throwing during both Knight and SlimeQueen death cleanup. `Enemy.Die()` had already set `isDead`, so the exception skipped the remaining death flow before the EXP notification and prevented subsequent death attempts.

`AttackTelegraphView.Release()` now checks `this == null` inside the concrete Unity object and uses a non-serialized release flag to make repeated calls before end-of-frame destruction harmless. Interface-backed Unity cleanup must validate the underlying object's lifetime inside the implementation; a caller-side interface null check alone is insufficient. See [the session log](./SessionLogs/2026-09-03.md) for verification and the remaining Play Mode checks.

## 2026-06-01 - Consumable And Weapon Input Relied On One Block Tag

Context:
Potions could be used during dialogue or authored presentation flows. Lightning Spear `Skill1` and an active real-weapon Rush also had paths that could read gameplay input outside the normal combat input gate.

Cause:
`PlayerConsumableInput2D` only checked `State.Skill.Blocked`, and `PlayerCombatInput2D` still forwarded blocked `Skill1` input to the current weapon runtime. If dialogue or presentation flow blocked input through `InteractState`, UI blocking, or transition/loading state without a reliable skill-block tag, consumable and weapon-specific runtime input could leak.

Fix:
Consumable, combat, Lightning Spear MarkRush, and real-weapon Rush direct-input paths now check the gameplay input suppression sources directly: non-idle player interaction state where relevant, blocking UI, active dialogue, scene transition, and loading presentation.

Prevention:
Gameplay input entry points should not rely on a single gameplay tag when the input contract also depends on UI, dialogue, interaction, transition, or loading flow state. Keep tag checks as combat-state gates, but add direct flow/UI gates before consuming items or forwarding weapon runtime input.

## 2026-06-01 - Pitfall Damage Fired Mob Death Results Before Pitfall Classification

Context:
Mobs that died from `HoleTrap` damage could run normal death results at the hole position. Split-capable Slimes could also run their normal death split branch before the pitfall death handler marked the death as a pitfall death.

Cause:
`PitFallExecutor` applied trap damage before `PitFallReaction2D.OnPitFallCompleted(...)` called the pitfall death handler. If the damage reduced HP to zero, `Enemy.OnEnemyAttributeChanged(...)` could enter `Mob.OnDeathStarted(...)` immediately, while the mob still looked like a normal death to loot and split logic.

Fix:
`PitFallExecutor` now opens a non-serialized mob pitfall death resolution window around trap damage and completion handling. `Mob.OnDeathStarted(...)` suppresses monster loot during that window, and `Slime` marks `isPitFallDeath` before trap damage can trigger death.

Prevention:
Any hazard flow whose damage can synchronously kill an enemy must establish death-result context before applying damage. Do not rely on a later completion callback to classify loot, split, summon, or lock-sensitive death behavior.

## 2026-06-01 - Title BGM Survived Unresolved Non-Title Scene Music

Context:
Title BGM could keep playing after leaving `TitleScene` when the newly loaded non-title scene did not resolve hub, corridor, boss, or pre-combat carryover BGM.

Cause:
`RunRouteBgmService.RefreshSceneBgm(...)` only changed music when a known scene/route BGM resolved. If no non-title BGM resolved, it left `currentMusicRef` and the `SoundManager` music source untouched, so the previous title track continued.

Fix:
After all non-title BGM resolution paths fail, `RunRouteBgmService` now stops only when the cached current music matches the configured title BGM. Valid hub/corridor/boss/carryover BGM paths still return before this stop path.

Prevention:
Scene music routers need an explicit "no resolved music" branch for boundary tracks such as title music. Do not rely on a later scene-specific resolver to replace a boundary BGM when some scenes intentionally or accidentally have no BGM source.

## 2026-06-01 - Encyclopedia Category SFX Followed Data Swap Instead Of Book Turn

Context:
When switching encyclopedia item categories, the page-flip sound and the visible image/info update could feel mistimed.

Cause:
Category-change audio was attached to `EncyclopediaItemTab` data-swap and completion callbacks. The book presentation did not expose a callback for the actual page-turn animator start, so the flip sound played when content changed rather than when the book started turning.

Fix:
`EncyclopediaBookPresentation` now exposes an optional page-turn-start callback for left/right page turns. `EncyclopediaItemTab` plays the start sound on request, the flip sound when the book turn starts, and the end/content sound immediately after new category content is rebuilt.

Prevention:
For UI sequences with authored motion and content rebinding, put SFX hooks on the presentation beat that owns the visible rhythm. Data swap callbacks should only own content/data sounds when that is the audible event.

## 2026-06-01 - HeavySlash Approach Distance Leaked Into Attack Origin

Context:
DemonKing HeavySlash needed to stop short before warning, but the later warning and attack placement needed its own configurable player position inside the sector warning.

Cause:
`stopBeforeTargetDistance` was reused in `ResolveSlashWarningPrediction(...)`, so the same spacing value controlled both the first approach stop and the predicted warning/commit origin.

Fix:
HeavySlash now uses `stopBeforeTargetDistance` only for the first approach move. Warning tracking positions the sector so the player sits at `playerAnchorInWarningRadius` of the slash radius, and commit movement solves the boss root position from `SwordSlashOrigin` so the locked sector origin and actual hit position match.

Prevention:
For boss attacks with a prep approach and a separate attack placement, keep approach spacing fields separate from hit-origin or socket alignment calculations. Do not reuse movement-stop offsets inside warning/commit prediction unless the design explicitly says the attack should also be offset.

## 2026-05-31 - DemonKing Charge VFX Immediately Played Disappear

Context:
HP50 `WallBounceRush` Charge VFX could appear to play only the `Disappear` state instead of showing the `Loop` state through the rush.

Cause:
`chargeDisappearStartProgress` was added after the existing `AL_DemonKing_Hp50WallBounceRush` asset was authored. If the serialized asset did not carry the field yet, the runtime could receive a zero-like progress value, which made the travel-progress callback switch the same VFX instance into `Disappear` on the first progress tick.

Fix:
WallBounceRush now resolves missing or near-zero `chargeDisappearStartProgress` to `0.9` at runtime. The pattern also exposes `chargeVfxFlipX`, and the Workbench shows cue-level `Flip X`, so Charge VFX horizontal flip tuning has a runtime-backed field instead of relying on negative scale values that were previously normalized away.

Prevention:
When adding serialized timing fields to existing AL assets, guard zero/missing values if zero would create a destructive timing behavior. For VFX flip controls, expose an explicit boolean or a consumed runtime field; do not assume negative scale means flip when target-size scaling uses absolute values.

## 2026-05-31 - DemonKing FinalDesperation Test Left Terminal Runtime State

Context:
Testing `AL_DemonKing_FinalDesperation` through the DemonKing Workbench Actual Pattern Runner could leave the DarkLord body stuck in the 10% pose after cancellation.

Cause:
FinalDesperation is a terminal pattern. It marks `DemonKingRuntimeData.FinalDesperationStarted`, plays/holds `DarkLord_10Percent`, and intentionally blocks normal `RestoreCombatPose()` while final desperation is active. Cancelling the transient runner ability stops the coroutine, but the terminal runtime flag can remain because that flag is normal fight state, not temporary runner state.

Fix:
The Workbench now has a `Refresh Runtime State` button. It cancels transient runner execution, clears FinalDesperation test state, releases animation holds, clears afterimages/motion/groggy state, hides EgoSword as held, and restores the combat idle pose.

Prevention:
Treat terminal boss patterns as stateful runtime transitions when testing them in isolation. After testing FinalDesperation or any future terminal phase, use the Workbench refresh path before continuing normal pattern tuning in the same Play Mode session.

## 2026-05-31 - DemonKing Workbench Synthetic Preview Could Drift From Runtime

Context:
The DemonKing Pattern Workbench could show a Charge VFX socket/effect preview that did not match the actual 50% Charge pattern in Play Mode.

Cause:
The Workbench composite preview was a synthesized authoring timeline. It displayed cue/socket policy from descriptors, but it did not execute the live `AbilityLogic`, `DemonKingController`, `DemonKingVfxSocketMap`, `AbilityMotionController2D`, `AttackTelegraphService`, and VFX spawn code paths that determine actual runtime placement.

Fix:
The Workbench now has a Play Mode-only Actual Pattern Runner. It wraps the selected `AL_DemonKing_*` in a transient `AbilityDefinition`, gives it to the live DemonKing `AbilitySystem`, and runs the real pattern code against the selected/live DemonKing and target. A live runtime preview toggle can render that Play Mode scene inside the Preview Window so socket/effect placement is checked against the actual runtime output instead of only the synthesized timeline.

Prevention:
Use the synthetic timeline for fast authoring orientation, but use Actual Pattern Runner plus Live Runtime Preview for runtime-sensitive socket, VFX, warning, movement, sound, shake, and cleanup checks. Do not treat descriptor-only preview output as proof that runtime presentation is correct.

## 2026-05-31 - Software Cursor Could Leave Visible Screen After Display Change

Context:
Changing resolution or screen mode while the custom mouse cursor was active could make the cursor appear to disappear.

Cause:
`MouseCursorService` hides the OS cursor when its software cursor sprite is active, then positions the UI cursor directly at `Input.mousePosition`. After a resolution or fullscreen-mode transition, Unity can report a pointer position outside the new screen bounds, and some cursor pivots/hotspots can place the entire cursor image outside the visible screen at an edge. Hardware cursor mode can also keep using Unity/OS cached cursor texture state unless the cursor texture is explicitly reapplied after the display transition.

Fix:
The software cursor position is now clamped to a visible screen-space rectangle based on `Screen.width`, `Screen.height`, the cursor rect size, `lossyScale`, and pivot. Non-finite pointer values fall back to the screen center. `MouseCursorService` also tracks `Screen.width`, `Screen.height`, and `Screen.fullScreenMode` and forces hardware cursor texture reapply after those values change.

Prevention:
Any cursor path that hides or replaces the OS cursor must keep its rendered image visible across display transitions and screen-edge positions. Do not assume raw `Input.mousePosition` is already valid for the current output size immediately after `Screen.SetResolution(...)`, and do not skip `Cursor.SetCursor(...)` reapply solely because the texture/hotspot cache still matches.

## 2026-05-31 - DemonKing Laser Warning Was Double-Clipped

Context:
DemonKing EgoSword CrossLaser and 10% FinalDesperation laser warnings could sometimes appear missing or much shorter than the actual laser.

Cause:
The laser code already resolved a wall-bounded rectangle by raycasting to the nearest wall, then passed that rectangle through `AttackTelegraphSpecUtility.WithThinWarningOutline(...)`. That utility enables wall-clipped mesh rendering, so `AttackTelegraphWallClippedMeshView` raycasted again from the rectangle start edge. When the precomputed start edge was already near a wall, the second clip distance could collapse to nearly zero.

Fix:
Pre-clipped DemonKing laser rectangles now render through `WithThinWarningOutlineOnly(...)`, preserving the computed length and width without applying another wall-clip pass. Non-laser line warnings keep the existing wall-clipped outline path by default.

Prevention:
Do not pass already wall-clipped rectangle or line geometry back into a generic wall-clipping telegraph path unless a second lateral wall sample is intentional. Add an explicit opt-out when a pattern owns the wall distance calculation itself.

## 2026-05-31 - GameOver Canvas Hid Inventory Popup

Context:
The defeat/victory GameOver flow allowed Inventory as a narrow exception, but opening it from GameOver made the Inventory invisible or unusable because it rendered below the GameOver screen.

Cause:
`GameOverCanvas` is authored with a higher sorting order than `PopupCanvas` and `HoverCanvas`. The input exception alone was not enough because the visual and raycast order still favored GameOver.

Fix:
The GameOver-owned Inventory open path now temporarily lifts only the existing `Popup` and `Hover` canvases above GameOver and restores their original sorting state during close/reset cleanup. The same path also applies Inventory inspection-only mode.

Prevention:
When allowing flow-owned UI above a modal presentation, verify both input ownership and canvas sorting/raycast order. Do not solve a narrow flow exception by globally reordering persistent canvases or editing prefab YAML unless that broader UI policy is intended.

## 2026-05-31 - DemonKing Cue Scale Was Serialized But Ignored

Context:
Sword GroggyCounter's `DarkLordGroggyReleaseVfx` could appear unchanged even when the branch cue scale was increased in the Inspector or Workbench.

Cause:
`DemonKingVfxCueRef.scale` was serialized and displayed, but runtime spawn paths mostly used only `targetSize` or a fallback diameter. The built-in `GroggyRelease` branch also bypassed the branch cue's target size and used the generic explosion diameter.

Fix:
Cue spawn paths now resolve a scaled target size, and built-in `GroggyRelease` uses the branch cue's target size and scale. Built-in Impact keeps authored prefab scale unless a cue explicitly supplies size or scale data.

Prevention:
When exposing visual tuning fields in the Pattern Workbench, verify that every displayed field is consumed by the runtime spawn path. For VFX cue refs, target size, scale, socket, rotation, prefab override, and fallback kind should all have a concrete runtime effect or be hidden/marked legacy.

## 2026-05-31 - Weapon Swap Left Aim Presentation Override Active

Context:
Swapping weapons during an attack could leave the newly equipped weapon rotation fixed at the previous attack direction.

Cause:
Weapon attacks can lock `WeaponPresentationRig2D` through `BeginAimPresentationOverride(...)`, but weapon swap resets transient ability execution through `AbilitySystem.ResetTransientRuntimeState()`. That path can stop the running coroutine before its normal `finally`-based `EndAimPresentationOverride(...)` release runs, so the shared presentation rig kept `LockedAtCast` or `FacingSideOnly` state across the next weapon visual.

Fix:
`WeaponEquipController` now clears the rig's attack-owned aim presentation override at `Equip(...)` and `Clear(...)` boundaries before changing the active weapon visual.

Prevention:
Cleanup for shared presentation state that survives weapon prefab activation must be owned by the weapon lifecycle boundary, not only by ability coroutine normal or `finally` exits. Keep cinematic presentation locks separate from attack-owned aim overrides.

## 2026-05-31 - Loading Overlay Reveal Hid Fade Before It Was Opaque

Context:
After portal travel, a delayed corridor loading presentation could begin during a frame drop or heavy preload window, and the black fade DimPanel was barely visible while the gameplay screen remained visible.

Cause:
`SceneTransitionCoordinator.TryRevealDelayedLoadingPresentation(...)` revealed the loading overlay without forcing it opaque, then immediately hid the scene fade overlay. `LoadingOverlayController` normally fades its overlay in during `Update()`, so a frame drop between those operations could leave both overlays transparent.

Fix:
The delayed loading reveal now calls `RevealManagedPresentation(immediate: true)` before hiding the fade overlay, making the fade-to-loading handoff black on the same frame.

Prevention:
When handing off between full-screen transition overlays, make the incoming overlay active and opaque before hiding the outgoing overlay. Do not rely on a later `Update()` fade step to cover a load or prewarm stall.

## 2026-05-31 - DemonKing WallBounceRush Count Could Be Spent On Tiny Rushes

Context:
HP50 `WallBounceRush` could still consume the configured rush count while the boss was close to a wall, making fewer full visible rushes appear than the authored count.

Cause:
The endpoint was wall-safe, but the pattern still accepted the exact player direction even when that direction immediately hit a nearby wall. The result was technically a completed rush, but visually it read as a tiny collision/pause instead of one of the intended set-piece charges.

Fix:
WallBounceRush now resolves a visible trajectory before each rush. It prefers the player direction, but if that path is shorter than the minimum visible distance it tests nearby angles inside a limited cone and uses the longest candidate. A dedicated wall-rush probe collider can be authored on the DemonKing to make wall stopping match the visible body.

Prevention:
For boss set-piece movement with a fixed visible count, validate both collision safety and presentation length. A wall-safe endpoint alone is not enough if short endpoints still consume authored beats.

## 2026-05-30 - DemonKing WallBounceRush Point Raycast Let Body Cross Walls

Context:
HP50 `WallBounceRush` could appear to spend collision/rush counts behind or past arena walls, so the configured rush count looked lower than intended.

Cause:
The pattern resolved the wall endpoint with a center-point raycast and then snapped the boss root to that endpoint. The boss body has visible area, so the center point could stop at the wall while the sprite/body crossed it.

Fix:
Resolve retreat, warning, and rush endpoints with a body-radius `CircleCast` plus stop skin before moving or snapping the root.

Prevention:
For large boss root movement, do not use point raycasts as final wall-stop endpoints. Use a body-size cast or authored movement bounds, and keep warning geometry derived from the same stopped endpoint as the movement.

## 2026-05-30 - Portal Entrance Could Play During Active Scene Transition

Context:
After the title -> tutorial -> hub -> run flow, a DragonCorridor `ScenePortal` could play its entrance pull-in presentation and then not move to the target scene.

Cause:
`ScenePortal.CanInteract(...)` checked route resolvability but did not check whether `SceneTransitionCoordinator` was still active from a previous scene transition. The real travel call happens after the entrance presentation, and `ScenePortalTravelService.TryTravel(...)` rejects requests while a transition is active. That rejection path returned `false` without a diagnostic, so the player was restored after the presentation with no clear portal-specific log.

Fix:
`ScenePortal.CanInteract(...)` now blocks interaction while `SceneTransitionCoordinator.IsTransitionActive` is true. `ScenePortalTravelService` now logs a warning when travel is rejected because another scene transition is already active.

Prevention:
Pre-travel presentations must share the same acceptance gates as the final travel request. If a presentation delays the actual transition call, validate global transition locks before starting the presentation and log any late rejection path.

## 2026-05-30 - DemonKing Groggy Dim Raised Body Above Its Own VFX

Context:
During DemonKing GroggyRecoverCounter, EyeFlash and sword GroggyRelease VFX could appear behind the DarkLord body.

Cause:
The Groggy world dim overlay highlighted the boss by temporarily moving all DemonKing SpriteRenderers to `Projectile / Order 2`, while the EyeFlash and GroggyRelease prefabs rendered at `Projectile / Order 1`.

Fix:
Keep the DarkLord/DemonKing root body SpriteRenderer on `Entity / Order 0` and render the Groggy dim panel with the Flowering-style policy on `Entity / Order -1`. VFX remain on Projectile, so they draw above the Entity body without body sorting mutation.

Prevention:
Do not solve DemonKing focus/dim effects by raising the body into the VFX Projectile layer. For body readability, use a lower Entity-layer dim panel, tint, outline, or authored highlight that does not change the body sorting layer/order.

## 2026-05-30 - VFX Auto Builder Reset Inspector Particle Tuning On Play

Context:
Explosion debris bounce particle prefab values could revert when entering Play Mode, and Unity logged `Setting the duration while system is still playing is not supported` from `ExplosionDebrisBouncePrefabBuilder`. A later DemonKing wiring pass also tried to create a second Resources mirror of the tuned HighArc prefab, which risked letting the runtime copy drift from the Inspector-authored prefab.

Cause:
The editor builder's `InitializeOnLoadMethod` path rebuilt prefabs when existing assets differed from generated preset/material checks. Entering Play Mode can trigger editor domain reload/delay calls, so Inspector-authored particle changes were overwritten by generated defaults. The builder also configured newly added `ParticleSystem` components while Unity still considered them playing. Mirroring a prefab for runtime loading created another authoring surface for values to diverge.

Fix:
Limit the automatic builder path to missing prefab creation only; explicit menu rebuild remains available at `Tools/VFX/Rebuild Explosion Debris Bounce Prefabs`. Create generated ParticleSystem children inactive, stop/clear them, configure modules, then reactivate them. For DemonKing HighArc runtime use, move the authored prefab itself into `Resources/DemonKing/Vfx` instead of maintaining a generated mirror.

Prevention:
Editor auto-builders for authored VFX should not repair existing prefab assets on play/domain reload unless the user explicitly invokes a rebuild command. Generated prefab scaffolds may create missing assets automatically, but preserving Inspector tuning must be the default behavior. When a runtime path requires `Resources`, move or directly reference the tuned prefab rather than copying it into a second prefab that can fall out of sync.

## 2026-05-30 - Manual ParticleSystem Prefabs Had No Useful Inspector Preview

Context:
The generated explosion debris bounce prefabs existed, but the normal ParticleSystem/Prefab preview did not show how the effect behaves.

Cause:
`TopDownDebrisBounceEmitter2D` drives particles manually through `ParticleSystem.SetParticles(...)` from the component update loop. The built-in ParticleSystem preview can play authored module emission, but it does not run this custom virtual-height simulation.

Fix:
Add an Editor-only preview window that instantiates a hidden temporary prefab copy, steps the emitter manually, and renders it through a hidden camera into a `RenderTexture`. The prefab builder now also resolves `Sprites-Default.mat` before the older default particle/fire fallbacks so the generated presets use the intended square sprite-style material.

Prevention:
For manually driven VFX helpers, include a dedicated Editor preview path instead of relying on the Inspector ParticleSystem preview. Preview tools must render temporary instances and must not mutate prefab assets directly.

## 2026-05-30 - Cinematic Fixed Waits Treated As Camera Completion

Context:
Merchant, run-special NPC, shortcut, and tutorial cinematic flows could advance speech bubbles, choices, or gameplay release before the visible camera/letterbox presentation had fully settled.

Cause:
Several flows used authored focus/return wait seconds as if they represented camera completion. Those values are only minimum hold durations and do not account for Cinemachine blend completion, follow damping, moving camera targets, or the extra frame needed for the output camera to settle. `TutorialCombatIntroSequence` also released gameplay before its letterbox-out routine finished.

Fix:
Flow-owned camera waits now perform the existing authored minimum wait and then wait for the Cinemachine brain to stop blending and for the intended target's viewport position to remain stable for consecutive frames. `TutorialCombatIntroSequence` now releases gameplay only after letterbox-out completes.

Prevention:
For cinematic camera flows, treat serialized wait seconds as minimum presentation holds, not as completion gates. Advance speech bubbles, choices, gameplay release, or cleanup only after camera settle and visible closing presentation have finished.

## 2026-05-30 - Dialogue Text Preview Used A Different TMP Coordinate Space

Context:
`Tools/Dialogue/Text Animation Tuner` showed `[wave]`, `[slowshake]`, `[shake]`, and `# CameraShake` character impact motion as more dynamic than the actual DialoguePanel. After switching to `TextMeshProUGUI`, assigning the real `DialogueText` source could make the preview text disappear. A later pass could also throw `IndexOutOfRangeException` in `TextMeshProUGUI.SetSharedMaterials(...)`. Even after source/effective rect diagnostics were valid, the preview could still render black.

Cause:
The preview rendered a world `TextMeshPro` object with a fixed small font size and arbitrary orthographic camera framing, while runtime dialogue uses `TextMeshProUGUI` in the DialoguePanel Canvas. The same absolute TMP vertex offsets therefore appeared at different visual scales. The follow-up UGUI preview then copied the source `DialogueText` RectTransform literally; that authored text rect has height `0` because the parent `DialogueTextCon` supplies the usable layout area, so the preview mesh had no visible text area. The source `fontSharedMaterials` array was also copied into a fresh preview TMP instance whose internal material/submesh slots did not match that array. `PreviewRenderUtility` did not reliably render the hidden world-space UGUI CanvasRenderer output.

Fix:
The tuner preview now renders a hidden world-space `Canvas + TextMeshProUGUI`, resolves `GlobalUIRoot.prefab` `DialogueView.dialogueText` as the default source, and copies its text/rect/font settings before applying the shared text animation utility. Preview-only effective text sizing resolves zero or invalid source dimensions from the parent container before falling back to `1720 x 250`, and the tool reports source/effective size diagnostics. Material copying is limited to the font and primary shared material instead of copying the source material array. The preview now uses a hidden preview camera and `RenderTexture` instead of `PreviewRenderUtility`.

Prevention:
When adding editor previews for UI text vertex animation, render through the same TMP component family and layout scale as runtime, or require an explicit runtime source object. Do not tune UI vertex offsets from a world TMP preview with arbitrary font/camera scale. Also do not treat a zero-size child text rect as the final render area when the authored UI relies on a parent container, do not copy `fontSharedMaterials` arrays between unrelated TMP instances, and prefer explicit `RenderTexture + Camera.Render()` for UGUI previews when `PreviewRenderUtility` produces black output.

## 2026-05-29 - DemonKing Pose Holds Restarted Current Animator State

Context:
`DarkLord_Hand_Groggy`, `DarkLord_Sword_Groggy`, `DarkLord_Hand_GroggyCounter`, `DarkLord_Sword_GroggyCounter`, and `DarkLord_Sword_Slash` could still look like they replayed even after adding once-per-pattern start guards.

Cause:
The first pass guarded only full clip start calls. Pose hold helpers still called `Animator.Play(...)` when the Animator was already on the same state: `HoldGroggyPoseAnimation()` restarted the current Groggy pose at frame 0, and last-frame holds could jump a completed same-state clip back to its computed last-frame normalized time.

Fix:
DemonKing same-state pose holds now reuse the current Animator state instead of replaying it. Groggy eye-flash holds freeze the current Groggy frame when already in the Groggy state, and last-frame holds skip `Animator.Play(...)` when the same state has already reached or passed the requested frame.

Prevention:
For DarkLord state-library clips, distinguish between a deliberate frame switch and a pose hold. Use `Animator.Play(..., 0f)` only when a clip should actually restart; when the current state already represents the desired pose, freeze or continue that state instead of resampling it.

## 2026-05-29 - DemonKing Facing Used The Wrong Sprite Baseline

Context:
DarkLord/DemonKing body sprites are authored facing left in the source sheet, but many DemonKing animations and pattern windows appeared to face away from the player.

Cause:
The shared `Enemy.TryApplySpriteFacingTargetX(...)` helper treats the cached initial `SpriteRenderer.flipX` value as the right-facing baseline. DemonKing's unflipped art is left-facing, so using that helper directly inverted the intended direction.

Fix:
`DemonKingController` now owns a local left-facing baseline: `flipX == false` means facing left and `flipX == true` means facing right. Auto-facing, pattern-facing, fallback facing direction, and sword hold offset mirroring use that DemonKing-specific baseline.

Prevention:
When adding a boss with non-right-facing source art, do not assume the shared `Enemy` facing helper is correct. Confirm the authored sprite's natural facing direction and either set the prefab's initial `flipX` to the right-facing baseline or add a boss-local facing policy before wiring pattern animation timing.

## 2026-05-29 - Apprentice Sword Abilities Ignored Authored Animation Hit Events

Context:
`AM_Skill2.anim` had a `SendEvent` animation event for `Event.Anim.SwordSkill2.Hit`, but `ApprenticeHeroSwordChargeSpin` did not create its release hitbox/effect from that event timing. `ApprenticeHeroSwordDashStab` also used the `Swing1` trigger, whose `AM_Swing1.anim` clip emits `Event.Anim.SwordCombo.Hit`, while spawning its hitbox immediately.

Cause:
The ability logics played animation triggers and immediately spawned hitboxes. Their data assets did not own the relevant hit-event tag fields, so authored animation events were emitted by `AbilityAnimationEventRelay` but had no local ability consumer for pattern timing.

Fix:
`ApprenticeHeroSwordChargeSpinData` now owns a release hit event tag and timeout, and `ApprenticeHeroSwordDashStabData` owns a hit event tag and timeout. Their ability logics wait for the configured tag after the animation trigger before spawning the hitbox, with timeout fallbacks so a missing clip event does not stall the ability.

Prevention:
When a weapon clip receives an animation event for gameplay or local effect timing, verify the active `AbilityLogic` waits for the same data-owned `GameplayTag`. Do not assume an authored clip event automatically creates pattern-local hitboxes or effects unless an ability container, cue, or logic waiter consumes it.

## 2026-05-29 - Boss Auto Facing Ran During Encounter Presentation

Context:
Actual DemonKing could appear to shake left/right during the boss encounter presentation before combat began.

Cause:
`BossEncounterDirector` disables boss combat during the encounter sequence, but `DemonKingController.Update()` still ran its per-frame target-facing path outside the base boss FSM tick. `FaceCurrentTarget()` also compared raw X positions and wrote `sprite.flipX` directly, so near-equal X positions could repeatedly flip the visual.

Fix:
`DemonKingController.CanAutoFaceTarget()` now requires `IsCombatActive`, and `FaceCurrentTarget()` uses the shared `TryApplySpriteFacingTargetX(...)` helper with a small non-serialized deadzone.

Prevention:
Boss-specific per-frame target facing must be gated by combat state unless the encounter intro or a pattern explicitly owns that facing. Prefer shared facing helpers with deadzones over direct `sprite.flipX` X-position comparisons.

## 2026-05-28 - Run Route Catalog Made Normal Bosses Look Like Final Context

Context:
Normal reward bosses should run as the default route flow three times before the single final boss, but direct boss-scene testing made normal bosses appear to use final-route behavior or an incomplete route context.

Cause:
`RunRouteCatalog.asset` had only one normal stage and only the Dragon Spili route in `normalRouteSets`, while `SceneDomainCoordinator` seeded direct scene Play with only the current RouteSet. A single-stage development plan makes the current boss both first and last, so final-route checks and boss-exit routing can be misleading during direct scene tests.

Fix:
Restore `RunRouteCatalog.asset` to three normal RouteSets (`Shadow`, `Dragon Spili`, `Slime Queen`) plus `DemonkingRouteSet` as final. `PortalRouteManager.SeedDevelopmentPlan(...)` now builds the deterministic full catalog plan for direct Play and sets `currentStageIndex` to the tested RouteSet. `BossEncounterEndDirector` debug logging now reports RouteSet, `IsFinalRouteSet`, active-plan state, stage index, and catalog.

Prevention:
Treat NormalRoute as the default boss reward path and FinalRoute as the special one. When testing a boss scene directly, verify the route context with `BossEncounterEndDirector.logDebug` before diagnosing reward suppression. Keep `RunRouteCatalog.normalStageCount`, normal RouteSet list, and final RouteSet covered by the boss reward reveal validator.

## 2026-05-28 - Boss Reward Portal Reveal Sub-Effects Used Split Timing

Context:
The boss reward portal reveal looked unsynchronized: SpriteMask reveal, dust particles, portal root tremble, and camera shake did not feel tied to the same rise timing.

Cause:
Portal reveal sub-effects were triggered from separate moments in the routine. Loop and burst dust both played at reveal startup, root tremble used global time for its oscillation phase, and scene ParticleSystems authored under the moving reveal root could inherit the lowered root motion instead of staying at the authored dust output point.

Fix:
`BossRewardObjectRevealPresentation` now uses one reveal timeline. The mask and collider lock span the reveal. Loop dust starts with the reveal and stops at completion, burst dust plays only at completion, loop camera shake and root tremble use the reveal normalized envelope, and root tremble uses reveal elapsed time for its phase. Particle prefab spawn poses are resolved from the final reveal-root position, and scene loop ParticleSystems under the moving reveal root are temporarily detached/restored during playback.

Prevention:
When adding portal reveal polish, route it through the reveal timeline instead of firing it from independent setup or completion code. Effects that should live during the rise should use reveal normalized progress; effects that should punctuate arrival should run after the final reveal progress is applied. Do not let ground dust inherit the lowered gate root unless the intended effect is attached to the moving gate itself.

## 2026-05-28 - Boss Reward Reveal Used Mask And Particle References From The Wrong Scope

Context:
In `HeoMinSeok_Boss`, the reward chest did not visibly activate after boss defeat, and the reward portal's mask/particle reveal did not play as expected.

Cause:
The chest reveal was dust-only but still had `maskedRenderers` serialized from component reset, with no `revealMask`. Runtime reveal therefore set the chest renderer to `VisibleInsideMask` without a local mask, making it appear invisible. The portal reveal mask was parented under the same portal root used as `revealRoot`, so the mask moved with the portal and could not clip a bottom-up reveal. The dust arrays also referenced prefab asset ParticleSystem components instead of scene instances.

Fix:
Chest reward dust now belongs to `TreasureChest.PlayRewardReveal()` with one serialized dust ParticleSystem plus spawn anchor/offset. `BossRewardObjectRevealPresentation` remains for portal gate reveal behavior only: mask interaction, root rise, particles, collider lock, global mask isolation, camera shake, and local tremble. The boss reward reveal apply tool now removes stale chest reveal components, serializes `TreasureChest` reward dust fields, clears serialized gate masks for runtime generation, and validation reports stale chest reveal components or portal mask wiring mistakes.

Prevention:
For reward reveal authoring, do not put `BossRewardObjectRevealPresentation` on reward chests. Configure chest dust on `TreasureChest`. Keep mask/masked-renderer wiring only on portal gate reveals. Gate masks must stay outside the transform that moves during reveal, or leave the mask field empty so runtime can generate one from the target renderer. Particle fields may reference either scene ParticleSystem instances or prefab ParticleSystem assets; prefab references need a spawn anchor/offset that represents the intended reveal dust position.

## 2026-05-28 - Dialogue Boss Silhouette Ignored First Face Tag

Context:
`DarkLord_Tutorial` authored the first Ink line with `# face:Tutorial`, but the boss dialogue silhouette still appeared with the `Normal` portrait shape.

Cause:
The Dialogue opening sequence played the boss portrait/silhouette intro before the first Ink line was consumed. The `face` tag was processed only after the intro completed and the first line began, so the silhouette setup had already chosen the `Normal` sprite.

Fix:
`DialogueController` now previews the first playable Ink line through a cloned `Story` state, extracts the matching `face` tag without advancing the real story, and passes that label through `DialoguePresentationSequencer` / `CinematicDirector` to the portrait intro. `PortraitController.SetupSilhouetteMode(...)` can now set the silhouette sprite from that label.

Prevention:
When a presentation intro depends on Ink tags from the first line, preview the tag state before opening presentation instead of waiting for normal line playback. For boss silhouette variants, author the first playable line with a valid `face` label and verify that the assigned `SpriteLibraryAsset` contains the same label.

## 2026-05-28 - Global SpriteMask Clipped Reward Portal Reveal

Context:
`HeoMinSeok_Boss` uses `GlobalVisionMaskRoot` for its boss gimmick. The reward portal also uses a local `SpriteMask` reveal so the gate can rise from below after boss defeat.

Cause:
Unity `SpriteRenderer.maskInteraction` responds to every `SpriteMask` whose sorting range includes the renderer; it does not target one specific mask. When the reward portal renderer is set to `VisibleInsideMask` for the local reveal, the global player vision mask can also clip it.

Fix:
`BossRewardObjectRevealPresentation` now has `isolateGlobalVisionMasksDuringReveal`, defaulted on for reward reveals but only applied when a local reveal mask or masked renderers exist. While the gate reveal is active, it stores eligible active `GlobalVisionMaskController` child mask ranges, skips player-following masks owned by `PlayerVisionMaskFollower`, narrows the remaining masks to the dark overlay renderer's sorting range, and restores the original ranges on completion, stop, or disable. The target-scene apply tool enables this option for portal reveal components, and validation reports it as required in global vision mask scenes.

Prevention:
For any future scene that combines a local reward gate SpriteMask reveal with `GlobalVisionMaskRoot`, keep global vision mask isolation enabled and verify both the dark overlay cutout and the portal reveal in Play Mode. Do not assume `VisibleInsideMask` means "inside only my local reveal mask," and do not include player-following light/vision masks in broad scene-mask range rewrites.

## 2026-05-29 - Reward Portal Isolation Narrowed Player Vision Mask

Context:
In the Shadow boss reward portal reveal, the player Light/vision mask appeared disabled while the portal reveal mask was active.

Cause:
The reward portal isolation pass enumerated every `SpriteMask` under active `GlobalVisionMaskController` objects. `GlobalVisionMaskController` also instantiates the player-following vision mask prefab with `PlayerVisionMaskFollower`, so that player mask had its custom sorting range narrowed with the scene isolation masks.

Fix:
`BossRewardObjectRevealPresentation` now skips any `SpriteMask` that has `PlayerVisionMaskFollower` on itself or a parent while applying global vision mask isolation.

Prevention:
When sweeping `SpriteMask` components from a global controller, separate scene-level masks from player-following presentation masks before changing range state. `Mask Source = Supported Renderers` is not a safe targeting fix for this conflict; if the portal reveal edge needs a square shape, keep the reveal mask as sprite-based and author a square mask sprite.

## 2026-05-28 - Global Ending IntroOverlay Appeared On Play Start

Context:
The `IntroOverlay` authored under `GlobalUIRoot` for the ending outro could appear immediately when entering Play Mode if the prefab or scene instance was left active for authoring.

Cause:
`GlobalUIRoot` carries the authored `EndingOutroView`, but not necessarily an `EndingOutroPlayer` on the same active object. The player-owned `hideViewOnAwake` path therefore does not cover a globally authored view object that starts active.

Fix:
`EndingOutroView.Awake()` now snaps the view hidden when it is active due to scene/prefab authoring. `EndingOutroView.Show(...)` marks its own activation so the first real outro playback can activate an inactive authored view without the `Awake()` startup hide immediately turning it off again.

Prevention:
Presentation view roots that can live under persistent global UI should own a safe runtime-closed startup state, even if a separate player component also hides them. Do not rely only on scene/prefab inactive authoring for fullscreen overlays that may be toggled on during editing.

## 2026-05-28 - Ink Rich Text Hex Color Split Into Tag

Context:
DarkLord tutorial Script 1 tried to color the hidden `???` laugh line purple with TMP rich text, but Dialogue showed literal `<color=` instead of colored text.

Cause:
Ink treats `#` inside a line as the start of an Ink tag. The source line used `<color=#A855F7>...`, so the compiled JSON split the visible text at `^<color=` and stored `A855F7>...` as a tag instead of body text. The Dialogue typewriter also used `DOText`, which can expose partial rich-text tags while typing.

Fix:
Use a named TMP color tag (`<color=purple>...`) for the tutorial line and update the generated TextAsset JSON to match. `DialogueView` now sets the full rich text on TMP first and animates `maxVisibleCharacters`, so rich-text tags are never revealed as partial body text during typing.

Prevention:
Do not put raw `#RRGGBB` rich-text color values directly in Ink body text unless the `#` is confirmed to survive Ink compilation. Prefer named TMP colors or verify the generated JSON contains a single visible text token with the full rich-text tag.

## 2026-05-28 - Dialogue Reward Opened Under Suppressed Reward And Hover Canvases

Context:
Completing an affection reward during Dialogue could log `Coroutine couldn't be started because the game object 'RewardPanel' is inactive!` from `RewardDisplayUI.OpenUI()`. After the reward canvas was restored, hovering a reward slot could also log the same coroutine error for `ItemDetailPanel` because `HoverCanvas` was still inactive.

Cause:
Dialogue non-dialogue UI suppression hides both `GlobalCanvasLayer.Reward` and `GlobalCanvasLayer.Hover` while Dialogue is playing. Affection rewards are flow-owned UI that can intentionally open before Dialogue fully exits, so `UIManager` accepted the reward panel while its parent `RewardCanvas` was still inactive from suppression. Reward slots then use the normal inventory hover path, which needs `HoverCanvas` to be active before `ItemDetailPanel` starts its open presentation coroutine.

Fix:
`DialogueService` now lets a flow mark a captured suppressed layer as temporarily visible so an in-progress suppression fade will not immediately hide it again. `RewardDisplayUI.OpenUI()` uses that boundary to reactivate the authored Reward and Hover canvas roots before enabling the reward panel. Reward is restored as interactable/raycasting, while Hover is restored visible but non-raycasting. When the reward popup closes while Dialogue is still playing, the reward owner hides both canvas roots again so the dialogue suppression window remains intact.

Prevention:
When an overlay can be opened as part of the same flow that suppressed its canvas layer, the overlay owner must restore every authored canvas root it needs before starting coroutines or UI animation. Do not assume `UIManager.PushUI()` activates inactive parent canvas roots, and remember that reward item slots depend on the shared Hover layer.

## 2026-05-28 - Terminal Outro View Destroyed With Duplicate GlobalUIRoot

Context:
Entering the DemonKing boss scene from Hub or Corridor could complete the death SpeechBubble and Dialogue handoff but skip the ending outro.

Cause:
The DemonKing scene's `EndingOutroPlayer` could hold a serialized `EndingOutroView` reference from that scene's `GlobalUIRoot` prefab instance. When the scene was reached from another playable scene, a persistent `GlobalUIRoot` already existed, so the destination scene's duplicate `GlobalUIRoot` destroyed itself and invalidated the serialized outro view reference before playback.

Fix:
`EndingOutroPlayer` now resolves a live `EndingOutroView` from the current `GlobalUIRoot` before playback when its serialized view is missing or not ready, and `BossDefeatEndingSequence` revalidates the playable outro player immediately before starting the outro.

Prevention:
For presentation UI authored under persistent global roots, validate cross-scene entry, not only direct scene play. Any terminal flow that references global UI children should re-resolve live `GlobalUIRoot` objects after scene load and duplicate-root cleanup instead of trusting stale scene-instance references.

## 2026-05-28 - Tutorial Direct Portal Dropped Weapon Runtime State

Context:
Using the tutorial gate from `TutorialCorridor` into `DarkLord_Tutorial` could leave the spawned player without the tutorial weapon.

Cause:
`TutorialScenePortal` intentionally bypassed the normal `ScenePortalTravelService` route plan, but that also bypassed the player runtime-state capture used by normal scene portals. `DarkLord_Tutorial` then spawned a fresh player prefab and the restore bootstrapper had no pending weapon inventory state to apply. A later source fix added preserve flags, but existing scene instances did not serialize those new fields, so they could still deserialize as `false` and skip capture. After capture was restored, the tutorial default weapon still could not be restored because `Weapon.ApprenticeHeroSword` was assigned in the scene bootstrap but missing from `ItemDatabase.allWeapons`, so `PlayerRuntimeResolverBridge.ResolveWeapon(...)` could not map the captured id back to a `WeaponDefinition`.

Fix:
`TutorialScenePortal` now captures the current player's `PlayerRuntimeState` through `PlayerRuntimeCaptureBridge`, stores it in `GamePlayDataManager`, and prepares a minimal pending transition context before starting the direct scene load. If load acceptance fails, it restores the previously pending state/context. The runtime now preserves by default without relying on newly added serialized true values; only the explicit inverse `resetPlayerRuntimeStateOnTravel` option disables capture. `WD_ApprenticeHeroSword` is also registered in `ItemDatabase.allWeapons` so the captured tutorial weapon id can be resolved during destination restore.

Prevention:
When adding direct tutorial scene travel, check whether the destination depends on spawned-player inventory, relics, consumables, or ability runtime state. Direct scene travel must either capture runtime state explicitly or deliberately reset the destination loadout. Avoid adding positive serialized bool defaults to existing scene components when a stale false value would break critical runtime preservation. Any weapon that can be saved/restored by id must be present in the active `ItemDatabase.allWeapons`, even if it is tutorial-only and not default-unlocked.

## 2026-05-28 - Hub Intro Draft Ink Opened Empty Dialogue

Context:
The DarkLord-to-Hub intro reached the Dialogue UI, but the dialogue body was empty during the temporary MSUpgradeNpc Hub intro test.

Cause:
The temporary Hub intro JSON stored each line under a named Ink knot such as `HUB_INTRO_JUNK`, while `HubIntroAfterDarkLordSequence` had blank `dialogueStartPath` fields in the saved Hub scene. Ink therefore started at the generated root, found only `done`, and opened/closes the Dialogue presentation without yielding visible text.

Fix:
The Hub intro authoring tool now wires the temporary draft start paths and validates that draft JSON assets use their matching knot. The temporary JSON files also mirror their line at root so already-saved blank start paths can still show placeholder text after Unity imports the updated TextAssets.

Prevention:
When creating temporary Ink JSON by hand, either put playable content at root or wire the exact `startPath` used by the runtime caller. Validation must cover the asset/start-path pair, not just that a TextAsset reference exists.

## 2026-05-28 - DemonKing Final Threshold Waited For Groggy Exit

Context:
Dropping DemonKing to the 10% FinalDesperation threshold while he was already Groggy left him in the invulnerable-looking Groggy state until the Groggy duration ended, then started the 10% pattern.

Cause:
The health gate reserved FinalDesperation, but the active `State.Status.Groggy` tag still drove the boss FSM back into `BossGroggyState` on the next reactive transition. The forced start also reused normal pattern evaluation, which can still be affected by stale blackboard HP/selection gates.

Fix:
FinalDesperation now marks the final phase started, ends active Groggy effects/tags immediately, and reserves the final pattern through a forced pattern reservation that bypasses normal selection gates at execution start.

Prevention:
Terminal threshold patterns that must preempt reactive states should explicitly clear or override the reactive state tag and should not depend on ordinary AI pattern selection/evaluation gates.

## 2026-05-28 - DarkLord Tutorial Relied On Scene-Only HUD And Camera Authoring

Context:
Entering `DarkLord_Tutorial` from `TutorialScene` could still show the normal Gameplay/Boss HUD and could jump straight to the boss focus instead of first showing the spawned player.

Cause:
The previous fix deactivated HUD objects in the `DarkLord_Tutorial` scene instance, but the actual `GlobalUIRoot` can persist from the previous scene through `DontDestroyOnLoad`. Scene YAML authoring for inactive HUD roots therefore did not cover runtime entry. The sequence also started the first boss focus immediately after player registration, so scene transition fade-in could hide the player-facing starting beat.

Fix:
`TutorialBossEncounterSequence` now snapshots and hides the runtime `GlobalUIRoot` Gameplay/Boss HUD canvas roots and default HUD component roots at sequence start, restores that snapshot when the scene unloads or the sequence is canceled, waits for the active transition fade to finish, frames the player for the initial beat, then moves to the boss focus. The tutorial fake game-over request also carries a tutorial-only return button label.

Prevention:
For tutorial scenes reached through runtime scene transitions, validate both scene-authored objects and persistent UI/service state. Do not assume inactive objects saved in the destination scene will affect an already-loaded `DontDestroyOnLoad` UI root, and do not start camera focus beats until the transition fade is no longer active.

## 2026-05-28 - Scene Fade Image Rendered Behind Ending Outro

Context:
The DemonKing terminal ending outro reached its final slide and waited for the configured slow title transition duration, but the visible black fade did not appear before `TitleScene` loaded.

Cause:
The ending outro was authored as a later child under the same `FadeInOutCanvas` that owns the shared `SceneFadeTransitionService` `FadeImage`. The fade coroutine was increasing the fade image alpha, but Unity rendered that image behind the outro because of sibling order.

Fix:
`SceneFadeTransitionService` now moves the configured overlay root to the last sibling whenever it is activated, so authored UI inserted into the same fade canvas cannot sit above the black transition image.

Prevention:
For shared transition overlays, validate both canvas sorting and sibling order. A correct fade duration and alpha curve do not prove the fade is visible if the overlay image is below scene-authored presentation UI.

## 2026-05-28 - Scene Fade Canvas Rendered Behind Destination Scene UI

Context:
The DemonKing terminal ending could load `TitleScene`, but the requested post-load TitleScene fade-in did not visibly play.

Cause:
The active fade image sibling order was corrected inside its own `FadeInOutCanvas`, but the destination scene can load new canvases after the transition starts. The persistent `GlobalUIRoot` fade canvas uses normal overlay sorting, so the old active fade image can continue changing alpha behind newly loaded TitleScene UI.

Fix:
`SceneFadeTransitionService` now elevates the active overlay's parent canvas to the maximum sorting order while a fade is running, repeats that elevation during fade frames after scene load, and restores the saved canvas sorting state when the transition or overlay fade session ends.

Prevention:
For scene-load fade-in bugs, validate the active transition canvas against destination-scene canvases, not only the fade image alpha and sibling order. A `DontDestroyOnLoad` overlay must be re-promoted after `LoadSceneAsync` because the destination scene can introduce newer or higher-sorted canvases.

## 2026-05-28 - Deferred Fade Replacement Promoted A Destroyed Owner

Context:
After the DemonKing terminal ending loaded `TitleScene`, Unity threw a `MissingReferenceException` from `SceneFadeTransitionService.PromotePendingReplacementIfAvailable()` when `EndTransitionSession()` tried to destroy the old fade service owner.

Cause:
The scene-load coordinator can still hold a `SceneFadeTransitionService` reference after Unity has destroyed that service object during `LoadSceneMode.Single`. The deferred replacement path then promoted the TitleScene service but accessed `gameObject` on the destroyed old owner. In the same state, the post-load fade-in can be skipped because the old owner's overlay references are destroyed.

Fix:
`SceneFadeTransitionService` now destroys service owners through a helper that first respects Unity's destroyed-object null check. `SceneTransitionCoordinator` also re-resolves a replacement fade service after the scene load if the original service has been destroyed, begins a recovered transition session, snaps it to black, and then runs the configured fade-in on the replacement.

Prevention:
After `LoadSceneAsync`, do not assume a cached `UnityEngine.Object` service reference is still alive. Re-check Unity null semantics before post-load fade work, and recover through the destination scene's authored service when the original transition owner was destroyed.

## 2026-05-28 - Tutorial Presentation HP Canvas Used Stale Authoring

Context:
`DarkLord_Tutorial` had a valid `TutorialPresentationHpView`, heart slots, and laser references, but the fake HP UI did not appear during the laser sequence.

Cause:
The saved scene had an older generated `TutorialPresentationHpCanvas` setup: the HP references were wired, but the canvas/root transform and sorting authoring were not normalized and the sequence only restored CanvasGroup alpha. During the laser beat, the cinematic letterbox canvas can also draw above presentation UI unless the tutorial HP canvas is explicitly sorted as an overlay.

Fix:
The DarkLord authoring tool now normalizes the tutorial HP canvas RectTransform to full-screen overlay bounds with `localScale = Vector3.one`, keeps the canvas GameObject active, enables max overlay sorting, and validates that the HP canvas is renderable. `TutorialBossEncounterSequence` also ensures the referenced presentation HP canvas is active, enabled, and sorted before showing it at the laser timing.

Prevention:
For temporary scene-authored UI that is hidden with CanvasGroup alpha, keep the Canvas and root GameObjects active and validate transform/sorting separately from visibility alpha. A passing serialized reference check is not enough to prove UI can render above a cinematic overlay.

## 2026-05-28 - Tutorial Boss Camera Followed Real Boss Director

Context:
`DarkLord_Tutorial` should move the gameplay camera to the authored tutorial boss focus before Script 1 and again before Script 2, but the camera could fail to move when the sequence was wired through the real boss `CameraPresentationDirector` / `BossCam` path.

Cause:
The tutorial scene reused a real boss camera presentation path even though the tutorial boss is a stripped presentation scene object. That made the sequence depend on real encounter camera authoring that may be missing, disabled, or no longer aligned with the tutorial focus markers.

Fix:
The DarkLord tutorial authoring menu now disables `TutorialBossEncounterSequence.useCameraPresentationDirector` and wires the sequence to the explicit `BossFocusTarget` / `PlayerFocusTarget` gameplay camera path. Validation reports if the real boss camera director path is enabled.

Prevention:
Scene-local tutorial presentations should use their own authored focus targets unless the target scene intentionally owns a complete real boss camera setup. Do not treat a present `CameraPresentationDirector` as sufficient for tutorial camera authoring.

## 2026-05-27 - Cinematic Protection Did Not Block Dash Tags

Context:
`DarkLord_Tutorial` should never allow player movement, dash, attack, skill, or aim input, but the player could still move or dash after the tutorial boss sequence validation passed.

Cause:
`PlayerCinematicProtection` disabled several input producer behaviours, but movement and ability systems can still read cached movement sources or activation tags. The shared UI control tag set also did not include `State.Move.Dash.Blocked`, so dash activation could survive when only UI-style control blocking was active.

Fix:
Make `PlayerCinematicProtection` apply the existing UI control block tag set and explicitly add `State.Move.Dash.Blocked` for the protected window. `TutorialBossEncounterSequence` now keeps the player protection held after completion for `DarkLord_Tutorial` because that scene has no intended controllable player timing.

Prevention:
Cinematic player locks must block both input behaviours and gameplay tags. Do not treat disabled input components as sufficient when `MovementMotor2D`, cached intent sources, or GAS activation can still consume state after another system restores player interactor state.

## 2026-05-27 - Tutorial Boss Sequence Locked Before Player Registration

Context:
`DarkLord_Tutorial` validation passed with `lockPlayerControls = true`, but the player could still move during the laser presentation.

Cause:
`TutorialBossEncounterSequence` acquired `PlayerCinematicProtection` immediately at `Start()`. In the tutorial scene, `playOnStart` can run before the player is registered in `PlayerRuntimeRegistry`, so the sequence had no player transform and skipped the control/targetability lock while validation only checked serialized booleans. A later scene fade or player spawn path could also restore the current player to an interactive state unless the sequence held a `SceneFadeTransitionService` player-unlock blocker and maintained the lock after registration.

Fix:
Wait for a resolved player transform before acquiring the sequence state, subscribe to `PlayerRuntimeRegistry.PlayerRegistered` / `PlayerUnregistered`, hold `SceneFadeTransitionService.SetPlayerUnlockBlocked(...)`, and maintain the current registered player lock during the sequence. Keep the acquired player protection for the full sequence, including dialogue, laser, collapse, and fake game-over setup.

Prevention:
For play-on-start tutorial or cinematic flows that depend on spawned player components, validate serialized intent but also make runtime acquisition wait for player registration before starting the protected flow. If a scene fade or spawner can restore player state, the cinematic owner must hold the transition unlock blocker and reapply the lock to the currently registered player.

## 2026-05-27 - Event-Driven Tutorial Info Trigger Required Collider

Context:
Combat tutorial info should be opened by the door-close sequence through `TutorialInfoTrigger.FireNow()`, not by player collider entry.

Cause:
`TutorialInfoTrigger` still had `[RequireComponent(typeof(Collider2D))]` even though its public `Fire`, `FireNow`, and `FireAfterDelay` methods already support event-driven timing. This forced unnecessary collider authoring on sequence-owned tutorial prompts.

Fix:
Remove the `Collider2D` requirement from `TutorialInfoTrigger`. Collider setup remains optional and is used only for `OnTriggerEnter2D` activation.

Prevention:
For components that support both trigger-collider activation and direct event/code activation, do not use `RequireComponent` for the optional activation path. Document which activation mode needs scene collider authoring.

## 2026-05-27 - Boss Encounter Aim Block Did Not Freeze Presentation Facing

Context:
Boss encounter intros needed the player body and weapon presentation to stop turning left/right from Aim while the encounter camera/dialogue sequence was running.

Cause:
Blocking or disabling `PlayerAim2D` stopped live aim updates, but `PlayerAnimatorController2D` and `WeaponPresentationRig2D` still read cached `AimDirection` / `MouseWorld` every frame. The first legacy `BossTalkManager` fix also did not cover scenes driven by `BossEncounterDirector`.

Fix:
Player body facing and weapon presentation now expose owner-token cinematic locks, and boss encounter owners acquire/release those locks for modern, legacy, and tutorial boss encounter flows.

Prevention:
For cinematic or boss flows that must freeze facing, block both the input producer and the aim-driven presentation consumers. Do not assume disabling `PlayerAim2D` prevents all visible Aim-following behavior.

## 2026-05-26 - Accepted Scene Transition Could Still Abort Before Load

Context:
Title intro Space-hold skip could appear to complete but intermittently remain on the title scene.

Cause:
`SceneTransitionCoordinator.TryLoadScene(...)` returned `true` as soon as it started the transition coroutine. If `SceneFadeTransitionService.TryBeginTransitionSession()` then failed inside that coroutine, the coordinator cleared its routine and exited without loading the requested scene, leaving the caller with no retry path.

Fix:
The coordinator now logs the fade-session begin failure and falls back to direct `SceneManager.LoadScene(...)` for the accepted target scene.

Prevention:
Scene transition APIs that report request acceptance must either complete the scene load or provide an explicit fallback/logged failure path after asynchronous setup begins. Do not let a post-acceptance fade/session setup failure silently end the transition.

## 2026-05-28 - Toxic Rush Was Limited By Random Move Bounds

Context:
Slime Queen P2Short toxic rush still stopped before reaching the level edge even after the pattern was changed away from the original short fixed distance.

Cause:
The first fix interpreted "map edge" as the authored `SlimeQueenRandomMoveBounds` area. That added a fourth stop condition to toxic rush and could terminate the rush at the random-move authoring volume instead of the actual wall/end of the level.

Fix:
Toxic rush now builds its segment from a long wall cast using the configured wall layer. Its intended stop conditions are only wall collision, normal HoleTrap fall, or player collision. The center follow-up slam is only tied to the normal HoleTrap fall path.

Prevention:
Do not reuse movement/landing authoring bounds as a combat dash termination rule unless the design explicitly says the pattern is range-limited by that volume. For Slime Queen P2Short toxic rush, keep `SlimeQueenRandomMoveBounds` out of the rush stop condition.

## 2026-05-28 - Drain Groggy Reused Movement Invulnerability

Context:
Slime Queen P2Short trapped in an open drain for the intended 4-second free-damage window did not take player damage.

Cause:
`SlimeQueenPhaseTwoBase.BeginDrainControlLock()` still called `SetPatternMoveDamageBlocked(true)`. That helper is for airborne/rush movement patterns and applies the shared `State.Invulnerable` tag, so `CombatDamageAction` suppressed incoming damage through `CombatInvulnerabilityUtil`.

Fix:
Drain control now aborts the current pattern, locks movement/pitfall handling, and blocks passive contact damage without applying the movement-pattern invulnerable tag. `EndDrainControlLock()` still clears pattern-move damage blocking defensively in case a previous pattern left it set.

Prevention:
Do not use movement-pattern damage blockers for groggy, trap, stun, or free-damage windows. If the target should remain hittable, separate action/movement locking from invulnerability tagging.## 2026-05-26 - Manually Generated Unity Empty Lists Swallowed Following Fields

Context:
The timed common relics `승리의 깃발`, `추격자의 발톱`, and `붉은 계약` showed tooltip output like `[[ ]] [0]` even though the logic assets had visible `attribute`, `displayNameOverride`, and `value` lines in text.

Cause:
The manually generated Unity YAML wrote empty lists as:

```yaml
passiveEntries:
  []
```

Unity did not deserialize the following root fields reliably from that shape, so the ScriptableObject loaded default values for the timed buff fields.

Fix:
Use inline empty-list serialization for manually generated Unity assets:

```yaml
passiveEntries: []
```

Prevention:
When creating or rewriting Unity `.asset` files outside the Editor, verify empty lists, field indentation, and root-field deserialization with `rg` checks. Prefer matching Unity's existing inline `field: []` style for empty serialized arrays/lists.

## 2026-05-26 - Global DamagePayloadConfig Shadowed UnityGAS Type

Context:
`ApprenticeHeroSwordHitConfig` failed compilation with `CS1503` when passing its damage config into `DamageSnapshotBuilder.BuildFromBaseValues(...)`.

Cause:
The project still has both a legacy global `DamagePayloadConfig` and the current `UnityGAS.DamagePayloadConfig`. In a global-namespace source file, unqualified `DamagePayloadConfig` resolved to the legacy global type even though `using UnityGAS;` was present.

Fix:
Apprentice Hero Sword damage config now explicitly uses `UnityGAS.DamagePayloadConfig`.

Prevention:
New weapon damage data that feeds `DamageSnapshotBuilder` should spell `UnityGAS.DamagePayloadConfig` explicitly until the legacy global compatibility type is removed.

## 2026-05-26 - Overlapping Time Freeze Restored Stale Zero

Context:
`Flowering` Bloom cut-in pauses combat time while playing unscaled presentation. If the pause menu or another freeze-owning UI opened during that cut-in, the screen could remain frozen after the presentation ended.

Cause:
Both Bloom cut-in and stack UI freeze paths stored `Time.timeScale` locally and restored their cached value later. When UI opened while Bloom had already set `Time.timeScale = 0`, UI cached `0`; Bloom then restored gameplay to `1`, and closing the UI restored the stale cached `0` back over the resumed value.

Fix:
Bloom cut-in now acquires a `GameFlowInputBlocker` before setting `Time.timeScale = 0`, blocking unrelated pause/new UI entry during the protected cut-in window and releasing it in cleanup. `UIManager` also avoids writing a stale cached zero over a later non-zero restore when a freeze overlap has already been resolved by another owner.

Prevention:
Do not allow independent time-freeze owners to enter freely inside a cinematic/presentation freeze window. Use `GameFlowInputBlocker` for protected flow windows, and when restoring a UI-owned freeze that cached `0`, avoid overwriting a later non-zero `Time.timeScale` restored by the original freeze owner.

## 2026-05-26 - Paused Cut-in Blocked Weapon Animation Event Timing

Context:
`Flowering` Bloom Skill1 should play its weapon animation during the cut-in fade and use an animation event to start the weapon reveal.

Cause:
Bloom cut-in presentation pauses combat by setting `Time.timeScale` to `0`. A normal Animator update mode does not advance under scaled time while paused, so weapon animation events can fail to fire or fire only after time resumes. The existing Skill1 clip also pointed its event at the old SwordSkill2 hit tag, so even a fired event would not identify Flowering reveal timing clearly.

Fix:
The Bloom cut-in path plays the `FloweringBloomData.cutInAnimationTrigger` on the weapon animation channel, temporarily switches the weapon Animator to unscaled time for the cut-in, and restores the previous update mode afterward. The Skill1 clip now sends `Event.Anim.Flowering.WeaponReveal`, and the reveal coroutine waits for that tag with a timeout before starting the SpriteMask reveal.

Prevention:
When presentation pauses `Time.timeScale` but still depends on Animator events, explicitly decide whether that Animator must run on unscaled time for the paused section. Use feature-specific gameplay event tags for animation timing instead of reusing another weapon's hit/event tag.

## 2026-05-26 - Weapon Reveal Replayed After Cut-in Fade

Context:
`Flowering` Bloom weapon reveal should play once during the cut-in fade. In Play Mode it could play once during the fade, then play a second time after the fade when active Bloom state began.

Cause:
`FloweringBloomPresentationController.PlayCutIn(...)` and `BeginActiveBloom(...)` both called `ApplyWeaponBloomSprite(true)`. The first fix made active apply idempotent, but `FloweringRuntimeState.BeginBloom(...)` still called `EndBloom()` before active state setup. That `EndBloom()` released the presentation and restored the weapon sprite immediately after the cut-in reveal completed, so active Bloom entered from an inactive sprite and started reveal again.

Fix:
The active weapon sprite apply path returns early when the Bloom sprite is already applied or a weapon reveal coroutine is still running. `FloweringRuntimeState` now preserves the cut-in presentation across the immediate `BeginBloom(...)` setup reset, while normal Bloom end, cancellation, weapon swap, and owner disable still release presentation state. Reveal coroutine handles are also cleared on early exits, so a failed/empty reveal attempt does not permanently block a later valid apply.

Prevention:
Presentation entry points that can be called from both transition setup and steady-state activation must be idempotent. Do not run full presentation release between a transition reveal and the steady-state that consumes that revealed visual state. Guard effect/reveal starts against already-running coroutines and already-applied visual state before starting a new coroutine.

## 2026-05-25 - Hitbox-Owned Slash Visual Followed Owner

Context:
`Flowering` Bloom attack should leave each BloomSlash visual at the strike position like a short-lived slash mark while its collider turns off early.

Cause:
The BloomSlash hitbox prefabs owned their visuals correctly, but `MeleeHitboxActor.attachToOwnerOnSetup` was enabled. `Setup(...)` parented each hitbox instance to the player while preserving world position, so the lingering visual continued to follow player movement after spawn.

Fix:
Bloom attack now forces `overrideAttachToOwnerOnSetup = true` and `attachToOwnerOnSetup = false` in its spawn context, and the BloomSlash prefabs are authored with `attachToOwnerOnSetup` disabled. The collider still expires through `activeTime`; the visual remains only for its authored animation clip lifetime.

Prevention:
For hitbox-owned slash marks or lingering attack visuals, do not enable owner attachment unless the design explicitly wants the visual to move with the attacker. Prefer world-parented hitbox instances with short collider lifetime and clip-length visual lifetime for strike marks.

## 2026-05-25 - Runtime AttackBase Component Needed Concrete Collider First

Context:
`Flowering` Bloom dash logged a `NullReferenceException` every time dash tried to spawn a slash hitbox.

Cause:
`FloweringDashSlashHitboxActor` derives from `AttackBase`, whose base attribute requires `Collider2D`. `Collider2D` is abstract, so adding the hitbox actor first to an empty runtime GameObject can fail Unity's automatic required-component creation and return a null component before `Setup(...)` is called.

Fix:
The dash slash spawn path now adds a concrete `BoxCollider2D` before adding `FloweringDashSlashHitboxActor`, checks the returned actor before setup, and the actor explicitly declares `RequireComponent(typeof(BoxCollider2D))`.

Prevention:
Runtime-created `AttackBase` subclasses should declare and add their concrete collider type before adding the behavior component. Do not rely on a base `RequireComponent(typeof(Collider2D))` to create a valid collider on an empty runtime GameObject.

## 2026-05-25 - Detached Dash Slash Hitbox Scanned Too Narrowly

Context:
`Flowering` Bloom dash slash marks could appear while the intended slash damage did not apply.

Cause:
The detached dash slash hitbox only scanned once at spawn time and filtered `OverlapBoxAll` by damage layers before resolving `CombatHurtbox2D`. That made the hit path more fragile than the existing melee hitbox path: fast dash timing, trigger update timing, or collider/root layer differences could cause a valid target to be missed.

Fix:
The dash slash hitbox now scans during its active lifetime and accepts a target when either the hit collider layer or resolved damage target root layer matches the authored damage mask. Wall line-of-sight blocking is still checked before damage is applied.

Prevention:
Detached short-lived hitboxes should either reuse the shared melee hitbox actor or match its target resolution tolerance. Avoid spawn-frame-only scans for effects that are meant to remain active across multiple frames.

## 2026-05-25 - Delayed Dash Augment Was Tied To Dash Ability Token

Context:
`Flowering` Bloom dash should create three delayed slash hitboxes and three red slash marks after the dash starts.

Cause:
The delayed slash coroutine checked the dash ability spec cancellation token between slash spawns. A short dash ability can complete or cancel before the delayed visual/hit sequence finishes, so later slash marks and hitboxes can be skipped even though Bloom is still active.

Fix:
Dash slash scheduling now follows Bloom runtime state lifetime and weapon cleanup. Bloom end, weapon swap, owner disable, and transient runtime reset still stop remaining coroutines and destroy temporary hitboxes/effects.

Prevention:
Delayed weapon augment effects that intentionally outlive the triggering global ability should be cancelled by the owning weapon runtime state, not by the trigger ability token, unless the design explicitly says the delayed effect should disappear when that trigger ends.

## 2026-05-25 - Active Fade Service Was Replaced During Scene Load

Context:
Title intro completion should fade to black, load the gameplay scene, then fade the next scene in through the shared `SceneTransitionCoordinator` flow.

Cause:
When the title scene had no authored `SceneFadeTransitionService`, the transition began with a runtime fallback overlay. Loading the next scene awakened an authored `SceneFadeTransitionService` under `GlobalUIRoot`, and the existing singleton replacement policy destroyed the fallback while the coordinator still held it for `FadeInAsync()`. The same trap also applies to any active title-authored transition owner that must survive long enough to finish the fade-in before yielding to the loaded scene's authored service.

Fix:
`SceneFadeTransitionService` now defers replacing an active transition owner with a loaded authored service until `EndTransitionSession()`. The pending authored overlay is reset transparent/inactive while it waits, so it cannot cover the active fade-in. The active owner remains alive for fade-out, load, post-load settle, and fade-in, then promotes the authored service after the transition ends.

Prevention:
Do not replace or destroy a fade service while `IsTransitionActive` is true. Scene-loaded authored services should wait until the current transition owner has completed fade-in before taking over singleton ownership, and any deferred overlay must be visually hidden while pending. Title-origin transitions should prefer a scene-root authored fade service over runtime fallback so transition duration can be tuned in the Inspector.

## 2026-05-25 - Intro Entry Fade Overlapped Slot Panel Close Fade

Context:
Starting a new empty profile slot should show one start-button-to-intro fade before the first intro image appears.

Cause:
`TitleMenuController.BeginIntroLaunch(...)` started `TitleIntroPlayer` and then called `TitleProfileSlotPanelUI.CloseUI()`. The profile slot panel's own fade-out could run under the intro overlay root fade, making the start fade appear to reset and run twice even with no additional input.

Fix:
The intro launch path no longer starts the profile slot panel close animation after intro playback begins. The panel can remain active behind the intro overlay because the overlay blocks raycasts and covers the title UI until scene transition.

Prevention:
For title intro entry, keep exactly one visual fade owner. Do not run panel close fades underneath the intro overlay start fade unless the transition is explicitly sequenced after the overlay has fully covered the UI.

## 2026-05-24 - Intro Entry Fade Was Conflated With First Image Fade

Context:
The title intro slow-start request needed two separately tunable beats: the intro overlay FadeIn immediately after pressing `시작하기`, and the first slide image FadeIn after the overlay is visible.

Cause:
The first pass interpreted "intro starts slowly" as only the first slide image FadeIn duration, so the start-button-to-intro overlay FadeIn had no separate data field.

Fix:
`TitleIntroSequenceSO` now separates `introStartFadeDuration` from `initialImageFadeDuration`. `TitleIntroPlayer` fades the authored intro overlay root first, then starts first-slide image FadeIn together with text typing.

Prevention:
For title/popup presentation timing, distinguish container/root visibility transitions from content-specific media transitions. Do not use a slide image fade field to represent the screen or overlay entry fade.

## 2026-05-23 - Intro Text Was Coupled To Image Fade

Context:
The title intro design keeps slide text visible while images fade in or out. Text typing starts together with the new image fade-in, and completed text remains visible during old-image fade-out.

Cause:
`TitleIntroPlayer` treated image fade-in, text typing, wait, image fade-out, and text clear as separate sequential steps. That made text disappear as part of image transition cleanup instead of letting text lifetime follow the script timing.

Fix:
`TitleIntroPlayer` now runs text typing and image fade-in as one input-polled phase, and image fade-out no longer clears the completed text. The next slide replaces text only when its own typing starts.

Prevention:
For staged narrative UI, define text lifetime separately from media transition lifetime. Do not clear narrative text from image fade cleanup unless the design explicitly says the text should fade or disappear with the image.

## 2026-05-23 - Scene-Local UI Replaced Persistent Global Panel

Context:
After moving from lobby to corridor, pause still opened but the settings panel could fail to open. The global UI root was intended to persist as the UI source of truth.

Cause:
`SettingsPanelUI` and `KeyBindingPanelUI` had their own singleton replacement policy. A scene-local panel could destroy the existing persistent instance, then later be destroyed together with a duplicate scene-local `GlobalUIRoot`, leaving no usable global panel.

Fix:
The child panels now destroy only themselves when another valid instance already exists. `EnsureInstance()` remains a lookup/search path, while persistent ownership stays with `GlobalUIRoot` and `UIManager`.

Prevention:
Do not let global UI child panels delete an existing persistent representative to win a static instance slot. Scene-local duplicates should defer to the root/UI manager ownership policy, and selected-button/EventSystem visual policy should be handled separately.

## 2026-05-23 - Intro Skip Prompt Overwrote Authored Visuals

Context:
The title intro skip prompt should be scene-authored so its icon, text, fill color, and custom art can be tuned in Unity. `TitleIntroView.Show()` still forced the skip prompt glyph/label to Space at runtime.

Cause:
The view treated skip prompt display as runtime-owned presentation instead of only projecting the authored root/fill state. This made custom prompt visuals fragile because show-time setup could overwrite authored icon/text/color references.

Fix:
`TitleIntroView` now keeps authored skip prompt icon/text/fill color untouched by default. Runtime glyph and fill color application are available only through serialized opt-ins or explicit projection calls.

Prevention:
For scene-authored UI prompts, runtime view code should activate/reset/progress the authored objects without replacing their icon/text/color content unless an explicit serialized opt-in says the prompt value is data-driven.

## 2026-05-23 - Intro Advance Input Skipped Multiple Phases

Context:
Title intro text advance should complete the currently typing text, then still wait for the configured post-text delay before image fade-out. A click or short Space release during typing could immediately push the intro into the fade-out phase.

Cause:
`TitleIntroPlayer` polled raw advance input separately in typing, wait, and fade coroutines. When a typing coroutine consumed an advance input and ended, the next coroutine could poll the same frame's input event again and treat it as a wait/fade skip.

Fix:
`TitleIntroPlayer` now records the frame that consumed an advance input. Later phases ignore click or short Space advance events already consumed in that frame, while Space hold skip remains independent.

Prevention:
For staged presentation flows, consume physical input events at the flow-owner level before crossing coroutine/state boundaries. One click or key release should not be able to complete typing and skip the following wait/fade phase in the same frame.

## 2026-05-23 - Locked Slot Art Was Written To Item Icon

Context:
Inventory locked-slot presentation needed `InventorySlotLock` to appear as the slot background, with the item icon layer empty. The locked slot still changed the serialized `icon` / `itemImage` sprite to `InventorySlotLock`.

Cause:
`ItemSlotUI.RefreshLockedSlot()` reused the item icon rendering path through `ItemDisplayIconUtility.ApplyRaw(icon, lockedSlotSprite, ...)`, so the lock art was treated as an item icon instead of slot background presentation.

Fix:
`ItemSlotUI` now writes `lockedSlotSprite` only to the serialized `backgroundImage` reference. Locked slots clear item icon/level content first, and missing `backgroundImage` wiring logs a warning instead of falling back to the icon image.

Prevention:
Do not use `icon`, `itemImage`, or item display helpers for locked-slot frame art. Locked slot visuals belong to the authored slot background layer; item icon images should only display actual item icons or remain empty.

## 2026-05-23 - Legacy Button Interactable Blocked HoldActionButton

Context:
Chest reroll prefab wiring had `HoldActionButton`, `HoldFillButtonView`, and `RerollHoldProgress` connected correctly, but Space hold still did not start reliably.

Cause:
`HoldActionButton` was intended to own hold availability, but `CanUse()` still checked the optional companion Unity `Button.IsInteractable()`. A legacy `Button` with `interactable = false` could therefore block the shared hold input even when `ChestScreen` set `HoldActionButton` itself usable.

Fix:
`HoldActionButton.CanUse()` now uses its own `interactable` state as the hold gate. The optional Unity `Button` can still mirror visual/interactable state for compatibility, but it no longer decides whether a hold can start.

Prevention:
For hold-only controls, do not reintroduce Unity `Button.IsInteractable()` as an input gate. Feature screens should call `HoldActionButton.SetInteractable(...)`, and Unity `Button` should remain optional legacy compatibility only.

## 2026-05-23 - UI Submit Did Not Start Hold Action

Context:
Chest reroll Space input changed the selected Unity `Button` to its pressed color, but the shared hold action did not start.

Cause:
Unity UI Submit can drive `Button` pressed visuals independently from a custom hold component. `HoldActionButton` only relied on its own key polling, so a selected button could visually react to Space without routing that Submit event into the hold flow.

Fix:
`HoldActionButton` now implements `ISubmitHandler` and bridges selected UI Submit input into the same keyboard hold path, while still verifying that the Submit action's active control matches the configured `holdKey`.

Prevention:
For hold buttons that can be selected by the EventSystem, bridge UI Submit into the hold component instead of assuming `Button` pressed visuals imply the hold action has started.

## 2026-05-23 - Keyboard Hold Missed The Usable Window

Context:
Chest reroll needed Space hold to work through `HoldActionButton`, but pressing Space could fail to start the hold.

Cause:
`HoldActionButton` only started keyboard hold on the key-down frame. If Space was already held while the chest reveal was finishing, or while the reroll button was still non-interactable, the original key-down frame was missed and the hold never started after the button became usable.

Fix:
`HoldActionButton` can now start keyboard hold while the configured key is already pressed and the button becomes usable. Keyboard completion blocks restart until the key is released, so one long press cannot repeatedly complete the action.

Prevention:
For keyboard hold buttons inside animated or gated UI, support "key already held when usable" behavior and separately block repeated completion until release.

## 2026-05-23 - Filled Image And Clip Width Double-Applied Hold Progress

Context:
Chest reroll hold fill was authored with a filled `Image` and the same `RectTransform` assigned as the clip/fill root.

Cause:
`HoldFillButtonView` drove both `Image.fillAmount` and the same rect width by the same progress value. This effectively applied progress twice, so early hold progress could be too small to notice.

Fix:
`HoldFillButtonView` now skips clip-width resizing when the fill `Image` is already a filled Image on the same `RectTransform`. It also reapplies progress on enable and avoids deactivating its own GameObject when the clip root is accidentally assigned to the owner.

Prevention:
For hold-fill buttons, either drive a filled Image or drive a separate clip-root width. Do not double-drive the same fill geometry with both mechanisms.

## 2026-05-23 - Duplicate Hold Progress Owners Hid Authored Fill UI

Context:
Chest reroll was being tested with the shared hold-fill button components, but the fill was not visibly progressing.

Cause:
`ChestScreen` still owned direct mouse/Space hold detection and progress calculation while an authored `HoldActionButton`/`HoldFillButtonView` could also be present on the same reroll button. This left two possible owners for the same progress state and made it easy for the shared fill view to be unconnected or reset by the legacy path.

Fix:
`HoldActionButton` now owns pointer/keyboard hold timing and progress, while `ChestScreen` subscribes to hold events and only owns reroll eligibility, chest close/shake/open presentation, and refresh execution.

Prevention:
Do not let feature screens and shared hold-button components both calculate the same hold progress. If a `HoldActionButton` is authored for a button, feature code should consume its events instead of duplicating pointer/key hold detection.

## 2026-05-25 - P2 Drain Completion Was Treated As Permanent Blocking

Context:
After a phase-two Slime Queen sank into a broken drain and resurfaced, the same drain could no longer receive hit damage.

Cause:
The phrase "block/close the drain" was implemented as a permanent `DrainPipe.isBlocked` state. `TryApplyDamage(...)` returned false while blocked, so the drain became unusable instead of returning to its original cork state.

Fix:
`DrainPipe` no longer has a permanent blocked state for P2 boss drain completion. After the 4-second drain sequence, it restores the boss, resets the pipe to unbroken cork visual state, clears the hit count to `0`, and remains damageable.

Prevention:
For this mechanic, "close/restore the drain" means reset to the initial damageable drain state. Do not model P2 drain completion as a permanent disable unless the design explicitly changes.

## 2026-05-25 - Slime Queen Groggy Gauge Was Not Wired

Context:
`SlimeQueen`, `SlimeQueenP2Short`, and `SlimeQueenP2Long` did not expose the same groggy/stagger gauge behavior as the other bosses.

Cause:
The Slime Queen prefabs did not author a `StaggerGaugeSystem`, and the Slime Queen phase-two HUD special case explicitly hid the shared groggy bar while showing dual health bars. That meant phase-one had no stagger target component for `CombatDamageAction`, and phase-two had no visible groggy gauge even after the combat component existed.

Fix:
`SlimeQueenBossBase` now ensures a runtime `StaggerGaugeSystem` for every Slime Queen variant and wires it to the existing stagger attributes plus a 3-second status-only Groggy effect. Phase-two Slime Queen Short/Long bodies now use the same boss HUD registration path as other bosses and appear as separate HUD slots.

Prevention:
When adding a new boss or special multi-body HUD path, verify both sides of groggy support: the combat target must have a configured `StaggerGaugeSystem`, and each active boss body must register its own HUD slot instead of hiding the groggy view or adding concrete boss branches to `BossHudController`.

## 2026-05-25 - Drain Control Lock Stopped Drain Pull

Context:
P2 Slime Queens stopped as soon as they entered the open drain suction radius instead of being pulled into the drain.

Cause:
The drain acquisition path called `BeginDrainControlLock()`, which set the shared pit-fall runtime lock. `SlimeQueenBossBase.Update()` responds to that lock by calling `movementMotor.StopAllMotion()` every frame, and `MovementMotor2D.FixedUpdate()` can also overwrite direct Rigidbody velocity with zero. This fought the `DrainPipe` suction velocity.

Fix:
`SlimeQueenPhaseTwoBase.Update()` now returns immediately while drain-locked, before the shared pit-fall stop path runs. `DrainPipe` creates a drain context at acquisition time, disables the boss `MovementMotor2D` for the suction/submerged lifecycle, pulls the boss by direct `Rigidbody2D.MovePosition`, and restores the motor when the boss leaves the drain.

Prevention:
When an environmental gimmick owns a forced movement phase, do not rely on Rigidbody velocity while the target's normal movement motor is still active. Explicitly transfer movement ownership for the whole forced phase and restore it in the same lifecycle context.

## 2026-05-25 - Drain Boss Suction Reused Pawn Radius

Context:
Slime Queen P2 bosses could become unable to act immediately after spawning when a drain was already open, and the first fix changed P2 drain acquisition to direct trigger contact only.

Cause:
P2 boss drain acquisition originally reused `DrainPipe.suctionRadius`, which is intentionally broad for Pawn slime cleanup. That radius was too large for phase-two boss spawn safety, while the trigger-only workaround removed the planned "enter drain suction range and get pulled in" behavior.

Fix:
`DrainPipe` now has a separate Inspector field, `phaseTwoBossSuctionRadius`, for P2 Slime Queen acquisition. Pawn slime suction still uses `suctionRadius`; P2Short/P2Long acquisition uses the new radius, then the existing drain lock/submerge/restore flow. Selected `DrainPipe` gizmos draw both ranges separately.

Prevention:
Do not reuse a broad mob cleanup radius for a boss gimmick acquisition rule when spawn safety and gimmick proximity need different tuning. Split the serialized authoring fields and show both authoring ranges in gizmos.

## 2026-05-22 - Detached Telegraph Cleanup Was Owned Only By Caster Coroutine

Context:
SlimeQueenP2Short body inflate could leave its circular warning blinking in the scene when the boss died during the warning window.

Cause:
The warning was created as a detached `AttackTelegraphView`, while its timed destroy coroutine ran on the caster's `AttackTelegraphService`. If the caster was destroyed before the coroutine completed, the detached warning had no remaining owner-held cleanup path.

Fix:
Body inflate warning views are now retained by the Slime Queen body-inflate host and cleared from ability `finally`, pattern end/abort, disable, and destroy paths.

Prevention:
Detached telegraph or presentation views must either be self-owned for their lifetime or retained by the gameplay owner that can clear them during cancel, death, disable, and destroy cleanup. Do not rely only on a coroutine hosted by an object that may die before the detached view.

## 2026-05-19 - Affection Reward UI Waited Behind Dialogue Blocker

Context:
Dialogue could stop after affection gain when the affection change unlocked a reward.

Cause:
`AffectionRewardProcessor` passed the dialogue continuation callback into `RewardDisplayService.ShowReward(...)`. During dialogue, `DialogueService` owns an external UI input blocker, so `RewardDisplayService` could not open `RewardDisplayUI` and left the request queued. Dialogue was waiting for the reward close callback, while the reward UI was waiting for dialogue's blocker to release.

Fix:
When an affection reward is earned while external UI input is blocked, the reward display request is queued without the dialogue continuation callback and the dialogue callback is invoked immediately. `RewardDisplayService` now retries queued reward presentation once UI opening is allowed again.

Prevention:
Do not make a UI popup close callback the only continuation path while the popup itself is blocked by the current flow's input blocker. Queue the popup separately or use an explicit owner handoff.

## 2026-05-19 - Affection Tween Kill Skipped Dialogue Continuation

Context:
Dialogue could stop advancing after the affection gain presentation.

Cause:
`AffectionUI.PlayGainAnimation(...)` forwarded the dialogue continuation only from the DOTween sequence `OnComplete`. If the UI was disabled, destroyed, or a new affection animation killed the existing sequence before completion, DOTween did not run that completion callback and the dialogue tag flow stayed waiting.

Fix:
`AffectionUI` now tracks the active gain sequence and pending continuation callback. `OnComplete`, `OnKill`, `OnDisable`, and replacement animation paths all converge through a single completion helper that snaps the UI to the final affection value and invokes the pending callback once.

Prevention:
Dialogue-blocking presentation code must not rely only on tween `OnComplete`. Any presentation that gates Ink/dialogue continuation needs an interruption path that invokes the continuation exactly once.

## 2026-05-19 - World Drop Sprite Hidden By Mask Interaction

Context:
Weapon replacement created the dropped weapon object correctly, and its SpriteRenderer, sprite, material, and color alpha looked valid, but the dropped weapon sprite was not visible in the world.

Cause:
`DroppedWeapon.prefab` had its active weapon image SpriteRenderer authored with `SpriteMaskInteraction.VisibleInsideMask` and the Default sorting layer. Without a matching SpriteMask, the renderer can be fully hidden even when the component is enabled and alpha is 1. Default sorting also made the drop less consistent with other world pickups.

Fix:
`DroppedWeapon.prefab` now disables the unused root SpriteRenderer, clears SpriteMask interaction on the actual `WeaponImage` renderer, and moves that renderer to the Entity sorting layer used by generic world pickups.

Prevention:
For world pickup and drop prefabs, check `SpriteRenderer.maskInteraction` and sorting layer before assuming material, sprite assignment, or alpha is the visibility cause. World pickup visuals should normally use no SpriteMask interaction unless a visible SpriteMask is intentionally authored with the prefab.

## 2026-05-19 - Runtime SampleAnimation Did Not Drive Laser Clip Playback

Context:
DemonKing EgoSword laser VFX spawned, but the visible SpriteRenderers stayed on their initial prefab sprites instead of playing the authored Start/Idle/End clips.

Cause:
The VFX tried to use code-driven `AnimationClip.SampleAnimation(...)` instead of real Animator components. The prefab had no Animator Controller bound to the Start/Body renderers, and later edits also left a stale manual sampling call in the Idle phase.

Fix:
`DemonKingEgoLaserVfx` now controls playback by calling `Animator.Play(...)` for `Start`, `Idle`, and `End` states. The VFX prefab has separate Animator components on the Start and Body SpriteRenderer objects, and those Animators reference dedicated Start/Body controllers using the authored AnimationClip assets. Idle clips are marked looping.

Prevention:
For sprite clips authored as Unity AnimationClips, prefer an Animator Controller on the GameObject that owns the animated SpriteRenderer. Keep gameplay timing code-driven when needed, but let Animator own frame binding and clip playback.

## 2026-05-19 - Transparent Beam Sprite Bounds Made Laser Look Tiny

Context:
DemonKing EgoSword laser VFX used a sliced 64px-high sprite sheet where the visible beam occupied only a small strip inside a mostly transparent frame.

Cause:
The first runtime VFX implementation sized the tiled `SpriteRenderer` directly to gameplay `laserWidth`. That scaled the full transparent sprite rect down to the hitbox width, so the actual visible beam pixels became much thinner than the intended attack width and made the Body segment look missing.

Fix:
`DemonKingEgoLaserVfx` now separates ray length from visual thickness. Body uses `SpriteRenderer.size.x` for tile length, keeps source sprite height for `size.y`, and applies Y transform scale from a serialized `sourceBeamHeightUnits` value. Start uses the same visual scale. The VFX drives the authored AnimationClip assets through Animator Controllers instead of manually stepping sprite arrays.

Prevention:
For beam/ribbon sprites with transparent padding, do not treat the sprite rect height as the visible beam height. Author or serialize the visible beam height separately, and scale only the visual axis while preserving tile size on the repeat axis.

## 2026-05-18 - Attached Target VFX Used Root Transform Instead Of Visual Bounds

Context:
Electric electrocute status particles and discharge snap/trail points needed to appear around the monster body center and scale across small mobs, large mobs, and scaled dummy targets.

Cause:
The presentation context fell back to `target.transform.position`, and attached visuals kept a fixed world scale. Several enemy roots are authored at the base or have collider/sprite offsets, so root position and fixed scale did not match the visible monster sprite.

Fix:
`SpawnedPresentationHook` now has opt-in sprite-bounds anchor and uniform scale modes. Electric electrocute status particles use `SpriteRenderer.bounds.center` and `SpriteRenderer.bounds.size`, and Electric discharge visual points use the same sprite-bounds center.

Prevention:
Target-attached body VFX should not assume the target root transform is the visual body center. For presentation that must cover the rendered unit, use an explicit visual anchor policy such as sprite bounds, and leave root-position spawning as the compatibility default.

## 2026-05-18 - Manual WhileActive Presentation Had No Release Handle

Context:
Electric electrocute needed a looping particle visual that attaches to each debuffed monster and stays alive until the status ends.

Cause:
`GameplayEffect.presentationWhileActive` previously forwarded spawned visuals through one-shot presentation playback only. Looping ParticleSystems with auto-detected lifetime could auto-release too early, while `ManualRelease` visuals had no retained handle for the effect removal path to release.

Fix:
`GameplayEffectPresentationRouter` now stores handles for ManualRelease while-active visuals, spawns them through `PresentationSpawnService.SpawnPersistent(...)`, and releases them from `RemoveWhileActive(...)`. Auto-release while-active visuals still use the existing merged presentation path.

Prevention:
Any sustained `GameplayEffect` visual with `ManualRelease` must have an owner-held handle and an explicit release call on phase/effect exit. Do not rely on one-shot auto lifetime for looping status particles.

## 2026-05-18 - Destroy Cleanup Recreated Runtime Service

Context:
Stopping Play or closing a scene logged `Cannot set the parent of the GameObject 'PresentationAssetProvider' while its new parent '[RuntimeServices]' is being destroyed`, followed by a leaked `PresentationAssetProvider` scene object warning.

Cause:
`PresentationPreloadService.OnDestroy()` tried to release active manifests without creating a provider, but `ApplyManifest(...)` and `ApplyRouteSetManifest(...)` fell back to `PresentationAssetProvider.CurrentProvider`. That property creates a provider when none exists, so cleanup code could spawn a new runtime service while `[RuntimeServices]` was already being destroyed.

Fix:
Make `PresentationPreloadService.ReleaseAllActiveManifests(...)` pass a no-provider-creation mode into manifest apply helpers. Cleanup can still clear active manifest references and record completed operations, but it no longer creates `PresentationAssetProvider` during destruction.

Prevention:
`OnDestroy`, scene-close, and application-quit cleanup paths must use non-creating service lookups. Avoid properties or helpers named like `Current...` if they have `EnsureInstance()` fallback behavior.

## 2026-05-18 - Editor Preview Mutated Prefab Asset

Context:
Upgrade UI lake preview restoration ran during assembly reload and play-mode state changes, then Unity Inspector repeatedly failed with `EditorStyles.toolbarButtonRight` errors.

Cause:
`Resources.FindObjectsOfTypeAll<UpgradeTreeUI>()` returned `UpgradeTreeUI` components that live in Prefab Assets. The editor preview restore path called `UpgradeLakePresentation.Initialize(...)`, which attempted to create and parent a generated `LakeSurface` child under a Prefab Asset transform.

Fix:
Skip persistent Prefab Asset objects, unloaded scenes, and Prefab Stage objects in the upgrade lake editor preview loop and in `UpgradeTreeUI` editor preview/restore methods. Keep automatic material restoration no-create/no-initialize so assembly reload and play-mode transitions do not call `UpgradeLakePresentation.Initialize(...)`. Later follow-up removed the Upgrade edit-mode LakePreview path entirely: the custom `UpgradeTreeUI` Inspector was deleted, `UpgradeLakePresentation` no longer uses `ExecuteAlways`, and editor preview methods were removed.

Prevention:
Editor preview/update loops that use `Resources.FindObjectsOfTypeAll` must filter out persistent assets, unloaded scene objects, and Prefab Stage contents before mutating transforms, components, materials, or generated children. Automatic cleanup/restoration handlers for assembly reload, play-mode transitions, and `OnDisable` should restore only existing state; they must not create helper components or generated children. Do not reintroduce Upgrade LakePreview without a separate, no-scene-mutation editor design.

## 2026-05-06 - CurrentTask Drift

Context:
Feature implementation continued while `Docs/CurrentTask.md` still described the old project memory system task.

Cause:
The document was treated as required reading but not as an actively maintained task contract.

Fix:
Update `Docs/CurrentTask.md` at the start of each active task change, and keep detailed progress in `Docs/SessionLogs/`.

Prevention:
Before implementation, confirm `CurrentTask.md` matches the user's current requested work. If it does not, update it first.

## 2026-05-14 - Chest World/UI Presentation Timing Conflation

Context:
While fixing chest first-open input blocking, the world `TreasureChest` open presentation lifetime was used to extend the wait before the chest UI opened.

Cause:
The GameObject open presentation and the chest UI first-open reveal were treated as one timing chain, even though the UI reveal timing is authored separately.

Fix:
Restore `TreasureChest` open-to-UI timing to its existing animator/fallback behavior, and keep input blocking inside the chest UI reveal presentation.

Prevention:
Do not use world object particle/effect lifetime to time chest UI reveal opening. When the issue is "UI reveal blocking", start and end blockers from the UI presentation owner.

## 2026-05-14 - Chest First-Open Blocker Gap

Context:
The chest UI reveal was blocked correctly, but the gap between `TreasureChest` GameObject interaction/open prelude and the later UI reveal still allowed V inventory and ESC pause input.

Cause:
The GameObject open presentation and UI reveal were kept separate for timing, but input blocking was also scoped only to the UI reveal owner.

Fix:
Acquire an external UI input blocker from `TreasureChest` immediately on first-open interaction, allow that owner to open only its intended chest UI, then hand off to the inventory/chest UI reveal blocker.

Prevention:
For first-open chest behavior, keep world and UI presentation timing separate, but treat them as one input-blocking sequence with explicit ownership handoff.

## 2026-05-14 - NPC Feature UI Opened Before Dialogue Blocker Release

Context:
After moving dialogue input blocking into `DialogueService` through `GameFlowInputBlocker`, the Upgrade NPC feature stopped opening its popup.
Later, the same dialogue-exit wait could leave a short player-control gap before the Upgrade UI open flow took over.

Cause:
`UpgradeFeature.Execute()` called `UpgradeManager.ToggleUI()` before requesting dialogue exit. The dialogue blocker was still active, so `UpgradeManager.OpenUI()` failed the `UIManager.CanOpenUI(...)` gate before its own open-presentation blocker could take ownership.
The later wait-for-release fix did not add a control-only handoff lock for the period between feature selection and `UpgradeManager.ToggleUI()`.

Fix:
Request dialogue exit first, then wait until dialogue playback and external UI input blockers are released before opening Upgrade UI.
During that wait, `UpgradeFeature` now acquires the existing `TS_BlockControlByUI` tag set through `PlayerUIControlLockBridge`, then releases it after `ToggleUI()` starts the normal UI open flow.

Prevention:
NPC features that open stack UI after dialogue should not open the UI while dialogue is still the active game-flow blocker. End or hand off the dialogue flow first, then open the feature UI.
If the feature must wait before opening, cover the wait with player-control-only handoff blocking. Do not use a new external UI input blocker for that wait unless the later owned-open exception is explicitly threaded through the same owner.

## 2026-05-16 - Shared Portal Prefab Carried Start-Run Semantics

Context:
After boss battle-end portals began using the common `BossBattleEndPrefabCatalogSO.portalPrefab`, runtime-spawned boss exit portals could not move to the next corridor.

Cause:
The shared `ScenePortal.prefab` carried `TransitionType.HubToRunStart` and a `RunRouteCatalogSO`. Boss exit portal instances inherited that start-run semantic instead of letting `PortalRouteManager` resolve `BossToCorridor` or `ReturnToHubAfterRun` from the active route.

Fix:
Keep shared portal prefabs semantic-neutral (`TransitionType.None`, no catalog), and put `HubToRunStart` plus `RunRouteCatalogSO` only on hub start portal scene instances. Boss battle-end now uses authored portal instances rather than catalog portal prefab creation.

Prevention:
Do not put route-start, boss-exit, or scene-specific transition semantics on a shared portal prefab that is used by multiple travel contexts. Author the semantic at the scene instance or through the active route resolver.

## 2026-05-17 - Boss BattleEnd Position Was Solved In Code Instead Of Authoring

Context:
Boss reward chest and exit portal placement needed fixed final positions after boss death.

Cause:
The implementation tried to solve fixed placement through runtime chest/portal spawning and anchor world-position capture. That kept placement policy in code and still depended on where helper anchors lived.

Fix:
Make chest and portal authored inactive scene objects. `BossBattleEndHandler` initializes/activates the authored chest and activates the authored portal. The object transforms are the placement source of truth.

Prevention:
For boss battle-end chest/portal placement, do not add spawn offsets, boss-position fallback, anchor capture, runtime prefab creation, or boss-prefab-owned placement components. Author the final objects in Unity and wire a scene `BossBattleEndHandler`.

## 2026-05-17 - Boss Physical Drops Were Removed With Placement Cleanup

Context:
While simplifying boss BattleEnd so chest and portal use authored inactive scene objects, the runtime magic stone and field-heal pickup spawning path was also removed.

Cause:
The cleanup treated every BattleEnd-created object as the same placement problem. Chest and portal are fixed authored objects, but magic stones and field heals are variable-count physical drops driven by `StageLootTable` and runtime modifiers.

Fix:
Keep chest and portal activation-only, but spawn boss magic stones and field heals as runtime physical pickups from the boss death position.

Prevention:
When removing runtime placement helpers, separate fixed authored result objects from variable-count physical rewards. Do not move magic stones or field heals into chest loot, and do not use authored chest/portal placement as the pickup origin unless the reward policy explicitly changes.

Follow-up:
Final-route chest suppression must follow the same split. Skipping the authored final-boss chest must not skip boss physical drops; only the chest activation/contents path is suppressed.

## 2026-05-17 - Player-Attached Objects Were Treated As Player Targets

Context:
Boss movement could react to a player-owned orbiting feather object even when the actual player was stationary.

Cause:
Shared enemy target acquisition and direct target assignment accepted child transforms as movement targets. Player-attached relic/effect colliders could therefore influence the target center used by boss movement.

Fix:
For `Player` targets, shared `Enemy` target acquisition and target assignment now resolve through `PlayerRuntimeRegistry`/`PlayerInteractor2D` and map player-owned child colliders/transforms back to the player root.

Prevention:
Player-attached objects should not carry unintended `Player` tags, hurtboxes, or body-blocking colliders. Movement targeting, damage targeting, and physics collision authoring must be reviewed separately for new player-attached relic/effect objects.

## 2026-05-17 - Shop Layout Duplicated Live Slot Objects

Context:
Shop maintenance was difficult because multiple scene `ShopSlot` objects were copied into layouts directly.

Cause:
The scene layout owned both slot positions and live slot behavior instances. A presentation change to `ShopSlot` could require duplicated scene updates.

Fix:
`MerchantNPC` can now use authored slot anchors plus a `ShopSlot` prefab reference, instantiate/reuse slots at runtime, and apply per-anchor stock filters.

Prevention:
For new shop layouts, author anchor transforms and slot filters instead of duplicating live `ShopSlot` objects. Keep the slot prefab as the central presentation/interaction source.

## 2026-05-17 - Locked Upgrade Nodes Could Not Explain Failure

Context:
Locked upgrade nodes had no click feedback for why the upgrade could not be purchased.

Cause:
`UpgradeSlotUI` disabled the button for locked nodes, so the click never reached `UpgradeManager.TryBuyUpgrade(...)` and the purchase failure reason could not be mapped to a warning popup.

Fix:
Locked upgrade buttons remain interactable while keeping the locked visual state. `UpgradeManager` maps purchase failure reasons to shared warning popup codes.

Prevention:
If a disabled-looking UI element needs to explain failure, keep a click path to the shared warning/tooltip system or add a separate explicit explanation trigger. Do not rely on `Button.interactable = false` when the user needs feedback.

## 2026-05-17 - Legacy Upgrade Panels Kept Horizontal Scrollbar State

Context:
The current upgrade panel prefab moved to overflow arrow navigation, but older scene instances still carried a serialized `ScrollRect.horizontalScrollbar` reference and a `Scrollbar Horizontal` object.

Cause:
The prefab was updated, but scene-level prefab overrides and old missing-prefab instances can preserve obsolete serialized UI references outside the representative prefab.

Fix:
`UpgradeTreeUI.ConfigureScrollRect()` now disables any legacy horizontal scrollbar before clearing the reference. `Tools/Validation/Scene Setup Validator` now checks inactive upgrade panels, reports missing overflow arrows, reports stale horizontal scrollbar references, and Auto Fix can detach/disable the stale scrollbar.

Prevention:
When replacing UI navigation authoring, validate both the representative prefab and scene instances. Do not treat a prefab-only serialized check as proof that old scene overrides were migrated.

## 2026-05-17 - Attached Particle Transform Did Not Guarantee Attached Simulation

Context:
Player heal recovery spawned `HealParticle` as a child of the player, but the visible particles stayed near the spawn position while the player moved.

Cause:
The implementation parented the spawned ParticleSystem transform but did not force attached heal playback to use local particle simulation before replaying the particle systems. For player-following presentation, Transform parenting alone is not enough if the ParticleSystem simulates emitted particles in world space.

Fix:
`PlayerHealParticlePlayback` now sets each spawned ParticleSystem `main.simulationSpace` to `ParticleSystemSimulationSpace.Local` before clearing and replaying it.

Prevention:
For VFX that must follow an owner after spawn, verify both the spawned transform parent and ParticleSystem simulation space. Parent-attached one-shot particles should set or author Local simulation unless world-trailing particles are explicitly desired.

## 2026-05-19 - Boss Encounter Presentation Did Not Own UI Input Blocking

Context:
ESC could still open pause/UI during boss encounter presentation windows, even though dialogue playback itself was blocked correctly.

Cause:
`DialogueService` owned the dialogue-only input block, while `BossEncounterDirector` and legacy `BossTalkManager` owned camera focus, transition wait, player cinematic protection, and timer pause without also owning a UI input blocker for the non-dialogue encounter windows.

Fix:
Boss encounter sequence owners now acquire a `GameFlowInputBlocker` for the full encounter presentation and release it on normal handoff, setup failure, disable, and coroutine interruption cleanup paths.

Prevention:
When a flow spans both dialogue and non-dialogue presentation, do not assume the dialogue blocker covers the whole flow. The outer flow owner must acquire its own `GameFlowInputBlocker` for camera/transition/handoff windows where unrelated ESC or UI opens must stay blocked.

## 2026-05-19 - World Text Readout Was Authored Without Canvas

Context:
The lobby training dummy damage record text did not appear during play review.

Cause:
The readout was authored as a 3D `TextMeshPro` MeshRenderer child using a `RectTransform` but no Canvas. Unity normalized the child RectTransform to the dummy center, and the text was not reliably visible as the intended above-dummy UI.

Fix:
`TrainingDummy.prefab` now owns a prefab-authored world-space Canvas with `TextMeshProUGUI`, and `TrainingDummyDamageReadout2D` targets `TMP_Text` so it can drive either UGUI or world TMP text safely.

Prevention:
For prefab-authored world UI labels, prefer a world-space Canvas plus `TextMeshProUGUI`, or use a plain Transform with 3D TMP deliberately. Do not rely on RectTransform positioning without a Canvas when the label must appear at a precise world offset.

## 2026-05-28 - Boss Reward Chest Appeared Missing From Duplicate Scene Instance Or Loot Exception

Context:
`HeoMinSeok_Boss` reward review showed the portal path running while the chest appeared not to activate.

Cause:
`HeoMinSeok_Boss` is not a final-boss scene, so final-route chest suppression should not explain direct scene testing there. The scene contains more than one `TreasureChest` instance; the canonical `BossEncounterEndDirector.treasureChest` reference points to `TreasureChest (1)`, so watching a different chest object can look like activation failed. The reward chest activation path also generated boss chest loot before calling `SetActive(true)`, so a loot-generation exception could leave the chest inactive while the later portal path still ran.

Fix:
Keep the final-route chest skip policy separate from portal activation, but do not apply it as the default diagnosis for `HeoMinSeok_Boss` direct tests. `BossRewardSpawnService` now activates the referenced chest before boss loot generation, so a later loot exception does not keep the reward object hidden. For setup/debugging, validate the active reward owner reference and inspect the referenced scene object, not any duplicate chest instance with a similar name.

Prevention:
When a boss reward chest appears inactive, first confirm whether the tested scene should actually be final-route suppressed. For non-final direct scene tests, confirm which `TreasureChest` object is wired into `BossEncounterEndDirector` or the active `BossBattleEndHandler`, then check the Unity Console for `[BossBattleEnd] ActivateTreasureChest failed.` Remove or clearly rename duplicate authored chest instances during scene authoring so the reward owner reference is unambiguous.

## 2026-05-29 - Portal Presentation Blocker Tags Were Captured Into Runtime State

Context:
After using a `ScenePortal`, player aim could remain fixed in the destination scene.

Cause:
`ScenePortal` released `PlayerCinematicProtection` before `ScenePortalTravelService.TryTravel(...)`, but kept `GameFlowInputBlocker` until after the travel request returned. `TryTravel(...)` captures `PlayerRuntimeState` before it starts the scene transition, and `GameFlowInputBlocker` applies `TS_BlockControlByUI` through `UIManager`, including `State.Aim.Blocked`. That temporary UI block tag could be saved and restored, leaving `PlayerAim2D` blocked after the portal.

Fix:
`ScenePortal` now releases both `PlayerCinematicProtection` and `GameFlowInputBlocker` before calling `TryTravel(...)`.

Prevention:
Any pre-travel presentation lock that writes gameplay tags must be released before the runtime capture boundary. Holding an input blocker "until travel accepted" is unsafe when acceptance performs capture synchronously.

## 2026-05-30 - One-Shot Animation Guard Blocked DemonKing Multi-Hit Motions

Context:
DarkLord body clips needed replay protection because Groggy, GroggyCounter, and Slash could restart or oscillate inside one pattern. After adding a pattern-level one-shot guard, DashStab no longer matched the intended three-hit PierceCombo rhythm.

Cause:
The guard was applied as a broad animation rule instead of a pattern-semantics rule. Single-commit clips and per-hit clips have different replay needs even when both are controlled by `Animator.Play(...)`.

Fix:
DashStab and multi-rush body starts use normal per-hit playback, while Slash and GroggyCounter keep one-shot start guards plus last-frame hold sampling. GroggyRecoverCounter now stays in Groggy through the eye flash, plays GroggyCounter for the impact, spawns state-specific VFX (`DarkLordGroggyReleaseVfx` for sword-held counters and `DemonKingImpactVfx` for hand-state counters), then restores idle after the impact hold.

Prevention:
Before applying a once-per-pattern animation guard, classify whether the state is a single visual commit, a frame sample/hold, or a repeated per-hit state. Also lock DemonKing auto-facing during authored motion windows so sprite flips do not masquerade as animation replay problems.

## 2026-06-02 - KillLock Query Compacted Runtime State Before Owner Refresh

Context:
Killing the final linked monsters for a KillLock chest at the same time could leave `RemainCount` stale and keep the chest locked, especially when navigation arrows refreshed in the same frame.

Cause:
`ChestMonsterKillLock.GetAliveMonstersNonAlloc()` looked like a read-only query but compacted the lock's tracked unit list. If it ran from `ChestMonsterKillLockNavigationView.LateUpdate()` after the deaths but before the next lock `Update()`, the lock owner no longer observed a list-count change and did not raise remaining-count or unlock events.

Fix:
`ChestMonsterKillLock.Update()` now recalculates cached remaining/unlock state every frame, and `GetAliveMonstersNonAlloc()` no longer mutates the lock's tracked unit list.

Prevention:
Keep gameplay-state compaction and event emission in the owning runtime component. UI/navigation/read-model queries should not mutate owner lists unless they also refresh the owner's cached state and events in the same call.

## 2026-06-02 - Same-Scene Teleport Landing Was Skipped By Ground Hole Path Check

Context:
`RunSameSceneTeleportNpcFeature` fade out/in was playing, but the player did not visibly move from `appearancePoint` to `landingPoint` for the authored jump-like arrival.

Cause:
The feature sampled the appearance-to-landing path against `HoleTrap` as if the arrival were grounded movement. When the authored path crossed a hole, it logged the path as blocked and warped directly to the landing point, so the landing presentation never played.

Fix:
Arrival movement now depends on distinct appearance/landing positions and an authored duration, not on intermediate `HoleTrap` path sampling. The final landing point is still checked for `HoleTrap` overlap, and the player body collider is temporarily disabled during arrival movement so pitfall trigger/stay logic cannot fire mid-cinematic.

Prevention:
Do not use grounded path-blocking checks for airborne or cinematic arrival paths. Validate the endpoint, then suppress collision/triggers only for the presentation window that needs to cross unsafe space.

## 2026-06-02 - Same-Scene Teleport Appearance Warp Still Used Grounded Target Safety

Context:
After the path-sampling skip was removed, the same-scene teleport arrival still showed no start particle and no movement when the authored `appearancePoint` was placed over `HoleTrap` space.

Cause:
The initial warp to `appearancePoint` still called the normal `WarpPlayer(...)` path, which rejects any target position overlapping `HoleTrap`. The arrival path policy had been changed to airborne, but the first warp still treated the airborne start point as a grounded final target.

Fix:
`WarpPlayer(...)` now has a narrow `allowHoleTrapTarget` option. Only the initial arrival-start warp uses it; final landing warps and non-arrival teleports still reject `HoleTrap` targets.

Prevention:
Keep arrival-start validation separate from final landing validation. For jump-like or airborne cinematic starts, allow the authored start point to overlap unsafe space only while body collider suppression and cinematic protection are already active. Never apply that exception to the final landing point.

## 2026-06-02 - Persistent SoundManager Sources Were Parentable To Scene Objects

Context:
The 0.1.5 player build crashed with a Mono access violation shortly after the DarkLord tutorial fake GameOver returned toward `ProtoTypeHub`. The 0.1.4 build passed the same route, while the 0.1.5 crash log stopped after DOTween null-target startup warnings and before the next Hub scene load warnings.

Cause:
Catalog-backed spatial sounds could reparent pooled `SoundManager` `AudioSource` objects under scene-owned follow targets. Because `SoundManager` is persistent, those pooled sources must not become children of scene objects; a scene unload can destroy the pooled source while the manager still holds it in its pool and runtime dictionaries.

Fix:
Keep catalog-backed sources parented under the persistent `SoundManager` roots. Store the follow target and local offset in runtime sound state, update followed source world positions in `LateUpdate`, and recreate any destroyed one-shot pool entries before reuse.

Prevention:
Persistent runtime service pools must own the lifetime of their pooled GameObjects. Do not parent pooled service objects under scene-owned transforms; follow scene targets by storing a reference and projecting world position instead. Before recycling a Unity object from a persistent pool, treat Unity fake-null as a destroyed entry and recreate it.

## 2026-07-04 - Contract Inversion Left Dangling Fallback Branch

Context:
During the assembly split, `PlayerInteractionPromptPresenter` was partially converted from concrete `WorldInteractionPromptController` calls to `UiCommandPlayback`.

Cause:
The direct call was inserted before the old fallback `else` branch was removed or wrapped in a backend-handled check, leaving a dangling `else` and an immediate compile break.

Fix:
`UiCommandPlayback.HideWorldPrompt()` and `RefreshWorldPrompt(...)` now return whether a backend handled the request. `PlayerInteractionPromptPresenter` uses that return value before falling back to the serialized `IWorldInteractionPromptView` contract.

Prevention:
When replacing concrete UI calls with playback contracts, convert the primary path and fallback branch in one edit. Search the touched file for orphaned `else` branches and run targeted syntax/reference searches before moving on to the next boundary.

## 2026-07-04 - Serialized Missing Script References Pre-Exist Assembly Split

Context:
During the assembly-definition split verification, a static YAML scan checked scene, prefab, and asset `m_Script` references against all `.meta` GUIDs under `Assets`, `Packages`, and `Library/PackageCache`.

Cause:
The first scan found `35` missing `m_Script` GUID references. `git grep` against `HEAD` found the same GUID references in committed assets/scenes, so this is a pre-existing serialization state rather than damage from the current asmdef moves. Known affected categories include ShadowBoss telegraph style assets, monster balance assets, Visual Scripting graph assets, multiple scenes with the same missing component GUID, `PixelLightTest`, `Frog_BOSS`, and `_Recovery` scenes.

Fix:
Partially fixed during the asmdef verification slice by restoring four legacy compatibility scripts with their historical MonoScript GUIDs: `DamagePopupSceneAnchor`, `MonsterDefinition`, `UIHoverKeepAliveArea`, and `BossDrop`. A later serialized GUID repair moved the two ShadowBoss `AttackTelegraphStyle` assets to the current `AttackTelegraphStyle` MonoScript GUID. A subsequent serialized cleanup moved `Frog_BOSS.prefab` from the old `Boss` GUID to the current `Boss` MonoScript GUID. The current static missing `m_Script` count is `6` project references, all Visual Scripting package/graph/settings references.

Prevention:
Before declaring scene/prefab/ScriptableObject validation complete, run a missing `m_Script` GUID audit after Unity import and either restore the missing scripts/packages, remove obsolete components/assets, or document intentionally ignored recovery/demo assets separately from active game content. Do not create duplicate compatibility classes for GUIDs whose current class already exists under another MonoScript GUID; migrate the serialized asset to the current script GUID instead.

## 2026-07-04 - Asmdef Meta Missing Importer Block Prevents Trustworthy Unity Assembly Output

Context:
During the six-assembly split verification, all target asmdef files existed and the static asmdef graph was valid, but Unity still had no `Core.dll`, `Gameplay.dll`, `Infrastructure.dll`, `Presentation.dll`, `UI.dll`, or `Editor.dll` under `Library/ScriptAssemblies`.

Cause:
`Assets/_Project/Runtime/Core/Core.asmdef.meta` only contained `fileFormatVersion` and `guid`; it was missing the `AssemblyDefinitionImporter` block present on the other asmdef metas. A bare GUID meta can preserve the GUID text but still make the asset importer state suspicious for Unity asmdef recognition.

Fix:
Restored the `AssemblyDefinitionImporter` block in `Core.asmdef.meta` while preserving GUID `560ed2d5beb94299be88e3bbd2aac48f`. Added asmdef meta importer checks to both the external static audit script and the Unity Editor validation window.

Prevention:
When adding or moving `.asmdef` files, verify the `.asmdef.meta` file keeps the same GUID and contains `AssemblyDefinitionImporter`. Do not treat a valid `.asmdef` JSON file as sufficient evidence that Unity imported the assembly definition asset.

## 2026-08-19 - Hub Portal Had Scene Manager But No Route Backend

Context:
Direct Play from `ProtoTypeHub` correctly skipped Hub arrival presentation and restored the player to `Idle`, but the run-start portal remained non-interactable. The portal diagnostic reported `isTransitioning=False`, `playerState=Idle`, `canResolve=False`, and `route=manager=null`.

Cause:
The first diagnosis found a real backend-rebinding gap, but fixing it did not resolve the playtest because the rebound manager was destroyed afterward. `ProtoTypeHub/SceneManagers` contains `RunTransitionResolver`, `PortalRouteManager`, and `SceneTransitionPolicyResolver` on one GameObject. Their auto-bootstrap instances can make a scene component a duplicate, and every duplicate path used `Destroy(gameObject)`. A resolver therefore removed the entire shared host, including the valid `PortalRouteManager`, whose `OnDestroy` unregistered `RunRoutePlayback` and produced the observed `manager=null` state.

Fix:
`PortalRouteManager` still repairs its static backend across Play/recompile lifecycle paths. In addition, the duplicate paths of all three co-located scene-flow services now use `Destroy(this)` so they remove only their own duplicate component and preserve sibling services on the shared host.

Prevention:
For Unity services that expose a separate static playback/query backend, do not treat a non-null MonoBehaviour instance as proof that the backend is registered. Restore backend registration from instance adoption and enable/reload lifecycle paths. When independently bootstrapped services may share a GameObject, duplicate cleanup must destroy the component rather than the whole host unless whole-host ownership is an explicit invariant.
## 2026-09-02 - Scene Prefab Overrides Collapsed Affection Fill Geometry

Context:
After the dialogue affection display migrated from one Slider to five filled heart Images, affection changes reached the UI and the first Fill reported `fillAmount = 1`, but the heart still looked empty in Play Mode.

Cause:
Scene instances of `GlobalUIRoot` retained legacy overrides for the original first Fill RectTransform. The overrides forced `m_AnchorMax.x` and `m_AnchorMax.y` to `0` while the updated prefab authored both as `1`. Because size delta remained zero, the runtime Fill rectangle became `0 x 0` even though its value, sprite, color, material, parent area, and sibling order were valid.

Fix:
Remove the two stale scene-instance overrides so the first Fill inherits the prefab-authored stretch anchors `(0,0)` to `(1,1)`. Twenty-five scene instances were repaired in this slice; `SlimeCorridor.unity` and `SlimeCorridor 1.unity` remain pending because the patch reader could not decode those large YAML files.

Prevention:
After changing the RectTransform layout of a prefab child that already has scene instances, inspect instance overrides for the old child file ID. A correct prefab value does not repair an existing scene override. Validate the runtime RectTransform size in addition to checking `Image.fillAmount`, and revert obsolete layout overrides across every scene that embeds the prefab.

## 2026-09-02 - Direct Boss Play Started a Run Without the Hub-Authored Timer

Context:
Dialogue correctly reintroduced the selected gameplay HUD roots, but the time HUD remained absent when Play Mode started directly from `HeoMinSeok_Boss_Dragon`.

Cause:
The editor direct-start path reset gameplay data and invoked `StartRun()`, while `RunTimeLimitSystemManager` existed only in the Hub scene. `RunTimerHUD` had no state source and disabled its `visibleRoot`; changing Canvas order or dialogue visibility could not make it render.

Fix:
Before the editor development `StartRun()` event, `SceneDomainDevelopmentRunTimerPolicy` now creates the existing timer package with the canonical config only when no timer exists. It tracks the fallback it owns and removes it during Title cleanup.

Prevention:
When editor direct-start policy synthesizes a run, verify every required run-scoped source normally inherited from Hub entry exists before emitting the run-start event. Do not patch the HUD with a fake value when its state owner is missing.

## 2026-09-02 - DialogueEffect Animator Was Evaluated After Deactivation

Context:
Dialogue exit reset the DialogueEffect root to hidden Idle, then theme cleanup could restore its AnimatorOverrideController while the effect GameObject was inactive.

Cause:
The controller/state refresh paths called `Animator.Update(0f)` unconditionally after `Rebind()` or `Play()`. Unity rejects explicit Animator evaluation on an inactive object and logged `Can't call Animator.Update on inactive object` during dialogue cleanup.

Fix:
DialogueEffect controller and state refreshes now evaluate immediately only while the Animator GameObject is active in the hierarchy. The next activation still explicitly plays Intro or Idle before evaluation.

Prevention:
Presentation cleanup may restore serialized/controller state while visuals are inactive. Guard immediate Animator evaluation with `activeInHierarchy`; do not reactivate a hidden presentation solely to force `Animator.Update(0f)`.

## 2026-09-04 - Generated Event Definition Referenced A Room Outside The Theme Library

Symptom:
Shadow, Dragon, and Slime procedural generation stopped before building any rooms with `Guaranteed room '...' does not belong to library 'Procedural...Library'` after the Parcel and Buffy event definitions were enabled.

Cause:
The event installers created themed `RoomTemplateSO` assets and connected them through `RunMapEventDefinitionSO`, but did not add those room assets to the corresponding `RoomThemeLibrarySO`. `DungeonGraphLayoutAssembler` intentionally requires every guaranteed room to belong to the active library.

Fix:
Both installers now add every event-pool room through `RoomThemeLibrarySO.EditorAddRoom(...)`, validate membership, and assemble each event room against the production generation profile before installation succeeds.

Prevention:
When adding any dynamic or follow-up guaranteed room, treat the event definition and theme-library membership as one atomic authoring operation. Installer validation must cover an actual graph assembly, not only null references and profile bindings.

## 2026-09-05 - Temporary Event Renderers Were Hidden Behind Room Tiles

Symptom:
The newly generated Parcel and Buffy NPC/object visuals were present and interactive but not visible inside procedural rooms.

Cause:
Their installer-created SpriteRenderers used the `Default` Sorting Layer with orders 19-21. Procedural room Floor and Wall tilemaps also use `Default`, at orders 50 and 60, so the room tiles rendered over the event visuals.

Fix:
Both event installers now assign every temporary event SpriteRenderer to the existing `Entity` Sorting Layer and use only local object orders 0-1. Installer validation rejects a generated event module whose renderers are on another sorting layer.

Prevention:
Editor-generated world actors and interactive props must explicitly set a project sorting layer instead of relying on SpriteRenderer defaults. Validate the saved prefab after generation because a high Order in Layer cannot overcome an earlier Sorting Layer.

## 2026-09-05 - Normal Boss Return Crossed The Hub Boundary And Lost The Generated Entry

Symptom:
The route became Corridor-to-Boss-to-`ProtoTypeHub` instead of returning to Grand Hall. Continuing from Grand Hall could reset run/player state, and the next procedural Corridor placed the player at an unrelated static spawn.

Cause:
The three data-driven Boss exit connections still targeted `ProtoTypeHub`, while Grand Hall reused the `HubToRunStart` transition type and therefore reapplied new-run policy. The normal RouteSets also kept `corridorEntryPointId: Default`, so ScenePortal travel did not identify the generated Start-room endpoint.

Fix:
Normal Boss exits now target `Grand Hall / GrandHall.BossClear` with no run action. Active-run Grand Hall departures replace only the route plan and skip start/reset policy. Normal RouteSets use `Corridor.<theme>.Lobby`, which ScenePortal travel publishes as a dynamic destination endpoint for `PlayerSpawner`.

Prevention:
When adding an in-run checkpoint, verify the complete boundary in both travel systems: destination scene, run action, player-state capture, route-plan replacement, transition policy, and generated endpoint ID. A scene named or presented like a lobby must not automatically inherit the actual Hub's run lifecycle semantics.

## 2026-09-05 - Temporary Event Room Inherited Internal Treasure-Room Walls

Symptom:
The Parcel and Buffy NPC/object pair spawned near the room center but was enclosed or obstructed by wall tiles.

Cause:
The event installers copied the complete `RoomBuildData` from a treasure-room template and removed only decorative layers. Internal structural wall tiles therefore remained in the generated event room.

Fix:
Event-room generation now fills the full room bounds with floor tiles and rebuilds closed walls along the complete rectangular outer boundary, including socket cells. `DungeonRoomBuilder` remains responsible for opening only the sockets selected by the generated graph. The six existing themed event-room assets were regenerated to match.

Prevention:
When a temporary room needs only another room's theme and sockets, do not treat that room's full structural tile data as the base layout. Rebuild the intended shell explicitly, but preserve the authored-room contract that every socket cell initially owns both Floor and closed Wall tiles; runtime generation opens connected sockets afterward.

## 2026-09-05 - Event NPC Installer Read Ink JSON Before Auto-Compilation Completed

Symptom:
The first art installer batch stopped with a missing `ParcelEventDialogue.json` even though C# compilation passed.

Cause:
Importing the Ink source queued its automatic compiler. Calling `InkCompiler.CompileInk` immediately afterward found the same file already compiling and returned before JSON was available. `ForceSynchronousImport` does not make the separate Ink compiler queue synchronous.

Fix:
`RunEventArtInstaller` compiles its two self-contained sources with `Ink.Compiler`, writes compiler-generated JSON, and imports that JSON before assigning NPCData. The second Unity batch completed successfully.

Prevention:
For batch asset authoring, await the Ink compiler's actual completion or synchronously compile self-contained sources before loading their generated asset. Asset import completion alone is not proof of Ink compilation completion. Do not save NPC references to missing JSON.

## 2026-09-05 - Procedural Corridor Navigation Omitted Before Runtime Rooms Exist

Symptom:
Remaining-monster arrows were present in legacy Corridor scenes but absent from the active procedural Corridors.

Cause:
The scene-level overlay was not authored into the new scenes. The navigation validator/auto-wiring predicate checked only already-authored room groups and door locks, so procedural scenes with runtime-generated groups were incorrectly considered to not need navigation.

Correction status:
The predicate now recognizes `DungeonGenerator`, and normal themed scene authoring ensures a single scene-root overlay. Runtime display remains a projection of the existing room group's registered monsters, with same-scene filtering and pending-spawn suppression. Code compiled and the focused Unity installer successfully wired all three normal procedural Corridor scenes. Play Mode confirmation remains manual (SessionLog 2026-09-05).

Prevention:
For procedural scene infrastructure, detect the authored generator rather than requiring its generated objects to exist in Edit Mode. Keep navigation outside roots cleared by room regeneration and retain the existing tracking-unit policy instead of scanning every Enemy in the scene.

## 2026-09-05 - GoblinTank Preparation Returned To Idle During Telegraph

- Cause: the 0.25-second non-looping preparation clip returned to Idle at normalized time 0.95, although the ability warning lasts 2 seconds. The raised-arms frame existed but was not held.
- Fix: remove the AttackReady-to-Idle transition, align generator/validator hold-ready policy, and send alive cancelled attack exits through the existing Recover cue.
- Prevention: when a runner owns warning timing, keep its ready pose until the next explicit cue and review cancellation/start-failure/death exits. Asset, authoring generator and validator must agree. Compilation passed; visual confirmation remains manual.

## 2026-09-05 - Direct Hub Spawn Left Permanent Bonus Hearts Empty

Cause:
Attribute initialization fills the original maximum before upgrade reapplication. Reapply intentionally grants no purchase heal. Direct Hub starts bypass the title's pending full-heal request, so no later Hub fill covers the increased maximum.

Fix:
PlayerSpawner requests the existing pending Hub-load full heal once for its initial player, only in a Hub outside an active run. Existing no-pending and confirmed-restore paths remain the consumers. Gameplay compilation passed; Play Mode confirmation remains manual.

Prevention:
Keep initial Hub healing after upgrade registration and state restoration. Do not make upgrade reapply or maximum-health changes universally heal: these also execute during in-run scene travel. Repeated spawn calls must not grant repeated recovery.

## 2026-09-11 - Death Return Could Not Resolve Hub Music Without Legacy Portals

Cause:
RunRouteBgmService could resolve Hub music only from the active route catalog, a single cached catalog return scene, or a HubToRunStart portal in the destination scene. EndRun clears the active plan before loading ProtoTypeHub; Grand Hall catalogs cache Grand Hall as their return scene, and ProtoTypeHub now uses a data-driven Grand Hall connection rather than the old start portal. The resolver therefore returns no Hub music and leaves the previous/GameOver track playing even though scene loading succeeds.

Fix:
Keep matching authored catalog music first, then use a configurable default Hub SoundRef when SceneDomainNamePolicy classifies the destination as the actual Hub. No death-flow, scene/portal data, run lifecycle or Grand Hall domain change. The existing sceneLoaded cache invalidation still ensures that external GameOver music is replaced.

Verification:
Production and the new 10-case regression fixture compile with 0 errors. Cases cover Hub-only matching, valid catalog sound, configurable/unset fallback, no active route/no portals, Grand Hall cache mismatch and matching-cache precedence. Native Play Mode tests and audible death-return verification were not executed because the project Editor was open.

Prevention:
Presentation needed after EndRun must not depend solely on run-owned data that EndRun removes, or on navigation objects that can be retired independently. Keep the actual Hub classification separate from an in-run checkpoint's route return scene.

Superseded later on 2026-09-11:
The Hub-only fallback and its fixture were replaced by scene-authored music requests. `SceneBgmRequester` requests ProtoTypeHub's `bgm.hub` directly, independent of route state and portals. Core rejects inactive/unloaded scene play/stop requests; unloading a requester never stops destination music. All 18 configured scenes passed native authoring validation and all 11 focused Unity Play Mode cases passed. Audible full death-return playtesting remains manual. See `Docs/StructureMemory/SceneMusicRequests.md`.

## 2026-09-11 - Batch Scene Music Authoring Reserialized Unrelated Data

Cause:
Saving scenes through the current Unity version also serialized existing Tilemap/default component data and UI poses. Restoring a headless editor's untitled SceneManagerSetup then failed after the requested BGM writes had already succeeded.

Fix:
Guard setup restoration against unsaved/invalid entries. Preserve only the BGM object, SceneRoots entry and encounter music fields on the verified clean pre-task scene/prefab text. Read-only comparisons established that removing those additions exactly reproduced the originals; duplicate IDs and requester counts were checked before applying. Reopen all 18 resulting scenes through native Unity validation. Final scene/prefab diff contains 985 added lines and no deletions across the 21 approved assets.

Prevention:
Treat native SaveScene as a potentially broad serialization operation, not proof of a narrow diff. Record the pre-task state, inspect every asset change, preserve unrelated user edits and validate any targeted cleanup natively. An authoring command's cleanup exception does not imply earlier asset saves were rolled back. Keep ongoing validation read-only.

## 2026-09-11 - Minimap Contents Compared World Positions To Layout Cells

Cause:
DungeonMapRoomNode.WorldBounds copies DungeonRoomPlacement's integer layout-cell bounds despite its legacy name. Content tracking compared those bounds directly to chest transforms and heart landing world positions. ProceduralShadowCorridor's generated root is translated by (-49.35, -2.04), so the nearest-room fallback assigned contents to other rooms. Earlier fixtures only used identity coordinates and missed this boundary.

Fix:
DungeonGenerator passes the builder's FloorTilemap to DungeonMapRuntimeController and its content tracker. Source positions are converted through GridLayout.WorldToLocal/LocalToCellInterpolated before shape-based room resolution, including on bootstrap and re-enable. No new per-frame search or UI-owned gameplay state is introduced.

Prevention And Verification:
Document coordinate units even when an existing property name cannot be changed safely. Test translated, rotated and nonuniform grids plus actual drop destination/lifecycle events. Native execution also revealed that test hearts lacked a concrete Collider2D: add CircleCollider2D before FieldHealPickup2D rather than relying on its abstract RequireComponent. After correcting the fixtures, all 22 focused content/bell cases passed in Unity Play Mode.

## 2026-09-12 - Mirrored Offsets and Conditional Effect Restore

Apprentice slash symptom: positive side offsets appeared above the player facing right but below facing left. The raw perpendicular (-direction.y, direction.x) rotates the vertical basis downward at left-facing aim, whereas the visual mirror mode preserves world-up. The offset now follows the same world-up convention and leaves right-authored values unchanged.

Conditional upgrade lifecycle trap: active-effect snapshots do not preserve the runtime condition owner. Saving the travel-speed GE as an ordinary timed effect would restore an orphan bonus alongside purchased-upgrade reconstruction. ISceneReappliedEffectSource explicitly excludes this runtime-owned conditional effect from snapshots; the upgrade reevaluates room state after reapplication.

Prefab authoring verification also caught layout reflow when adding the acquisition label. Its LayoutElement ignores parent layout; unrelated existing RectTransform values were preserved. New UI should not silently resize existing chest content.

### 2026-09-12 — Instant-heal targets and generated-wall physics drift

- Symptom: hub dummy received HP damage but no prototype hit pause. Cause: synchronous HP restoration hid damage from the post-effect HP comparison. Fix: scoped HP-decrease event observation with finally cleanup in CombatDamageAction; regression covers the authored TrainingDummy prefab.
- Symptom: GeneratedWall remained Ground and escaped Wall-filtered collision queries. Cause: installer mapping and validator explicitly treated Floor and Wall as Ground. Fix: shared physics-layer name mapping, runtime assignment before socket blockers, five scene layer migrations, updated regression assertion.

Related verification: [2026-09-12 session](SessionLogs/2026-09-12.md).

### 2026-09-12 — Boss pattern saved the hit-pause zero speed

Reported symptom: a boss could stay frozen mid-pattern after hit stun. Code evidence: CombatHitPause2D temporarily writes Animator.speed=0 while DemonKingController.HoldPatternAnimatorSpeed independently snapshots animator.speed; ReleasePatternAnimationHold can subsequently restore that captured zero. Fix: boss local-pause immunity plus reading the pre-pause speed when entering a DemonKing pattern hold. Avoid unconditional speed=1 restoration because intentional holds and authored speed overrides exist. Regression fixtures compile; reported in-game reproduction/PlayMode execution remains unverified.

### 2026-09-12 — Held attack lost its re-aim window and raw lunge direction

Cause: held input extended meleeControlLockActive indefinitely, and automatic repeats could restart before the intended re-aim tail. Separately, AbilityMoveDirectionResolver2D read walking's filtered MoveInput; the attack lock zeroed it, changing the resolver from movement direction to aim fallback during held chains. Fix: release after motion/lunge independently of held input, defer auto-repeat through the existing re-aim gap, and expose raw live input to ability direction resolution. No player Animator root-motion callback was found; authored lunge parameters were preserved.

World hitstop now uses the existing shared pause-owner service with unscaled expiry and disable cleanup, avoiding independent timeScale restore snapshots or boss animation-speed ownership conflicts. Regression fixtures compile; actual reproduction/playtest remains unverified.

### 2026-09-12 — Mirrored slash placement must mirror its offset basis

Symptom: Flowering, Lightning and shared SwordCombo slash centers shift vertically when aiming left although the right-facing offset is correct. Cause: the ordinary perpendicular (-aim.y, aim.x) rotates its authored side offset downward when aim.x becomes negative, while the visual uses a mirrored facing convention. Fix: match the existing ApprenticeHeroSword basis (-aim.y * facingSign, abs(aim.x)) for these slash centers. Do not apply this convention indiscriminately to direction-local projectiles, trails or bounds projection. Regression coverage: Flowering horizontal/diagonal mirrored center checks in CombatFeelLootQuestPlayModeTests; compiled, PlayMode execution pending.

## 2026-09-12 - Cleared portal visual guessed catalog shape instead of using travel gate

- Symptom: a defeated officer's Grand Hall portal rejected travel but retained the active sprite and center particles.
- Cause: GrandHallClearedPortalView required exactly one NormalRouteSets entry; all current officer catalogs have an empty list and put their destination in FinalRouteSet. The earlier presentation-only test called ApplyCleared directly and missed Update's real condition.
- Correction: project the existing RunRoutePlayback BossAlreadyDefeatedThisRun warning used by ScenePortal interaction. Keep run/Grand Hall scope and existing sprite/particle cleanup.
- Prevention: test destination-driven visual entry conditions, including authored catalog shapes, in addition to the visual mutation method. Do not maintain a separate destination/defeat resolver in presentation code.
## 2026-09-13 - Final corridor used permanent clears; final boss direction disabled

- Grand Hall's RequiredBossClearScenePortalAccessRule accepted permanent profile clears OR current-run clears. Previous runs therefore unlocked the final corridor without current-run officer kills.
- The actual ProceduralDemonkingCorridor BossGate binding references Corridor_demon_king_Boss, whose A-to-B direction was disabled, independently preventing boss-room entry.
- Corrected the requirement to active-run defeatedBossIds, shared it with disabled portal visuals, and enabled only A-to-B while preserving the current-run defeated-boss guard.
- Check both the scope of progress records and the enabled flag on the actual scene-bound connection when diagnosing travel. Never erase permanent profile progress to fix a run-only gate.

## 2026-09-13 - Chloe entry dialogue HP report remains unresolved

- Report: the first Chloe dialogue immediately after room entry temporarily displays full HP; prior HP returns after dialogue.
- Investigation correction: normal dialogue uses BlockControlOnly and does not pause Time.timeScale. A scaled restore retry is not evidence of this bug. Reverted the speculative retry patch and pause fixture.
- No HP writes found in dialogue/cinematic protection; AttributeValue publishes changes after updating CurrentValue. Inspect live scene-entry restoration and HUD bindings to establish the actual cause; no visual fix confirmed.

## 2026-09-13 - Unbounded vision masks overlapped weapon-local reveal masks

- PlayerVisionMask and candle masks had custom ranges disabled. Apprentice reveal ranges alone did not isolate other sprites at the same sorting order, and dropped spear mask had no local sorting scope.
- Restricted vision masks to MaskRender and isolated charge/dropped-spear masks in local SortingGroups. Host item sorting must target the group instead of rewriting child-local mask/render order.
- Existing layer IDs/order retained; reference/compile checks passed, simultaneous rendered overlap testing outstanding.


## 2026-09-13 - Sprite sheet template replacement must preserve YAML indentation boundaries

- During Crimson asset authoring, an unanchored search for the spriteSheet-level outline field matched a nested sprite outline and retained unwanted template slices. Caught by exact frame-count validation and repaired before handoff.
- Anchor replacement boundaries to complete YAML lines at the intended indentation; verify exact frame count, unique sprite internal IDs, rect bounds, and prefab sprite references. Build success alone does not validate Unity sprite metadata.


## 2026-09-14 — Ability-owned cut-in pause blocked its own cleanup

- Symptom: Flowering activation could leave scaled gameplay frozen, including when overlapping hitstop.
- Cause: CombatHitPause2D.Run stopped every nested iterator while TimeScalePausePlayback.IsPaused; Bloom acquired its own pause before yielding cut-in and released only after that cut-in completed. UnscaledDeltaTime inside the child could not help because MoveNext was never called.
- Fix: explicit, nested unpaused presentation scope; acquire/release and their finally are both inside the scope. Normal ability frames keep pause gating. Cancellation checks precede the gate and nested disposal remains paired.
- Regression coverage added for completion/cancellation and independent overlapping pause ownership; Unity execution remains unverified.


## 2026-09-14 — Chest collision camera shake deferred until UI close

- Cause: combat impact work added an unconditional full-pause return to CameraManualShakeDriver.LateUpdate. Chest UI legitimately requests manual shake while UIManager holds full pause; remaining duration was never consumed, so the effect played after UI close. Collision request timing itself was correct.
- Fix: optional runtime CameraShakeRequest.PlayWhilePaused defaults false and is forwarded by the service. Chest collision opts in; manual driver bypasses only its playback pause gate, retaining unscaled duration and transform cleanup. Combat default behavior is unchanged.
- Regression boundary: shared presentation clocks must distinguish UI presentation under intentional game pause from paused combat presentation. Do not globally remove pause handling or release gameplay pause tokens to play a UI effect.
- Added compiled regression source for chest completion under pause/no replay after release and subsequent combat request retaining default pause behavior. Unity execution pending.


## 2026-09-14 — Soul-heart absorption skipped player damage feedback

- GE_Damage_Spec returned immediately when absorbShieldAttribute consumed all incoming damage. HP stayed unchanged, so neither HP-popup listeners nor the later DispatchDamagePresentation path ran. Mob and hazard damage assets both reference the soul-heart absorb attribute.
- Before that full-absorption return, actual positive player shield loss now emits a player-styled numeric popup and dispatches the existing hit feedback. Failed shield mutation, zero damage and invulnerability do not create feedback. HP spillover retains the original HP feedback path, avoiding duplicate hit reactions. Existing spillover popup semantics remain HP damage.
- Regression source covers shield surviving, exactly depleted, HP spillover and invulnerable cases, with real PlayerHitFeedback2D and recording camera/popup backends. Unity execution pending.

## 2026-09-14 — Duplicate receipt markers and physics-pose wall casts

- Definition membership (`Contains`) identifies an interchangeable return category, not an individual acquired slot. Applying it independently to every ItemSlotUI highlights every duplicate potion. ChestScreen now allocates at most the outstanding receipt count to matching slots, retaining gameplay's same-definition return policy.
- Hypothetical wall-slide casts must start from the Rigidbody2D physics pose, with child collider offsets adjusted for body rotation. Reading only Transform.position can use a stale presentation pose. The native regression harness checks low/high-speed tangential movement and corners; keep physics query validation when changing this path.
- Follow-up correction: using raw held input as a walking lock causes a rejected/repeated press during the released animation tail to stop movement without a new attack. Walking now follows the accepted attack's phase only; held input requests auto-repeat and does not own a movement lock.

## 2026-09-14 — Input-side attack timing diverged from actual GAS execution

- Symptom: walking during basic attacks across weapons after removing the raw-held-input lock.
- Verified structural causes: input code estimated duration from AbilityDefinition.recoveryTime before logic supplied RecoveryOverride; its animation threshold released at 80%; it compared the executed definition against a possibly changed selection after activation; Animator fallback differed from playback. GAS buffering also returns true for a queued request, so request success cannot represent actual execution start. The current player prefab has buffering disabled; this was a latent path, not evidence that buffering caused the reported current symptom.
- Correction: real execution start/end notifications, executed-definition classification, actual execution/recovery as a lock floor, full observed animation completion, shared Animator resolution. Old estimates and request-side BeginMeleeControlLock were removed.
- Regression: real AbilitySystem coroutines with no Animator and prolonged logic, buffered request vs later execution, cancellation/natural completion, player intent output, and the animation tail. Never treat a passing compile or a test that only toggles lock booleans as proof of this lifecycle.
## 2026-09-14 — Crimson projectile wall clipping through two collision paths

- Symptom: Crimson Boundary basic projectiles disappeared frequently beside walls.
- Cause: a shared 0.28 × 0.28 sweep queried both wall and damage layers, while the 0.32 × 0.32 prefab trigger independently entered AttackBase's wall-destruction path. Reducing only the sweep could not separate wall tolerance from enemy hit reach.
- Fix: author separate wall/damage colliders in Fireball.prefab, sweep each against its own mask, resolve the nearest valid contact and bypass inherited trigger handling only in CrimsonBoundaryProjectile2D. Preserve existing damage/burn/visual cleanup ownership.
- Prevention: when separating projectile collision channels, inspect both swept queries and Enter/Stay callbacks; avoid leaving a second wall-destruction path driven by the damage shape.

## 2026-09-15 — Independent Burn survives general boss effect cleanup

Source trace: BossControllerBase cleared GameplayEffectRunner and ElementGaugeSystem at death, but BurnStatus2D stores its own stacks and ticks separately. ConsumeAll is now called at death entry; dead-boss reapplication is rejected. A lethal Burn tick can synchronously invoke this cleanup, so the tick checks remaining stacks before spawning its subsequent pulse/VFX. MSBuild passed; death-presentation reproduction in Unity remains pending.

Related title handoff trap: hiding preload before the intro root or scene fade is opaque exposes the old slot screen. The outgoing cover now remains until its replacement is ready.

## 2026-09-15 — Charge sortAtRoot workaround did not resolve visual defect

The user reproduced premature full-blade charge in Shadow Corridor after the previous sortAtRoot change. That change had only build/static validation; mask interference had not been proven in a rendered frame. Remove that workaround and separate dedicated rendering channels for charge and spear drop mask/renderer pairs. Both sides receive the same exclusive bit, without Default. Do not report this visual defect resolved solely from MSBuild; verify partial charge together with world vision and drop masks in Play Mode. Implementation is applied; rendered acceptance is pending.

## 2026-09-15 — Ink blocking feature can consume the next spoken line

A standalone tag-only `# feature:` line compiled into the following spoken line in the scribe Ink. DialogueController intentionally skips display for blocking features and resumes by advancing, which would lose that dialogue. Give the cue its own nonempty sentinel line (`[camera cue] # feature: scribe_cue`), consumed by the blocking feature, and verify compiled Ink Continue() text/tags. Scribe's three knots were verified with 4/2/0 cues and no spoken text attached to those cues.

## 2026-09-15 — Slot introduction incorrectly waited for an active run

GrandHallScribeSequence waited for RunSessionStore.IsRunActive before presenting its slot-once intro, but the normal Hub_GrandHall connection does not start a run. The user saw no cinematic. The shared continuation predicate also required an active run, so removing only the initial wait would still abort speech/camera/dialogue. Split loaded-slot identity from active-run identity throughout intro continuation and keep return-stage/portal writes run-gated. Build/static checks passed; normal-route Play Mode acceptance pending.

## 2026-09-15 — Default letterbox fade hides Dialogue canvas

Grand Hall used CinematicLetterboxOverlay.PlayIn(duration, ratio, alpha), whose default faded layers include Dialogue. The cutscene continued but dialogue was invisible. Match HubIntroAfterDarkLordSequence's explicit PresentationFadedLayers (excluding Dialogue and GameplayHUD), and leave Dialogue's HUD lifecycle to Dialogue. Removed the redundant sequence-owned non-dialogue suppression. Build and static comparison passed; visual acceptance pending.


## 2026-09-15 — Camera-dialogue section visibility mistaken for movement visibility

The previous panel fix restored the upper frame after each camera movement and recreated GrandHall letterbox bars for every cue. The user's intended lifetime is the whole camera-directed dialogue section; Dialogue starts after the letterbox cinematic has ended. Keep the upper-frame hide state across spoken lines/Hub segments, restore only on returning to ordinary NPC dialogue, and never recreate bars inside dialogue cues. Do not couple this view state to advance-input blocking: hidden-panel spoken lines must remain advanceable, while the existing blocking feature callback guards camera movement. Source/scene checks and builds passed; visual acceptance remains pending.


## 2026-09-15 — Retained dialogue visuals need matching theme and animation lifetime

Keeping the lower frame visible across Hub session boundaries exposed ResetTheme restoring the name's prefab color during camera travel. Preserve the visible theme until the next ApplyTheme or actual retained-frame close, while still cleaning up dialogue effects. Separately, SnapGroupClosed made an already-visible GrandHall upper frame disappear instantly; use the existing animated PlayGroupClose and wait for completion before movement. Hub had not exposed this snap because its upper frame was hidden before segment opening. Builds/source checks passed; Play Mode visual acceptance pending.


## 2026-09-16 - Facing Locks Bypassed By Secondary Update Paths

Boss parallel speech layout could switch tail sides each frame; weapon attack-start presentation derived facing directly from normalized cast direction even though idle facing already used a world-space dead zone. Some boss patterns also called direct facing updates after committing their pose.

The display now retains each boss bubble direction independently; weapon attack start captures the already resolved rig side; affected boss AL routines retain facing through their commit/recovery windows. Review direct setters as well as automatic Update loops when diagnosing flip jitter. Builds and the added regression fixture compilation are checked separately from Unity Play Mode execution.

## 2026-09-16 — Charge still fully visible after rendering-channel split

- User reproduced full yellow blade before full orange charge in Shadow Corridor after the prior bit-2 change.
- Verified source: reveal/mask still shared Entity sorting with the authored EntitySightMask full order range. Rendering Layer separation is not evidence of sorting/stencil isolation.
- Correction applied: keep outer group world sorting, move its internal reveal and custom mask range to Default sorting; move suction from RenderRoot to AbilitySystem owner to avoid aim/mirror inheritance.
- Added a rendered coverage regression in CombatPresentationRegressionPlayModeTests; MSBuild passes, but the rendered test and in-game reproduction were NOT run. Do not mark the visual symptom resolved until those pass.
- Engine reference: [SpriteMask scope/range](https://docs.unity3d.com/cn/6000.0/ScriptReference/SpriteMask.html).

## 2026-09-16 — External vision stencils still bypassed the sword's local mask

- Reproduction: the user confirmed rotation-independent particles, but charge remained fully exposed in Shadow Corridor after the local Default sorting change.
- Verified cause 1: the outer charge SortingGroup still rendered on Entity. A real URP/D3D11 capture at 65% showed 431 pixels with sight off and 743 with PlayerVisionMask on; moving the **outer group** outside Entity removed that interference.
- Verified cause 2: Candlestick (two apertures), LightBead and Dead'sSkeleton had Custom Range disabled. Even a separate charge world layer was fully exposed by these unbounded masks (743 versus the 431-pixel baseline). Searching only PlayerVisionMask missed them.
- Fix: WeaponChargeEffect Sorting Layer after Entity/before ItemDisplay, sortAtRoot on charge group; all four unbounded apertures changed to exact MaskRender ranges with matching authored EntitySightMask children. Particle parenting remains the user-confirmed working change.
- Regression: Tools/Validation/ApprenticeChargeRenderProbe.cs uses actual compiled Gameplay/Core, current URP 17.4 renderer settings, charge sprites, PlayerVisionMask and before/after authored monster masks. GPU comparisons at three charge ratios and three rotation/mirror poses passed; world darkness/entity pixel parity and both skill direction locks/scene cancellation/stale-token checks passed. Logs and captures: Temp/apprentice-charge-evidence/. Main game scene interactive testing is still separate.
- Prevention: audit every active/spawned world mask and its effective outer sorting scope. Neither Rendering Layer bits nor a child renderer's local Sorting Layer prove stencil isolation. Keep IDs as strings or safely-sized integers when editing Unity YAML; verify duplicate IDs and child references before import.

## 2026-09-16 — Duplicate Chloe camera directors hide tuning changes

- Shadow boss scene has both the Witch prefab's CameraPresentationDirector and a scene-added director. Editing the second component does not tune the phase owner selected by CameraPresentationPlayback.Get (first component).
- The phase camera gizmo uses the same first component. Added an Inspector warning to the duplicate; edit the first director's Phase Lens Scale / Phase Camera Offset.
- No component was removed or migrated in this task. Before consolidating, verify encounter/death references and scene overrides. Do not diagnose nonresponsive tuning solely as a Scene view repaint problem.


## 2026-09-16 — Relic Q consumed cooldown with an unassigned projectile reference

- Symptom: Crimson LavaBall relic Q consumed cooldown and spawned nothing. The actual ALData_CrimsonBoundary.asset had no relicLavaBallPrefab entry; CrimsonBoundaryVisual2D.Spawn(null) returns null and the relic branch then exits. The prefab existed separately.
- Verification gap: WeaponRelicsNativeRegression constructed the projectile directly and never loaded the real Q data. The previous GUID audit checked only existing references, so it could not detect an absent required field. Earlier completion reporting overstated this asset linkage. The reason the field was absent is not established.
- Fix: explicitly link the authored Lavaball_RelicPiercing prefab; preserve its GUID and component fileID. Added CrimsonRelicAuthoredRegression to load the real definition/data/prefab dependency chain and activate Q through AbilitySystem. Static verification now asserts the required field exists, not merely that present GUIDs resolve.
- Related structure: Docs/StructureMemory/ScriptSystems/WeaponAndGASStructure.md.

## 2026-09-16 — Cached weapon re-equipped in its interrupted attack pose

- User confirmed weapons swapped during motion returned frozen in their previous pose. Equip reset only the prefab root before Animator.Rebind/Update(0); attack clips animate the child MotionRoot. The shared weapon prefab disables default-value writes on disable, and inspected Idle clips have no transform curves. Do not rely solely on Rebind or an empty Idle to recover the authored pose of a cached instance.
- Fix: WeaponVisualRig2D captures the explicit MotionRoot local position/rotation/scale once. WeaponEquipController restores after outgoing disable cleanup and before incoming activation/Rebind. Preserve nonzero authored offsets and scale; do not recapture an animated transform during re-equip. No prefab/Animator asset change.
- Verification scope: see the current SessionLog and Tools/Validation/WeaponSwapPoseRegression.cs; interactive weapon-by-weapon playtest remains separate from the isolated synthetic regression.

## 2026-09-16 - Checkpoint route queries cleared the committed event context
- Symptom: after Grand Hall -> procedural Corridor travel, the event planner logged route=<none>, visitOrder=0 and presentedEvents=0, although the ordinary layout generated successfully.
- Cause: CanResolveRoute/GetTravelBlockWarning reached TryPrepareHubStartPlan, which cleared the active plan for checkpoint portals. Interaction queries could therefore erase a just-activated route before DungeonGenerator.Start; all current start events require that route context.
- Fix/prevention: keep committed route/load context intact during pending-destination preparation. Replace active stages only in actual activation after preparation succeeds; preserve the pending selection checked by the destination warning. Do not use destructive run cleanup as part of an availability query.
- Regression: SceneConnectionRouteContextPlayModeTests.CheckpointQueries_PreserveCommittedRouteAndEventCandidates_UntilDeparture. Test source and runtime passed MSBuild; native Unity test execution and manual travel remain unverified.

## 2026-09-16 — Custom cinematic layer lists omitted gameplay HUD
- GrandHall's explicit PresentationFadedLayers omitted GameplayHUD; the common overlay previously treated caller lists/false capture as permission to skip HUD entirely. Hub separately disabled its HUD Canvas, bypassing visible fade.
- Shared letterbox PlayIn now always captures/fades GameplayHUD and BossHUD to zero; caller lists/flags only choose other UI. Hub's letterbox path leaves Canvas enabled for the fade. Keep Dialogue/Prompt opt-in separate to avoid hiding tutorial instructions or dialogue panels. Restore captured alpha/interaction rather than forcing a HUD enabled/visible at completion.
- Verification: source/build results in SessionLog. In-game fade timing and dialogue handoff still require visual confirmation.

## 2026-09-16: Reward artwork bound to an inactive legacy Image
- Symptom: all level reward cards still showed Attack_Card_1 after assigning distinct definition Icons.
- Cause: GlobalUIRoot's three LevelRewardCardView.iconImage references targeted inactive legacy Icon children; visible CardVisualMount Images retained fixed artwork. Data GUID validation alone missed the presentation target.
- Fix: point the three iconImage references to their existing active card-root Images, which also receive button tint. Keep the legacy children inactive.
- Prevention: trace definition -> Bind -> serialized Image -> active visible GameObject when verifying authored UI artwork. Check actual Editor rendering separately; static references do not establish visual success.

## 2026-09-17 — Odd Iron burst recaptured a displaced recoil pose

- Symptom: the revolver retained an offset after a barrage. Each PlayFireRecoil cached the current local position before stopping the previous recoil without resetting it. The authored 0.08s barrage interval is shorter than the 0.035s outward + 0.09s return cycle, so each new shot could redefine the resting position while displaced.
- Fix/prevention: cache the resting position once per recoil Transform, never on each interrupted shot. Restart interpolation from the current pose but keep both the kick target and return target relative to the fixed baseline. Disable/equip cleanup restores that baseline. Related to the cached weapon interrupted-pose issue above.
- Verification: Gameplay.csproj MSBuild passed and source lifecycle/diff checks passed. Live Unity barrage and disable/re-equip verification remain unexecuted.

## 2026-09-17 - Player-only Crimson sampling and world prompt letterbox mismatch

Context: Crimson basic-attack motion played in Editor but was absent in the player; world interaction prompts appeared fixed at screen center. The user confirmed the basic-attack movement stop is expected.

Cause: Player.log repeatedly reported that non-Legacy clips cannot be sampled outside Editor without an Animator. The Crimson root called SampleAnimation but had no Animator. Separately, GamePresentationController included the world-space PromptLayout in unconditional ScreenSpaceCamera conversion when letterboxing; Editor Play bypassed letterboxing.

Fix: Author a controller-free, root-motion-disabled Animator on the Crimson sampling root. Skip base WorldSpace canvases in both GamePresentationController and PresentationCanvasAdapter. Keep the existing attack clock, clips, movement lock and world anchor owner.

Prevention: Verify manual clip playback in a player build, not only Editor. Screen-aspect adaptation must preserve world-space UI. This is related to the 2026-05-19 SampleAnimation entry, but does not replace the Animator-controller fix for those sprite VFX.

Verification: MSBuild passed with warnings; prefab bindings and scoped whitespace checks passed. Fresh Unity player build/visual acceptance remains pending; do not treat compilation as playback proof.

## 2026-09-17 - Settings layout retained in scene prefab overrides

Settings rows combined fixed widths with left alignment, and gameplay scene instances retained interior RectTransform values as prefab overrides. Changing only the prefab anchors would leave these instances using old geometry after disabling their horizontal layout drivers.

When replacing an authored layout driver with anchors, inspect scene overrides on the affected RectTransforms. Remove only obsolete layout properties so the instance inherits the updated prefab; preserve gameplay, reference and unrelated presentation overrides. This task migrated enabled build scenes only. See [SettingsPanelLayout](./StructureMemory/SettingsPanelLayout.md) and the [session log](./SessionLogs/2026-09-17.md). Static asset/geometry checks passed; Unity/player visual confirmation remains pending.

On 2026-11-04, merge `45d4b28a` brought ten old layout properties on the hub's `KeyMapping_Layout` and `OptionTitle` back from the second parent. The first parent already inherited the prefab. When the defect recurs after a merge, compare the scene instance in both parents and the merge result. Older scenes also pinned geometry on the rendered button and label below the slot. See the [settings scene repair](SessionLogs/2026-11-05-nadoman354-settings-scene-layout.md).

## 2026-09-17 - Title Overlay obscured software cursor after display-mode change

Evidence: In the rebuilt player, Windowed 1280x1024 -> FullScreenWindow 1920x1080 changed the title Canvas from ScreenSpaceCamera/UI/0 to ScreenSpaceOverlay/UI/0. CursorDiagnostic showed the cursor still enabled/active/opaque, not culled, inside the screen and without a hidden owner, but on Default/32767. The existing UI sorting layer is above Default. Returning to Windowed restored the camera-rendered title and cursor visibility; the user also observed visibility return after game start.

Fix: MouseCursorService assigns the existing UI sorting layer in both software-canvas creation and authored binding, retaining order 32767. Sorting order alone does not put a lower sorting layer above UI. The earlier unreadable-texture fallback warning explains why software rendering is used, but is not itself the display-switch failure. The display letterbox disappears at the logged 16:9 transition, excluding it as the direct occluder for this case.

Prevention: Define both sorting layer and order for overlay cursors. Compare canvas render modes across aspect-ratio/display transitions, not just cursor visibility flags. Keep bounded diagnostics until a rebuilt player confirms Fullscreen and Borderless behavior. Native post-fix visual acceptance remains pending.

## 2026-09-17 - Room candidate inserted outside its serialized list

Cause: appending a RoomObjectPlacementData item before monsterWaves assumes serialized field order. Dragon/Slime normal Stage3 samples place travelEndpointPlacements: [] before monsterWaves, so this anchor put the candidate outside objectPlacements. Text/GUID counts passed while Room Piece did not show the candidate.

Fix: move the existing item into the actual objectPlacements sequence in eight templates. Preserve coordinates and unrelated data. Parse all 30 room documents and assert candidate membership in buildData.objectPlacements, not merely presence in the file.

Prevention: resolve the target sequence boundary from structure; never infer it from a later sibling field. Validate parsed parent membership, reference/type/ID uniqueness and unchanged non-target data. Byte-preserving edits and whitespace checks alone cannot prove semantic correctness. Unity import and visual acceptance must be reported separately. See [session log](./SessionLogs/2026-09-17.md).

## 2026-09-17 - Camera manual-update mode and false status-HUD fallback warning

CameraShakeService called CinemachineBrain.ManualUpdate while temporarily selecting LateUpdate mode, producing repeated Editor errors while paused. Select ManualUpdate for the explicit call and restore the original modes in finally; the installed Cinemachine 3.1.5 uses the same non-fixed camera filter for both modes. Existing call timing and shake parameters remain unchanged.

StatusHudEntryView checked hard-coded child names and an uninitialized RectTransform cache before resolving assigned references. The authored prefab has different child names with valid references, so it emitted a false fallback warning. Resolve the existing root first and check actual serialized references; preserve rendering/fallback behavior in this minimal slice. Successful per-frame status-update logging was removed without changing status updates.

These are verified code/diagnostic issues, not proven causes of the run-entry native Transform::CountNodesDeep crash. MSBuild passed; Unity Play Mode and crash reproduction remain pending. See [session log](./SessionLogs/2026-09-17.md).

## 2026-09-17 — Batch UI tests interrupted by editor prewarm trace file locking

- Observed: the chest selection coroutine test failed on repeated `IOException: Win32 IO returned 1224` from `PrewarmTraceRuntime.Flush`, while the seven synchronous cases passed. The stack ran through `EditorApplication.update` and wrote `PrewarmTrace_HOSEO_LG17090.json`; AssetDatabase was also importing that generated trace file. This was unrelated to inventory assertions.
- Test isolation: the UI coroutine temporarily unregisters only the recorder's `FlushIfNeeded` editor update delegate and re-registers it during teardown. Do not globally ignore error logs to make the test pass. The next batch run passed all eight cases.
- The production recorder is unchanged. Treat resilient editor trace flushing as a separate follow-up, and remove only test-generated trace-session changes when cleaning the task diff.
- 2026-10-11 recurrence: the electric relic suite hit the same Windows1224 exception while writing `PrewarmTrace_nadom_DESKTOP-I2QAD8O.json`. Re-running with the existing `TestScenePresentationTraceIsolation` command-line option `-uiBatchValidationTests` removed recorder I/O from disposable fixtures without suppressing errors or changing production flushing. A later dash-duration assertion failure was separately diagnosed as fixture timing; it was not attributed to the recorder. See [electric revision record](./SessionLogs/2026-10-11.md).
- 2026-10-30 recurrence: Crimson charge Play Mode fixtures again encountered this mapped trace-file lock. Use the same `-uiBatchValidationTests` isolation for this suite; no production recorder fix or blanket log suppression belongs in the weapon change. Keep damage/target assertion failures separate from recorder exceptions. See [charge integration record](./SessionLogs/2026-10-30.md).

- 2026-11-01 recurrence: encyclopedia UI tests also require `-uiBatchValidationTests`. The unisolated rerun produced14 trace-lock failures; a separate authored-gradient assertion still expected the old hierarchy and was corrected for the approved sprite/inset layout. Keep these causes distinct. Production trace code is unchanged; see [integration log](SessionLogs/2026-11-01.md).

## 2026-09-18 — Guided tutorial dash retained its own UI control lock

- Source inspection found that GameFlowInputBlocker acquires UIManager's external block, whose BlockControlOnly profile also applies player control tags. Resuming time and directly calling GAS while retaining that block can reject the guided dash.
- The prototype releases only its UI flow block before trying the dash, reacquires it on rejection, and leaves independent pause/control ownership intact. Weapon/dash/swap input blocks remain owned until presentation cleanup.
- Isolated native regression verifies release/reacquire on rejected confirmation; shared time-service overlap/restoration checks pass. Full-scene Play Mode confirmation remains pending. See the tutorial section in [session log](./SessionLogs/2026-09-18.md).


### 2026-09-18 — Tutorial combat restoration must include presentation and lock handoff
- Restoring combat components to a tableau scene left its Witch Animator controller in place; enabling it displayed Chloe. Validate the runtime controller together with the restored boss, and check scene EventSystem duplication.
- Hiding HUD GameObjects before DialogueService captured them made inactive states become the dialogue restore baseline. Playable tutorial now owns DialoguePlayback suppression and releases it for fade-in.
- Editor scene normalization can ForceReleaseAll on PlayerCinematicProtection while the tutorial still caches ownership and maintains None. Combat handoff must restore its None interaction state even if the token was already released. Regression includes real interaction/input components and that normalization sequence.
- Related: `Assets/_Project/Runtime/Features/Tutorial/TutorialBossEncounterSequence.cs`, `Tools/Validation/TutorialBossCombatRegression.cs`, `Tools/Validation/TutorialBossSceneValidation.py`.


### 2026-09-18 — HeavySlash approach completion bypassed wall-safe movement
- A motor can stop at a wall while an ability coroutine later overwrites Transform.position with the unvalidated requested destination. HeavySlash first approach had this path even with motor wall safety enabled.
- First approach now clamps its body-cast destination and retains the achieved position at completion; no minimum positive travel is forced when already touching a wall. Regression checks blocked/clear casts and absence of completion teleport.
- This confirms a wall bypass mechanism, not the user's opposite-side target-coordinate cause. Commit-dash/other-pattern direct position writes remain outside this initial-approach fix.

### 2026-09-18 — Tutorial gunner retreat assumed a root Animator
- Symptom: gunner reacts to attacks but the basic-attack tutorial counter stays at zero.
- Confirmed cause: Editor.log contained repeated MissingComponentException at TickGunner's root GetComponent<Animator>().SetBool. The real prefab's Animator is on a child. The exception prevented clearing retreating, leaving progress gated.
- Fix: let existing Mob.UpdateAnimation own animation through its authored reference; the tutorial only requests motor motion and completes retreat. Do not add a root Animator to conceal the hierarchy mismatch.
- Regression: child-only Animator fixture, retreat completion, nine held hit events, duplicate rejection and gunner death/stage advance. Previous root-Animator fixture masked the real prefab mismatch.


## 2026-09-18 - Boss HUD Slide Requests During Dialogue Suppression

Related to inactive UI coroutine ownership above: BossHudController previously cached visible=true before StartCoroutine, even while its canvas was inactive. Subsequent refreshes could skip the entrance as already applied. Disabling the canvas could also interrupt a slide without invalidating its cached state. Defer slide requests while inactive and invalidate initial presentation state on disable; keep combat registration intact so restoration can replay the existing entrance. Verified by the isolated TutorialBossHudRegression checks; the reported tutorial scene symptom has not been visually reproduced in Play Mode.


## 2026-09-18 - Chest UI Edits Missed Embedded Global Prefab Copies

Symptom: selected items lost their old backgrounds but permanent empty selection frames did not appear, despite a passing standalone prefab structure check. Cause: ChestUI.prefab edits do not propagate to the independent embedded chest hierarchies in GlobalUIRoot and its variants. Fix: applied the same narrow frame authoring to the six global UI prefabs and validated all seven copies plus native sprite import. Prevention: trace the actual runtime prefab ownership before editing or claiming visual application; a standalone prefab/source build check does not certify the live UI.

## 2026-09-18 - Tiny health depletion skipped the death notification

- Symptom: a monster can have an apparently empty health bar, continue moving/attacking, and no longer lose health from subsequent hits.
- Verified mechanism: AttributeValue.RecalculateValue wrote zero but skipped OnValueChanged when the previous positive residual was at most 0.001. Mob checks for lethal health only in OnEnemyAttributeChanged; subsequent zero-to-zero damage does not emit another event. Float accumulation can leave such residuals (180 HP with repeated 1.2 damage is covered).
- Fix: always notify a positive-to-nonpositive transition, preserving the threshold for other small changes and avoiding duplicate notifications at zero. Threshold-based event suppression must not swallow depletion transitions used by gameplay lifecycle consumers.
- Verification: isolated Unity regression fails on the old source and passes after the fix; Core MSBuild passes. The reported ArcaneTankGolem jump encounter itself has not been reproduced in Play Mode, so this mechanism is confirmed but encounter attribution remains unconfirmed.
- Regression: Tools/Validation/AttributeDepletionRegression.cs. See [session log](./SessionLogs/2026-09-18.md).
## 2026-09-18 - Same-state final pattern transition and premature encounter completion

- Demon King's recall-to-final transition attempted to enter the already-current `BossPatternExecuteState`; the state machine correctly ignored that transition, so the final ability never entered. Exit the old state first, then reserve the new pattern, then re-enter. Reserving before exit is also unsafe because exit clears reservations. Interruption must release recall motion and presentation ownership.
- Slime phase-one death called `NotifyBossDefeated`, which paused run completion timing even though split bosses remained. Individual deaths in a managed encounter must defer the notification to the encounter's clear condition. The director now reports completion before its optional reward delay.
- The actual Demon King scene contains an unpacked object missing status-HUD components already present in the prefab. Validate the runtime scene hierarchy, not just the source prefab.


## 2026-09-20 - World-clipped telegraphs were scaled again by caster parents

- `AttackTelegraphService` parents its reusable view beneath the caster. The mesh renderer used raycast distances and directions measured in world space directly as local mesh vertices, so caster scale/reflection distorted the already-clipped result. World rotation alone does not cancel inherited scale or shear.
- Keep clipping/progress math in world units, transform final mesh offsets with `worldToLocalMatrix.MultiplyVector`, and submit border positions in world coordinates. This preserves the existing parent-owned cleanup lifecycle; detaching views is unnecessary.
- Regression: `Tools/Validation/StatusTelegraphRecoveryRegression.cs` compares transformed mesh and outline coordinates across uniform, reflected and nonuniform rotated parents, including the wall endpoint.


## 2026-09-20 — Chest reroll destruction must unbind never-enabled slots

- Observed in the isolated Unity regression: a slot bound while inactive subscribed to ChestContainerAdapter.OnChanged, then was destroyed during reroll before ever becoming active. A subsequent reroll invoked Refresh on the destroyed ItemSlotUI and threw MissingReferenceException.
- Cause: relying only on OnDisable to release subscriptions created by Bind; a never-enabled object has no matching disable callback.
- Fix: ChestScreen explicitly calls Bind(null, -1) before deleting generated slots in both full cleanup and unselected-offer rebuild. Selected views remain bound and alive.
- Prevention: when explicit binding can precede activation, release binding explicitly in the owner teardown path. ChestSelectionRecoveryRegression repeats rerolls and checks retained views/cells.

## 2026-09-20 — Tutorial cinematic token lost during Editor startup

- Evidence: Editor.log recorded PrototypeTutorialOpeningSequence acquiring PlayerCinematicProtection, then SceneDomainCoordinator.NormalizeDevelopmentPlayerState calling ForceReleaseAll while the opening yielded its letterbox startup, before player arrival began. The sequence later set interaction to None after waking, but its final Release had no token left to restore Idle. This explains the reported post-cinematic control loss; do not solve it by force-enabling every player input or releasing other owners.
- Fix: acquire cinematic protection after yielding letterbox startup and immediately before scripted arrival captures its input snapshot. Keep the same token through wake and release at the requested 70% escape handoff. Native regression simulates the normalizer, exercises the updated acquisition order with real input/interactor components, and checks that handoff restores input/Idle while early wake stays locked.
- Camera ownership: the opening now owns an authored camera anchor; scripted arrival explicitly skips its internal camera rebinding when an external sequence owns framing. Keep startup/landing/wake and cancellation under the same owner to avoid camera jumps.

## 2026-09-20 — Camera getters erased tutorial focus

- Cause: CameraBootstrap.GetMainCamera/GetPlayerCamera/GetBrain/GetLegacyFollow called EnsureRuntimeRigForCurrentScene on every read. EnsureRuntimeRig reset camera priority and rebound legacy Follow/LookAt to PlayerRuntimeRegistry's player, even while the legacy follower was disabled by a cinematic. PlayerHubSpawnPresentation2D.ResolveStartPosition reads the main camera immediately after the tutorial assigns its opening anchor, so the authored focus was overwritten.
- Fix: return existing cached camera references without running setup. Missing-rig initialization and explicit scene-load/player-registration setup are unchanged; no DDOL lifecycle or scene-domain policy changes.
- Regression: CameraFocusQueryRegression repeated all four getters and failed before the fix with `Read queries reset cinematic priority`. The fixed implementation must preserve both target and priority, including when the optional legacy follower is absent. Avoid treating a state-mutating Ensure operation as a read accessor.

### 2026-09-20 — Chest initial height contaminated by decorative frame dimensions
- Symptom: five initial items produced a two-row grid but retained GlobalUIRoot's 415-high body instead of 225.
- Cause: ResolveHorizontalFrameHeight took the maximum of the measured grid height and authored/previous side decoration heights; CaptureInitialLayout then froze that result.
- Fix: a measured center grid determines body height plus frame padding; decorations follow the result. Top/bottom frame measurement remains unchanged.
- Regression coverage: ChestSelectionRecoveryRegression.FixedInitialSize includes both 415-high side decorations, five items with actual grid padding, selection/reroll/return, and recapture for a subsequent one-row chest. A grid-only fixture had missed the defect.

## 2026-09-20 — Scripted tutorial death bypassed item scatter

- The tutorial boss encounter intentionally disables PlayerDeathReturnToHub2D while it owns combat defeat, post-defeat dialogue and tutorial game-over routing. It calls PrepareForScriptedDeathPresentation at zero HP, which previously only blocked controls. Item scatter existed only in the normal CoDeathSequence, so the tutorial route never called it.
- Share scatter from the scripted preparation entry without re-enabling automatic death routing. Guard duplicate scatter and preserve original containers; emit non-interactable visual copies only. Also clear copies on destruction: a component disabled before scripted spawning cannot rely solely on a later OnDisable to clean new objects.
- Isolated native regression covers the disabled scripted entry, repeated calls, inventory retention, normal death route remaining inactive, and disabled-player destruction cleanup.

## 2026-09-21 — Forced parallel execution skipped deferred cooldown

- Symptom: entering a boss room during active Flowering Bloom left its skill ready again. Bloom uses a parallel execution and starts its 240-second base cooldown on end.
- Cause: scene travel cleans active Bloom and forcibly stops its coroutine before capturing runtime state. ForceCleanupParallelExecution did not settle startCooldownOnEnd when the execution coordinator finally block was skipped, allowing a zero cooldown snapshot.
- Fix: cancel and stop first, then settle a non-refunded deferred cooldown only while the execution token remains; normal finalization clears that token. Repeat cleanup does not restart an existing cooldown. This shared forced-parallel path also serves forced resets and cancellation.
- Coverage: parameterized persistent-state round-trip tests and an isolated Unity native coroutine-stop harness. Active Bloom still ends on travel; carrying the active buff across scenes is outside this fix.

## 2026-09-21 - Ending/loading omitted from the shared display viewport
- Symptom: user reported ending skip UI clipping in a 16:10 fullscreen player. The earlier 21:9 report was corrected to 16:10.
- Verified defect: GamePresentationController adapted only a fixed list of gameplay canvas layers. FadeInOutCanvas (ending) and LoadingCanvas were excluded, leaving their screen-edge content outside the intended central 16:9 area. Layout calculation showed the old skip rectangle overlapping the bottom-bar region; exact original draw-order occlusion was not runtime-confirmed.
- Fix: adapt direct Canvas children of GlobalUIRoot, preserving world-space canvases and excluding nested/service-owned overlays. Refresh when the root instance changes as well as when display/camera changes. Keep the existing restore behavior for a full viewport.
- Prevention: include inactive presentation canvases when checking viewport coverage; validate a player build because Editor Play bypasses letterboxing. Do not compensate by merely moving the skip label or assuming a larger sortingOrder always draws above another sorting layer.
- Validation: UI.csproj MSBuild passed; prefab coverage and six resolution geometry cases passed. Actual player visual/input verification remains pending. Structure map: Docs/StructureMemory/SettingsPanelLayout.md.

## 2026-09-21 - Variable-step UI chain resting jitter
- Report: chains across multiple screens visibly trembled while idle, especially at small resolutions.
- Reproduced mechanism: SettingsPanelFakeChainPresentation used ceil(frameTime/maxStep) and frameTime/count, changing Verlet integration duration between frames without rescaling stored displacement. In an isolated native Unity fixture, stationary endpoints still produced 0.1227722 local-unit peak-to-peak motion with frame times straddling 1/60 second. This identifies a solver defect, not definitive attribution of every low-resolution visual artifact.
- Fix: accumulate elapsed time and integrate at constant ticks; clear leftover time on reset and limit stall catch-up. Preserve UIChainDropPresentation's existing panel settle path and authored simulation parameters. No additional chain sleep behavior.
- Verification: UI.csproj MSBuild and isolated Unity fixed/variable-time, anchored/free-end, motion and reset checks passed; fixed-driver residual=0.00001525879 local units. Small-resolution player rendering and input feel remain unverified. Regression: Tools/Validation/ChainFixedStepRegression.cs.

## 2026-09-21 - Hidden fixed action mutated by keybinding swaps

The title/global keybinding row lists omitted DialogueAdvance, but the service still advertised it as remappable. Assigning Space after moving Dash, or resetting Dash individually, could move DialogueAdvance to another key; assigning Space to an empty secondary could clear it. Overrides persisted across restarts. Fixed-action policy now governs reads, mutation, conflict membership and persistence; stale overrides are removed. Preview applies only changed slots, avoiding reversal of an intra-action primary/secondary swap. Shared Interact/InventoryDrop F owners must move together when swapping with another action, otherwise InventoryToggle can still collide with dropping. See Docs/StructureMemory/InputBindings.md and Tools/Validation/KeyBindingRegression.cs.

## 2026-09-21 - Inspect the authored arrow sprite before choosing rotation
- Symptom: Buffy workout guidance arrows lay horizontally.
- Cause: authoring assumed the shared sprite pointed left and applied +90 degrees, but `DownArrow.png` already points down.
- Fix: preserve identity rotation for the three Buffy arrow roots and the content installer.
- Prevention: inspect the actual referenced image and both root/renderer transforms; a passing reward/lifecycle test does not verify visual direction.


## 2026-09-28 - Cursor Sorting Fix Was Present in Player but Ineffective

- Evidence: the September 27 Player.log contained 32 cursor snapshots at Default/32767 while competing UI canvases used the higher UI sorting layer. Cursor images were active, opaque and not culled. Inspection of that player's Infrastructure.dll confirmed the previous UI-layer assignment was included, so a missing rebuild did not explain the observations. The exact native initialization/reset point was not isolated.
- Correction: replace project-owned cursor Canvas rendering with Unity Cursor API. Convert non-readable theme sprites instead of hiding the OS cursor and falling back to an Image. Preserve owner-based hidden state during display notifications and restore default cursor on service disable; only the active service restores global cursor state on destruction.
- Prevention: verify actual player cursor backend/state rather than treating source sorting assignments or MSBuild success as visual acceptance. CursorMode.Auto requests Unity's supported backend and is not proof of hardware use. Native screen-edge behavior differs from the former image clamping.
- Validation and remaining player checks: `Docs/SessionLogs/2026-09-28.md`; regression source: `Tools/Validation/MouseCursorApiRegression.cs`.


## 2026-09-28 - Escape Recapture and Oversized Window Selection

- Evidence: the user's rebuilt player logged Windowed 1680x1050 followed by a new display request and Windowed 3440x1080. Saved preferences were Windowed 3440x1440. GameSettingsService injected ultrawide options without monitor bounds; the cursor service continuously forced Confined while focused and had no Escape-release state. Both behaviors predated the API migration and were missed in its original coverage.
- Correction: retain explicit Escape release until a focused inside-client click; focus/display callbacks cannot erase the latch. Filter window sizes against work area minus frame decorations; proportionally fit and persist oversized saved selections, and recheck bounds on application.
- Prevention: a cursor backend regression must cover confinement intent as well as visibility. Display tests must compare requested and actual client dimensions and saved settings, including a window larger than the desktop; avoid independent axis clamping that distorts aspect.
- Verification: see `Docs/SessionLogs/2026-09-28.md` follow-up.

## 2026-10-03 - Localization inventory blocks interactive Editor

- Evidence: user screenshot names LocalizationInventoryExport delayCall and a two-hour busy wait; no inventory completion record. Internal blocked API remains unknown.
- Prevention: never automatically run broad asset/prefab/scene inventory after domain reload in the user's interactive Editor. Require isolated batch mode after the user saves and closes the Editor, and log each asset boundary for diagnosis.
- Recovery limitation: removing the callback source does not interrupt the already executing managed callback. Forced termination requires user authorization because unsaved work can be lost.
- Verification: source-only guard inspection; compile/reproduction pending Editor recovery.

### Localization recovery verification and serialized binding traps

- Isolated reproduction stopped progressing after BG_Witch.asset during broad ScriptableObject traversal. The precise internal blocked API was not stack-captured. Restrict to reviewed source paths and skip ManagedReference descendants; filtered export completed with 2,508 rows.
- Unity source edits compiled in batch. Readback exposed stale inherited StringReference keys in scene prefab instances even after a scene save. SetDirty alone is insufficient for these direct property writes; RecordPrefabInstancePropertyModifications records the intended overrides. After repair, 492 caption and 1,897 font bindings passed persistent target/method/state and five-locale setter checks.
- Windows error 1224 occurred overwriting an imported Ink JSON with a mapped file handle. ReleaseCachedFileHandles then atomically replace a temporary file; all 16 active Ink files compiled successfully on retry.
- Do not equate successful table/glyph/binding checks with actual screen layout or Play Mode acceptance. See Docs/SessionLogs/2026-10-03.md.

### Localization display regression follow-up

- TMP font setter replaces the material when the atlas changes; direct localized set_font binding lost authored Outline/style. Bind the adapter and use TMP's public fallback material API with balanced references. Validate source style properties and the target shader _MainTex, not Material.mainTexture aliases.
- Filled tables missed quoted weapon variant literals and dynamically composed [[stat ID]] captions. Add explicit variant keys and raw-ID term captions; verify actual formatted display strings without translating gameplay IDs or percent classification inputs.
- A translated NPC asset getter does not refresh an already visible name. Locale callbacks must update the speaker label as well as the dialogue body, including pending dialogue transitions.
- Deferred Destroy left old relic sections in the same-frame layout calculation. Deactivate them before destruction; retain normal text-dependent height changes.
- Batch collection lookup returned null despite an existing collection asset. Resolve the known asset path before trying to create a new collection. Exact cache timing cause is not established.
- Verification: 2026-10-03 SessionLog follow-up; actual Play Mode visual acceptance remains separate.

### Screenshot disproved the font-style baseline

- User screenshot still showed no Outline and untranslated world nameplates/count. NpcNameplate.prefab authoredMaterial referenced NotoSansCJKtc with OutlineWidth=0, while HEAD referenced Galmuri9 BlackOutline with width=0.314. Prior equality tests compared against a contaminated baseline and could pass without visible Outline.
- Restore Korean original material references/presets through Editor APIs and reject localization-font materials as authored style. Also copy Korean preset properties into CJK default materials, retaining their own atlas/gradient/weight. Five-language rendered Outline-on/off pixel differences supplement property checks.
- Dialogue name labels and world nameplates are separate execution paths. Table completeness and DialogueView tests did not cover OnEnable-only NpcNameplatePresenter or merchant count refresh. First initialization and live locale changes require actual presenter lifecycle coverage.

### Dynamic relic and tutorial coverage gaps

- A general StatModifiers tooltip test missed weapon-exclusive description passthrough and TimedStat triggerLabel tokens. Validate actual ItemDatabase definitions with their assigned logic at every authored level, then format glossary/semantic tags before checking visible Hangul.
- Localized DefaultEffectTemplate does not help if BuildTooltip passes a separate Korean fallback. Reuse the default template or add a fixed key for that fallback; also test empty-template paths.
- Tutorial request page.title/body bypassed fixed-caption bindings. Key pages by the existing tutorial ID/index and refresh text without invoking navigation/hold-reset methods. Test actual scene-authored pages and retained hold/page values across locale changes.
- A scene traversal can silently skip a presenter supplied only by a spawned prefab. Load RunMerchantGroup explicitly and require MerchantRefreshInteractable to exist before testing its count refresh. Do not report presenter coverage from an empty foreach.
- Verification: 2026-10-03 omission follow-up in SessionLog; 1,500 dynamic projections, 8,591 checks, Unity/MSBuild and Addressables passed. Actual Play Mode visuals remain separate.

### Game-flow audit found display paths outside the original inventory

- Unicode-escaped const strings, non-Ink SpeechData arrays and nested intro/ending
  slides bypassed the original text inventory. Empty-cell/key/glyph tests do not prove
  output coverage; trace actual getter → formatter → TMP/Speak/SetText for each flow.
- Encyclopedia title localization existed only in the empty preset branch, while the
  real prefab always provided nonempty Korean presets. Test actual authored nonempty
  values and failure/override branches, not only default fallback lookups.
- Title card runtime values also had fixed String Events bound to authored captions.
  A delayed locale event could overwrite numbers. Remove fixed events only from
  explicitly runtime-owned labels via Unity API and keep the inventory aligned.
- Title PreviewScene Canvas has authored zero scale until CanvasScaler runs; geometry
  probes must emulate a visible scale. Runtime bounds code skips zero parent scale.
- Verification: 2026-10-03 SessionLog. Broad game-flow Play Mode coverage still pending.

## 2026-10-04 — 중첩 표시 데이터와 언어 전환 재추첨

- Speech entries/lines, Intro/Outro slides.text와 RouteSet 표시명은 단순 최상위 문자열 조사에서 빠졌다. 실제 사용 자산의 SerializedProperty 경로를 export하고 모든 배열 후보를 표시 getter까지 검증했다. 직접 원문 Text/GetLine 반환과 비어 있지 않은 도감 preset의 번역 우회를 수정했다.
- UI locale refresh 때 Random을 다시 호출하면 사망 문구가 바뀐다. Begin에서 고른 형식 키를 유지하고 적의 표시 키를 사라지기 전 확보하여 문장 대조 없이 다시 번역한다. 검증은 speech의 RNG 소비량과 death cache의 재추첨 없음도 확인한다.
- Editor Preview는 Awake를 자동 보장하지 않는다. GameOver의 비직렬화 titleText가 ResolveReferences에서 설정되므로 실제 초기화 경로를 실행한 뒤 표시를 검증한다. 데이터 슬롯에 직접 참조가 없다는 이유만으로 runtime UI 누락이라고 판정하지 않는다.
- 관련: Docs/StructureMemory/LocalizationFlow.md, Docs/SessionLogs/2026-10-04.md. 실제 Play Mode 입력/프레임/레이아웃 검수는 별도.

## 2026-10-04 — 실제 화면 검수의 초기화/위치 계산 함정

- Batch executeMethod 진입 직후 collection import가 null로 실패한 경우, authoring 저장 후 EditorApplication.delayCall에서 검증을 시작하니 정상 로딩/검증됐다. 실패한 첫 실행을 성공으로 보고하지 않는다.
- 실제 게임 언어는 GameSettingsService의 저장 설정이 초기화 때 적용한다. SelectedLocale만 직접 변경하면 초기 preference가 다시 덮어쓸 수 있다. 실제 SetLanguage를 사용하고 검수 이전 preference를 복원한다.
- 씬을 연속 직접 로드하면 정상 portal 경로의 camera/overlay 수명과 다르다. 검수는 씬별 새 Editor Play Mode에서 실행하고 검은 PNG를 실패로 판정한다. 직접 ShowHover만 호출하면 HoverUIController의 경계/anchor 계산을 건너뛴다. 화면 밖 tooltip은 실제 hover controller 경로에서도 재현되는지 확인한다.
- isTextOverflowing만으로 시각적 잘림을 판정하지 않는다. Overflow 모드, TMP color alpha0인 숨겨진 허수아비 라벨, 화면 밖 월드 라벨을 구분하고 실제 PNG를 함께 검토한다. 허수아비 초기 한국어 문자열은 alpha0이며 첫 피격 이후 실제 표시 때 번역 getter를 재실행한다.
- ExitPlaymode의 domain reload가 없는 설정에서도 cleanup이 실행되도록 Editor update와 초기화 callback 양쪽에서 guard된 복원을 예약한다. 검수 시작 전 정확한 저장 경로의 byte backup을 만들고 종료 후 parity를 확인한다.

### TMP Outline이 남긴 CanvasRenderer atlas override

- RunTimerHUD는 TMP outlineColor/outlineWidth를 설정한다. 설치된 UGUI/TMP TextMeshProUGUI.SetOutlineThickness는 CanvasRenderer.SetMaterial(material, texture)를 사용해 renderer texture override도 지정한다. 반면 UpdateMaterial은 SetMaterial(material,0)만 호출한다.
- LocalizeTmpFontEvent가 font/fontSharedMaterial을 바꿔도 기존 renderer override가 남으면 `15:00` 문자열과 새 UV는 정상인데 이전 atlas의 이미지 조각을 그린다. material.mainTexture와 font.atlasTexture 비교만으로는 이 override를 검출하지 못한다.
- 글꼴 교체 시 TextMeshProUGUI의 canvasRenderer.SetTexture(matched.mainTexture)를 함께 호출한다. 추가 머티리얼 인스턴스/매 프레임 보정/타이머 로직 변경 없이 대상 atlas를 맞춘다. 실제 Play Mode의 타이머 픽셀도 확인해야 한다.

## 2026-10-05 - Encyclopedia ScrollRect layout and scrollbar authoring traps

- ItemDetailPanel의 활성 VerticalLayoutGroup이 ScrollRect.content인 ContentPanel의 위치/높이를 제어했다. 이동하는 Content의 위치는 ScrollRect가 제어해야 하며, 콘텐츠 내부 배치용 LayoutGroup과 구분해야 한다. 부모 레이아웃 갱신은 사용자가 이동한 위치를 덮어쓸 수 있다.
- 스크롤바는 높이 0, Sliding Area는 폭 0/높이 -20인 설정이었다. 참조가 연결돼 있다는 사실만으로 조작 가능 여부를 판단하지 말고 실제 RectTransform 크기와 레이아웃 제외 여부도 확인한다.
- 세로 ScrollRect의 normalized position은 1이 위, 0이 아래다. 연결된 Scrollbar는 BottomToTop 방향으로 설정해야 손잡이 위치와 본문 위치가 일치한다.
- GlobalUIRoot의 nested prefab override가 손잡이 AnchorMax.x를 0으로 고정하고 있었다. 기본 프리팹 수정 뒤에도 기존 인스턴스 재정의가 폭/방향/위치를 덮어쓰는지 확인한다.
- 위 설정을 수정하고 ID/참조 보존 및 nested override 대조를 통과했다. 실제 Play Mode 입력/시각 검증은 미실행이며, 상세 범위와 수동 확인 절차는 `Docs/SessionLogs/2026-10-05.md`에 기록했다.
- 후속 본문 폰트 편집에서 PrefabUtility.LoadPrefabContents가 TMP의 null sharedMaterial을 채우고 Scrollbar가 손잡이 anchor를 구동하여 본문 외 값이 저장됐다. 저장 성공만으로 변경 범위를 검증하지 말고 작업 전 사본과 컴포넌트별 실질적 차이를 확인한다. 이 작업은 원본 prefab asset의 Unity API 저장으로 부작용을 정리하여 기존 스크롤 설정을 보존했다.
- BlackOutline 재질만 연결해도 TMP vertex color가 검정/진회색이면 본문과 검은 외곽선이 구분되지 않는다. 본문 글자색과 LocalizeTmpFontEvent.authoredMaterial도 함께 확인하고, 중첩 인스턴스의 오래된 font/material/cache override가 원본 수정 내용을 가리지 않는지 검증한다. 스킬 본문의 일반 Galmuri authoredMaterial을 BlackOutline으로 수정했으며 5개 언어의 스타일/atlas 연결 35개 검사를 통과했다.
- 2026-10-11 creature authoring에서 loaded prefab의 native component를 `EditorJsonUtility.ToJson` → `FromJsonOverwrite`로 복원하면 내부 fileID/native reference가 정상 remap되지 않아 기존 참조가 사라졌다. 기존 prefab 사본을 복구하고 JSON round-trip 복원을 제거했다. prefab 내부 객체 참조의 보존은 원본/결과 컴포넌트별 serialized reference와 실질적 diff를 비교하며, 필요한 값만 명시적인 Unity serialization/property 경로로 보존한다. JSON snapshot이 만들어졌거나 Unity batch exit가 0이라는 사실만으로 authoring 성공을 판정하지 않는다. 최종 reference/semantic 비교는 해당 작업의 검증 자료에 기록한다.
- 같은 authoring에서 prefab load/preview가 기존 TMP/monster HUD material 값을 변경하고, metadata identity의 `RequireComponent(Mob)`가 legacy elite Frog의 누락된 required motion component까지 추가했다. metadata component는 기존 gameplay dependency를 강제하지 않도록 requirement를 제거했고, 승인된 identity 추가 외 원래 monster document 값을 복원·비교했다. Unity 자동 dependency 추가와 editor preview 값은 사용자 승인 범위를 넓히지 않는다. 최종 보존 비교는 [검증 report](../outputs/encyclopedia-implementation-2026-10-11/serialized-preservation-report.json)를 참고한다.

### 2026-10-12 - Creature tabs must hide the complete Item detail and share PageCover

- ItemRightPage의 내부 Content만 숨기면 형제인 이름·스토리·힌트·배경이 Creature 상세 뒤에 남는다. presenter가 붙은 RightPage 전체를 끄면 같은 root의 책갈피도 사라진다. authored ItemDetailPanel 전체를 별도 표시 경계로 연결하고 기존 내부 projection root는 유지한다.
- 새 Creature pages가 PageCover 다음 sibling이면 같은 Canvas에서 Reveal 위에 그려진다. Content pages → PageCover → full illustration 순서를 authoring에 유지하고 실제 first/mid/post-reveal 화면을 검토한다. 책 상단/프레임을 바꾼 후에는 mask alpha도 대조해야 한다. 이번 기존 둥근 상단 틈은 sibling 순서만 고쳐도 확대된 portrait 일부를 노출했다.
- Item presenter를 제거하고 root를 미리 숨기는 isolated fixture는 실제 Item→Creature 통합 검증이 아니다. 실제 presenter·제목·스토리·책갈피를 보존한 round-trip과 hidden 입력/locale 검증을 별도로 실행한다. native 한글 캡처는 실제 LocalizeTmpFontEvent 로딩/적용 완료 뒤 수행한다.
- 숨긴 Item presenter가 책갈피 root에서 계속 활성인 경우 locale callback을 단순 무시하면 같은 Item selection 복귀의 early-return 때문에 이전 언어가 남는다. 숨김 시 pending만 기록하고 표시 시 현재 선택 projection을 갱신한다. [구조/검증 경로](StructureMemory/EncyclopediaCreatureTabs.md).

## 2026-10-07 — Test boss startup and blank hurtbox ownership

- TestScene instantiated active boss prefabs with their normal BossEncounterDirector. OnEnable disabled combat and acquired player protection/input/state locks before the spawn method returned. The open catalog independently blocked attack input. User Editor logs showed the Dragon entering its intro and then failing to find a DialogueController in TestScene.
- Test spawn now disables instance intro/death controllers in the same frame, lets OnDisable release their owners, calls FinishEncounterIntro before Start enters SpawnState and then BeginCombatEncounter. Successful actor generation closes the catalog and releases test input. Production prefab cinematic authoring is unchanged.
- A real hurtbox/GAS damage check exposed an additional DragonBoss authoring trap: CombatHurtbox2D.ownedColliders contained `[null]`. Awake's old Length>0 guard treated it as configured, so OwnsCollider rejected every actual collider. Entirely blank arrays now follow the existing own-collider fallback; arrays containing an authored collider preserve their explicit boundary. No prefab/schema edit was needed.
- Prevention: prefab instantiation proves neither combat readiness nor damage target resolution. Check Start/OnEnable owners and actual physical hurtbox-to-GAS HP reduction. Validate arrays for usable references rather than length alone; do not widen an explicitly authored collider list.
- Source and focused regression checks: `TestSceneValidation.VerifyBossCombat`, `CombatPresentationRegressionPlayModeTests.Hurtbox_*` and the active boss-combat task. Final runtime evidence is recorded in the 2026-10-07 SessionLog.

## 2026-10-08 — Inactive chest size snapshot covered the fourth offer

- `31766cfc` changed GlobalUIRoot RightMiddleFrame's stored Rect width56 to13 but retained LayoutElement.minWidth56. The first-open cache was already present. uGUI LayoutUtility ignores inactive Behaviours, so capture under inactive InventoryRoot used13 and cached total468; active arrangement later used56 and covered39 units of the grid (29 of the fourth slot). The previous total511 had no overlap.
- ChestScreen/ChestFirstOpenRevealPresentation sources matched the pre-UI-optimization manifest, and the compared chest subtree matched semantically. This was an older authored-data/lifecycle interaction, rather than the player inventory slot-reuse change.
- Fix: inactive width measurement also reads enabled authored LayoutElement min/preferred widths. Active uGUI measurement and disabled-layout behavior are retained; no scene/prefab/GUID change or UI activation is needed.
- Prevention: capture and display must use the same layout constraints. Test real authored inactive-bind→active-reveal geometry and both viewport containment and foreground-decoration overlap; a correctly placed grid or a basic nonblank image test does not establish unobscured icons.
- Evidence: isolated Unity6000.4.2f1 before/after layout probe and focused ChestRevealWidthRegressionPlayModeTests. Final tests and preserved-authoring hashes are recorded in the 2026-10-08 SessionLog.

## 2026-10-07 — Performance fixture MaxHP inflated the heart HUD

- Symptom: the first Player probes showed about 85–100 ms idle frames and multi-second crowd frames. These numbers were rejected as game-performance evidence.
- Cause: the measurement helper raised linked maximum attributes to 100000 for survival. `PlayerHealthHeartHUD.GetHeartCount` divides MaxHP by `healthPerHeart` (1), and `EnsureHeartTokenCount` instantiates that many UI tokens; HP changes then traverse all tokens again. The fixture requested about 100000 hearts.
- Correction: measurement-only helpers preserve normal HP/resource values and hold one counted existing `State.Invulnerable` tag, releasing only their own count on cleanup. The player probe records actual heart-token counts and rejects counts over 128. No production HUD or authored asset was changed.
- Prevention: keep representative gameplay values in performance fixtures; check presentation object counts before interpreting slow frames. Separate byte-unit calibration from allocation-event counts: this backend reports `GC.Alloc` values as TimeNanoseconds, not bytes.
- Evidence: `Docs/SessionLogs/2026-10-07-frame-performance-measurement/README.md` and its excluded-probe archive. Final results use corrected fixtures only.

## 2026-10-08 — UI batching equivalence and asynchronous capture boundaries

- Symptom: replacing a zero-amount ready-flash material with UI/Default appeared similar in RGB but changed translucent alpha (actual authored Canvas sample 64 versus 128). Initial UI captures also showed extra fallback batches because locale font callbacks were not settled.
- Cause: the ready shader uses a different alpha blend even when its effect amount is zero. Fixed frame waits do not prove package font loading completed; GameSettingsService can also apply the saved language after its asynchronous String Table load.
- Correction: retain the same ready shader in shared immutable idle materials and compare exact RGBA/stencil output. Capture only after the app's saved-language initialization, successful global preload, active font handle/result agreement and two stable LateUpdates. Rapid overlapping test locale preloads and incomplete teardown were repaired through awaited test sequencing; logs were not suppressed.
- Prevention: distinguish rendering equivalence from visually similar RGB, runtime recorder samples from Editor native frame history, batch transitions from unique material counts, and permanent authored cells from generated selection slots. Native vertex zero/blank previews are unavailable evidence.
- Evidence: [UI batching report](SessionLogs/2026-10-07-ui-batching/README.md), 119/119 native regressions and the matched 18-screen capture pair. No localization package/bootstrap or general shutdown lifecycle fix was bundled.
- Fixture asset trap: cloning only one menu button left a serialized sibling sword reference pointing into the prefab asset. Awake/OnEnable hiding then changed that asset before the ownership assertion failed. Clone the minimum common subtree, inspect remapped references while inactive and compare protected asset hashes afterwards. The two defaults were restored to the exact starting snapshot and the guarded regression passed. Prefab API roundtrips can add unrelated nested overrides; review the complete diff rather than assuming a two-property edit produced only two differences.
- Structural authoring follow-up: LoadPrefabContents can select TMP default font materials and drive Scrollbar handle anchors before a scoped save. GetPropertyModifications/SetPropertyModifications also filtered redundant authored nested overrides in this project, stripping unrelated inheritance intent. Snapshot original persistent TMP/RectTransform values and the PrefabInstance's raw serialized modification array before loading; bind internal references while the hierarchy is unchanged, restore original font/style and raw modifications, then clear the loaded Scrollbar tracker and restore original handle values before saving. Verify the complete saved diff. Saving successfully or preserving modification count alone is insufficient. Windows mapped imported files can reject an in-place snapshot overwrite (1224); exact recovery used an atomic whole-file replacement with the Editor closed and SHA256 verification, never hand-edited YAML.
- Render-order follow-up: preserve a whole row-root Selectable subtree so its child Graphics retain original Button ancestry. The first paired pointer failure probed a hidden UISize child using activeSelf and hit an unrelated button from its stale transform; snapshot activeInHierarchy with each side rendered and keep strict checks for visible controls. Separately, attaching an anchor from an inactive logical owner to an active layer briefly fired locale OnEnable before hiding it. Set initial anchor visibility before reparenting; inactive text equality exposed this real lifecycle difference.
- World HUD follow-up: shared HP mesh and per-actor status Canvas reorder transparent cross-actor overlaps even when single-HUD RGBA matches. Test HP-only plus HP/count/timer intersections at equal/different Z and routes back to the legacy projection. Native crowd fixtures must separately record actual mesh/legacy rendering paths. Intentional overlapped rigidbodies moved through collision resolution; fixed-pose sorting/draw captures temporarily suspend only disposable actor physics with explicit restoration/metadata. Reject position/visibility failures as performance evidence.

## 2026-10-11 — Manager destruction must dispose relic procs and temporary tokens

The new dash relic exposed an existing ownership gap: RelicProcManager unregistered its gameplay relay on disable, but neither it nor RelicInventory disposed procs on owner destruction. A HideAndDontSave RelicRuntimeToken does not automatically die with the owning GameObject. Explicit unequip-only cleanup was insufficient.

RelicProcManager.OnDestroy now unregisters the relay and removes each proc from its list before calling Dispose, so reentrant unregistering is safe. Disable retains equipment state. Proc disposal removes its exact modifier/subscriptions and destroys its separate temporary token. The focused manager-destruction test checks the live owner's electric bonus returns to zero, the token is Unity-null, and later ability completion cannot reapply it. See `ElectricRelicPlayModeTests.DestroyingProcManager_ClearsDashBonusSubscriptionAndOwnedTemporaryToken` and [relic map](StructureMemory/ScriptSystems/RelicRuntimeStructure.md).

## 2026-10-11 — Local paralysis needs action time and stable attack ownership

Freezing a monster's Animator/FSM/coroutine does not freeze global Time.time deadlines or native WaitForSeconds. Rook dash, Mob recovery, ranged rest, detached warning expiry and attached attack lifetimes used such independent clocks. The approved paralysis adds a target-owned action clock that excludes only paralysis intervals, including the expired interval before LateUpdate has committed it. Ordinary hit pause and unbound world effects retain prior timing. Standalone coroutine terminal work must also wait when paralysis begins on its last yielded frame.

MeleeHitboxActor's immediate scan consumed a shared target ID before the base damage gate, so pausing only the base trigger could lose the hit permanently. Gate before ID insertion and preserve registries through resume. Bound classification must be cached separately from the Unity owner reference: destroyed owners become Unity-null, but their attached attack must not turn into an independent projectile. Death/disable stops bound work and prevents reopening a canceled timed effect after re-enable. Core reads Enemy's existing IsDead through ICombatActionState; it does not duplicate life state. New feature53/53 native cases passed, including remaining time, shared IDs, owner death/disable/destruction and independent projectile continuation. See [task record](SessionLogs/2026-10-11.md).

Suggested Later: expanded validation found `CombatFeelLootQuestPlayModeTests.Impact_FreezesParticipants_SlowsWorld_AndPreservesMenuPause` expects an ICombatHitPauseImmune boss to freeze, while the existing FreezeVictim method returns before adding a pause to that boss. The method is byte-identical between HEAD and the current source; the failure occurs at the immediate victim assertion before any frame/LateUpdate. Selected suite130/131 passed. Do not hide the failure or alter this unrelated impact policy within the electric slice; reconcile the test expectation and current boss impact policy separately.

## 2026-10-12 — Natural expiry must stay separate from restore cleanup and damage callbacks

- Trap: restoring an effect snapshot with remainingTime0 left it active until the next timer tick. A new natural-expiry listener would then treat this already-expired restore as a fresh expiry and trigger Current Jar damage.
- Correction: after restoring remaining time, an exhausted snapshot ends through ordinary cleanup and repository removal, without natural notification. Positive-time restoration reconnects state and permits its later genuine expiry.
- Related lifecycle risk: expiry damage can kill a target and clear its effect collection. The Runner queues natural expirations, finishes repository iteration and tag/payload cleanup, clears the pending queue, then notifies listeners. Jar disposes its source-owned subscription and also guards a previously captured delegate after disposal.
- Verification route: ElectricExpiryRelicPlayModeTests checks zero/positive restore, natural vs explicit removal, death/owner isolation, and collection clearing from expiry damage. Native results are recorded in [the session log](SessionLogs/2026-10-12.md).

## 2026-10-12 — Windows CSV text output changed localized field newlines

- Exact Unity String Table readback rejected the Korean shock description because CSV fields contained CRLF while the prepared translation JSON contained LF. The values displayed similarly, but the files were not equal.
- Cause: `Path.write_text` used default Windows newline translation on CSV text whose record writer already emitted CRLF. It also converted intentional field LF and created blank CRCRLF records.
- Fix: write the electric subset CSV with `newline=''`, preserving the CSV writer's record separator and the original field values. Do not normalize away differences in the exact native readback assertion.
- Verification: parsed CSV has31 rows (header+30), each with6 columns; all150 values exactly match the sidecar. Native LocalizeBatch retry compares the same150 values successfully. See [session log](SessionLogs/2026-10-12.md).

## 2026-10-12 — Temporary effect tags can outlive their scene owner after snapshot restore

새 혼란 GE의 granted tag는 TagSystem의 explicit count에도 들어간다. PlayerRuntimeCaptureCoordinator가 explicit tag와 GE를 함께 캡처하면, 복원 시 explicit count와 GE 부여 count가 겹쳐 GE 제거 후에도 조작 반전 태그가 남을 수 있다. 또한 술 노출 owner/충전량은 복원되지 않아 머리 연출과 실제 상태가 어긋난다.

술 노출은 현재 owner의 정확한 GE/source와 granted tag만 scene snapshot에서 제외한다. 다른 상태의 저장 규칙이나 DTO schema를 바꾸지 않는다. 앞으로 source-local 상태를 추가할 때 effect snapshot만이 아니라 explicit tag snapshot도 함께 확인한다. 기존 ISceneReappliedEffectSource marker는 HUD duration을 숨기는 부수 효과가 있어 이번5초 버프에는 그대로 적용하지 않는다. [구조](StructureMemory/AlcoholExposure.md), [범위](ActiveTasks/spiri-alcohol-exposure.md).

## 2026-10-12 — Localized font replacement detached SpeechBubble theme materials

- 증상: 한국어 서기관 말풍선 본문이 밝은 배경 위의 흰색 글씨로 표시됐다. 같은 공용 프리팹을 쓰는 NPC와 보스 테마도 영향 경로를 공유한다.
- 원인: SpeechBubble은 Awake에서 만든 전용 재질의 `_FaceColor`를 바꾸고 Vertex Color를 흰색으로 유지한다. 번역 폰트 callback은 표시 재질을 공유 fallback으로 교체하지만, 기존 테마 코드는 이전 전용 재질을 계속 수정했다. Vertex Color의 흰색은 2026-04-12부터 정상 동작의 일부였으며, 기본 글자 Outline도 번역 전부터0이었다. 실제 화면색과 프리팹 저장값/런타임 Vertex Color를 구분해야 한다.
- 수정: LocalizeTmpFontEvent가 폰트·atlas·참조 적용 뒤 `LocalizedFontApplied`를 알리고, SpeechBubble이 새 localized 재질을 복제해 현재 테마색을 재적용한다. 풀 비활성화 동안 구독을 유지하고 파괴 때 해제한다. 공유 원본은 변경하지 않으며 이전/최종 전용 재질은 말풍선이 정리한다. 이전 atlas를 가진 재질을 다시 연결하거나 색을 Vertex Color로 옮겨 rich-text 색 곱셈을 바꾸지 않는다.
- 검증 경로: `Tools/Validation/SpeechBubbleLocalizationRegression.cs`의 실제 공용 프리팹,5언어 폰트 콜백, 기본/보스 테마, 두 화자 색 분리, 풀 재사용과 재질 정리. 실행 결과와 제한은 [SessionLog](SessionLogs/2026-10-12.md)에 기록한다. 현재 구조는 [LocalizationFlow](StructureMemory/LocalizationFlow.md)를 참고한다.

## 2026-10-14 — HUD mesh routing must include newly merged Canvas graphics

- 충돌: 로컬 HP Mesh 최적화와 원격 그로기 바 추가가 같은 HUD 필드·초기화·배치와 프리팹 참조에 인접한 변경을 넣었다. 네 참조와 양쪽 계층을 모두 유지해도 의미상 충돌은 남을 수 있다.
- 함정: 기존 Canvas 활성 조건은 상태 슬롯 또는 HP 겹침만 고려했다. 새 그로기 바는 같은 Canvas의 일반 Graphic이므로 status 없는 독립 몬스터에서 숨겨진다. 겹침 bounds에 그로기 영역을 넣지 않으면 이웃 HP와 겹치는 경우에도 분리된 Mesh/Canvas 순서를 계속 사용한다.
- 수정 경계: 보이는 그로기 바를 Canvas 활성 조건과 projection bounds에 포함한다. 기존 Mesh 등록·fallback·정리와 원격 게이지/회복 계산을 보존하며, 시간에 따른 회복은 LateUpdate에서 갱신한다. prefab marker 해소에서는 새 GUID/fileID를 만들지 않고 양쪽 레코드 합집합을 유지한다.
- 검증: 실제 합쳐진 prefab의 네 typed 참조와 legacy HP Image6개, status 없는 Canvas, 그로기 영역만 교차하는 raster/sorting, 게이지·HP 숨김·회복·재사용·구독 해제를 함께 확인한다. 프리팹 비교 fixture가 참조를 재저작하는 경우 원본 prefab typed readback을 별도로 추가해야 한다. [결과](SessionLogs/2026-10-14.md), [구조](StructureMemory/MonsterWorldHudAndStatus.md).

## 2026-10-30 — Pixel sprite sheet authoring must validate named slices and persistent texture references

- 증상: 새 HP PNG를 Multiple로 가져왔을 때 자동 잘라내기 항목4개가 이미 있어, 비어 있을 때만 명시적 slice를 넣는 조건이 건너뛰어졌다. 기대한 Frame/Track/Fill/Trail 이름을 찾지 못했다.
- 수정: 명시한4개 이름이 일치하는지 검사한 뒤 새 시트의 rect/border를 Unity TextureImporter API로 지정했다. 기존에 정상 authoring한 명명 정보는 재사용한다. meta를 수동 편집하거나 기존 프리팹 GUID를 바꾸지 않았다.
- 추가 관찰: Sprite.texture로 material에 연결한 첫 결과는 저장된 MainTex 참조가0이었다. 저장용 texture는 AssetDatabase.LoadAssetAtPath<Texture2D>로 가져오도록 바꾸고, 원본 PNG GUID/fileID가 material에 유지되는지와 별도 Unity 프로세스의 실제 렌더를 확인했다. 내부 임시 texture 생성 원인까지 확정한 것은 아니다.
- 예방/검증: 배열 개수만으로 importer 설정 성공을 판단하지 않는다. 이름·원본 경로·저장된 참조를 확인하고 native 렌더를 승인 원본과 비교한다. 최종 양쪽 렌더 경로4상태는 RGB 오차0, Play Mode30/30 통과. [기록](SessionLogs/2026-10-30.md), [구조](StructureMemory/MonsterWorldHudAndStatus.md).

## 2026-10-31 — Retired chain buttons and native stripped references

- 증상/원인: 하단 사슬 버튼의 UIChainDropPresentation을 disabled로 바꿔도 Awake가 실행되어 이전 reach constraint가 새 고정 버튼 위치를 이동시켰다. 슬라이더 손잡이 또한 Slider가 stretch anchors를 구동하므로 height delta를32로 주면 부모 높이까지 더해졌다.
- 수정: 고정 버튼의 사슬 제약·자동 재생 참조를 해제하고 기존 컴포넌트는 보존했다. 슬라이더는 Handle Slide Area 높이32/손잡이 height delta0, Fill Area 높이12로 저작했다. 런타임 공용 동작을 변경하지 않았다.
- 저장 함정: 사용하지 않게 된 nested prefab component의 `stripped` 레코드를 Unity가 저장 중 정리했다. PrefabInstance override만 원복하면 그 override가 가리키던 기존 식별자를 잃을 수 있다. 범위 밖 원본 블록과 기존 stripped 참조를 함께 보존하고, 실제 authored object 삭제는 오류로 처리한다. 작업 전 사본을 기준으로 ID·GUID·폰트/재질과 변경 경로를 검사한다.
- 후속 저장 함정: Windows에서 사용 중인 prefab 파일을 `File.WriteAllText`로 truncate할 때 Win32 1224(user-mapped section)로 실패했다. 범위 보존 결과를 같은 폴더의 임시 파일에 먼저 쓰고 `File.Replace`로 교체한 뒤 재import하도록 수정했다. native 저장 직후 실패하면 원본 사본과 scoped blocks를 비교해 범위 밖 변경을 복구한 다음 재시도하며, 최종 source별 변경2블록/CanvasGroup 추가1개와 나머지 블록 보존을 확인했다.
- 도감 레이아웃 후속에서도 기존 localization 검증 TXT를 덮어쓸 때 동일한 1224가 재현됐다. 이번 범위의 보고서 경로를 별도로 전달하고 임시 파일 작성 후 원자적 교체를 사용한다. `LocalizeTmpFontEvent.OnEnable`은 null `authoredMaterial`을 채울 수 있으므로 native prefab 로드 전 값을 캡처하고 저장 직전 원복한다. 실제 TMP font/material 참조 보존 검사만으로 이 캐시 필드 변경을 놓치지 않는다. 또한 해석되지 않는 기존 nested material 참조는 해당 binding을 건드리지 않아도 native 저장이 null로 정규화할 수 있다. 최종 저장에서도 세 참조만 바뀐 것을 확인한 뒤 원본 instance record를 정확히 복구했다. 재실행 때도 보존 검사가 필요하다. [도감 검증](../outputs/encyclopedia-layout-2026-11-01/README.md).
- 2026-11-03 레벨업 버튼/제목 native authoring에서도 대상 외 TMP material과 nested override 정규화가 재현됐다. 저장 전 사본의 대상 외 document 127개를 정확히 복구하고, 대상 기존 14개/new 17개만 달라지는지 및 최종 import 후 해시를 확인했다. 이전 Git HEAD가 아니라 작업 직전 사용자 변경 상태를 기준으로 보존한다. [적용 기록](../outputs/levelup-ui-integration-2026-11-03/README.md).
- 검증 경로: [SettingsSkinScopeValidation](../Tools/Validation/SettingsSkinScopeValidation.py), [실제 화면/동작 검사](../Assets/_Project/Editor/Tools/Validation/SettingsSkinValidation.cs), [결과](SessionLogs/2026-10-31.md).


## 2026-11-01 — Unity Search LMDB native crash during Play assembly reload

- Evidence: Unity 6000.4.2f1 crashed when Play was pressed. Crash log reports `SearchTask 'Update' thread did not stop within 5 seconds`, then `mdb_cursor_sibling` / `mdb_cursor_prev` / `mdb_cursor_get` / `LMDB_Enumerator::Next` / `LMDBIndexStorage::RemoveDocuments`, with background MergeArtifacts work. This stack matches [UUM-141720](https://issuetracker.unity.com/issues/11928/crash-on-mdbcursorsibling-when-performing-various-actions-in-the-editor). No game-script exception appears at the original crash point.
- User-approved workaround without version change: close Editor; back up local Search settings and generated Library/Search; empty `UserSettings/Search.index` roots; set `indexOnEditorStartup = false`; move old cache aside. Do not delete the whole Library or disable domain reload. Settings are user-local, and advanced indexed property/dependency searches are limited.
- Verification: same Editor, TitleScene, three real Play/Stop cycles and Project name search; zero SearchTask timeout/native crash markers. Only 2,984 bytes of Search bookkeeping regenerated, no LMDB asset index. Saved data and preexisting work preserved. [Evidence and rollback](../outputs/unity-search-crash-workaround-2026-11-01/README.md).
- Separate recurring problem: `PrewarmTraceRuntime.Flush` Windows 1224 errors still occurred on later Play cycles. This confirms the previously recorded trace-writer problem also affects ordinary Editor use; do not attribute it to the LMDB crash or hide it with test-isolation flags in production. Suggested Later: scoped trace writer atomic-write/lifecycle fix.
- Native verification cleanup: loading/closing the Editor also reserialized GlobalUIRoot and several Monster prefabs. Restore only verified test side effects, preserving prior UI changes; compare exact pre-test hashes where available. A prior backup may predate approved UI work and must not be restored blindly.

## 2026-11-01 — Sparse ability YAML expands on save; mapped asset text cannot be overwritten live

- Changing only Crimson's fallback range text through `SetDirty`/`SaveAssets` expanded the sparse ability asset with hundreds of unrelated default fields. A direct file-write alternative then met Windows1224 because Unity had mapped the asset. Both were caught during scoped verification, before handoff.
- Restored only this task's verified serialization side effects from the immediate pre-task baseline and applied the one range-text replacement while the Editor was closed. The authoring command now leaves fallback YAML alone and updates only targeted StringTable entries through Unity APIs. Future text-only edits of sparse legacy assets should preserve their byte-level scope and avoid direct writes while mapped by the Editor.
- Migration text replacement must match the whole value prefix: `{val:3~6` instead of `3~6`; the latter also matches the suffix of `4.03~6` and is not idempotent. Exact expected locale values and unrelated prefab/asset preservation are checked by [scope validation](SessionLogs/2026-11-01-crimson-magic-circles/verify-scope.py).

## 2026-11-01 — Puddle material owner and native particle authoring traps

- Assigning only the MeshRenderer material is insufficient: `PuddleShaderVisual` restores its serialized `materialTemplate` during initialization/validation. The approved ground material must be assigned to both; validate saved prefab bindings and native pixels.
- Unity missing-component references need an explicit Unity null comparison before `AddComponent`; `??` does not use that comparison. Particle velocity X/Y/Z curves must all use the same mode. Both issues were found in native authoring and corrected before handoff.
- New pixel flame prewarming is restricted to Play Mode so preview-prefab validation does not schedule particle jobs during `OnValidate`. A non-Play preview tool must not invoke lifecycle methods through native `SendMessage`, which produced `ShouldRunBehaviour()` assertions; the disposable capture tool invokes only the two presentation initializers explicitly.
- Stop and clear preview ParticleSystems before assigning their random seeds. The final native capture rerun has zero related curve, seed, lifecycle, shader or script error markers.
- [Evidence, scoped tests and file purposes](../outputs/puddle-pixel-integration-2026-11-01/README.md), [current owner map](StructureMemory/PuddlePixelPresentation.md). These corrections do not change gameplay timing or the legacy buff indicator hierarchy.

## 2026-11-01 — Stock generation filters do not validate later inventory changes

- Source inspection found that merchants filtered current weapons only at roll time, then reused saved stock without observing acquisition or shell restoration. An old offer can therefore conflict with current ownership even when the initial filter is correct. The user's precise acquisition timing was unavailable; this was not reproduced in Play Mode.
- MerchantNPC now follows registry/inventory lifecycle and MerchantRunStateService repairs only conflicting unsold entries. Do not rebuild every valid offer on inventory changes. Partial replacement must include retained entries in duplicate exclusions and category caps.
- Weapon acquisition emits OnInventoryChanged before MerchantNPC marks the purchased slot SOLD. Reconcile from the deferred update instead of inside that callback, so the just-purchased slot is not replenished. Keep slot detail projection aligned when an offer changes. [Evidence, source checks and unexecuted manual scenarios](SessionLogs/2026-11-01-merchant-owned-weapons/README.md).

## 2026-11-01 — Cross-source loot exclusions need owner and snapshot boundaries

- Including every opened chest without excluding the requesting chest would make a reroll reserve its own previous offers. Pass the source chest in the nonserialized context and let RetainedItems alone reserve kept selections. Boss bonus lists also bypass random generation, so final chest insertion applies the same weapon filter.
- Saved dungeon chestLoot is a scene-exit snapshot. Combining it with the regenerated live dungeon can keep consumed items excluded or reintroduce the source chest's old roll. Loaded successful dungeon IDs use live inventories; only unloaded dungeons contribute snapshot leftovers.
- Candidate filtering alone leaves old maximum-level relic shop offers intact. Existing per-slot stock reconciliation now uses the common level rule and reacts to relic restore/changes, while deferred notifications preserve purchase-before-SOLD ordering. [Source review, compile evidence and pending runtime checks](SessionLogs/2026-11-01-chest-shop-policy/README.md). No runtime test reproduction is claimed.

## 2026-11-02 — UI prefab geometry shadowed by scene and nested-prefab overrides

- 2026-11-05 호감도·업그레이드 공용 보상 창에서도 같은 문제가 재현되었다. 빌드 대상 게임 씬 12개가 중앙 앵커를 사용하는 `RewardUI`에 X=960과 상단 Y 앵커를 저장하고 있었다. 해당 RectTransform의 위치·크기 오버라이드 5개씩을 제거하고 실제 씬의 상속값을 대조했다. [수정 범위와 검증 기록](SessionLogs/2026-11-05-nadoman354-reward-popup-centering.md).

- 실제 화면 후속 확인: 전용 TMP OutlineWidth를 0.5로 올린 이전 보정은 글자 내부와 강조색을 덮어 검은 덩어리처럼 보였다. Computer Use로 실제 허브에서 관찰한 뒤 OutlineWidth/FaceDilate를 0.18로 균형 조정했다. 또한 슬롯 스킨 작업에서 Button의 루트 Image까지 Raycast Target을 꺼 클릭 대상이 사라졌다. 루트만 켠 후 실제 선택/탭/페이지/스크롤/유물 레벨 표시를 확인했다. 장식 Image의 입력 비활성화를 클릭 표면까지 일괄 적용하지 않는다. [최종 화면 및 수정 기록](../outputs/encyclopedia-live-ui-fix-2026-11-02/README.md).

- 재설계 적용 후 재발: 허브의 이름 누락, Q/E 아이콘 누락, 하단 화살표 겹침을 사용자가 보고했다. 이전 정리의 raw UTF-8 + LF 전용 패턴이 CRLF 씬을 건너뛰어 ProtoTypeHub의 104개, 총 10개 씬의 1,001개 아이템 override가 남았다. 읽을 때 줄바꿈을 정규화하고 저장할 때 원래 CRLF를 보존하여 해당 override만 제거했다. 검색 결과가 0이라는 사실만으로 상속 정리가 끝났다고 판단하지 않는다. 특정 씬에서 보이는 이름/입력 안내/페이지 버튼의 실제 target ID를 직접 역추적한다. [수정 기록](../outputs/encyclopedia-item-layout-fix-2026-11-02/README.md).
- 같은 화면의 상세 아이콘은 InventorySlot 프로필의 (-16,-16) 이동과 1.7배 확대, pivot 변경을 작은 도감 틀에 그대로 적용해 범위를 벗어났다. 도감의 저작된 중앙 64×64 상자에서 위치·크기를 소유하고 프로필의 스프라이트·회전만 사용하도록 수정했다. 전용 TMP 외곽선도 alpha 1만으로 충분하지 않아 실제 18px 글자 크기에 맞게 두께/선명도를 조정했다. 소스·MSBuild 확인이며 새 Play Mode 재현/시각 확인은 실행하지 않았다.

- 증상: 실제 허브의 설정 행들이 하단에 겹치고, 도감 오른쪽 상세가 오른쪽으로 밀려 잘렸다. 독립 프리팹의 이전 검증 결과만으로 실제 씬 배치를 보장할 수 없었다.
- 소스 원인: 설정 원본의 상단 anchor/Y 간격을 사용 씬 12개의 override가 하단 anchor/Y=0으로 덮어썼다. 도감은 중간 GlobalUIRoot 프리팹이 ContentPanel의 X=0을 285로 덮어썼다. 이 중첩 상속 경로를 독립 UI fixture만으로 놓칠 수 있다.
- 수정: 충돌하는 설정/키설정 위치 override 633개와 도감 X override 1개만 제거했다. 기존 오브젝트 식별자, 다른 override와 참조를 보존했다. 전체 Revert나 프리팹 재생성을 하지 않았다.
- 확인 한계: 사용자 스크린샷과 직렬화 소스/변경 전후 비교에 근거한다. 이번 수정 후 Unity 화면·Play·테스트·컴파일은 실행하지 않았다. 향후 배치 확인은 원본 프리팹뿐 아니라 중간 프리팹과 실제 사용 씬까지 상속 경로를 확인한다. [변경 및 보관 기록](../outputs/ui-layout-normalization-2026-11-02/README.md).
- 후속 누락: 사용자가 설정 정상화와 아이템 도감의 지속적인 잘림을 확인했다. 첫 소스 검사는 YAML에 독립 레코드가 없는 중첩 상속 fileID를 건너뛰어 씬의 ContentPanel X=285를 놓쳤다. 원본 7990481634182577215와 씬에서 쓰는 3812683333260759081의 대응을 중첩 instance 6488204919046368278 및 35개 명시적 source pair로 확인했다. 사용 씬 12개의 해당 X override만 추가 제거했다. 직접 레코드 맵에 없다는 이유로 nested override를 검토 대상에서 제외하지 않는다. 몬스터/보스 및 설정 변경 없이 소스 비교만 완료했으며, 후속 실제 화면 확인은 미실행이다. [후속 기록](../outputs/encyclopedia-scene-layout-followup-2026-11-02/README.md).

## 2026-11-02 — Rendered control siblings and same-frame Escape ownership

- Settings skin source inspection found that each stepper's raycast-enabled
  background was a sibling of its Button/TMP object. Pointer events bubble to
  parents, so clicking the background could not reach the sibling Button.
  Enable raycasting on the full-rect Button/TMP object and leave the background
  non-raycasting while retaining its targetGraphic tint reference.
- KeyBindingPanelUI's close-request handler ran before CanCloseOnEscape and
  cleared active capture before closing. Separately, clearing capture in Update
  could expose the same Escape to UIManager/TitleMenuController later that frame.
  Consume active capture first and retain the cancellation frame for all callers.
- These mechanisms were identified by source/serialized inspection, not a Play
  Mode reproduction. [Change record and validation limits](../outputs/ui-gameplay-fixes-2026-11-02/README.md).

## 2026-10-26 — Ink 문장 줄바꿈이 불필요한 대사창을 늘림

- 증상: 짧은 문답으로 합의한 대사도 실제 Ink에서는 문장마다 줄을 나눠, 같은 화자의 짧은 말과 슬라임 만담을 여러 번 넘겨야 했다. 멜타 시민권 후속담은 한 분기에서 13개 대사창이 나왔다.
- 원인: 읽기 좋은 초안의 줄바꿈을 그대로 Ink에 옮겼다. 현재 재생기는 `Story.Continue()` 결과를 한 대사창으로 표시하므로, 한 줄에 합쳐야 할 문장들이 각각 별도의 카드가 됐다.
- 예방: 같은 창에서 읽을 짧은 문장·만담은 하나의 물리 줄과 하나의 `loc:` 키로 저작한다. 화자가 바뀌거나 의도적인 반응 컷인 경우에만 분리한다. 합칠 때는 표시할 표정·연출과 번역 값을 함께 정리하고, 실제 Ink 런타임에서 비어 있지 않은 출력 수를 세어 검증한다. 이번 수정은 후속담을 선택 전 4개 + 선택 후 2개, 총 6개 대사창으로 확인했다.


## 2026-10-30 - Stat Projection Alone Did Not Verify Normal And Skill Damage

- Symptom: NormalMul/SkillMul could change through relics and stat presentation, but the shared HP postprocess/payload construction did not consume those category multipliers.
- Cause: earlier stat assertions checked modifier values rather than an actual target's HP. Shared postprocess consumed critical/final multipliers without a normal/skill category owner.
- Correction: the existing player weapon-input owner supplies ICombatAttackKindSource; Core captures NormalMul/SkillMul at payload construction, including separate direct elemental HP constituents. Frozen projectiles keep their captured amount after later rewards. Unclassified damage/DOT do not borrow the bonus, and OddIron explicitly retains its fixed physical channel.
- Verification: new PlayMode tests apply the resulting payload to a real AttributeSet and verify HP loss, immutable preexisting element arrays, frozen payload values and fixed OddIron damage. See the [session](SessionLogs/2026-10-30.md).
- Recurring test trap: the first expanded run had 57/58 passes because the pre-existing prewarm recorder threw Windows 1224 while writing imported JSON. The existing -uiBatchValidationTests switch isolates disposable validation spawns; the unchanged 58-case run then passed. The final expanded run passed 59/59. The rotated diagnostic file was restored byte-for-byte using the pre-task SHA-256 and an existing Git blob, preserving the user's uncommitted history rather than resetting to HEAD. Use diagnostic isolation before the first run and preserve writable trace data up front. No recorder/Ink production code was changed or unexpected-log assertions suppressed.
- Suggested Later: existing NormalMul/SkillMul minValue=1 still clamps negative relic tradeoffs. Keep this separate from the approved new positive rewards.

## 2026-10-30 - Ink loc 태그만으로 보스 번역 등록을 확인하면 누락을 놓침

- 증상: 멜타 본대사·조우 및 마왕 조우의 33개 줄에 `loc:` 태그는 있었으나 같은 키가 CSV와 실제 Shared Data/언어별 String Table에 없어 외국어 번역이 등록되지 않았다.
- 원인: 수정한 스크립트의 키만 가져오고 확인했으며 현재 NPC가 참조하는 Primary/Encounter Ink 전체와 실제 테이블의 집합 대조를 하지 않았다. Ink 컴파일 성공이나 태그 존재는 번역 등록의 증거가 아니다.
- 보완: 33개 키를 원문 및 외국어 4종과 함께 CSV에 등록한 뒤 공식 importer로 반영했다. 현재 보스 4명의 연결 Ink 8개, 대사·선택지 키 198개와 테이블 값 990개를 대조했다. 자세한 결과는 [작업 기록](SessionLogs/2026-10-30.md)의 보스 대사 번역 누락 보완 항목에 있다.
- 재확인: 대사 번역 완료를 보고하기 전에 활성 NPC의 Primary/Encounter Ink 모두에서 키를 수집하고, 원문·컴파일 loc 태그·CSV·Shared Data·5개 언어 테이블을 대조한다. 번역 등록 완료와 원어민/실제 화면 검수 완료는 구분한다.

## 2026-11-01 - Unity Startup Clears Temp And Races Generated-Project Builds

- Symptom: task scratch scripts under `Temp/` disappeared on the next Unity batch launch. Running MSBuild concurrently with Unity startup also produced CS0006 for `Temp/bin/Debug/Ink-Libraries.dll` after dependencies had just built.
- Cause: Unity owns and clears the project `Temp` directory; generated project builds also emit intermediate assemblies there. Those operations do not safely parallelize.
- Prevention: keep task scripts/evidence outside Unity's `Temp`, wait for the Editor process to exit, then run generated-project MSBuild. Use `Process.WaitForExit()` when waiting only for the Editor; PowerShell `Start-Process -Wait` can wait for persistent child services too.
- Resolution: final merchant native regression passed 226 checks; final MSBuild was rerun after Unity exited. See [session](SessionLogs/2026-11-01.md).

## 2026-11-02 - Preserving Blocking Dialogue Exposed Camera Sentinel Text

- Symptom: the scribe's three-clear audience showed `[camera cue]` twice. The October fix for skipped speech after affection effects preserved every nonempty blocking line, including older camera sentinel lines that had relied on automatic advancing.
- Cause: `feature` identifies an operation, not whether its line contains player-facing text. Treating all blocking text identically either loses real speech or exposes internal cue placeholders.
- Correction: explicitly mark the six camera-only Ink lines with `presentation_only`. DialogueController still runs the feature and waits for its callback, then advances only marked presentation lines. Unmarked affection/failure/feature dialogue keeps its existing display-after-effect behavior. The September sentinel note above describes the earlier advancing behavior; the explicit marker is now required.
- Verification boundary: Ink source/JSON parity and 9/4, 2/2, 1/0 spoken/cue counts were checked. Native callback regression coverage is in `DialogueBlockingPresentationPlayModeTests` within the existing SceneSmoke test source; see [session](SessionLogs/2026-11-02.md) for execution status and Editor import limitations.

## 2026-11-02 - StringTable Save Canonicalizes Unrelated Scalar Formatting

- Symptom: saving five new merchant entries through Unity also uppercased Unicode escape digits in the existing Fire Dragon's Fang name and removed its optional English YAML quotes. The decoded values did not change, but a strict preservation audit correctly rejected the unrelated raw-record drift.
- Cause: adding a narrow set of StringTable entries still serializes the complete table using Unity's canonical scalar formatting.
- Prevention: capture current user preimages, compare every old entry/ID, and restore only semantically identical quoting/escape-case drift before scoped copyback. Preserve unrelated user edits rather than resetting tables to Git HEAD. Reject any actual value/metadata difference for investigation.
- Verification: cumulative NPC native337/337 and preservation216/216 passed; all previous locale records and CSV values were preserved. See [session](SessionLogs/2026-11-02.md).

## 2026-11-02 - Windows Open-Editor Asset Copy Requires Atomic Replacement

- Symptom: after isolated Unity validation, an in-place copy of the reviewed merchant NPC asset failed with Windows OSError 22 while the main Editor remained open. The destination had not been truncated; its pre-task hash still matched.
- Boundary: the user's Editor may retain imported file mappings. Releasing cached handles in another Unity process does not release those handles. Do not close or restart the user's Editor to complete a scoped copy.
- Resolution: check destination hashes against captured user preimages, write reviewed bytes to a hidden sibling task-owned staging file, then use atomic replacement. Recheck every copied file and protected file; never reset to Git HEAD or overwrite a concurrent edit. This is task copyback tooling, not new runtime save behavior.
- Verification: all 15 reviewed assets/data files matched the native validation outputs after atomic replacement, and 232 preservation checks passed. See the [session](SessionLogs/2026-11-02.md).


## 2026-11-02 - Ink JSON Write Failure Leaves Asset Import Paused

- Symptom: the interactive Editor remained on `Importing assets` for more than 15 minutes. The log completed its initial asset refresh in 94.130 seconds and project loading in 318.590 seconds, then reported Windows 1224 while writing `MerchantDialogue_Animated.json` in `InkCompiler.OnCompleteCompilationStack`.
- Cause: the vendor compiler called `StartAssetEditing`, then `File.WriteAllText`, then `StopAssetEditing` without a finally block. A mapped-file write exception skipped the matching stop and the later progress/auto-refresh/build/Play Mode cleanup. The holder of the mapped file was not identified.
- Correction: guarantee `StopAssetEditing` in finally; route JSON write IOExceptions through the existing per-file compiler error reporting so normal cleanup still runs. Clear the previous unhandled error after a later successful compilation. Preserve successful output and keep failures visible rather than treating a stale JSON as a successful write.
- Verification: the isolated Unity 6000.4.2f1 regression reproduced the actual Windows 1224 using a live mapped JSON section and passed 39 checks, including preserved JSON, error reporting, resumed native asset import, cleanup, UTF-8 retry and the existing syntax-error path. Generated-project InkEditor MSBuild also passed. See [session evidence](SessionLogs/2026-11-02.md) and [native regression](../Tools/Validation/InkCompilerImportRecoveryRegression.cs).
- Boundary: close the stalled interactive Editor before applying this recovery patch; editing C# alone cannot run the new cleanup in the already paused session. This patch does not forcibly release another file mapping or add write retries. Confirm the actual game Editor restart separately, and preserve the local vendor fix when upgrading Ink.


## 2026-11-02 - Prewarm Trace Mapped JSON and Retry Flood Recovery

- Symptom: the current Editor log contained 6,830 PrewarmTraceRuntime.Flush stack entries for Windows 1224 while saving the tester trace JSON. This is the recurring production recorder failure previously isolated out of UI tests, separate from the Ink compiler fix.
- Cause: File.WriteAllText directly truncated a JSON imported under Assets; a live mapped section rejected that operation. lastFlushRealtime was updated only after successful writes, so a failed attempt could repeat on every Editor update after the interval elapsed.
- Correction: stage the complete JSON under Library/PrewarmTrace and replace/move it into the unchanged tester path. Record attempt time before I/O; retry failed saves at five-second intervals and warn once per failure episode. Preserve dirty measurements on I/O/access failure, clear failure state on success, and contain the same errors during the final Play Mode exit flush. Preserve the JSON schema, twenty-session retention, recommendation paths and trace-isolation callback names.
- Verification: isolated Unity 6000.4.2f1 reproduced the old direct-write Windows 1224 and passed 40 production-recorder checks, including replacement while a mapped view remained alive, a replacement-denying lock, denied staging writes, previous JSON/meta preservation, 1,000 throttled updates, recovery/latest measurements, final flush, reader/import and retention. Generated-project Editor MSBuild passed. See [session evidence](SessionLogs/2026-11-02.md) and [regression entry point](../Tools/Validation/PrewarmTraceRecoveryRegression.cs).
- Boundary: no global file-handle release, trace-path migration or Player trace service was added. Failed staging files are reused rather than accumulated, and successful moves remove them. Persistent replacement denial can still leave the latest measurements unsaved when Play Mode/Editor ends; existing history must not be deleted to suppress that warning. Confirm normal interactive Editor use separately.


## 2026-11-05 — New affection unlock rewards can be missed by older profiles

- Symptom/risk: 새 item-unlock 보상을 기존 호감도 단계에 연결해도 이미 그 단계를 넘긴 프로필은 유물/무기 해금을 받지 못할 수 있다.
- Cause: AffectionRewardProcessor는 이번 상승 구간(fromLevel < targetLevel <= toLevel)의 보상만 실행한다. 기본 해금에 없던 신규 유물은 과거 저장 ID에도 없을 수 있으므로 NPCData 연결만으로 기존 프로필을 지원할 수 없다.
- Current handling: ItemManager 초기화/해금 조회에서 준비된 item DB와 NPC 보상 데이터, committed profile affection을 사용해 획득된 UnlockItemAffectionEffect의 ID만 기존 set에 더한다. NPC DB가 아직 없으면 이후 조회에서 재시도한다. 다른 보상 효과/팝업/인벤토리 지급은 재실행하지 않고, 기존 SaveData ready guard와 commit 타이밍은 유지한다.
- Prevention: 이후 단계별 해금 콘텐츠 추가 시 새 프로필뿐 아니라 이미 임계값을 넘긴 프로필의 소급 권한과 source readiness를 소스 검토에 포함한다. 이번 작업에서는 자동 테스트/Unity Play Mode를 실행하지 않았다. [작업 범위](ActiveTasks/melta-chloe-tier-two-unlocks.md).
