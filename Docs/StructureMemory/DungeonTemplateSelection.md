# Dungeon Template Selection

Status: implementation map, not an Architecture or Contracts source of truth.

## Scope And Ownership

`DungeonGraphLayoutAssembler` still creates logical topology, reserves room roles and places compatible sockets/room bounds through the existing physical embedding functions. Its `TemplateSearch` partial owns only one topology attempt's template candidate domains, temporary assignments, bounded search and final-result comparison. No room/policy schema, enum value, scene, prefab, tile painter, monster spawning or save DTO is changed by this feature.

- `DungeonGraphLayoutAssembler.cs`: existing topology/role/physical algorithms, guaranteed-template placement, quota-slot reservation and cross-topology result selection.
- `DungeonGraphLayoutAssembler.TemplateSearch.cs`: domain preparation, cached graph distances and template/shape comparisons, constrained search, physical validation and repeat diagnostics.
- `DungeonTemplateSelectionReport.cs`: immutable runtime-only repetition metrics and per-topology search report.
- `DungeonLayoutModels.cs`: exposes the selected report on `DungeonLayoutResult.TemplateSelection`; legacy layouts may leave it null.
- `DungeonGenerator.cs`: appends the report to the existing once-per-generation log, not per-frame logging.
- `RoomAuthoringDungeonPreview.cs` / `RoomPieceEditorWindow.cs`: carry/display the same report separately from failure messages; a successful relaxed result is not a generation failure.

## Assignment Flow

1. Existing role assignment chooses guaranteed-template nodes and reserves enough Combat roles. Concrete Combat templates selected during reservation are provisional: quota fulfillment may redistribute across any compatible Combat nodes. Only explicitly guaranteed template identities remain fixed and excluded from other nodes.
2. Domains filter by role, usable weight/bounds, required socket directions and opposite-axis alignment, and topology placement requirements. Global quota feasibility is checked during assignment rather than fixing size/reward tags to provisional nodes. Node/template weights retain authored selection weight and existing exact/extra-socket multipliers. Candidate order is stable for the same input/seed.
3. Forward checking computes currently usable options for every unassigned node. Pick the node with the fewest options, then highest degree, then a seed-derived tie-break. An empty remaining domain rejects the tentative assignment immediately.
4. Global quota lower/upper bounds and the mandatory remaining Large count prune choices before completing the layout. Positive `RequiredCombatRoomRule.Count` values are exact targets; zero-count rules remain inactive for compatibility. All matching rules apply, including overlapping rules. Incompatible rules fail rather than silently overfilling. `MaximumLargeCombatRoomCount` is never softened.
5. Order options by repetition tier, then weighted random ordering without replacement inside that tier. Failed branches undo their assignment. No global Unity random state is consumed.
6. A complete assignment resolves sockets and calls the existing physical layout/spacing relaxation path. Physical failures return to template search. Successful physical results are rechecked for roles, guaranteed identity, socket compatibility, placement restrictions, exact positive quotas and Large cap.
7. Repetition is measured from the actual result connections, counting each unordered pair once. Compare adjacent identical room, adjacent identical shape, non-adjacent nearby identical room, then nearby identical shape; only equal scores use corridor preference overrun and longest corridor. Same-room repetition also counts as same shape. Identity retains reference-or-roomId equality; shape retains explicit shape tags and the existing socket-mask fallback.

## Relaxation And Bounds

- Phase 0: no same-template or same-shape repeats within the policy's configured graph distance (normally two).
- Phase 1: allow non-adjacent nearby repeats, keep adjacent template/shape repeats forbidden.
- Phase 2: allow adjacent shape repeats, keep adjacent identical templates forbidden.
- Phase 3: allow adjacent identical templates as the last fallback.

Each phase allows at most 1,024 candidate assignments, 16 physical-layout attempts and four physically valid candidates. A repeat-free result with acceptable corridor lengths can stop earlier. A phase with valid results returns its best; it does not relax further merely to improve corridor length. Across topology attempts, up to eight physically successful candidates are compared, still subject to the existing topology-attempt cap. These are deterministic work limits, not elapsed-time cutoffs or optimality guarantees.

The selected result's `SearchSteps` and phase history describe that topology's search, not total work across rejected topologies. Remaining repeated pairs distinguish two guaranteed identities, no non-repeating pair in static hard-valid domains, and a bounded solution under coupled/physical constraints. Budget exhaustion is explicitly different from exhaustive failure; reports do not claim that every remaining repeat is mathematically unavoidable.

## Compatibility And Verification

- Same input/seed is deterministic after the patch, but the selected map can differ from the old greedy algorithm. Start a fresh run after changing the algorithm/content; do not assume old in-memory placement-ID snapshots describe a newly generated layout.
- An impossible exact quota no longer succeeds by choosing the last over-quota candidate. Content must provide enough compatible normal/non-reward choices where those slots are required.
- Mandatory concrete templates and room roles remain governed by the existing role phase. Template rollback can redistribute Combat size/reward quotas, but does not relocate a guaranteed template or redesign graph topology. Another outer topology attempt can choose a different compatible guaranteed placement.
- Work occurs during generation/preview only. More candidate checking and physical attempts cost more than one greedy pass; use the native seed-sweep timings as local evidence, not as a production-frame performance guarantee.
- `DungeonTemplateSearchPlayModeTests` covers the scarce-neighbor regression, an explicit failed branch requiring rollback, provisional quota-slot reassignment, fixed guaranteed identity, relaxation order, hard count rejection, quality ordering, deterministic replay and a production-theme seed sweep. `ProceduralRoomRuntimeBindingPlayModeTests` checks existing quotas, guaranteed-room placement and physical binding; `DungeonReturnPortalPlayModeTests` checks compatibility with generated dead ends.

Promotion candidate: if these exact quota and bounded-relaxation semantics stabilize as a designer-facing contract, propose a narrow Architecture/Contracts update with approval. No additional public manager or general-purpose constraint framework is needed.
