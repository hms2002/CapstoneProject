# Prefabs Folder Guide

Prefabs are grouped by gameplay ownership and usage.

Use:

- `Player`
- `NPCs`
- `Monsters`
- `Bosses`
- `Items`
- `UI`
- `Map`
- `VFX`

Do not use a sibling `Characters` folder next to `Monsters` and `Bosses`, because monsters
and bosses are also characters. Use `Player` and `NPCs` explicitly.

When moving prefabs, keep their `.meta` files with them so Unity GUID references survive.
