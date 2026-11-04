# Encyclopedia item layout and paper illustration viewer

- Mode: Implementation, explicitly requested by the user on 2026-11-01.
- Goal: align Weapon/Relic pages to the revised book, move the description shortcut clear of the title, replace the dark full-view panel with a paper page and working portrait arrows, and show one centered message for undiscovered details.
- Scope: the existing encyclopedia prefab, its display presenters, editor authoring, three localized messages and focused validation. Discovery/save ownership, original illustrations/book art, gameplay, scenes, packages and input definitions are unchanged by this task.
- Design: a fixed description hint at the bottom of the right page; the scrolling detail ends above it. The illustration viewer reuses the existing pixel-paper sprite and keeps uncropped original proportions. Portrait arrows share the existing selection, including first/last boundaries and close/reopen behavior.

## Changed files and purposes

| File | Purpose |
| --- | --- |
| `Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab` | Item title/grid/pagination alignment, transparent detail background, authored page wrapper/footer/empty text, paper viewer and arrow references |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs` | Hide the entire undiscovered detail and show its localized message; keep the wrapper as the tab visibility boundary |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureDetailView.cs` | Centered discovery messages and full-view arrow/title/page projection, sharing the existing portrait state and listener cleanup |
| `Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureTab.cs` | Select Monster/Boss message without sending a silhouette to the right page |
| `Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaLayoutAuthoring.cs` (+ Unity-generated metadata) | Reproducible native prefab authoring; original font/material preservation |
| `Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaCreatureLocalizationAuthoring.cs` | Three five-language messages and task-local, atomic report output |
| `DataSheets/Localization/GameText.csv`, `Assets/_Project/Data/Localization/GameText Shared Data.asset`, `GameText_{ko,en,ja,zh-Hans,zh-Hant}.asset` | Keys/translations for item not acquired, monster not encountered and boss not encountered |
| `Assets/_Project/Tests/PlayMode/Procedural/EncyclopediaCreaturePlayModeTests.cs` | Real presenter layout captures, single-message states, portrait boundary/synchronization/input-lock regressions |
| `outputs/encyclopedia-implementation-2026-10-11/creature-localization-report.txt` | Existing importer refreshed this generated receipt on its initial invocation; subsequent runs write task-local evidence |
| `verify_scope.py` | Compare original prefab IDs/fonts/unrelated records, CSV rows and six protected saves against the immediate task baseline |
| `Docs/StructureMemory/EncyclopediaCreatureTabs.md`, `Docs/StructureMemory/ScriptSystems/EncyclopediaStructure.md` | Current authored wrapper, unknown-state and full-view ownership map |
| `Docs/TaskIndex.md` | Mark the previously reported encyclopedia hint overlap as resolved, retaining the independent full-flow review |
| `Docs/SessionLogs/2026-11-01.md` | Record this implementation, verification and remaining limits |
| `Docs/ErrorLog.md` | Existing native authoring/Windows mapped-file trap recurrence and cache-reference preservation |

## Validation

- Unity 6000.4.2f1 native authoring and compilation succeeded. The latest Editor and ProceduralPlayModeTests MSBuild commands both exit 0; the generated Editor project includes the new authoring source.
- [Scope comparison](scope-report.json) passes: all 2,770 original prefab records retained, 49 authored records added, zero unrelated record changes, original font/material references preserved, exactly three new CSV keys with five translations each, every previous CSV row retained, six original save hashes unchanged.
- Initial PlayMode run: 29/30 passed. The new five-language visual test found a Chinese item-title height overflow. After sizing correction, the [full final PlayMode run](playmode-final-results.xml) passed **30/30** (73.32 seconds). It covers discovery/reveal/tab behavior, seven-image viewer navigation and the five-language native layout fixture. All 31 native captures were generated. After the final horizontal title-fitter cleanup, the [focused five-language native rerun](playmode-handoff-results.xml) also passed **1/1** (26.92 seconds). Final authoring and test processes both exited 0; Korean weapon/relic and both Chinese title captures were visually reviewed. The title width now has one owner: the parent layout.
- Native save callbacks populated two unrelated font caches; explicit non-nested cache restoration preserves those. Separately, `SaveAsPrefabAsset` normalizes three pre-existing unresolved nested material references to null even when nested bindings are excluded. The original nested record was restored byte-for-byte after verifying those three references were its only changes, including after the final authoring pass. The final prefab passes preservation. [Recovery receipt](native-cache-recovery.json). Future authoring reruns require the same scope check; resolving those legacy nested references is Suggested Later, outside this layout task.

## Authoring and ownership

Apply `EncyclopediaLayoutAuthoring.Apply` after the legacy creature builder and approved skin authoring. `ItemPageContent` contains the original `ItemDetailPanel` ScrollRect, the fixed description hint and `UndiscoveredItem`. Runtime code only toggles/binds these authored objects. Unacquired item and unencountered creature silhouettes remain on the left list; the right page contains a single category-specific sentence.

The paper viewer owns no separate portrait index. `EncyclopediaCreatureDetailView` updates both portrait images, the list selection portrait and the full-view controls together. Escape closes the viewer first; tab/entry changes and disable retain the existing cleanup flow.

## Limits

Full hub-to-combat manual acceptance and a Player build are separate from the native UI fixtures. NPC/Consumable discovery policy and historical acquisition tracking are unchanged. No Architecture/Contracts or Presentation HTML change is requested.

The existing simplified regression fixtures produce TMP missing-glyph warnings while their native font bindings are intentionally absent; the five-language visual fixture retains and waits for the real bindings. This is not a clean-Console claim. No production error suppression was added. Native evidence uses the existing `-uiBatchValidationTests` trace-file isolation flag.

## Reviewed screenshots

- [Weapon / fixed footer](native/weapon-ko.png), [detailed description at scroll bottom](native/weapon-detailed-bottom-ko.png), [Relic / no empty story frame](native/relic-ko.png).
- [Paper illustration viewer](native/boss-full-ko.png), [unacquired item](native/item-unknown-ko.png), [unencountered monster](native/monster-locked.png), [unencountered boss](native/boss-locked.png).
- [Chinese title layout](native/weapon-zh-Hans.png). The native fixture also covers English, Japanese and Traditional Chinese with real localized font bindings.

Source and Markdown whitespace checks passed; Unity-generated prefab empty-value trailing spaces are retained as emitted by the native serializer. Documentation links and target paths were verified.

## Doc impact

SessionLog, existing StructureMemory and ErrorLog are updated, with the TaskIndex overlap status corrected. DecisionLog is unchanged. No Architecture/Contracts promotion or Presentation HTML stale candidate was found.
