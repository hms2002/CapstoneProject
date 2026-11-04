# Approved settings / key mapping integration

The approved clean title, upper chains and two-column single-page layout are applied to main GlobalUIRoot, TitleScene and the shared KeyMapRow. Settings uses eight controls; keys use 19 authored rows. Aseprite source and PNG skin are in [SettingsSkin](../../../Assets/_Project/Art/Sprites/UI/SettingsSkin/README.md).

## Evidence

- [Play Mode presenter validation](validation.txt): **1,451 checks passed**, both source hierarchies, five locales, text bounds, authored row reuse/order, hit targets, upper-chain interaction unlock, key capture/conflict/discard and repeated close/reopen. The title fixture uses the `TitleScene` name to exercise the title-local presenter branch.
- [Serialized scope validation](scope-validation.txt): existing IDs, metas, fonts/materials and unrelated blocks preserved against before-task snapshots. Global214 / Title331 / Row33 existing scoped blocks changed; no original object removed.
- [Existing render-layer regression suite](render-layer-tests.xml): **3/3 passed**, projection under transform changes, visibility/lifecycle ownership, existing click/navigation references.
- Unity native compilation passed. Final `MSBuild Editor.csproj /t:Build /p:Configuration=Debug` exited **0**, including UI, authoring and validation sources. An earlier concurrent run lost `Temp/bin/Debug/Core.dll` while Unity entered Play Mode; the final build ran after Unity exited.
- Native Aseprite re-export is byte-identical to the production PNG: SHA256 `8842902329fc45fd8cb2ff6ebc4c725852ec8ada7bf2dc53bcecd3351b3421f3`.
- Twelve new localization keys have all sixty values in the source CSV and native tables. Structure/art Markdown links and scoped whitespace were checked.

## Native captures

These are actual Unity GPU renders of the saved assets and presenters in disposable fixtures, not browser mockups. Full captures and native execution logs remain under `Library/CodexSettingsSkin`.

- [Settings, Korean](global-settings-ko.png)
- [Keys, Korean](global-keys-ko.png)
- [Keys, English](global-keys-en.png)
- [Keys at 1280×720](global-keys-ko-1280.png)
- [Title settings](title-settings-ko.png)
- [Title keys](title-keys-ko.png)

## Limits

No player build, full title/pause menu manual walkthrough, physical keyboard input or display-mode transition was performed. Resolution captures use a 1920×1080 reference Canvas rendered at 1280×720, 1920×1080 and 2560×1440; they do not certify other aspect ratios. The fixture invokes the existing button/rebind/confirmation paths without saving the user's bindings or display settings. Forced locale transitions emitted transient missing-glyph warnings before localized fonts finished applying; stable five-locale captures and text bounds passed. Existing independent legacy UI copies are outside this migration and retain dynamic-row compatibility.

## In-place navigation follow-up

Settings and key settings now replace one another immediately. The initial settings drop and final settings close remain animated. An authored CanvasGroup on the settings root hides its complete presentation, including upper chains, while retaining its logical stack entry and unapplied display selections. Key input/conflict/discard behavior and saved preferences are unchanged. No art or geometry changed.

- [Native Play Mode validation](navigation-validation.txt): **1,548 checks passed**, including immediate entry/return before any settling helper, five locales/both sources, title-local ownership, two UIManager stack round trips, original settings opening, pending display choices and time-lock release. The suite also refreshed 17 GPU captures under `Library/CodexSettingsSkin/Captures`; the main Korean key view was visually inspected.
- [Follow-up serialized scope check](navigation-scope-validation.txt): each source changed only the settings root component list and presenter visibility reference, and gained one CanvasGroup. All other existing blocks, geometry, art and IDs match the follow-up snapshot.
- Native compilation/import and final `Editor.csproj` MSBuild passed (exit0). The previous integration's three render-layer tests were not rerun for this navigation-only change. Full-scene manual walkthrough and Player build remain unexecuted.
- Execution log: `Library/CodexSettingsNavigation/validation3.log`; build log: `Library/CodexSettingsNavigation/msbuild.log`. A native authoring retry resolved Unity's fake-null component check and a Windows mapped-file truncation failure; the final writer stages and atomically replaces the scoped output.
