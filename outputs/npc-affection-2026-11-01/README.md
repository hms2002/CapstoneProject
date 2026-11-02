# NPC Affection Verification — 2026-11-01

- [Native checks](validation.txt): 226 passed using production assemblies, imported merchant content and in-memory saves.
- [Unity final validation](unity-final-validation.log): batch process exit 0.
- [MSBuild](msbuild-final.log): final generated Editor project build passed (exit 0), run after Unity exits. Existing project warnings remain.
- [Read-only audit](static-checks.txt), reproduced with [audit.py](audit.py): all 190 active merchant locale values, preserved existing prefab/table state, supplied PNG hash and local documentation links.
- [Frame 0](merchant-bubble-0.png), [Frame 1](merchant-bubble-1.png), [Frame 2](merchant-bubble-2.png): actual merchant and supplied sheet rendered through Unity. Existing nameplate canvases and other fixture NPCs are hidden in these isolated previews.

No full manual gameplay or Player build was performed. See the [session log](../../Docs/SessionLogs/2026-11-01.md) for scope, changed files, verification limits and authoring instructions.
