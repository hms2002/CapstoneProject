---
status: active
authority: structure-memory
category: input
last_reviewed: 2026-09-21
---

# Input Bindings

## Purpose And Ownership

`InputBindingService` owns runtime primary/secondary bindings and `settings.input.*` PlayerPrefs.
`InputBindingDefaultsSO` and `Resources/InputBindingDefaults.asset` provide defaults and the editable-action policy.
Gameplay reads through Core `InputActionQuery`; UI may read the concrete service for glyphs and editing.
`KeyBindingPanelUI` owns an unapplied working copy. Confirming a swap updates that copy; Apply persists it.

## Current Actions

- Inventory open/close: V / I.
- Inventory drop: F; `ItemHoverController` consumes the action, retaining the existing hover/container/last-weapon checks.
- Level reward open: R; `LevelRewardSessionController` retains run, reward, combat, dialogue and popup eligibility. The old serialized `openKey` remains only for compatibility.
- Minimap expand: M / Equals (the keyboard +/= key); shrink: N / Minus. `DungeonMinimapSizeController` uses its existing collapsed/normal/expanded steps, visibility and blocking-UI policy.
- Dialogue advance is fixed Space, with the existing Interact alias. It is absent from editing, conflict detection and override persistence.
- Dialogue choice number keys and reward-window reroll/choice shortcuts keep their existing context behavior.

## Conflict And Save Rules

- Interact and InventoryDrop may share a key because they operate in separate world/inventory contexts; their default is F. InventoryToggle and LevelRewardOpen are not exempt.
- A conflicting change swaps all editable owners of the old/new keys. Swapping only one F owner would leave a duplicate with the inventory toggle.
- Only slots explicitly changed by a request are applied; an unchanged secondary slot must not undo a primary/secondary exchange.
- Authored action order is filtered against the editable policy and supplemented with missing editable actions. Older title/global UI row lists therefore expose new actions without prefab surgery.
- Historical DialogueAdvance overrides are ignored and removed on load. Public mutation APIs reject fixed actions.
- The one-time `settings.input.contextActions.v1` upgrade fills an empty inventory secondary with I if free. New action defaults do not displace existing custom keys; conflicting new slots remain None and can be assigned in Settings. Nonempty existing inventory secondary bindings are preserved.
- Item detail hints carry an optional action ID across the Core/Gameplay/UI boundary; UI resolves its current primary key, or secondary when primary is empty. Active hints refresh on BindingChanged and unsubscribe on disable.

## Extension Points And Validation

Append action IDs without changing existing enum values. Update defaults, label, consumer and regression coverage together.
Keep fixed actions out of the editable list; hiding a row alone is insufficient.
`Tools/Validation/KeyBindingRegression.cs` runs against real compiled assemblies in an isolated Unity project; it guards its product name before using disposable PlayerPrefs.
Manual verification still covers actual keyboard layouts, authored screen layout, item dropping and level-reward gameplay entry.
This map is a reconstruction aid, not a new Architecture/Contracts authority. No promotion is required for this change.
