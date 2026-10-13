---
status: complete
authority: active-task
mode: Implementation
last_reviewed: 2026-10-12
---

# Encyclopedia Tab Visibility And Reveal

The user approved the proposed fix for old Item detail remaining behind Creature pages and the shared Reveal failing to cover the new pages.

## Confirmed Causes

- ItemRightPage.contentRoot covers only ContentPanel/Content; header, story, hint and background remain outside this hide boundary.
- Book child order draws PageCover before CreatureLeftPage/CreatureRightPage.
- Previous isolated tests remove Item presenters and pre-hide their roots, so they did not exercise actual Item→Creature integration.

## Approved Plan

- Separate complete ItemDetailPanel visibility from internal item projection. Keep the RightPage bookmark rail alive; gate hidden input/localized rebuilds and cancel deferred scroll/glossary work while preserving item selection.
- Order Item pages, Creature pages, PageCover, then full illustration. Preserve the order in authoring.
- Verify the existing rounded cover mask against the flattened book and enlarged illustration. Adjust only the existing Reveal mask's upper coverage if required; do not change animation timing or unrelated art.
- Add real Item-presenter integration tests for tab round-trips, Reveal, interruption, close/reopen and hidden input. Retain existing isolated presentation tests as limited coverage.

## Allowed / Boundaries

Runtime EncyclopediaItemRightPage/Screen changes needed for visibility and cleanup; one additive serialized Item panel-root reference; existing prefab/authoring wiring and sibling order; existing mask upper pixels if confirmed necessary; focused tests and evidence; narrow task/memory/error/session updates. Current illustration594×389/Y−7, headings, original character/book art, item definitions, saves, GUIDs/meta, persistent enums, scene authoring, packages, managers, bootstrap, shader systems and Architecture/Contracts/Presentation HTML stay outside the change.

## Success / Verification

Item headers/story/hints/background are hidden on Monster/Boss/NPC tabs, bookmarks remain available, selection restores on Item return, hidden item input does not respond, deferred work is canceled, and Reveal occludes both new pages without exposing corners. Validate real presenters in the authored prefab, interrupted sequences and close/reopen, native rendered Reveal phases, MonoBehaviour/serialization/assembly risks, Unity/MSBuild and preservation against fresh snapshots. If actual ProtoTypeHub manual gameplay or automatic Editor execution is unavailable, report that separately rather than equating the isolated fixture with full-hub verification.

## Outcome

Completed the approved runtime visibility/input/locale cleanup, additive full-panel reference, persistent prefab/authoring render order and existing Reveal-mask upper-gap repair. Current enlarged illustration and all unrelated protected assets remain unchanged. Unity6000.4.2f1 final PlayMode26/26 and Editor/test-project MSBuild exit0 passed. Native early/mid/post-reveal captures retain real Item presenters and authored font bindings. Preservation23/23 confirms only2 prefab records changed, 2581 original records retained,64 protected hashes match and target first-frame uncovered area is0%. Ten protected save/runtime-output files and three historical renders were restored byte-for-byte after testing.

The first integration run was24/25 because its synthetic keyboard event did not reach headless player input state. The test now uses temporary in-memory Input System settings, verifies a real pressed shortcut and restores the original settings. No production input settings were changed. Full ProtoTypeHub manual gameplay and Player build were not run. Existing isolated fixtures still remove Item presenters/font localizers and remain limited tests. [Evidence and changed files](../../outputs/encyclopedia-tab-visibility-reveal-2026-10-12/README.md).
