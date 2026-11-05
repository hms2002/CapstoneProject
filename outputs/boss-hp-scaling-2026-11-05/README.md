# Boss HP scaling — 2026-11-05

Applied the approved starting multipliers in the existing boss controllers.

- First normal boss stage: x2.
- Second normal boss stage: x2.5.
- Third normal boss stage: x3.
- Demon King: x5 (2800 -> 14000 with the current source profile).

The runtime multiplier follows encounter order. Profiles remain baseline data. Slime Queen P2 bodies scale independently. DarkLord_Tutorial keeps its authored HP; no active route defaults normal bosses to first-stage scaling. Only Health and MaxHealth change.

Changed source files:
- [BossControllerBase.cs](../../Assets/_Project/Runtime/Features/Bosses/Common/FSM/Core/BossControllerBase.cs): apply one Start-time stage multiplier to both HP values, maximum first.
- [DemonKingController.cs](../../Assets/_Project/Runtime/Features/Bosses/DemonKing/DemonKingController.cs): resolve final-boss multiplier to x5.
- [Current structure map](../../Docs/StructureMemory/ScriptSystems/BossAndMobEncounterStructure.md#boss-encounter-hp-multipliers-2026-11-05): ownership, current values, initialization and manual verification limits.

[Gameplay MSBuild log](msbuild.log): exit 0. Source/reference/diff checks performed. No automated tests, Unity import/Editor compilation, Play Mode or Player build were run. Balance and pattern exposure still require manual gameplay.
