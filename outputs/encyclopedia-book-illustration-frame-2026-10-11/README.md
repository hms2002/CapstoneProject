# Encyclopedia illustration section enlargement — 2026-10-11

The user explicitly chose enlarging the section itself toward the book's brown border. This enlarges the frame, retaining the existing portrait fit rather than cropping/stretching the picture to cover it.

## Changes

- `EncyclopediaUI.prefab`: only Illustration Rect3202883555615465603 changes from550×371 at y−25 to594×389 at y−7. The lower edge remains−396, preserving the4-unit gap before affection starts at−400. In the existing1920×1080 geometry, calculated clearance from the brown rule is left1.51/right1.82/top1.64pixels. These are calculated values, not new screenshot measurements.
- `EncyclopediaCreatureUIAuthoring.cs`: retains this enlarged frame in the existing AlignBookContentInsets() flow and points its evidence to this folder. The common25-unit title positions remain intact.
- FitInParent, portrait references/pivot, original art, name overlay, grids, lower content, hidden count, FullView1-unit edge placement and arrow behavior remain unchanged. The expanded frame can cover some upper corner decoration while preserving the brown rule.

## Executed validation

- Unity6000.4.2f1 native authoring/import exit0: [log](authoring-unity.log).
- Editor MSBuild exit0: [console](msbuild-editor-console.log). Relevant generated source inclusion retained; no generated project edits.
- [Preservation20/20](serialized-preservation-report.json): exact one Rect changed, only anchoredPosition.y and sizeDelta.x/y; other2582records and all2964font/sprite/material/Fit fields identical; all eleven protected file hashes match. Frame bottom/affection top and previous title/control geometry retained.

## Unexecuted validation

The CLI rejected PlayMode dispatch because this project was open in interactive Unity Editor PID38076. No tests or new native captures ran for this larger frame. [Dispatch report](test-dispatch-report.json). The prior21/21 and captures in `before/` belong to the preceding550×371 layout and must not be interpreted as this follow-up's runtime validation.

No test runner started, so no test-save restoration was needed; the open Editor and user save files were left untouched. Full-hub manual gameplay, larger-frame visual review, Player build and production locale-font checks remain unexecuted. The asset is applied and available for review in the open Editor.

Doc Impact: SessionLog + existing StructureMemory + ActiveTask/TaskIndex. ErrorLog/DecisionLog not changed by this follow-up. No Architecture/Contracts/Guides/Presentation HTML changes or HTML stale candidate.
