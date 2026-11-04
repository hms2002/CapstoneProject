# Player stats, potion HUD and shared reward skin

Approved on 2026-10-31. These are the original pixel sheets reviewed in `player-stats-potion-ui-v3` and `reward-popup-ui-v1`; `Source/` contains the corresponding editable Aseprite masters. Text, potion icons and reward icons remain separate, existing game data.

| Sheet | Named sprites | Import / borders |
| --- | --- | --- |
| PlayerStatsPotionSkin.png, 96×96 | stat_panel, stat_row, stat_divider, potion_slot, key_badge | Point, PPU100, uncompressed, no mipmaps. Panel/slot borders2; badge1. |
| RewardPopupSkin.png, 256×128 | reward_panel, reward_crest, reward_icon, reward_button, reward_divider | Same import settings. Panel borders32, icon12, button6; crest/divider simple. |

The stats/potion artwork uses four opaque colors; reward artwork uses six. Transparent areas use alpha0. Existing icon/font colors are not part of these palettes. Preserve sheet dimensions and named sprite IDs when re-exporting.

## Authored views

- `Prefabs/UI/PopupUI/PlayerStatUI/`: panel, section and row prefabs shared by inventory and chest. The default panel is390×742, with342×38 rows and6px gaps. Basic contains HP, attack, movement, attack speed, critical chance, critical damage and evasion; Other contains fire, bleed, poison, electric, skill and normal attack damage. Attack speed uses Percent×100 with0 decimals. Existing calculations and green/red acquisition deltas remain in the presenters. Current definitions have no icons; the existing empty icon child stays bound but inactive in this text-only design.
- Main `Prefabs/UI/GlobalUIRoot.prefab`, `GameplayHUDCanvas/LeftUpperUIGroup/PlayerConsumableHUD`: four existing50×50 slots, centered36×36 icon,16×14 key badge at bottom-right above the icon. Existing HUD placement and inventory behavior remain intact.
- Main `GlobalUIRoot`, `RewardCanvas/RewardPanel/RewardUI`: decorated card, title, divider, reward slots, description and close button. The default card is564×420 and grows with longer descriptions/multiple rewards. `EffectSlotUI` and `UnlockSlotUI` share96×96 frames. Existing title/body/button objects were reparented with their references retained.

`Editor/Tools/UI/PlayerStatsAndRewardUiAuthoring.cs` applies these assets through native prefab authoring. Neither approved GlobalUIRoot subtree is a nested prefab instance; the tool retains the disk serialization of all unrelated nested instances as well as existing font/material references. The original RewardUI animation owns the composed card; child animations retain callbacks without an additional slide/fade.

`Editor/Tools/Validation/ApprovedUiSkinValidation.cs` runs disposable Play Mode fixtures, actual presenter binding and native GPU captures for five locales. It tests stat refresh, potion consumption, shared reward opening/closing and multiple/unlock layouts. It does not grant rewards or save scenes. Run `ApprovedUiSkinValidation.Run` with `-batchmode -uiBatchValidationTests` (no `-quit`); reports/captures go to `Library/CodexApprovedUiSkin/Captures`.

For the stats-only revision, `PlayerStatsAndRewardUiAuthoring.ApplyStatRevision` updates the existing section/attack-speed definitions, localized Other title and row prefab without reauthoring the potion/reward views. Keep the matching title row in `DataSheets/Localization/GameText.csv` synchronized. `ApprovedUiSkinValidation.AuthorStatRevisionAndRun` authors then validates; `RunStatRevision` validates already-saved assets. Use the same batch flags above; output is `Library/CodexStatRevision/Captures`. Its temporary relic fixture exercises real detached projection, transfer acquisition, cancellation and removal, and captures base/delta views in five locales. It never saves the illustrative relic or player state.

See `Docs/ActiveTasks/player-stats-potion-reward-skin.md` and `Docs/SessionLogs/2026-10-31.md` for scope and recorded verification. Full game scenes and legacy independent GlobalUIRoot copies require their own visual acceptance if later included.
