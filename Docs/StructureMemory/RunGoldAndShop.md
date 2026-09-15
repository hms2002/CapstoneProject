# Run gold and shop

Current structure map; implementation reference, not a contract.

## Ownership and lifecycle

GamePlayData.runGold owns the current run balance. Existing CurrencyManager exposes GetGold/AddGold/SpendGold and OnGoldChanged. RunSessionLifecycleService resets gold at start, end and development reset; inactive runs expose zero. Gold does not enter persistent magic-stone currency. No new singleton or bootstrap object was introduced.

## Drop and HUD flow

ExperienceRewardSource uses its existing valid death-reward gate, so suppressed rewards also suppress gold. For procedural room encounters, MonsterSpawnRoomGroup assigns an exact runtime gold share to every planned spawn before combat. Normal Combat rooms carry 120 gold and Large Combat rooms carry 180 gold; shares are weighted by each monster prefab's base experience and sum to the room budget. Non-Combat room waves are explicitly assigned zero so event/treasure-authored monsters cannot fall back to uncapped rewards. AlarmBellInteractable owns a separate 50-gold encounter budget, spread across its planned spawns. Summoned units that already suppress experience continue to suppress gold.

Unconfigured/legacy ExperienceRewardSource users retain the fallback of clamp(baseExperience * 6, 40, 600), followed by a uniform integer roll within ±15% (ceil lower/floor upper). All paths still spawn the existing GoldPickup2D from GoldPickup.prefab and split the total across at most eight pickups. Pickup scatter, delayed homing, collection and HUD flow are unchanged. GoldPickup2D and ExperiencePickup2D collect from Update upon reaching the tracked player position (0.01 world-unit tolerance), after the homing delay. They have no trigger callback or required Collider2D; existing authored colliders do not control collection. Collection marks the pickup consumed before reward callbacks to prevent duplicate grants. Nineteen monster prefabs reference the pickup.

Art/Sprites/Items/gold.png is the supplied image, imported as a point-filtered 16 PPU sprite. GlobalUIRoot contains an authored GoldUI row above the magic-stone row. CurrencyUI.showRunGold selects the balance and event; the UI owns no currency state.

## Merchant flow

ShopDefinitionSO.usesRunGold selects run-shop behavior. ShopDefinition_RunGold.asset uses three unrestricted weapon/relic slots with category weights of 1:3 (25%/75%) and a fourth dedicated Consumable slot, maximum three weapons and one consumable. ShopInventoryRoll retains existing candidate restrictions, including chest-only weapons. Prices are rolled once into saved stock: weapons 1150–1250; Common relics 400–500; Rare 600–700; Epic 900–1000; consumables 850–950, inclusive. The policy centers relic purchasing near a 600-gold unit, potions near 1.5 units (900), and weapons near 2 units (1200).

MerchantNPC stores gold stock under scene/merchant ID/position in existing run merchant states, preserving purchases and prices on revisit. MerchantPurchaseService spends/refunds the selected currency around existing acquisition. MerchantShopPolicy requires an active run. Initial run shops use no hub refresh, discount or slot upgrade modifiers. Hub currency and pricing remain unchanged.

Prefabs/Loot/RunMerchantGroup.prefab is the placement prefab derived from the hub MerchantGroup, with gold shop policy and ShopSlot_RunGold presentation. Its DialogueTrigger now matches the hub Merchant: shared MSShopNpc data, primary Ink, prompt anchor and sprite highlight, offering the same talk prompt, portrait and choices. The four existing authored anchors are all enabled by the shop definition. Existing three-slot saved stock expands through MerchantRunStateService while preserving sold entries. The first three authored anchors use Any and the fourth uses Consumable. Run-gold rolls allow consumables only in the dedicated slot; hub rolls retain their existing policy. Missing saved slots use their actual anchor index when appended. Existing four-slot saved stock is retained until a new run/stock generation. It reuses the hub prefab instantiation path, without creating a new runtime HUD hierarchy.

