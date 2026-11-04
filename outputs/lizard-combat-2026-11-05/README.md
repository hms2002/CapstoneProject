# Lizard combat initial balance — 2026-11-05

Applied to the existing runtime scripts and authored monster assets.

| Enemy | Initial balance |
| --- | --- |
| Warrior | Range 2.2; warning 0.8s with slow approach at 0.75x player base speed; frenzy 2.2s at 1.3x; turn cap 270 degrees/s; one contact attempt; stationary recovery >=1.1s; additional rearm 1s. |
| Mage | Moving warning 1.2s, fixed direction for final 0.2s; one shot; moving shot rest 1.5s; roughly 3.2s baseline cadence; retreat below 3 until 4, approach above 5, otherwise strafe; movement 0.5x/0.25x player base speed. |
| Mage projectile | 1.3x player base speed; homing 0.8s at 75 degrees/s, +/-60-degree cone; permanently straight after release; total life 2.6s. |

Evidence:
- [Unity native compile/import log](unity-compile.log): Unity 6000.4.2f1 exit 0; no Play Mode or tests.
- [MSBuild log](msbuild.log): Editor.csproj build exit 0, including Gameplay and Editor changes.
- [Session log and changed-file purposes](../../Docs/SessionLogs/2026-11-05.md).
- [Current structure map](../../Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#lizard-frenzy-and-moving-homing-shot-2026-11-05).

Compilation does not establish combat balance. Manual mixed-enemy combat, moving-cast readability, dash/contact behavior, low-frame-rate acceptance and Player build remain unexecuted. Existing LizardMage burst-budget regressions need separately authorized updates.

## Manual-play feedback tuning

- Warrior range: 2.2 -> 3.74 (+70%); frenzy speed: 1.3x -> 1.6x player base speed.
- Mage projectile: 2x XY sprite/collider size in PF_LizardMageProjectile.prefab; speed 1.3x -> 1.95x player base speed (+50%). Utility child coverage is preserved.
- The table above retains the initial balance as history. Current values are in the AL strategy assets.
- [Tuning MSBuild log](msbuild-tuning.log): Gameplay.csproj exit 0.
- [Unity tuning import attempt](unity-tuning-compile.log): exit 1 after project path selection; native import success is not established for this revision.
- No automated tests or Play Mode were run. The user playtested the initial version; the new tuning still needs replay.
