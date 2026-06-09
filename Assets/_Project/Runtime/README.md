# Runtime Folder Guide

Runtime contains project-owned C# code that ships in builds.

## Layout

- `Core`: foundational gameplay rules, contracts, and reusable systems.
- `Infrastructure`: Unity/external API adapters, global services, IO, loading, save, audio, input, pooling, rendering, and scene flow.
- `Features`: concrete gameplay use cases such as bosses, monsters, items, map gimmicks, progression, tutorial, and cheats.
- `UI`: Canvas-based UI screens and HUD view logic.

## Placement Examples

- `AbilitySystem.cs` -> `Core/Abilities`
- `AttributeSet.cs` -> `Core/Attributes`
- `CombatHitPayload.cs` -> `Core/Combat`
- `AddressableAssetProvider.cs` -> `Infrastructure/Addressables`
- `InputBindingService.cs` -> `Infrastructure/Input`
- `SlimeQueenP2Short.cs` -> `Features/Bosses/SlimeQueen`
- `DrainPipe.cs` -> `Features/Map/Trap` or `Features/Map/Shortcut`, depending on final ownership.
- `InventoryScreen.cs` -> `UI/Inventory`

Feature code should not be moved to `Core` just because multiple files call it.
