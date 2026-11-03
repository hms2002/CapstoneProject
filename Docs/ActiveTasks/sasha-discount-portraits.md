# Sasha Discount Dialogue And Portraits

- Mode: Implementation, approved in chat on 2026-11-02.
- Status: Completed (game assets and Notion text). Optional external image attachment awaits explicit sharing approval.
- Goal: Use supplied image 1 for Sasha's existing Normal portrait and image 2 for Surprised on authored no-gain choices; add the approved discount conversation unlocked at affection 1.
- Dialogue: greeting and pricing explanation are two separate click-advanced speech windows; choices appear after the second. Confidence branch grants +1 below cap; refusal keeps affection and shows Surprised. Preserve the existing cumulative pool and one choice conversation per NPC/run, including cap conversations without gain/indicator.

## Approved Scope And Assumptions

- Replace existing merchant portrait PNG byte-for-byte with attachment 1 while retaining its path/GUID/import identity. Add attachment 2 as a separate PNG with Unity-generated metadata and matching point/alpha/PPU settings.
- Preserve the existing SpriteLibrary GUID/local reference and Face/Normal label; add Face/Surprised via the installed Unity SpriteLibrary source authoring API.
- Apply Surprised to all seven existing authored affection-maintaining branches plus the new refusal branch. Positive branches and the start of every conversation/greeting use Normal, including at maximum affection.
- Add `affection_1_c` with the user's exact two-part opening and the agreed two choices/responses. Existing words/outcomes remain unchanged; only existing negative face tags change. Pool sizes become 3/6/8/8/8 at affection 0/1/2/3/4.
- Update the existing authoring/validation helpers, NPC candidate list, compiled Ink, six new keys/30 locale values and merchant inventory. Keep the existing Notion reference consistent.
- Protect reward values/assets, authored world merchant/bubble placement, UI prefabs/controllers, scenes, other NPC/boss content, existing GUIDs/serialized names/asmdefs/ProjectSettings and save ownership. No new runtime manager/UI hierarchy.

## Verification

- Stage asset changes in an isolated native Unity workspace while preserving the user's open Editor and real save slots. Copy back only reviewed, whitelisted asset changes with concurrent-edit guards.
- Verify exact attachment hashes, sprite full size/alpha/import settings, stable old GUIDs and local references, Normal/Surprised resolution, unchanged portrait UI geometry and tag-to-portrait mapping for every branch/reset.
- Verify two opening windows before choices, both branch outcomes, cumulative pool/cap/run allowance regressions, all active source/JSON/CSV/five-table strings and previous localization/data preservation.
- Run covered MSBuild and native checks. Report manual Play Mode/Player build separately if unexecuted.
- Doc Impact: SessionLog + existing DialogueNpcAffectionStructure and task routing; update prior scope receipts with a follow-up link. No Architecture/Contracts/Presentation HTML edits.

## Completed Evidence

- Unity 6000.4.2f1 import/compilation and production-assembly validation passed 430/430 checks, exit 0, in an isolated project with an in-memory profile. Actual portrait prefab/tag parsing, both expressions/reset, two opening windows, eight paths and all 245 active translation values were checked. [Native results](../../outputs/sasha-portraits-2026-11-02/validation.txt), [rendered portraits](../../outputs/sasha-portraits-2026-11-02/portraits-native.png).
- Preservation/copyback passed 232 checks. Both PNGs match the supplied bytes; existing normal/library metadata, prior dialogue/localization and 133 protected scene/runtime/reward/placement/settings files remain unchanged. Only 15 reviewed asset/data files were copied with concurrent-edit guards and atomic replacement. [Audit](../../outputs/sasha-portraits-2026-11-02/static-checks.txt).
- Generated Editor.csproj includes all four modified Editor sources; MSBuild passed, exit 0. No runtime source, UI prefab, scene, serialized schema or existing GUID was changed. [Build](../../outputs/sasha-portraits-2026-11-02/msbuild.log).
- The existing Notion merchant page now documents eight candidates, the two-window discount conversation and Normal/Surprised usage. Targeted text updates completed without warnings and preserved unrelated content/table widths. Original image attachment was rejected by automatic approval review because external sharing of the local files lacked explicit approval; no images were uploaded.
- Full manual Play Mode and Player build were not executed. Four foreign-language additions remain draft translations. The user's open Editor and real saves were not operated on. Temporary native-project cleanup and final hash/link checks are recorded in the [session](../SessionLogs/2026-11-02.md).
- Doc Impact: SessionLog + StructureMemory + ErrorLog (memory-mapped Windows asset copy trap), task routing and historical follow-up link. DecisionLog unchanged in this slice; no new ownership decision or Presentation HTML stale candidate.

## Scoped Happy Portrait Follow-up — 2026-11-02

- User approved one additional supplied 1254x1254 expression only for positive responses in `affection_0_a` and `affection_1_c`. The earlier all-positive-Normal rule above is the historical scope; these two branches now use Face/Happy, including capped positive choices. Other branches, text, localization, rewards and UI/world placement remain unchanged.
- Added `merchant_happy.png` with Unity-generated metadata and Face/Happy through the existing source factory; updated exactly two Ink tags/compiled JSON and the existing validation expectations. No new runtime code or schema. Native scoped39/39, preservation/copyback48/48 and generated Editor MSBuild passed. Full manual Play Mode/Player build were not run. Evidence: [focused checks](../../outputs/sasha-happy-2026-11-02/validation.txt), [session](../SessionLogs/2026-11-02.md).
