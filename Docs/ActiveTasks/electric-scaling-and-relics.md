# Electric scaling and relics

Mode: Implementation. 2026-10-12.
Status: complete. Active direct re-electrocution doubles the current requirement;
shock end resets it. Chain/Echo and in-game explanations are excluded.
Native96/96 PlayMode and Editor/test-project MSBuild passed for this continuation.
Earlier completed Thunder Heart/Current Shard curves, movement-ability stacking
and chain-only Echo damage/VFX. Native72/72, ten-relic/29-level actual description
readback and exact150-value locale readback passed. Earlier uncapped Life Current health scaling and
player-facing electric wording cleanup passed native13/13 and actual10-relic/24-level Korean
tooltip readback passed; native exact150-value localization and source/data checks
passed. Review artifact: `output/electric-relic-descriptions-2026-10-12.txt`.
Earlier completed Current Jar, Echo Coil and Conductor Ring runtime/data/localization.
Native selected84 unique cases passed across the initial83/84 run and corrected
Jar19/19 rerun. Native authoring/exact150-value localization readback, Editor and
test-project MSBuild passed; protected1159 files and11 new unique GUIDs verified.
Previously completed short shock explanations and fixed-attack relic
migration. Fixed/balance25 cases and separate five-locale1 case passed, with
Unity authoring/import/readback, MSBuild and data audits. Jar/Echo/Conductor
implementation is recorded below.
The approved Resonance Bell / Tingling Shackles slice
adds per-attacker target counts and exact actor-action pause/resume. Feature53/53
passed; expanded suite130/131 has one existing boss impact expectation conflict.
The prior
health/scene-count revision and duration relic passed
Unity30/30, authored/localized readback, and MSBuild. The earlier three-relic slice
passed Unity22/22. Manual balance/visual review and Player build remain unexecuted.

Scope authority: current user request and coefficient selection in this chat.
Existing localization docs are technical context for the narrow content translation.

## Accepted rules

- Initial discharge: base 5 + attacker's ElectricFinal × 1.0.
- Follow-on damage: base 5 + attacker's ElectricFinal × 0.5 (user-selected).
- Snapshot ElectricFinal before initial status application, then apply existing
  discharge modifiers after coefficient calculation. Missing attacker/provider
  contributes zero. No new critical roll or elemental gauge effect.
- Base maximum is three total targets including initial; Resonance Bell adds
  one/two/three for its owner. Basic chain hits do not renew shock.
- Six Rare relics, dropLevel 1:
  - Thunder Heart: each living shocked enemy in the owner's current scene adds
    1/1/2/2/3 ElectricAdd, maxLevel5, no enemy-count cap or radius.
  - Life Current (existing StormStep ID/file/GUID): maximum HP per complete cell
    adds 1/1/2/2/3 ElectricAdd, maxLevel 5, with no counted HP-cell limit.
  - Dash Coil: accepted movement-ability execution adds one stack for three seconds,
    1/1/2 ElectricAdd per stack, maxLevel3/max4 stacks. Every accepted use refreshes
    all stacks to three seconds, including at cap; failed activation does not count.
  - Thunder Knot: follow-on chain hits add 0.5/1/1.5 seconds to an existing
    electrocution's remaining lifetime, capped at its base four seconds; maxLevel 3.
    Initial targets are unaffected. Expired/missing/dead statuses are not recreated,
    and an already-longer status is never shortened. Unequip/manager destruction
    removes the source-owned hook. This is a relic exception to the baseline rule.
  - Resonance Bell: total target bonus +1/+2/+3, maxLevel3; total four/five/six.
  - Tingling Shackles: successful initial shock application or renewal causes
    0.1-game-second paralysis on living normal enemies, maxLevel1; bosses excluded.
- Derived bonuses recompute after restore; temporary dash bonus clears on detach.
  Numeric relic values are conservative initial balance choices.

## Allowed and forbidden

Allowed: electric effect coefficient fields/data; conditional electric and chain-duration relic logic;
existing enemy-registry query and dash marker; acquisition preview integration;
existing proc-manager destruction cleanup required by the new temporary token;
six definitions/strategies/database registrations; five-language CSV/table/
asset-key additions and electric explanation; focused authoring/tests and required
Unity-generated metadata for new files/assets; narrow task memory.

