# Damage Popup Pooling

Navigation aid, not an Architecture or Contracts source of truth.

## Purpose and current structure

`DamagePopupService` owns a private inactive stack for its existing authored `DamagePopupWorldText` prefab. Formatting, random position selection and overlap reservations stay in their original request order. No new Manager, singleton, persistent root, serialized field or scene authoring was introduced.

## Ownership and lifetime

- Active popups keep the parent returned by `ResolvePopupParent`. Unparented reused roots move to the active scene, just like fresh clones. Scene unload can still destroy an active popup.
- Expiration disables a popup and reparents only that inactive object under its owning service. Reuse restores the authored default font size/color and runtime style before applying the new model.
- Existing public `Setup` overloads retain their active-instance semantics; reset for a fresh presentation occurs in `PrepareForReuse`.
- Active-scene change and service destruction destroy retained inactive objects. Changing the prefab rejects old pooled instances on both acquisition and return.
- `LocalizeTmpFontEvent` keeps the package's localization ownership: disable ends subscriptions, and final destruction releases retained font materials. Material release is a code-reviewed cleanup path, separate from popup-destruction assertions.

## Key files and extension points

- [DamagePopupService.cs](../../Assets/_Project/Runtime/UI/Combat/DamagePopup/DamagePopupService.cs): request formatting, acquisition/return and pool disposal.
- [DamagePopupWorldText.cs](../../Assets/_Project/Runtime/UI/Combat/DamagePopup/DamagePopupWorldText.cs): movement/fade clock, setup, reuse preparation and expiration.
- [LocalizeTmpFontEvent.cs](../../Assets/_Project/Runtime/UI/Localization/LocalizeTmpFontEvent.cs): localized font/material lifetime.
- [CombatFeelLootQuestPlayModeTests.cs](../../Assets/_Project/Tests/PlayMode/Procedural/CombatFeelLootQuestPlayModeTests.cs): `FramePerformancePopupPlayModeTests` covers reuse, default style, parent-scene unload and inactive-owner cleanup.

## Pitfalls

Keeping active popups under a persistent pool parent changes scene-unload behavior. Reusing a popup without restoring defaults carries critical-hit font/style into a later ordinary popup. An inactive localized popup retains its material until it is actually destroyed. The inactive stack grows to observed concurrent demand; no visible popup cap or changed spawn policy was added.

Architecture promotion candidate: update the existing `DamagePopupService` inventory's pooling status in `RuntimeServiceOwnershipArchitecture.md` if that documentation change is approved. The service's existing UI-root ownership and bootstrap policy are unchanged.
