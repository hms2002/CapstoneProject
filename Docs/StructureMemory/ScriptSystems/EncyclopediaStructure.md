---
status: active
authority: structure-memory
category: script-system-map
last_reviewed: 2026-05-22
---

# Encyclopedia Structure

## Purpose

Map the first encyclopedia implementation so future content, prefab, and discovery-save work can start without rediscovering the UI/data boundary.

This is a fast structure map, not a final Architecture or Contract document.

## Current Structure

- `ItemDatabase` is the direct source for the current Item tab. Weapon, Relic, and Consumable sub-tabs read their existing item definition lists directly. `EncyclopediaCatalogSO` is not used by the current Item-tab path; `EncyclopediaScreen.SetCatalog(...)` remains only as compatibility for older interactable wiring until Monster/Boss get their dedicated provider/source shape.
- `EncyclopediaScreen` is the popup-stack screen and implements `IStackableUI`. It owns open/close policy, main-tab entry, and Tome/book presentation sequencing. Its active boundary is `screenActiveRoot`, which should resolve to the authored `EncyclopediaUI` popup root rather than the child `Book` object. It no longer owns Item sub-tab/page/selection/grid/detail state or direct Item data references. The current authoring slice enables the Item tab first and keeps Monster/Boss behind serialized disabled main-tab toggles.
- `EncyclopediaItemTab` owns Item-tab runtime state: `Weapon`/`Relic`/`Consumable` sub-tab, zero-based page index, selected entry index, and item data binding from `ItemDatabase`.
- `EncyclopediaItemLeftPage` is the authored Item LeftPage presenter for title text/icon, item sub-tab buttons, pagination buttons/text, entry count, and list notices. It relays sub-tab/page requests to `EncyclopediaItemTab` instead of owning selection state.
- `EncyclopediaEntryGridView` is the authored grid presenter for the slot pool. It owns `entryGridRoot`, `entrySlotPrefab`, `slotsPerPage`, and per-page slot binding, while `EncyclopediaItemTab` decides which page and entry are selected.
- `EncyclopediaItemRightPage` is the authored Item RightPage presenter. It binds common `Icon` / `Name` / shared `StoryText` body fields plus type-specific section roots under the authored layout. Weapon entries show story, stat text, and pooled `Panel_AbilityBlock_Encyclopedia.prefab` / `WeaponAbilityBlockView` instances under `AbilityContainer` for Skill1/Skill2 rows. Relic entries use the shared `StoryText` body for formatted effect text built from `RelicLogic.BuildTooltip(...)`, not `RelicDefinition.description`, then show preview level text. Consumable entries show only the common name and shared `StoryText` description. Weapon stat root and Relic level-preview root may be the same authored object; Weapon mode hides preview guide icons, while Relic mode keeps Prev/Next guides visible with disabled alpha when movement is not possible. Each normal weapon skill gets its own block instance; the switch guide appears only for a skill whose source object exposes multiple tooltip variants through `IAbilityTooltipVariantProvider`.
- `EncyclopediaLeftPageView` remains an obsolete migration fallback only and is hidden from Add Component. `EncyclopediaDetailPanel` is no longer part of the active Item layout; the GlobalUIRoot wiring tool removes legacy LeftPage and RightPage presenters so `EncyclopediaItemLeftPage` and `EncyclopediaItemRightPage` are the only active item page presenters.
- `EncyclopediaEntryButton` is an authored slot presenter. The preferred runtime path instantiates/reuses `EncyclopediaEntrySlot.prefab` under an authored grid root; serialized `entrySlots` remain a migration fallback only.
- The encyclopedia popup requests `MouseCursorDomain.Encyclopedia` while open. The domain is authored on `MouseCursorTheme` with four cursor policy fields: default, default pressed, item slot hover, and item slot hover pressed. `EncyclopediaEntryButton` marks the cursor interactable only for valid item categories (`Weapon`, `Relic`, `Consumable`) while the pointer is over a populated slot.
- `BookPixelRevealPresentation` drives a single overlay `Graphic` with the `UI/Book Pixel Reveal Overlay` shader.
- `EncyclopediaBookPresentation` requests authored UI book `Animator` states for book open/close/page turns, and drives content transitions through an authored `PageCover` image plus `ContentAppear` clip. Content appear samples the clip forward; content disappear samples the same clip backward. It supports either `CanvasGroup` or plain `Graphic/Image` DimPanel fade, treats DimPanel as part of the active lifecycle by turning it on during fade/open and off when transparent/closed/disabled, activates its own presentation object before open/page-turn playback when needed, and prefers Book/Tome/EarthTome named Animator objects or controllers containing `BookOpen`, `BookClose`, `BookLeftPage`, or `BookRightPage` states/clips.
- `BookWorldSpriteSequencePresentation` keeps its legacy serialized class name, but it now requests EarthTome world-book `Animator` states for closed/opened idle and open/close presentation.
- `EncyclopediaInteractable` opens the screen from a hub/world interactable.
- `EncyclopediaBookAssetPostprocessor` keeps encyclopedia book PNG imports on point-filtered Sprite settings. `EncyclopediaV1AssetBuilder` applies EarthTome 48x40 slicing before it wires generated prefabs.
- `EncyclopediaV1AssetBuilder` is editor-only support for generating the temporary catalog, popup prefab, and stand prefab. It is not the current `GlobalUIRoot` repair path and should not add legacy `EncyclopediaLeftPageView` or `EncyclopediaDetailPanel` presenters to new generated item UI.