Preserve existing field names, IDs/GUIDs, scenes/prefabs, font/layout, input,
damage pipeline, duration runner, save schema, asmdefs and bootstrap. No new
managers/singletons, packages, gauges, mitigation or unrelated relic effects.
Reuse existing icons. Architecture/Contracts/Presentation HTML are outside scope.

## Risks and success criteria

Procs own modifiers and subscriptions through their token. Equip/upgrade,
unequip and restore must not duplicate bonuses or retain stale derived state.
Count uses the living-enemy registry and the owner's current scene. Legacy movement
conversion is retained only for compatibility; the authored relic now watches maximum HP
and projected health changes without ElectricAdd feedback. The shared attribute
notification threshold remains unchanged; health is counted in complete cells.
Dash marker distinguishes normal completion
from hard-stop early exits. Previews must recompute dependent electric bonuses.

Done: coefficients, obtainable/upgradeable relics, tooltips and five-language
content agree; references are valid; focused damage, lifecycle/preview/dash and
localization tests pass. Run MSBuild after new sources enter generated projects
and actual Unity compile/import/tests; do not edit generated project files.
Manual combat balance/visual acceptance and Player build remain separately reported.

## Approved 2026-10-11 continuation

- Resonance Bell: Rare/dropLevel1/maxLevel3, total-target bonus +1/+2/+3. Base
  three remains in the shared trigger; an equipped owner's discharge resolves
  four/five/six total targets including the initial target. Snapshot once, no
  duplicate roots, same range/coefficients and basic shock lifetime rule.
- Tingling Shackles: Rare/dropLevel1/maxLevel1, fixed0.1-game-second paralysis
  on successful owner-caused shock application or renewal to a living normal
  enemy. Bosses are excluded through existing ICombatHitPauseImmune. Followers,
  duration extension and save/scene restoration do not trigger it.
- Actor-owned paralysis preserves current FSM/ability/motion and hit records.
  Movement, actor action animation, preparation/dash/recovery timers and attached
  attack judgments pause; independent already-fired projectiles continue. Renew
  to0.1 seconds without additive stacking; death/disable ends the hold. Existing
  longer hit pauses are not shortened. Expiry must not resume cancelled/dead work.
- Extend existing CombatHitPause2D with a paralysis-only actor action clock and
  pause-aware timing integration. With no paralysis, current global-time and
  ordinary hit-pause behavior remain unchanged. No world time-scale modification.
- Additional allowed scope: two logic SO types and authored definition/strategy
  pairs; target-count/shock-applied hooks; action timer/pause gates in current
  normal-monster/ability/attached-hit flows; narrow cleanup and focused tests;
  six-relic authoring, five-language content, tooltip and required memory.
- Preserve existing identity/schema/enum values, scenes/prefabs/animations,
  asmdefs/ProjectSettings/save/bootstrap and independent projectile behavior.
  New assets/source metadata are generated by Unity. No new Manager or Singleton.
- Done: obtain/upgrade/unequip/restore, owner isolation, counts4/5/6, boss exclusion,
  first/renewal vs chain behavior, true remaining-time pause/resume, hit judgments,
  overlap/death/disable cleanup and six localized tooltips pass actual Unity tests
  and MSBuild after generated project refresh. Record unresolved manual acceptance.

Doc impact: SessionLog, narrow StructureMemory and DecisionLog; ErrorLog only for
a verified recurring trap. Presentation stale check at completion.

## Approved content and fixed-attack continuation

- Player-facing electrocution text states discharge damage, four-second shock,
  basic three total targets including the initial target, and relic target increases.
  Remove formulas, coefficient percentages, gauge and renewal implementation prose.
- All currently authored relic attack-stat percentages become AttackAdd Flat:
  Worn Whetstone1/2/3, Master Whetstone1/2/3, Heavy Bracelet2/3/4,
  Last Breath3, Red Contract2, rotating iron-ball kill bonus2. Conditions,
  durations, other movement/attack-speed/basic-attack damage modifiers remain.
- White Feather damage becomes fixed3/4/5/6/7, independent of AttackFinal.
  Preserve identity, existing serialized field names/schema and other orbit/hit
  behavior; migrate existing coefficient fields' authored values and tooltip
  semantics through Unity API. User selected these fixed values in this chat.
