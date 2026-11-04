# Settings and key mapping skin

Approved clean-title reference, 2026-10-31. Flat dark violet panels/rows, thin frames, cyan title and gold section headings; upper chains reuse existing artwork.

- `SettingsSkin.png`: 256×64 RGBA pixel sheet, nine named regions. Panel/Row/Button/Key use five-pixel slice borders; Handle uses three. Track/Fill/Divider and Reset provide the remaining control art. Point filtering, no mipmaps, uncompressed, 100 PPU, FullRect.
- `Source/SettingsSkin.aseprite`: editable layer-per-region source saved by Aseprite.
- [make_settings_skin.lua](../../../../../../Tools/Art/make_settings_skin.lua): reproducible Aseprite script. Pass an existing `outputDir`; it writes the Aseprite source and PNG there. Place the PNG beside this README after generation.
- [SettingsSkinAuthoring.cs](../../../../Editor/Tools/UI/SettingsSkinAuthoring.cs): native importer/slicing and approved main-prefab/title-scene authoring. Existing shared KeyMapRow IDs are retained. Do not rerun against unrelated manual edits without reviewing its scope.
- [SettingsSkinValidation.cs](../../../../Editor/Tools/Validation/SettingsSkinValidation.cs): real-presenter Play Mode validation; invoke `SettingsSkinValidation.Run` with `-batchmode -uiBatchValidationTests`, without `-quit`. Evidence is written to `Library/CodexSettingsSkin/Captures`.

Runtime creates no skin texture or authored page hierarchy. Settings values and key bindings remain with the existing services/presenters.
