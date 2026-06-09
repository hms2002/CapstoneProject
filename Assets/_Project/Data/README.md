# Data Folder Guide

Data contains runtime-read `.asset` instances only.

## Hard Rule

Do not place `.cs` files here.

Examples:

- `AbilityDefinition.cs` belongs in `Runtime/Core/Abilities`.
- `AD_SwordAttack.asset` belongs in `Data/Abilities/Definitions`.
- `AbilityLogic_SwordCombo.cs` belongs in `Runtime/Features/Items/Weapons`.
- `AL_SwordCombo.asset` belongs in `Data/Abilities/Strategies` or `Data/Items/Weapons/Strategies`.

## Folder Intent

- `Definitions`: identity/config assets such as AD, WD, RD, attribute definitions.
- `Strategies`: ScriptableObject strategy instances that execute or choose behavior.
- `LogicData`: passive data consumed by concrete runtime logic.
- `Catalogs`: lookup tables and UI/database catalogs.
- `LoadingManifests`: explicit load/prewarm/release data.

If an asset is loaded by string path, `Resources`, Addressables address, or a custom registry,
do not move it until that reference is updated in the same migration.
