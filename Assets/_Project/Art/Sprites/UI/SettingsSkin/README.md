# Settings and key mapping skin

Approved clean-title reference, 2026-10-31. Flat dark violet panels/rows, thin frames, cyan title and gold section headings; upper chains reuse existing artwork.

- `SettingsSkin.png`: 256×64 RGBA pixel sheet, nine named regions. Panel/Row/Button/Key use five-pixel slice borders; Handle uses three. Track/Fill/Divider and Reset provide the remaining control art. Reset was revised on 2026-11-02 to a 32x32 circular arrow at source X184/Y32 (Unity rect X184/Y0); its sprite identity is retained. Point filtering, no mipmaps, uncompressed, 100 PPU, FullRect.
- `Source/SettingsSkin.aseprite`: editable layer-per-region source saved by Aseprite.
- [Original generator archive](../../../../../../Tools/Archive/2026-11-01-one-off/Tools/Art/make_settings_skin.lua.txt): historical 12x12 Reset source; the revised editable Aseprite file above is the current icon source. Rerunning the archive would restore the old icon.
- [Original integration authoring archive](../../../../../../Tools/Archive/2026-11-01-one-off/Assets/_Project/Editor/Tools/UI/SettingsSkinAuthoring.cs.txt): historical native slicing/layout authoring, not an active menu.
- [Current revision record](../../../../../../outputs/ui-gameplay-fixes-2026-11-02/README.md): icon scope, input correction, compilation and manual acceptance limits.

Runtime creates no skin texture or authored page hierarchy. Settings values and key bindings remain with the existing services/presenters.
