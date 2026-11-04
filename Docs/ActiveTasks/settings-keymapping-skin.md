---
status: complete
mode: Implementation
---

# Settings and key mapping approved skin

Approved 2026-10-31: user `응 이대로 가자` after `settings-clean-title-preview.html`.

- Goal: settings and key mapping each fit on one desktop page, with two columns, upper chains only, a separate clean centered title, thin violet frames and internal bottom buttons.
- Approved reference: `C:/Users/nadom/.codex/visualizations/2026/10/30/01a18b74-648b-7e30-ae32-011b0ef3851e/settings-clean-title-preview.html`. The ornate title and later whole-panel restyling are superseded.
- Allowed: scoped settings/keymapping subtrees in main GlobalUIRoot and TitleScene, shared KeyMapRow prefab, dedicated pixel skin assets/Aseprite source, required row-binding adaptation, localized section labels, Editor authoring/validation and narrow memory updates.
- Preserve: current services, saved settings and bindings, localization/fonts, input conflicts and close confirmation, popup ownership/lifecycle, upper chain simulation, unrelated prefab/scene objects and existing IDs/GUIDs. No new managers, packages or project settings.
- Implementation: author the two-column groups and 19 rows in existing assets; bind those rows through the current presenter. Preserve existing legacy dynamic-row support for unmigrated independent copies. Replace row/frame/control art with a dedicated reusable pixel sheet matching the preview. Author bottom buttons inside the panel and retire their lower chains.
- Verification: before snapshots, scoped native imports/serialization preservation, Unity compilation and targeted existing input/UI regressions, real presenter screenshots in five locales and representative resolutions, repeated open/close and input-confirmation behavior. MSBuild for included changed C# files.
- Risks: existing render-layer layout wrappers, native font/material overrides during asset loading, title-local versus global panels, chain anchor geometry and large translated labels. No standalone build/performance claim without execution.
- Documentation: SessionLog + SettingsPanelLayout/InputBindings structure maps; mark completed only after integration/validation. Presentation HTML is outside this scope.

Completed 2026-10-31: native authoring/import and compilation, 1,451 real-presenter checks across both sources/five locales, 17 GPU captures, 3/3 existing render-layer regressions, scoped serialization preservation, Aseprite source/export equality and final Editor.csproj MSBuild exit0. [Evidence and limits](../SessionLogs/2026-10-31-settings-skin/README.md). Full-scene manual acceptance and player build were not executed; independent legacy copies remain a documented migration follow-up.

Follow-up approved 2026-10-31: switching to/from key settings replaces the page immediately, without another drop/close animation. Preserve initial settings opening, root UI stack/locks, pending display selections and binding confirmation. Scope: two presenters, authored settings-root visibility CanvasGroup in main GlobalUIRoot/TitleScene, narrow authoring/validation and memory updates. Verify both title-local and UIManager stack entry paths, immediate visibility/interaction and repeated round trips. No geometry or art changes.

Follow-up completed: Unity native compile/import and 1,548 real-presenter checks passed, including same-frame entry/return, both navigation owners, pending selections, initial settings animation and stack time-lock release. Final Editor.csproj MSBuild exit0. In each serialized source only the settings root component list and visibility reference changed, plus one authored CanvasGroup; all other blocks and existing IDs match the follow-up snapshot. [Follow-up evidence](../SessionLogs/2026-10-31-settings-skin/README.md#in-place-navigation-follow-up). Full-scene manual walkthrough and Player build were not executed.
