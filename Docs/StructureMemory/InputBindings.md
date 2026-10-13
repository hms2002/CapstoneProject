---
status: active
authority: structure-memory
category: input
last_reviewed: 2026-10-05
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
- Dialogue advance is fixed Space, with the existing Interact alias. It is absent from editing, conflict detection and override persistence. Regular `DialogueController` also accepts Enter/numpad Enter directly for advance and choice confirmation; these aliases do not change saved bindings.
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

### Menu and level-card keyboard focus

- [TitleMenuController](../../Assets/_Project/Runtime/UI/Title/TitleMenuController.cs), [PauseMenuUI](../../Assets/_Project/Runtime/UI/Settings/PauseMenuUI.cs), and [LevelRewardSelectionPresenter](../../Assets/_Project/Runtime/UI/Progression/Leveling/LevelRewardSelectionPresenter.cs) own their screen eligibility and refresh a plain `ButtonNavigationGroup` in `LateUpdate`.
- `ButtonNavigationGroup` is declared alongside [MenuButtonHighlightPresentation](../../Assets/_Project/Runtime/UI/Common/MenuButtonHighlightPresentation.cs). It sets explicit links between existing eligible Buttons, focuses the first available control after animation unlock, remembers the last control across a child panel, and removes only its own selected object when suspended. It creates no hierarchy, component, service, or serialized state.
- Title/pause menus use vertical W/S and up/down navigation; level cards use horizontal A/D and left/right navigation. The existing `InputSystemUIInputModule` owns those fixed UI keys and Enter submission. Existing menu highlights and authored card selected colors project focus.
- These three navigation groups also dispatch a local Space Submit to their selected Button. Opening, suspension, an unavailable/animation-locked group, and a fresh card offer require Space release before another confirmation. A held Space never repeats, and a native Submit already performed in the same frame prevents a second Space Submit. Global UI actions are unchanged, so intro skip, tutorial/chest hold buttons, key/text capture and the regular dialogue's existing Space path retain their own input handling.
- [UIManager.CanProcessNavigationInput](../../Assets/_Project/Runtime/UI/Common/UIManager.cs) queries the top active stack entry, loading state, and existing external-block owner exceptions. This keeps the level session's owned popup operable under its own blocker while rejecting another flow's blocker and background-menu input.
- A new card offer resets focus to its first available card. Applying, flipping, rerolling, opening-frame and closing gates also cover Button submission. Existing 1/2/3 selections, R reroll and session-owned candidate/pause state remain with their existing owners.
- [DialogueController](../../Assets/_Project/Runtime/UI/Dialogue/DialogueController.cs) includes Enter/numpad Enter in the choice-confirm release gate. Holding Enter while revealing choices therefore keeps confirmation disabled until release and the existing guard delay finish.
- [UiKeyboardNavigationRegression](../../Tools/Validation/UiKeyboardNavigationRegression.cs) exercises the built UI/input assemblies in a disposable Unity project. Production scene layout and rendered presentation acceptance remain separate manual checks.

Append action IDs without changing existing enum values. Update defaults, label, consumer and regression coverage together.
Keep fixed actions out of the editable list; hiding a row alone is insufficient.
`Tools/Validation/KeyBindingRegression.cs` runs against real compiled assemblies in an isolated Unity project; it guards its product name before using disposable PlayerPrefs.
Manual verification still covers actual keyboard layouts, authored screen layout, item dropping and level-reward gameplay entry.
This map is a reconstruction aid, not a new Architecture/Contracts authority. No promotion is required for this change.

## 2026-10-08 key settings localization

KeyMapRow action captions are owned by KeyBindingRowUI.Refresh/InputBindingService.GetActionLabel. The copied Screen Mode LocalizeStringEvent was removed; localized fonts stay. Reset/listening use ui.keybinding.reset/ui.keybinding.listening. Locale changes refresh row labels without replacing working/saved bindings or cancelling key capture.
