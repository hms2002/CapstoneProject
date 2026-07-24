---
status: active
authority: source-of-truth
category: architecture
last_reviewed: 2026-06-09
---

# Scene Classification Architecture

This document defines how scenes are used as evidence for structure, validation, and refactoring decisions.

## Core Rule

Do not let legacy or archive scenes define current architecture.

Current structure decisions should use the official playable/prototype scenes named by the active task or by this classification policy.

## Scene Classes

| Class | Meaning | Refactor Authority | Validation Use |
| --- | --- | --- | --- |
| Canonical | Current official player-facing scene path. | May define architecture and contracts. | Required when the task touches that flow. |
| Prototype | Current working scene used for active development and validation. | May define current project structure when no canonical scene is promoted. | Preferred for current gameplay verification. |
| Legacy | Historical or migrated scene kept for reference. | Must not define current architecture. | Optional reference only. |
| Archive | Trash, disabled, or removal-candidate scene. | No architecture authority. | Excluded unless the task explicitly targets cleanup. |

## Current Project Default

Until a task says otherwise, current structure and validation use `ProtoType*` scenes.

Examples include:

- `ProtoTypeHub`
- `ProtoTypeCorridor*`
- `ProtoTypeBoss*`

Legacy scenes may be inspected to understand old behavior, but they must not be treated as the source of truth for current UI root, player, save, runtime service, or scene transition policy.

## Scene Metadata To Track

When a scene is promoted, demoted, or used as a recurring validation target, record:

- scene name
- class: Canonical, Prototype, Legacy, or Archive
- owner or feature area
- Build Settings inclusion
- whether it is a validation target
- `GlobalUIRoot` policy
- player prefab policy
- runtime service assumptions
- related architecture or structure memory document

## Refactor Rules

- Do not fix current systems by matching a legacy scene unless the task explicitly promotes that scene.
- Do not edit scene YAML as part of a refactor unless the user explicitly approved scene/prefab changes.
- If a code refactor depends on scene authoring, split the authoring migration into its own task.
- If a validator can catch the required authoring state, prefer validator coverage over runtime fallback creation.

## Validator Direction

`SceneSetupValidatorWindow` is the correct direction for production-facing authoring safety.

Useful validation targets include:

- exactly one `GlobalUIRoot`
- required `GlobalUIRoot` canvas/service references
- duplicate global UI services
- player prefab required components
- scene portal route ids
- Build Settings scene paths
- empty or missing loot tables
- duplicate item ids
- missing serialized references on authored presentation objects

Add validator checks only in a code slice that explicitly allows editor tooling changes.

## Related Documents

- [Scene Domain Bootstrap Architecture](./SceneDomainBootstrapArchitecture.md)
- [Runtime Service Ownership Architecture](./RuntimeServiceOwnershipArchitecture.md)
- [Current Project Context](../Overview/current-project-context.md)
- [Presentation Authoring Contract](../Contracts/PresentationAuthoringContract.md)