- Allowed: seven definitions/strategies' specific attack values/references and
  descriptions, feather damage/tooltip code, narrow five-language original/table
  changes, focused authoring/readback/behavior tests and task memory. No new
  gameplay managers, scenes/prefabs/fonts/asmdefs/enum values/save changes.
- In that earlier content slice, Jar/Echo/Conductor were design edits only.
  Their runtime/assets are now explicitly authorized by the continuation below.

## Approved three-relic implementation continuation — 2026-10-12

- Current Jar: natural shock expiry deals the applying owner's current
  ElectricFinal ×0.4. No flat damage; explicit removal, cleanse, death and scene
  cleanup do not activate it. A restored shock may expire normally later when
  its applying owner is available. Acquisition/removal does not deal damage.
- Echo Coil: immediately replay the original discharge's damage submissions once
  at50%, including its initial target. Reuse the original targets and resolved
  magnitudes; skip dead/inactive/missing targets. No shock renewal, duration
  extension, paralysis, fresh search or recursive extra activation.
- Conductor Ring: initial hit has no bonus; follow-on steps receive +20/+40/+60%,
  capped at60% and reset per discharge. Multiply after existing discharge damage
  modifiers. Snapshot equipped rules before the initial shock callback.
- All three have one level, Rare/drop1 and reuse existing icons. Add three logic
  types and definition/strategy pairs without changing existing identities,
  serialized schemas or persistent enum values. Existing proc lifecycle owns
  subscriptions; no new Manager/Singleton or scene/prefab/UI hierarchy edits.
- Allowed: narrow Core expiry notification and electric secondary-damage hooks,
  three relic procs, authoring/database registration, five-language strings and
  tooltip checks, focused gameplay tests and task memory. Keep the earlier six
  relics, fixed attack values, cap3 and follower status lifetime behavior.
- Risk: expiry damage can kill and clear effects. Notify only after the repository
  tick and end cleanup finish; test reentrancy and exact owner isolation.
- Done: native Unity authoring/import/readback and focused tests pass, generated
  source coverage and MSBuild pass, metadata/protected assets remain intact,
  and lifecycle/owner/expiry exclusions and combined Bell/Echo/Conductor behavior
  are verified. Manual combat balance, visuals and Player build are separate.

Doc impact: SessionLog, narrow StructureMemory and DecisionLog. ErrorLog only if
a verified lifecycle trap needs a durable entry. No Presentation side effects.

## Approved health/wording correction — 2026-10-12

- Current user correction supersedes the earlier three-health-cell limit. Life
  Current uses every full maximum-health cell at one cell per unit; retain the
  five-level1/1/2/2/3 curve. Runtime and detached acquisition/upgrade previews must
  agree above three cells. Current HP remains irrelevant. Other mode limits stay.
- Remove developer wording such as current scene and electric activation from
  player-facing electric relic text. Use the game's visible Electric, maximum
  health, dash, electrocution, chain damage, paralysis and boss vocabulary.
  Internal scene filtering, damage rules and event identities are unchanged.
- Allowed: shared health calculation and narrow tooltip fallback text, six
  authored description values, five-language source/table updates, existing
  health/locale regression adjustments, a read-only native tooltip exporter and
  task memory. Existing asset GUIDs/IDs, serialized fields/schema, authored
  numerical curves, scenes/prefabs/fonts, asmdefs/save/bootstrap remain intact.
- Risk/success: removing a shared cap must not uncap the shocked-enemy count.
  Verify >3-cell live stat, preview, maximum-HP change, restore and cleanup; native
  authoring/exact localization readback, relevant PlayMode tests and MSBuild.
- Export the actual localized descriptions and logic.BuildTooltip results used
  by the game for CurrentShard plus nine electric relics. Show one-level wording
  to the user for review; preserve all levels in the native readback artifact.
- Doc impact: SessionLog, narrow StructureMemory and DecisionLog; no Presentation
  side effects. Existing ErrorLog remains unless a new verified trap occurs.

## Approved curves, movement classification and chain-only Echo — 2026-10-12

- Current request overrides earlier Thunder enemy cap/three levels, CurrentShard
 2/4/6 curve, dash-completion single buff and Echo initial-target replay. Thunder
 counts every living active shocked enemy in the owner's scene, with five-level
 1/1/2/2/3 per-enemy bonus. Life Current remains uncapped and unchanged.
