---
status: active
authority: structure-memory
last_reviewed: 2026-11-01
---

# Test and Editor tool retention

Current user policy is recorded in AGENTS.md: automated tests run only on explicit request. Implementation, cleanup or review is not permission to start PlayMode, EditMode, CLI tests or automated play-mode checks.

Active regressions remain under `Assets/_Project/Tests`. Gameplay/save/state ownership checks are retained, including14 Crimson gameplay cases. Mixed encyclopedia discovery/UI cases remain intact. Generic UI ownership and reusable performance fixtures remain; they are not automatically run.

Completed task-specific UI/VFX acceptance and fixed migration tools are stored as text outside Unity's import tree under [Tools/Archive/2026-11-01-one-off](../../Tools/Archive/2026-11-01-one-off/README.md). The [manifest](../SessionLogs/2026-11-01-test-tool-cleanup/archive-manifest.json) preserves original paths, bytes and script GUIDs. Six whole suites and two Crimson methods retired90 cases;17 Editor sources and six standalone tool/input files retired.52 files moved including metadata, plus one partial-file before-copy.

The maintained Editor routes still include common content generators, room/weapon authoring, localization pipeline, TestScene authoring/isolation, frame CPU/GPU profilers, UI batching measurement and MonsterHealthBarGpuValidation. The retired tools applied fixed dated layouts/art and should not be reapplied over later authoring.

Recovery restores a coherent dependency group and its original metadata, never old whole-prefab/scene snapshots. Do not restore the full historical Crimson test file over a newer active version; recover only a requested method. Reflection-string references matter as well as compile-time calls. No new asmdef or runtime owner is introduced by archival.

Source inventory after cleanup:69 test C# files,1125 PlayMode case declarations and20 project EditMode case declarations. Unity Test Runner/package counts can differ until its next normal refresh. No test run or post-cleanup compile is claimed. [Evidence and limits](../SessionLogs/2026-11-01-test-tool-cleanup/README.md).

This is a navigation map, not a new Architecture/Contracts policy candidate. Presentation HTML is unaffected.
