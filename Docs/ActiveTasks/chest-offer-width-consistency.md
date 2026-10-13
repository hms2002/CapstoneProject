---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-08
---

# Chest Offer Width Consistency

- Mode: Implementation, authorized by the user's `작업해줘` after the clipping investigation.
- Goal: All four columns of chest offers remain visible after first-open activation; capture and displayed frame agree on authored width constraints.
- Confirmed cause: 31766cfc changed RightMiddleFrame Rect width56 to13, keeping LayoutElement.minWidth56. Inactive capture used13 and cached total468; active arrangement used56 and overlapped the grid by39 (fourth slot by29). Before that change, total511 had no overlap. A Unity6000.4.2f1 isolated layout probe reproduced both states.
- Allowed: Narrow ChestFirstOpenRevealPresentation width measurement source, focused Play Mode tests, dated/focused project memory and generated validation artifacts.
- Preserve: Existing dirty localization/batching work, selection/reroll/return rules and reveal timing, height calculation, serialized fields/GUIDs, production scenes/prefabs/atlases/fonts, bootstrap/save formats and Architecture/Contracts/Presentation HTML.
- Shape: Inactive width measurement honors an enabled authored LayoutElement's minimum/preferred widths without activating UI or changing asset authoring. Active measurement retains normal uGUI behavior. No new Manager/Singleton/DDOL or runtime hierarchy.
- Success: Actual authored chest copy bound/captured inactive then opened has consistent full width and no slot overlap with the right decoration or viewport boundary. Disabled layout constraints remain ignored; reopen and captured layout stability are checked.
- Verification: Generated-project MSBuild, real Unity Play Mode regression and existing relevant chest tests; inspect source/asset hashes and report unavailable checks. Root exclusively launches Unity.
- Documentation: SessionLog, narrow inventory/chest StructureMemory and ErrorLog startup-sizing entry. No Presentation HTML update is expected because ownership/layout purpose is unchanged.

## Outcome

- Source-only inactive width correction; no production asset authoring, new service or gameplay changes.
- Unity6000.4.2f1 compile/import and11/11 selected Play Mode tests passed (8 new width/layout cases +3 existing chest cases), zero failed/skipped.
- Actual GlobalUIRoot chest copy: stocks4/5/8 capture511 while inactive and show511 after activation. Offer world corners remain inside the viewport and before the right decoration; captured size remains stable during selection/reroll and refreshes after release/reopen.
- UI.csproj and ProceduralPlayModeTests.csproj MSBuild passed, including imported new test source; generated projects were not manually edited.
- Nine protected existing UI prefabs/scenes retained exact SHA256; original fonts/GUIDs and unrelated dirty work were not modified. Shared inventory/profile/run state guards passed in tests.
- Evidence: `Docs/SessionLogs/2026-10-08-chest-width/`; SessionLog, existing structure memory and ErrorLog updated. DecisionLog and Presentation HTML unchanged; no stale HTML candidate.