- Current Shard uses six levels1/2/3/4/5/6, retaining Common rarity, IDs/GUIDs/icon
 and its existing generic localized text identity.
- Dash Coil uses3-second, max4 stacking with per-stack1/1/2. User explicitly chose
 refreshing the entire stack duration at each movement-ability use, even at cap.
 Subscribe to existing AbilityExecutionStarted after successful activation;
 blocked/failed/queued-not-yet-started attempts do not grant stacks.
- Reuse AbilityDefinition.abilityTags classification; add Ability.Movement
 Resources tag with a new unused serialized ID, preserving existing serialized
 tag IDs. A reusable identity/ancestor query requires no new serialized schema.
 Initial tagged player reposition skills: global Dash, Apprentice dash stab,
 Lightning Spear mark warp and Real Weapon rush. Incidental basic-attack lunges
 and speed/dash enhancement buffs remain outside the initial category. Future
 abilities opt in through this classification tag. No new ability state grants.
- Echo records successful follower damage only, then repeats it once at50% with
 the chain VFX. Do not repeat the initial target's damage or hit snap. Keep the
 initial point as the first link origin, and render snaps only on replayed
 followers. Preserve original VFX scale/rhythm and all status/bonus boundaries.
- Remove Jar's death/cleanse exclusion sentence from player text only; keep its
 underlying natural-expiry/source/lifecycle filters.
- Allowed: relevant relic logic/proc/preview and uncapped registry query; existing
 ability classification query; one new tag/four identity-tag lists; original
 presenter-compatible replay API; authoring/current values/description templates
 and five-language data; focused tests/readback and task memory. Preserve enum
 values/serialized fields/SO schema, scenes/prefabs/fonts, existing tag IDs,
 asmdefs/save/bootstrap. No new Manager/Singleton.
- Done: all ten roster values and29 authored-level texts agree; native asset/tag
 readback and exact locale data pass; count/stack lifecycle, accepted-start,
 classification, preview/upgrade, follower-only damage/VFX pass meaningful Unity
 cases and MSBuild. Record manual balance/visual/Player checks separately.

Doc impact: SessionLog, StructureMemory and DecisionLog; ErrorLog only for a new
verified recurring trap. No automatic Presentation updates.

## Approved direct re-electrocution requirement — 2026-10-12

- User requests current required electric gauge ×2 for direct re-electrocution;
  followers and Echo are excluded. First fresh shock keeps the base requirement.
- User explicitly selected resetting to base when shock ends. Track the actual
  active shock instance; natural expiry, removal/cleanse or fresh replacement/
  restoration discard its multiplier. A direct renewal of already-active shock
  counts even when another effect/restore originally applied that shock.
- Effective requirement is the target's current max-electric attribute times a
  private transient multiplier.50→100→200→400→800 while active; do not clamp to
  the authored attribute max500 or mutate that authored definition/base value.
  Base-value changes remain visible through the same effective threshold lookup.
- Preserve residual buildup, existing non-electric rules and lifecycle cleanup.
  ClearAll/Rebuild reset only new records in addition to their existing behavior.
  No new all-gauge disable/death cleanup. Gauge/HUD/decay use the effective threshold.
- Electric overflow consumes one completion at a time and rereads its requirement
  after direct application; preserve other elements' current aggregation path.
- Allowed: ElementGaugeSystem runtime records/threshold/overflow and successful
  direct GE_ElectricShockTrigger boundary, meaningful new tests and narrow memory.
  No gameplay text/localization, assets/schema/IDs/enum, prefab/scene, package,
  Manager/Singleton, save/bootstrap or Architecture/Contracts/Presentation edits.
- Done: actual first/active reapply/expiry/reset/overflow and exclusions, current
  attributes and target isolation, UI ratios/decay/cleanup, non-electric regression
  pass Unity and relevant generated-project MSBuild. Asset/text hashes unchanged.

Doc impact: SessionLog, narrow StructureMemory and DecisionLog. ErrorLog only if
a verified recurring lifecycle trap appears; no player-facing description update.

## Direct re-electrocution completion evidence — 2026-10-12

