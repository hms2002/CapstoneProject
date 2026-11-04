# Chloe and Melta affection level 1 rewards

Mode: Implementation. User approved the numerical behavior, two supplied portraits, and recommended descriptions on 2026-11-05. Both new rewards use level 1; Melta's existing level-5 teleport reward stays intact.

Status: implemented. Gameplay MSBuild including Core exit 0; source/reference/locale/image-byte/diff inspection completed. No automated tests, Unity import/Editor compilation, actual rewards/vision/price/purchase/reentry/save-reload or Player build were executed.

Goal: in an active Shadow route's actual scene, player vision mask XY size is 70% of its authored baseline before Chloe affection 1 and 100% afterward. In an active Slime route's actual gold-shop scene, Melta affection 1 adds 10 percentage points to the existing global affection discount before pricing. Sasha's current 5% therefore becomes 15%; special offers keep their existing separate multiplier.

Allowed: existing affection query overload, GlobalVisionMaskController mask sizing, MerchantNPC regional modifier/repricing subscription, two InfoOnlyAffectionEffect assets and NPC bindings, supplied PNG copies and new meta identities, two localization keys and five-locale data/key mappings, narrow docs.

Preserve: original portraits and all existing asset identities, dialogue portraits, vision overlays/fades/positions/fog requests/lights, authored mask prefab/scene scale, NPC progression/save timing, Melta level 5, merchant original prices/stock/sold identity, other-area shops/hub shops, Sasha effects, special-offer assignment. No new manager/service/schema/serialized field/asmdef, prefab/scene/bootstrap/Architecture/Contracts/Presentation HTML edits, or user-save modification.

Descriptions: Chloe “클로에의 영역에서 어둠 너머를 더 멀리 볼 수 있습니다.”; Melta “멜타의 영역 내 상점에서 모든 상품의 가격이 10% 할인됩니다.”

Verification: source/reference/diff inspection; image byte/hash and locale data inspection; generated Gameplay MSBuild including Core. No automated tests or automated Unity Play Mode checks authorized. Actual vision, reward layout, price tags/purchase totals/reentry and save reload require later manual gameplay.
