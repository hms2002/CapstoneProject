# Field item shadows

Current structure map; not an Architecture or Contracts document.

## Purpose and approved appearance

Generic field relic and consumable drops use the existing `Shadow_0` pixel ellipse (17 × 6), with its original RGB and renderer alpha 0.34. Its width is 75% and height is 25% of the normalized item display box's largest dimension. The accepted spacing is **-4 pixels in a 32-pixel preview**, not -4 percent: the visible contours overlap by four preview rows after computing their first contact.

## Structure and ownership

- `Assets/_Project/Prefabs/Items/Pickups/PF_WorldItemPickup2D.prefab` owns an authored `Shadow` child renderer and `WorldItemShadowPresenter2D`. Its original `Render` child, item display presenter, collider, root position and pickup references are preserved.
- `Assets/_Project/Runtime/Presentation/Loot/WorldDrops/WorldItemShadowPresenter2D.cs` owns only shadow positioning and visibility. It uses serialized sprite/contact pairs, matching the source renderer's actual normalized size, position and sorting. It reads no texture pixels and creates no objects at runtime. Placement is recalculated when the source sprite or its local transform changes; sorting and visibility follow the source each LateUpdate.
- The shadow renderer uses the existing sprite at `Assets/_Project/Art/Sprites/Player/Pixel_Plains_Premium_Pack/Characters 24x24/Shadow.png`, an unlit Sprites-Default material, and one sorting order behind the icon.
- `Assets/_Project/Editor/Tools/Validation/WorldItemShadowAuthoring.cs` bakes the visible contour from original PNG source bytes into the pickup prefab. As of 2026-11-05, the current active relic/consumable folders contain 74 definitions and 74 distinct sprites, all referenced by the refreshed contact list. Archived definitions are excluded. Texture import settings are unchanged.

## Lifecycle and presentation boundaries

- Existing `WorldItemDropTweenAnimator` discovers the component through `IWorldItemDropLandingVisual`. Travel hides the shadow; the landing routine restores it. Disable clears travel/cache state and hides it. Re-enable refreshes on the next LateUpdate.
- Null icons, disabled fallback renderers (including custom weapon visuals) and icons without baked contact data have no generic shadow. Separate weapon, field-heal, currency and merchant presentation prefabs are unchanged.
- `ItemDisplayVisualPresenter2D` still owns the actual icon transform, outline targets and drop spin bounds. The shadow is a sibling and is excluded from these calculations. Item delivery, collision, rarity, saves and drop root movement are unchanged.

## Extension entry point and pitfalls

- After replacing an icon, changing its alpha contour or adding a relic/consumable icon, run **Tools > Items > Bake Approved Field Item Shadows**, then review the prefab diff. Existing contact values otherwise describe the old image. The baker reapplies the approved appearance.
- An unmatched sprite is deliberately hidden by `RefreshShadow`; retained code and an intact Shadow child do not establish current icon coverage. On 2026-11-05, 37 current definition/icon references were absent from the old contact list after icon replacement/integration. Refreshing only the serialized contact list restored coverage; the runtime component and authored appearance were retained. See [recovery log](../SessionLogs/2026-11-05-CubeHood-field-item-shadow-rebind.md).
- **Tools > Items > Validate Approved Field Item Shadows** checks production definition coverage, normalized dimensions, sorting, icon bounds, outline exclusion and directly invoked landing/disable callbacks, and attempts a native review capture. It does not establish actual Play Mode callback dispatch or full gameplay acceptance.
- The bake reads PNG bytes rather than requiring `TextureImporter.isReadable`. The component's serialized sprite list deliberately keeps the covered icons referenced by the pickup prefab.
- The initial application used a source-based offline bake because the already-open Editor was stalled in `Importing assets`. Contour/serialization checks and separate C# compilation passed; Unity import/compilation, native validation and actual Play Mode visual acceptance were **not executed**. See [evidence](../../output/world-item-shadows-2026-11-02/README.md).

No Architecture/Contracts promotion candidate is needed for this local presentation addition.