- Runtime changes are limited to ElementGaugeSystem and GE_ElectricShockTrigger.
  Requirement records use the existing gauge definition's trigger/element identity,
  independent of damage-popup metadata, and belong to the actual active status.
- Unity6000.4.2f1 selected PlayMode96/96 passed, including24 new requirement cases
  and72 existing electric/status/localized-tooltip cases. First/renewed/expired/
  cleansed/replaced/restored shock, live attributes, isolation, overflow, HUD/decay,
  ClearAll/Rebuild, invalid applications and follower/Echo/extension/Jar exclusions
  are covered. Fire/Blood/Poison overflow remains on the existing path.
- Editor.csproj and ProceduralPlayModeTests.csproj MSBuild exit0. Native import
  generated the new test metadata and refreshed test-project source coverage;
  generated projects were not edited. Evidence: Library/CodexElectricRetriggerThreshold/.
- Protected3072/3072 existing data/tag/scene/prefab/metadata hashes match the
  pre-change baseline. New test GUID has one Assets declaration. Current game
  descriptions are unchanged. Manual combat balance and Player build
  are unexecuted. See the latest [task record](../SessionLogs/2026-10-12.md).
- Doc Impact Check: SessionLog + StructureMemory + DecisionLog. No current ErrorLog,
  Architecture/Contracts/Guides or Presentation changes; Presentation stale:none.

## Earlier completion evidence

- Earlier narrow continuation: removed the parenthetical clause from the electrocution
  explanation/fallback in all five locales. Native open-Editor readback of the one
  existing key passed after user-triggered Assets Refresh; temporary import bridge
  removed. Gauge investigation is read-only: repeated shock does not modify threshold
  or resistance; configured base50, attribute bound500 is not trigger escalation.
  No gauge mechanics were changed. See the latest [record](../SessionLogs/2026-10-12.md).

- Latest movement/replay slice: actual Unity6000.4.2f1 authoring and localization
  passed for Thunder5 tiers, CurrentShard6 tiers and Coil3 tiers/max4 stacks.
  Four movement abilities carry the new category; old serialized tag IDs remain.
- Selected72/72 native PlayMode passed, including accepted-start/rejected input,
  identity/ancestor classification, owner isolation, timer refresh/expiry/preview/
  restore, unlimited count, follower-only Echo and actual prefab replay segments,
  scales and snap counts. Actual Korean readback exported10 relics/29 levels.
- Only6 existing locale keys/30 values changed; all30×5 exact source/table values
  match. Stable identities preserved.1477 baseline files:1466 unchanged,11 requested
  data files changed; trigger original bytes restored. Four ability original scalar/
  nested values preserved; Unity additionally serialized existing schema defaults.
- New movement tag GUID unique. SessionLog/StructureMemory/DecisionLog updated;
  ErrorLog unchanged. No Presentation stale candidate. Manual balance/visual/Player
  checks unexecuted. Logs/audits: `Library/CodexElectricMovementRevision/`;
  [record](../SessionLogs/2026-10-12.md), [actual descriptions](../../output/electric-relic-descriptions-2026-10-12.txt).

- Latest health/text slice: Life Current counts every full maximum-health cell,
  retaining1/1/2/2/3 and maxLevel5; currentHP is irrelevant. Enemy-count3 cap stays.
- Native Unity6000.4.2f1 authoring and exact30-key×5-locale readback succeeded.
  CSV changes are14 existing keys/70 values; all other values and asset identities
  preserved. Actual Korean compiled tooltip readback exported10 relics/24 levels
  and verified no developer terms or obsolete health cap clauses.
- Selected13/13 PlayMode passed, including100-cell runtime, all5 health levels,
  actual acquisition/upgrade projections, restore/unequip and5-locale wording.
  Protected1101 baseline files:1095 unchanged, six intended description changes;
  trigger pre-authoring bytes restored. Logs/audits: `Library/CodexElectricHealthText/`.
- SessionLog/StructureMemory/DecisionLog updated; ErrorLog unchanged. No Presentation
  stale candidate. User text review and manual balance/visual/Player checks remain.
  See [record](../SessionLogs/2026-10-12.md) and [all-level actual descriptions](../../output/electric-relic-descriptions-2026-10-12.txt).