## Key Files

- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaCatalogSO.cs`
- `Assets/LeeJunMo/Script/Looting/ItemDatabase.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaScreen.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaItemTab.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaItemLeftPage.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaItemRightPage.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaLeftPageView.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaEntryGridView.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaDetailPanel.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaEntryButton.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/BookPixelRevealPresentation.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaBookPresentation.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/BookWorldSpriteSequencePresentation.cs`
- `Assets/LeeJunMo/Script/Encyclopedia/EncyclopediaInteractable.cs`
- `Assets/Shader/UIBookPixelRevealOverlay.shader`
- `Assets/Editor/EncyclopediaBookAssetPostprocessor.cs`
- `Assets/Editor/EncyclopediaV1AssetBuilder.cs`
- `Assets/Editor/EncyclopediaExistingGlobalUIRootWireTool.cs`
- `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab`
- `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab`
- `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaScreen.prefab`
- `Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/`
- `Assets/Sprites/UI/Encyclopedia/EarthTome/`

## Runtime Flow

- The hub object uses `EncyclopediaInteractable.CanInteract(...)` to require player `Idle` state, an openable data source on the screen, and `UIManager.CanOpenUI(screen)`.
- On highlight, the interactable can play the field book open sequence. On unhighlight, it can play the close sequence. This is presentation-only and does not own interaction state.
- On interaction, the interactable applies assigned data sources such as `ItemDatabase` or legacy `EncyclopediaCatalogSO`, hides the world prompt, and opens the screen through `UIManager.TryPushUI(screen)`.
- The screen opens as `ExclusiveModal`, blocks other exclusive modal UIs, freezes time, blocks player control through the UI stack lock profile, and allows ESC close.
- First open resets the main tab to `Item`, then plays `DimPanel` fade-in, book motion-root drop, and the UI book `BookOpen` Animator state. The DimPanel may be authored as a `CanvasGroup` root or as a single `Graphic/Image`; both paths are driven by `EncyclopediaBookPresentation`. After the open presentation finishes, `EncyclopediaScreen` calls `EncyclopediaItemTab.ShowDefault()`, which selects `Weapon`, page index `0` displayed as page `1`, and the first visible weapon entry when one exists.
- LeftPage item sub-tab buttons switch `Weapon`, `Relic`, and `Consumable` through `EncyclopediaItemTab`. `EncyclopediaBookPresentation` plays content disappear by reverse-sampling `ContentAppear`, hides content, requests the `BookLeftPage` Animator state, swaps the list/title/detail for the new sub-tab, then plays content appear. The Item tab does not pre-hide content before asking presentation to transition.
- RightPage main-tab buttons route through the `BookRightPage` Animator state for future expansion. In the current slice, only the Item tab binds data; Monster/Boss buttons should remain disabled/no-op until their pages are authored.
- When an `EncyclopediaEntryGridView` is assigned through `EncyclopediaItemLeftPage`, it deactivates serialized migration slots as needed, instantiates the authored slot prefab pool under the authored grid root, and reuses that pool for page rebuilds. It does not search the runtime hierarchy for missing fallback slots during population.
- Item selection does not animate; it only refreshes the right-side detail through `EncyclopediaItemRightPage`. The presenter clears type-specific sections before rebinding so previous Weapon/Relic/Consumable sections do not remain visible after selection changes. Weapon ability rows are created from the encyclopedia ability-block prefab pool and display Skill1/Skill2 only. Relic Q/E preview changes the encyclopedia preview level from `1..maxLevel` and refreshes the formatted `RelicLogic.BuildTooltip(...)` effect text without using inventory-owned relic levels; `1 / 1` relics still show disabled Prev/Next guide objects.
- Book open and Item sub-tab/page swaps should bind while page content is active for layout but hidden by the book content `CanvasGroup`, then force LeftPage/RightPage layout and scroll position before reveal. `EncyclopediaItemTab` tracks the selected item object and skips right-page rebinding when the same entry is clicked again, so repeated selection does not restart detail layout or scroll reset.
- Monster/Boss category buttons should remain hidden/disabled for the current item-first slice unless their serialized category toggles are intentionally enabled for follow-up work.
- The left-page `TitleGroup` is authored in Unity. Wire title text/icon through `EncyclopediaItemLeftPage`; direct screen title references are no longer part of the primary structure.
- Close requests are handled through `ICloseRequestHandler` so the optional UI book close/root close presentation can finish before the screen is popped from `UIManager`.
- If `EncyclopediaScreen.closeOnRuntimeAwake` is enabled and the authored UI root starts active, the screen snaps book/root presentation closed and deactivates `screenActiveRoot` during runtime `Awake()` without playing open/close animation. This keeps Play mode from opening the encyclopedia just because the layout was left active for authoring, and prevents sibling objects such as `DimPanel` from remaining visible.

## Data Ownership

- Weapon entries come from `ItemDatabase.allWeapons` and display existing `WeaponDefinition` name, icon, story, stat modifiers, and ability definitions. If no item database is assigned to `EncyclopediaItemTab`, the current Item page has no source data.
- Relic entries come from `ItemDatabase.allRelics` and bind existing relic display/icon data plus per-level effect text from `RelicLogic.BuildTooltip(relic, previewLevel, context)`. The current encyclopedia Relic body does not display `RelicDefinition.description`.
- Consumable entries come from `ItemDatabase.allConsumables` and bind existing consumable display/icon/description data only. Restore amount and target attribute are not shown as encyclopedia metadata in the current Item RightPage.
- Monster entries own encyclopedia-facing `id`, `displayName`, `image`, `type`, `attackStyle`, `stageText`, `storyText`, and connected source prefab.
- Boss entries own the same encyclopedia-facing fields plus optional `NPCData`.
- Boss affection display reads current state from `AffectionManager.GetAffection(npcId)` and reward rows from `NPCData.affectionRewards`.
- v1 treats all entries as visible. Discovery and save ownership are intentionally deferred.

## Authoring And Editor Support

- Runtime scripts expect scene/prefab-authored references. They do not create canvases, buttons, TMP text, images, presenter components, or arbitrary hierarchy at runtime.
- Layout-facing references should be grouped by authored tab/page component: `EncyclopediaItemTab` for Item state/data, `EncyclopediaItemLeftPage` for Item LeftPage controls, `EncyclopediaEntryGridView` for the slot grid, and `EncyclopediaItemRightPage` for Item RightPage details. `EncyclopediaScreen` should keep only stack, main-tab, and presentation references.
- The encyclopedia presenter components include explicit edit-mode auto-wiring helpers. `Reset()` and the `Auto Wire References` context menu resolve obvious child names such as `LeftPage`, `RightPage`, `EntryGridRoot`, `TitleGroup`, `PageButtonGroup`, `ItemDetailPanel`, `Icon`, and `StoryText`. `OnValidate()` is not used for reference discovery, component adding, or `SetDirty`, and runtime `Awake`/`OpenUI` does not perform broad self-wiring. Missing required runtime references are logged as authoring warnings instead.
- The encyclopedia list is one approved runtime-instantiated UI path: `EncyclopediaEntryGridView` may instantiate authored `EncyclopediaEntrySlot.prefab` instances under `entryGridRoot`.
- Weapon ability rows are the other approved runtime-instantiated UI path: `EncyclopediaItemRightPage` may instantiate/pool authored `Panel_AbilityBlock_Encyclopedia.prefab` instances under `AbilityContainer`. `AbilityContainer` should behave as a vertical list with child height control enabled; otherwise zero-height ability block roots can overlap and make Skill1/Skill2 look like they are inside one panel.
- Variant-capable weapon skills may temporarily use an extra pooled ability block as the external switch preview. Before any ability block is hidden or reused as a normal row, `WeaponAbilityBlockView.ResetPooledPresentationState()` must restore preview mute, `CanvasGroup` alpha/raycast flags, switch guide state, and `LayoutElement.ignoreLayout = false`.
- Current Item RightPage detail UI should use only `EncyclopediaItemRightPage`, not `ItemDetailPanel`, `EncyclopediaDetailPanel`, `WeaponDetailViewV2`, `RelicDetailView`, or `ConsumableDetailView` directly. `ItemDetailPanel` is inventory hover UI and re-parents itself to the hover canvas during `Awake()`. `ItemDetailPanel` under the encyclopedia RightPage should be treated as a layout/ScrollRect/content host, not a presenter owner. The encyclopedia presenter only mirrors the needed data: weapon story/stats/skills, relic preview/effect, and consumable description. Relic effect text uses `RelicLogic.BuildTooltip(...)` as the body source and Consumable descriptions use the shared `StoryText` field; do not revive separate relic/consumable description fallback text fields unless the layout is explicitly split again. Boss detail should not wire affection/reward UI through this panel; it should use a later boss-specific presenter.
- `EncyclopediaScreen.screenActiveRoot` should point to `EncyclopediaUI`. `Book` is only the animation/content object inside that screen, so closing only `Book` is not sufficient when `DimPanel` is a sibling under `EncyclopediaUI`. If this reference is missing at runtime, the screen may resolve only its parent-chain `EncyclopediaUI` active boundary to prevent DimPanel leaks; it still warns when no authored boundary exists.
- `EncyclopediaBookPresentation` expects `DimPanel` (`CanvasGroup` or `Graphic`), `BookMotionRoot`, the child book `Animator`, optional content roots, `pageCoverImage`, and `contentAppearClip` to be wired. Assign `pageContentRoots` to the LeftPage/RightPage content objects if tab turns must physically `SetActive(false)` during animation instead of only hiding through a `CanvasGroup`. `EncyclopediaScreen` does not add this component at runtime; the safe GlobalUIRoot wiring menu or Inspector authoring must add/wire it for persistent prefab authoring.
- `EncyclopediaEntrySlot.prefab` owns the holder sprite image, `Icon`, `IndexText`, `TitleText`, `SelectedMarker`, `LockedMarker`, `Button`, and `EncyclopediaEntryButton` references. `EncyclopediaEntryButton` resolves named children before using fallback searches so the holder sprite is not mistaken for the icon.
- Book presentation expects authored `Image`, `CanvasGroup`, `SpriteRenderer`, `Animator`, and `AnimationClip` references. Runtime code requests named Animator states and toggles assigned groups; it does not step sprite frames itself.
- Imported UI book assets currently use `Updated_Paper_Book` Style 1 open/close and content-appear PNG frames plus `Content/5 Holders`, `Content/6 Highlighter`, and `Book Side Tabs` sprite folders for slot/tab authoring. The editor builder slices EarthTome sheets as 48x40 horizontal frames before loading frame arrays; EarthTome close animation is generated from the `OpenBook` frames in reverse order rather than from `EarthTome_16x16_CloseBook.png`.
- Encyclopedia TMP text should reference `Assets/Font/Galmuri9 SDF.asset` for both `fontAsset` and `fontSharedMaterial`; long detail text uses overflow instead of ellipsis so content is not silently hidden by TMP truncation.
- The editor menu `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` is the current safe repair path for the authored `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab` encyclopedia layout. It loads the prefab through `PrefabUtility.LoadPrefabContents`, finds existing roots by authored names such as `EncyclopediaUI`, `Book`, `DimPanel`, `LeftPage`, `RightPage`, `ItemDetailPanel`, `TitleGroup`, `PageButtonGroup`, `SlotGrid`, and `AbilityBlockContainer`, then wires `EncyclopediaScreen.screenActiveRoot` to `EncyclopediaUI` plus `EncyclopediaBookPresentation`, `EncyclopediaItemTab`, `EncyclopediaItemLeftPage`, `EncyclopediaEntryGridView`, and a single `EncyclopediaItemRightPage` on `RightPage`. It removes duplicate child `EncyclopediaItemRightPage` presenters, legacy `EncyclopediaDetailPanel` components under RightPage, and legacy `EncyclopediaLeftPageView` components under LeftPage. For RightPage item sections it connects existing roots such as `DescriptionRoot`, shared `WeaponStatsRoot`/`LevelPreviewRoot`, `WeaponAbilityRoot`, `RelicPreviewRoot`, and `RelicEffectRoot`; it wires `LvTxt` to both `weaponStatsText` and `relicLevelText`, gives `PrevPreview` / `NextPreview` 32x32 layout size, and creates/reuses a `PageCover` overlay for `ContentAppear`. It should not create new visual detail section roots, item sub-tab buttons, slot grid roots, or an ability container.
- The editor menu `Tools/Encyclopedia/Rebuild V1 Assets` builds temporary/generated support assets from current source data. It is not the preferred path for the current authored `GlobalUIRoot` layout and now shows a confirmation warning before rebuilding generated shell assets:
  - `Assets/LeeJunMo/Datas/Encyclopedia/EncyclopediaCatalog.asset`
  - `Assets/LeeJunMo/Animations/Encyclopedia/UIBook/*.anim`
  - `Assets/LeeJunMo/Animations/Encyclopedia/UIBook/AC_Encyclopedia_*.controller`
  - `Assets/LeeJunMo/Animations/Encyclopedia/EarthTome/*.anim`
  - `Assets/LeeJunMo/Animations/Encyclopedia/EarthTome/AC_Encyclopedia_EarthTome.controller`
  - `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab`
  - `Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaScreen.prefab`
  - `Assets/LeeJunMo/Prefab/Interactables/EncyclopediaStand.prefab`
- The generated stand prefab still needs a scene/global UI screen instance to exist; it can resolve a scene `EncyclopediaScreen` if the direct reference is not assigned.

## Extension Entry Points

- Add final monster/boss planning text through their eventual dedicated data source/provider, not by expanding item-side duplicated catalog data.
- Add Monster/Boss pages by enabling the existing category toggles and then adding the planned sub-tab/theme filtering and dedicated detail presenters.
- Extend Item sub-tab layouts by wiring additional item-specific detail roots under `EncyclopediaItemRightPage`; do not reintroduce inventory hover behavior.
- When adding non-item sub-tabs, extend the page-title preset selection so each sub-tab can change both title text and title icon without changing the `TitleGroup` hierarchy.
- Add final book visuals by replacing the generated screen prefab art/sprite arrays/materials while keeping serialized screen references.
- Adjust final left-page grid layout in Unity by editing `EntryGridRoot` and `EncyclopediaEntrySlot.prefab`; do not reintroduce fixed serialized slot rows for new layout work.
- Add release discovery by adding a save-owned discovered ID set and recording IDs from weapon acquisition, monster encounter, and boss encounter/defeat events.
- If discovery becomes source-of-truth behavior, promote the visibility rule to `Docs/Contracts/` or `Docs/Architecture/` before broad content work.

## Known Pitfalls

- Do not add runtime fallback construction for the screen hierarchy. Missing fields should be treated as prefab/scene authoring gaps.
- Do not add runtime `AddComponent` self-repair for `EncyclopediaBookPresentation`, Item page presenters, or other encyclopedia UI dependencies. Use the wiring tool or Inspector authoring, then let runtime warnings expose missing references.
- Do not attach both `EncyclopediaDetailPanel` and `EncyclopediaItemRightPage` to the same active Item RightPage hierarchy. `EncyclopediaItemRightPage` is the item presenter; `ItemDetailPanel` is only a layout host in this context.
- Do not attach `EncyclopediaLeftPageView` to the active Item LeftPage hierarchy. `EncyclopediaItemLeftPage` is the current left-page presenter.
- Auto-wiring is a convenience for already-authored encyclopedia layout objects, not a replacement for Inspector review. Generic child names such as `Text (TMP)` can still bind to the wrong field if the hierarchy lacks clearer names.
- Do not add raw runtime-created `Button`, `Image`, `TMP_Text`, or `Canvas` objects for the encyclopedia list or detail page. Only instantiate the authored slot prefab and authored encyclopedia ability-block prefab.
- If `entryGridRoot` and `entrySlotPrefab` are both assigned, serialized legacy `entrySlots` are deactivated and ignored for layout. Keep them only as fallback/migration data until prefab authoring is fully cleaned.
- Do not put Item sub-tab/page/selection state back on `EncyclopediaScreen`. New Item behavior should live in `EncyclopediaItemTab`, and Monster/Boss should get their own tab presenters.
- Do not rely on leaving `EncyclopediaUI/Book` active in the prefab or scene as the runtime open state. If it is active for authoring convenience, `EncyclopediaScreen.closeOnRuntimeAwake` should close it immediately on Play startup.
- `Panel_AbilityBlock_Encyclopedia.prefab` must carry valid `WeaponAbilityBlockView` references. If title/body/icon references are missing, ability rows can instantiate but appear partially empty.
- Do not treat Skill1 and Skill2 as variants of the same ability block. They are separate ability rows. Only variant-capable skills, such as LightningSpear-style replacement skills, should show the switch guide and external preview behavior.
- Do not return an ability-block preview instance to the pool with `LayoutElement.ignoreLayout = true`. A later normal skill row will be ignored by the parent `VerticalLayoutGroup` and can overlap another row.
- Do not confuse category transition reveal with item selection. Item selection must stay immediate for v1.
- Do not use `Animator.speed = -1` for encyclopedia content disappear. Reverse-sample the authored `ContentAppear` clip on the page cover instead.
- `EncyclopediaInteractable.OnDisable()` should snap/stop authored book presentation state instead of calling the normal unhighlight close animation, because inactive objects cannot start coroutines.
- Do not treat UI book close as stack-free visual-only cleanup. The current close animation relies on `ICloseRequestHandler` so gameplay lock remains until the deferred pop.
- If `pageContentRoots` are not assigned, page-turn presentation can still hide content visually through the page content `CanvasGroup`, but it will not physically deactivate individual LeftPage/RightPage objects.
- EarthTome sprite sheet slicing assumes 48x40 frame cells. If the asset changes size or spacing, update `EncyclopediaBookAssetPostprocessor` before rebuilding prefabs.
- Do not store discovered IDs in the catalog; discovery is player save state.
- Do not copy item/weapon/relic/consumable definitions into the catalog as encyclopedia source data. Use existing item data and keep encyclopedia-specific UI state in presenters/views.
- Do not re-add common metadata output for item definitions in `EncyclopediaItemRightPage`. ID, relic rarity/max level, and consumable restore amount/target belong to later explicit layout decisions, not automatic shared presenter text.
- Do not assume the DimPanel is missing just because it has no `CanvasGroup`. A plain `Image`/`Graphic` DimPanel is valid and should be faded through `EncyclopediaBookPresentation.dimPanelGraphic`.
- Do not use `Book` as the encyclopedia screen active boundary. `Book` is a child presentation object; `EncyclopediaUI` is the popup root that must be activated/deactivated so `DimPanel` and sibling page roots cannot leak into gameplay.
- Do not let `EncyclopediaBookPresentation` auto-pick arbitrary child animators from slots or buttons. The book Animator should be the named Tome/Book animator or an Animator controller with `BookOpen`, `BookClose`, `BookLeftPage`, or `BookRightPage` states/clips.
- Do not use generic child names such as `SkillInfoGroup` as the weapon ability container. That can bind into an ability-block prefab internals instead of the authored RightPage `AbilityBlockContainer`.
- The interactable's scene-screen fallback is only a wiring fallback. Production scenes should still assign references deliberately.
- New C# files will not be covered by MSBuild until Unity imports them and regenerates the `.csproj` files.
- TMP font prefab fixes must update both serialized font and shared material references. A screen can still appear to use the default TMP look if only `fontAsset` is replaced.
- Manual YAML prefab edits should be validated in Unity because prefab references to TMP, UI components, external sprites, and cross-prefab slot components are serialization-sensitive.
- Do not add Animator components or AnimationClip references to encyclopedia prefabs through broad YAML text replacement. Regenerate through `EncyclopediaV1AssetBuilder` or edit in Unity so fileID ownership and document separators stay valid.
- For the current `GlobalUIRoot` layout, do not use `EncyclopediaV1AssetBuilder` as a repair tool. Use `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia`, then inspect the resulting Inspector references because duplicate generic child names can still require manual correction.

## Promotion Candidate

Not yet. Promote after the release discovery/save path is implemented and the authored prefab has been validated in play mode.
