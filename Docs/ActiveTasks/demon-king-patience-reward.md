# Demon King affection 1: patience reward

Mode: Implementation. Approved 2026-11-05; clarification explicitly selects 15 → 17 minutes and “마왕의 인내심이 2분 늘어납니다.”

Status: implemented. Gameplay MSBuild (including Core) exit 0; source/reference/diff/localization/image-byte inspection completed. Unity import, actual popup/HUD/run transitions/save-reload and Player build remain unexecuted; no automated tests ran.

Goal: keep the authored base at 900 seconds. Saved Demon King (NPC 1005) affection >=1 adds 120 seconds to new-run initial time and inactive HUD preview. Use the supplied PNG as the affection reward icon.

Allowed: existing RunTimeLimitSystem initial-time calculation; existing asset localization key map; new InfoOnlyAffectionEffect asset and supplied icon with new meta identities; DarkLordNpcData level-1 binding; one key in CSV/inventory/shared/five locale tables; narrow task and memory docs.

Preserve: existing saved remaining time on restore/scene transfer, current-run time after earning the reward, time-over/pauses/warning rules, base config 900, existing icon pixels and other rewards. No new manager/service/schema/serialized field, save migration, scene/prefab/bootstrap/UI hierarchy change, or existing GUID replacement.

Success: unearned new runs start at 15 minutes; earned new runs at 17 minutes; inactive preview uses the same calculation; the existing reward popup shows the supplied icon and approved localized summary. Existing saved affection >=1 qualifies without a forced retroactive popup. Current-run progress is committed at the existing run-end boundary.

Verification: source/reference/diff and locale review, source image byte comparison, generated Gameplay project MSBuild (including Core key map). Automated tests, Unity Play Mode, and native runtime acceptance are not authorized and will not run.
