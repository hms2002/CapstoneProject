# Run gold and shop

Current structure map; implementation reference, not a contract.

## Ownership and lifecycle

GamePlayData.runGold owns the current run balance. Existing CurrencyManager exposes GetGold/AddGold/SpendGold and OnGoldChanged. RunSessionLifecycleService resets gold at start, end and development reset; inactive runs expose zero. Gold does not enter persistent magic-stone currency. No new singleton or bootstrap object was introduced.

## Drop and HUD flow

ExperienceRewardSource uses its existing valid death-reward gate, so suppressed rewards also suppress gold. It spawns GoldPickup2D from GoldPickup.prefab: a baseline of clamp(baseExperience * 6, 40, 600), then a uniform integer roll within ±15% (ceil lower/floor upper), split across at most eight pickups. Current common base XP values 5/10/15 yield baselines of 40/60/90 gold (random ranges 34–46 / 51–69 / 77–103). Pickup scatter and delayed player homing follow experience pickup behavior. GoldPickup2D and ExperiencePickup2D collect from Update upon reaching the tracked player position (0.01 world-unit tolerance), after the homing delay. They have no trigger callback or required Collider2D; existing authored colliders do not control collection. Collection marks the pickup consumed before reward callbacks to prevent duplicate grants. Nineteen monster prefabs reference the new pickup.

Art/Sprites/Items/gold.png is the supplied image, imported as a point-filtered 16 PPU sprite. GlobalUIRoot contains an authored GoldUI row above the magic-stone row. CurrencyUI.showRunGold selects the balance and event; the UI owns no currency state.

## Merchant flow

ShopDefinitionSO.usesRunGold selects run-shop behavior. ShopDefinition_RunGold.asset uses three unrestricted weapon/relic slots with category weights of 1:3 (25%/75%) and a fourth dedicated Consumable slot, maximum three weapons and one consumable. ShopInventoryRoll retains existing candidate restrictions, including chest-only weapons. Prices are rolled once into saved stock: weapons 1000–1300; Common relics 200–400; Rare 500–800; Epic 900–1200; consumables 200–300, inclusive.

MerchantNPC stores gold stock under scene/merchant ID/position in existing run merchant states, preserving purchases and prices on revisit. MerchantPurchaseService spends/refunds the selected currency around existing acquisition. MerchantShopPolicy requires an active run. Initial run shops use no hub refresh, discount or slot upgrade modifiers. Hub currency and pricing remain unchanged.

Prefabs/Loot/RunMerchantGroup.prefab is the placement prefab derived from the hub MerchantGroup, with gold shop policy and ShopSlot_RunGold presentation. Its DialogueTrigger now matches the hub Merchant: shared MSShopNpc data, primary Ink, prompt anchor and sprite highlight, offering the same talk prompt, portrait and choices. The four existing authored anchors are all enabled by the shop definition. Existing three-slot saved stock expands through MerchantRunStateService while preserving sold entries. The first three authored anchors use Any and the fourth uses Consumable. Run-gold rolls allow consumables only in the dedicated slot; hub rolls retain their existing policy. Missing saved slots use their actual anchor index when appended. Existing four-slot saved stock is retained until a new run/stock generation. It reuses the hub prefab instantiation path, without creating a new runtime HUD hierarchy.

## Rooms and tuning

Each BossThemes Slime/Dragon/Shadow/DemonKing folder has a themed Shop RoomTemplateSO with the merchant placement. All four corresponding libraries include it. Slime/Dragon/Shadow generation profiles guarantee their shop template. DemonKing uses its own DemonKingShopLayoutPolicy with one shop, referenced by ProceduralDemonkingCorridor; the shared corridor policy is unchanged.

Tune drop amounts in ExperienceRewardSource, category weights in ShopDefinition_RunGold, price ranges in ShopDefinitionSO.RollGoldPrice, and visual placement in the authored prefabs/room templates. Initial economy is approximately 17–22 ordinary 60-gold monsters per weapon; actual encounter mix needs playtesting.

## Verification and pitfalls

Editor and procedural test projects compiled with the new pickup explicitly included through a temporary external MSBuild targets file; generated project files were not edited. Added currency/reset and price-range regressions were compiled, not executed. Unity import, visual layout, pickup feel, purchase/revisit behavior and procedural generation still need Editor playtesting. Do not infer runtime test success from compilation. No Architecture/Contracts promotion or Presentation HTML update is currently identified.

Price rounding: newly rolled run-shop prices round the units digit half-up to multiples of 10 gold, using (price + 5) / 10 * 10 integer arithmetic. Authored range endpoints remain valid; existing saved prices are retained. Gold pickup amounts are unaffected.


## Boss encounter gold (2026-09-13)

BossEncounterEndDirector owns baseGoldReward (800 default) and goldPickupPrefab, authored in the four production boss scenes. Its guarded CompleteEncounterRoutine emits 680–920 gold (inclusive +/-15%) across up to eight pickups before finale/terminal-ending presentation, only in an active run. Total integer gold is preserved via quotient/remainder distribution. Payment belongs to encounter completion, including split/multi-phase bosses, rather than each managed enemy death. Tutorial director remains unassigned. Unity pickup/ending timing needs playtest confirmation.
