# Combat Status HUD Lifetime

Navigation aid; not an Architecture or Contracts source of truth.

- CombatBuffDebuffApplier applies effects through the target AbilitySystem. HUD synchronization is forwarded to an applier on the PlayerStatusRuntime owner, even when the original caller is a puddle/source object.
- A tracked HUD handle belongs to target + OwnerKey + ActiveGameplayEffect reference. Refreshing a shared non-stacking effect from another source reuses that handle; separate active effects retain their individual lifetimes.
- The tracked source follows ActiveGameplayEffect.SourceObject. IndependentDuration survives source destruction until effect expiry. WhileSourceAlive still removes the tracked effect after its current source disappears. Recipient disable/destroy releases its tracked HUD handles.
- PlayerStatusRuntime keeps handle-based registrations. StatusHudService collapses visible projections by (OwnerKey, StatusId), choosing the entry with greatest RemainingTime without adding stacks. Empty keys are not deduplicated; different weapon slot owners remain distinct.
- GameplayEffectRunner and gameplay modifiers are unchanged. Do not replace exact active-effect tracking with asset-only lookup for source-distinct cooldown effects.
- Regression coverage: OverlappingAlcoholSources_KeepOneHudHandleUntilEffectExpires and StatusProjection_DeduplicatesSameOwnerButPreservesDifferentSlots.


## Quest curse progress projection

- `SteelTrainingLevelRewardEffectSO` and `UnextinguishedFireLevelRewardEffectSO` remain the owners of saved objective counts. Their status requests supply `ProgressText` as current/target only while incomplete.
- Optional runtime `ProgressText` flows through `StatusApplyRequest` -> `ActiveStatusEntry` -> `StatusHudDefinition.CreateEntry` -> Core `StatusHudEntry`. No serialized definition fields or save schema were added. Sources can supply `N%` for percentage-based objectives; the existing two curses use counts.
- `StatusHudEntryView` reuses the authored stack text in the center with a black TMP outline while progress is present. It restores the original anchors, offsets, alignment, font size, color and outline when a pooled entry returns to ordinary stacks. HUD owns no objective state.
- Regression: `Tools/Validation/StatusTelegraphRecoveryRegression.cs` covers projection, zero count, percentage, completion and slot reuse.

## Entry binding performance

- `StatusHudEntryView` prepares its valid authored/fallback tree once and caches Outline. Editor OnValidate invalidates style preparation; no new runtime UI path was introduced.
- Stack formatting is keyed by ShowStacks, StackCount and ProgressText. Disable invalidates binding so pool reuse redraws correctly. The full currentEntry is still replaced on every Bind for tooltip metadata. Duration values are projected each frame, but `HudDisplayTextCache` reuses the string when the exact formatted characters are unchanged; fill continues its original cadence.
- Status hover refresh uses a typed entry path so a ticking status does not repeatedly box entries, reopen the hover session or restart deferred positioning. Tooltip layout responds to content, TMP/font changes, locale and viewport changes while anchor tracking continues. Numeric caches preserve the runtime's existing formatting and culture rather than introducing a new rounding policy.
- `StatusHudPerformancePlayModeTests` uses the production entry prefab and verifies setup/format suppression, timer updates, highlight, progress layout and reuse. See `Logs/Performance/StatusHudOptimization_20260928.md` for before/after evidence. The measured absolute savings are small; this is not a demonstrated frame-stall fix.
