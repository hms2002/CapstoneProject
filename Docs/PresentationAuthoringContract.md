# Presentation Authoring Contract

## Goal

Keep Ability / Effect / direct Presentation authoring consistent while legacy GameplayCue remains in compatibility mode.

## Core Rule

- `Presentation` is for on-site, pattern-specific authoring.
- `Cue` is for reusable finished presets.
- Runtime execution is shared, but authoring intent is different.

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

## Legacy GameplayCue

- Existing tag-based GameplayCue paths stay alive for compatibility.
- New pattern-specific work should prefer local `Presentation`.
- Migration away from legacy cue tags should be gradual, not forced in one pass.
