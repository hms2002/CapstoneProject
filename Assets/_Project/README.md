# _Project Folder Guide

This folder is the target home for project-owned runtime code, data assets, prefabs,
art, audio, scenes, editor tools, and tests.

## Core Rule

- `Runtime`, `Editor`, and `Tests` contain `.cs` files.
- `Data` contains runtime-read `.asset` instances only.
- `Data` must not contain `.cs` files, even when the class is a ScriptableObject type.
- Third-party folders such as `Plugins`, `TextMesh Pro`, and `Ink` stay outside this tree.

## Runtime

Runtime code is grouped by responsibility, not by author name.

- `Core`: reusable game rules and contracts that are not owned by a specific feature.
- `Infrastructure`: Unity/external-service implementation, IO, global services, and platform-facing systems.
- `Features`: concrete game use cases that would disappear if the feature were removed.
- `UI`: Canvas-based screens, presenters, and input-facing view logic.

Core is not a catch-all folder. A file can enter `Core` only when it satisfies all of these:

- Used by at least two unrelated features.
- Does not contain a specific weapon, monster, boss, scene, or screen name.
- Does not depend on UI, scene flow, audio, or feature-specific presentation.
- Still has a meaningful purpose if one feature is removed.
- Represents a reusable game rule, contract, or foundation system.

## Data

`Data` is organized by data type and authoring purpose.

- Ability definitions, ability strategies, tags, cues, formulas, item definitions, loot tables, route manifests, and UI catalogs live here.
- ScriptableObject class files live under `Runtime`, not here.
- Strategy asset folders use `Strategies` instead of `Logics` to avoid confusing `.asset` instances with `.cs` logic code.

## Migration Risk Rules

Move files in this order:

1. GUID-only references: regular `.cs`, `.asset`, prefab, sprite, and audio references.
2. Editor path-search files: files used by tools that call `AssetDatabase` or search folders.
3. String/external-registry files: `Resources`, Addressables addresses, scene names, load manifests, Ink, and file-system paths.

Do not move risk level 3 files until their string paths or registry entries are updated in the same change.

## Naming Boundaries

Avoid folders named `Misc`, `Etc`, `Temp`, `Common2`, or similar catch-all names.
Use `_MigrationPending` for unclear files, and record why the file is blocked.