- Latest three-relic slice: Current Jar liveE×0.4 on natural expiry, one50% damage
  replay, additive20%-step cap60% implemented.9 definitions/strategies validated,
  all/default lists append exactly3 while preserving earlier order,75 total relics.
- Actual Unity6000.4.2f1 ApplyBatch and LocalizeBatch succeeded;30keys×5locales
  match exactly. Windows CSV newline conversion was corrected without weakening
  comparisons. Selected84 unique cases passed: initial83/84, then Jar19/19 after
  fixing an anonymous test status ID and asserting actual restored state.
- Editor and ProceduralPlayModeTests MSBuild exit0; generated projects include
  all new sources and were not manually changed. Protected1159 files are unchanged,
  including the trigger restored to its pre-authoring bytes;11 new GUIDs unique.
- SessionLog/StructureMemory/DecisionLog/ErrorLog updated. No Presentation stale
  candidate. Manual balance/visual/Player checks unexecuted. Logs/XML/audits:
  `Library/CodexElectricThreeRelics/`; [record](../SessionLogs/2026-10-12.md).

- Latest content slice:7 relics migrated; all72 registered strategies have zero
  AttackMul references.20 selected GUIDs unique,7 original definition metadata
  and5 protected hashes preserved;33 keys×5 locales matched source/native tables.
- Fixed/balance25 cases passed. The first locale case timed out before string
  checks under180 headless frames; WaitForLocalization now uses30 realtime seconds
  with the same success assertions, and isolated locale1/1 passed. All26 selected
  cases passed across runs. Logs/XML: `Library/CodexFixedAttackRelics/`.
- Unity6000.4.2f1 authoring/localization readbacks and Editor/tests/final-tests
  MSBuild exit0. Generated projects were not manually edited.
- SessionLog/StructureMemory/DecisionLog/ActiveTask/TaskIndex updated; ErrorLog
  unchanged this slice. No Presentation stale candidate. Manual balance/visual/
  Player validation unexecuted. Earlier gameplay evidence follows.

- Latest slice: actual Unity6000.4.2f1 authoring/import and21-key×5-locale readback
  passed. Feature53/53 PlayMode cases passed; expanded suite130/131 failed only
  the pre-existing immune-boss impact expectation. FreezeVictim is unchanged
  against HEAD. Logs/XML/summary/metadata audit: `Library/CodexElectricBellParalysis/`.
- Editor and test-project MSBuild passed. Generated sources were refreshed by
  Unity; projects were not manually edited. Protected9 hashes,19 unique source/
  asset GUIDs,6 definitions in both database lists and105 translated values passed.
- Suggested Later: reconcile the old impact test with current boss immunity.
  Actual combat balance, visual/overflow/CJK acceptance and Player build are
  unexecuted. SessionLog/StructureMemory/DecisionLog/ErrorLog updated;
  Presentation stale candidate:none. Earlier completion evidence follows.

- Unity6000.4.2f1 PlayMode30/30 with existing fixture trace isolation, including
  relic health/scene/dash lifecycle, bounded extension, damage coefficients and
  five-locale tooltips. XML/logs: `Library/CodexElectricRelicsRevision/`.
- Actual Unity authoring/import and15-key×5-locale table readback passed;
  initial Editor/tests MSBuild exit0 and final tests MSBuild exit0. Generated
  projects include both relic sources and were not manually edited.
- Unique metadata for12 source/assets, protected8 hashes, four database entries,
  translation/token parity, effect coefficients/cap and memory links checked.
- SessionLog + StructureMemory + DecisionLog + existing ErrorLog recurrence
  updated. No Architecture/Contracts/Guides or Presentation changes; Presentation
  stale candidate: none. See [task record](../SessionLogs/2026-10-11.md).

## Suggested Later — three remaining design candidates, not implemented

Candidates use the equipped attacker's current total-target cap. Values are proposals
for future balance review, not an approval to implement this slice.

| Candidate | Proposed effect |
| --- | --- |
| 외로운 피뢰침 | Initial discharge damage +50% if no follow-on target is found. |
| 뇌전 칼날 | Each complete five bonus attack grants ElectricAdd +1, capped at +3. |
| 사냥꾼의 축전기 | Directly killing a shocked enemy grants ElectricAdd +2 for four seconds; up to three stacks, kills refresh remaining duration. |
