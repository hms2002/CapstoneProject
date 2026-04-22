# Presentation Authoring Contract

## Goal

Keep Ability / Effect / direct Presentation authoring consistent while legacy GameplayCue remains in compatibility mode.

## Core Rule

- `Presentation` is for on-site, pattern-specific authoring.
- `Cue` is for reusable finished presets.
- Runtime execution is shared, but authoring intent is different.

## Ownership Rule

Presentation authoring is split by **who owns the timing and rhythm**.

- **AL / pattern data owns execution presentation**
  - warning duration
  - hit / explosion / projectile VFX
  - SFX
  - camera shake
  - local telegraph style references
- **FSM state / owner owns state-rhythm presentation**
  - state transition intro / armed / recover visuals
  - chase-linked warning geometry that must follow a state lifecycle
  - mask / overlay / tween that exists because a specific state is active
- **runner / helper only consumes and cleans up presentation**
  - it should not become the long-term authoring owner of fixed presentation data
  - it may hold temporary runtime handles and hide/reset them on `Cancel/finally`

In short:

> **AL owns "how this pattern looks when it executes", state owns "what must stay visible while this state is alive", runner owns only temporary runtime handles plus cleanup.**

## AbilityDefinition

### One-shot phases

- `OnCastStart`
- `OnCommit`
- `OnEnd`
- `OnCastCancelled`
- `OnExecutionCancelled`
- `OnHitConfirmed`

For these phases:

- `Sound` plays once
- `CameraShake` plays once
- `Spawned Presentation` plays once
- `Cue` executes once

### Sustained phases

- `WhileCasting`
- `WhileActive`

For these phases:

- `Sound` starts once on phase enter and loops until phase exit
- `CameraShake` plays once on phase enter
- `Spawned Presentation` plays once on phase enter
- `Cue` is added once on phase enter and removed once on phase exit

These fields are **not** per-frame retrigger hooks.

## GameplayEffect

### One-shot phases

- `OnExecute`
- `OnRemove`

### Sustained phase

- `WhileActive`

`WhileActive` follows the same rule as Ability sustained phases:

- looped audio
- enter pulse shake
- enter pulse presentation
- add/remove cue lifecycle

## GameplayPresentationDefinition

`GameplayPresentationDefinition` uses the same contract as `GameplayEffect`:

- `OnExecute` = one-shot
- `WhileActive` = loop audio + enter pulse presentation/shake + add/remove cue
- `OnRemove` = one-shot

## Authoring Guidance

Use `Presentation` when:

- the timing is owned by a specific AL or local gameplay logic
- the position, direction, count, or branch is pattern-specific
- the effect is unlikely to be reused as a common preset

Use `Cue` when:

- the presentation is a reusable finished preset
- multiple systems or patterns should share the same presentation package
- catalog management is more important than local tuning

Use **state/owner-local presentation** when:

- the visual exists because a specific FSM state is active
- the visual must start on `Enter` and end on `Exit`
- the visual is tied to chase / intro / armed / recover rhythm rather than the pattern commit itself
- the visual needs to track owner movement continuously while the state stays alive

Examples:

- `ShadowServant`, `StrangeCandlestick`
  - warning / hit / projectile presentation data is primarily **AL-owned pattern presentation**
- `Witch` abilities
  - `WorldPresentationHook`, warning style, failure/charge presentation are primarily **AL-owned pattern presentation**
- `DeadsSkeleton`
  - intro warning, armed warning, sight mask expand/reset are currently **state-owned presentation**
  - explosion hit/failure presentation is **AL-owned pattern presentation**

## Cleanup Contract

Presentation ownership also determines cleanup responsibility.

- **state-owned presentation**
  - must be cleaned up from the state's `Exit`
  - if the state can be interrupted by suppression / death / disable, the combat object fail-safe path must also be able to hide/reset it
- **runner-owned temporary handles**
  - must be cleaned up from `Cancel/finally`
- **global fail-safe cleanup**
  - must be callable without knowing concrete runner/helper types
  - for general mobs this is currently standardized through `IMobPresentationCleanup`

Related docs:

- [Mob Cleanup Contract](./MobCleanupContract.md)
- [Boss Encounter Architecture](./BossEncounterArchitecture.md)
- [Pattern Data Ownership Review](./PatternDataOwnershipReview.md)

## Current Project Status

The project is no longer in a "presentation is only practical glue" state.

Current progress:

- Witch boss abilities already author a meaningful amount of local pattern presentation in AL.
- General mob cleanup has a shared presentation cleanup contract through `IMobPresentationCleanup`.
- `ShadowServant`, `StrangeCandlestick`, and `DeadsSkeleton` already distinguish between:
  - AL-owned pattern presentation
  - state-owned presentation rhythm
  - runner cleanup responsibility

Remaining work is mostly about **expanding this ownership rule consistently**, not inventing a brand-new presentation model.

## Legacy GameplayCue

- Existing tag-based GameplayCue paths stay alive for compatibility.
- New pattern-specific work should prefer local `Presentation`.
- Migration away from legacy cue tags should be gradual, not forced in one pass.