## Rooms and tuning

Each BossThemes Slime/Dragon/Shadow/DemonKing folder has a themed Shop RoomTemplateSO with the merchant placement. All four corresponding libraries include it. Slime/Dragon/Shadow generation profiles guarantee their shop template. DemonKing uses its own DemonKingShopLayoutPolicy with one shop, referenced by ProceduralDemonkingCorridor; the shared corridor policy is unchanged.

Tune ordinary room budgets in MonsterSpawnRoomGroup, special-event budgets in their event owner, category weights in ShopDefinition_RunGold, price ranges in ShopDefinitionSO.RollGoldPrice, and visual placement in the authored prefabs/room templates. A standard six-Normal/one-Large combat stage pays exactly 900 gold before special-event and boss additions. An Alarm Bell adds at most 50; the boss adds 43–57. This targets about 1.5 baseline relic units per stage and makes a 1200-gold weapon affordable after saving across stages. Actual room composition and purchase cadence still need playtesting.

## Verification and pitfalls

Editor and procedural test projects compiled with the new pickup explicitly included through a temporary external MSBuild targets file; generated project files were not edited. Added currency/reset and price-range regressions were compiled, not executed. Unity import, visual layout, pickup feel, purchase/revisit behavior and procedural generation still need Editor playtesting. Do not infer runtime test success from compilation. No Architecture/Contracts promotion or Presentation HTML update is currently identified.

Price rounding: newly rolled run-shop prices round the units digit half-up to multiples of 10 gold, using (price + 5) / 10 * 10 integer arithmetic. Authored range endpoints remain valid; existing saved prices are retained. Gold pickup amounts are unaffected.


## Boss encounter gold (2026-09-13)

BossEncounterEndDirector owns baseGoldReward (50 default) and goldPickupPrefab, authored as 50 in the four production boss scenes. Its guarded CompleteEncounterRoutine emits 43–57 gold (inclusive +/-15%) across up to eight pickups before finale/terminal-ending presentation, only in an active run. Total integer gold is preserved via quotient/remainder distribution. Payment belongs to encounter completion, including split/multi-phase bosses, rather than each managed enemy death. Tutorial director remains unassigned. Unity pickup/ending timing needs playtest confirmation.


## Homing pickups and portal settlement (2026-09-13)

- EXP, gold and magic stones all collect from Update when within .01 world units of the tracked player's position. Stone's authored collider remains a trigger but has no currency-grant callback. Serialized speed/delay fields and prefab values remain unchanged.
- HomingPickupTravel.Speed evaluates min(40, initialSpeed + 20 * trackingSeconds). Each pickup owns tracking time and target; initial drop delay does not count toward acceleration. Gold/EXP Initialize and stone OnEnable reset tracking state.
- HomingPickupTravel.TryCollectFollowing performs three active-type queries only on travel, then calls each pickup's guarded collection path for the requested player. It owns no currency, persistent state, scene object or registry. Nonfollowing/disabled/consumed pickups do not grant; failed eligible grants remain and block travel with warning. Existing consumed flags prevent callback/deferred-Destroy double grants.
- Integration: ScenePortalTravelExecutor before capture/start/end run; SceneConnectionTravelExecutor after departure and before dungeon/player capture/run action; TutorialScenePortal before load plus refreshed player capture; DungeonReturnTravel before warp. Keep this order so level-up-triggered health/cooldown effects enter the outgoing snapshot and magic stones enter the active-run pending ledger before run end.
- New portal implementations should call this helper after access validation and before state capture/run termination/warp. A denied travel request must not grant loot. Successful individual grants remain consumed if another pickup fails or scene loading fails; retries only grant the remainder.
- Gameplay -> Core and Infrastructure -> Gameplay dependency directions retained. No new Manager, singleton or DDOL object. No Architecture/Contracts promotion needed for this small helper.
