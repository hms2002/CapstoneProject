# Combat Status HUD Lifetime

Navigation aid; not an Architecture or Contracts source of truth.

- CombatBuffDebuffApplier applies effects through the target AbilitySystem. HUD synchronization is forwarded to an applier on the PlayerStatusRuntime owner, even when the original caller is a puddle/source object.
- A tracked HUD handle belongs to target + OwnerKey + ActiveGameplayEffect reference. Refreshing a shared non-stacking effect from another source reuses that handle; separate active effects retain their individual lifetimes.
- The tracked source follows ActiveGameplayEffect.SourceObject. IndependentDuration survives source destruction until effect expiry. WhileSourceAlive still removes the tracked effect after its current source disappears. Recipient disable/destroy releases its tracked HUD handles.
- PlayerStatusRuntime keeps handle-based registrations. StatusHudService collapses visible projections by (OwnerKey, StatusId), choosing the entry with greatest RemainingTime without adding stacks. Empty keys are not deduplicated; different weapon slot owners remain distinct.
- GameplayEffectRunner and gameplay modifiers are unchanged. Do not replace exact active-effect tracking with asset-only lookup for source-distinct cooldown effects.
- Regression coverage: OverlappingAlcoholSources_KeepOneHudHandleUntilEffectExpires and StatusProjection_DeduplicatesSameOwnerButPreservesDifferentSlots.
