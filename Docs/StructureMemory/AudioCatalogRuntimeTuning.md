---
status: active
authority: structure-memory
category: audio-runtime-tuning
last_reviewed: 2026-06-02
---

# Audio Catalog Runtime Tuning

## Purpose

Fast map for Play Mode audio tuning through `Tools/Audio/Audio Catalog`.

## Current Structure

- `AudioCatalogSO` remains the asset source of truth for keys, variants, bus/category, volume, pitch/playback speed, loop/spatial policy, cooldown, and distance.
- `AudioCatalogWindow` edits the selected catalog through `SerializedObject`, saves the asset after changes, and calls the active `SoundManager` during Play Mode.
- `SoundManager` owns runtime `AudioSource` instances. Catalog-backed active SFX sources retain their catalog, key, `SoundRef`, playback context, pitch roll, loop ownership, and optional follow target/offset so edited catalog values can be reapplied without restarting the clip.
- Current BGM stores its catalog, key, base volume multiplier, and pitch roll separately from SFX pool state.

## Key Files

- `Assets/LeeJunMo/Script/Audio/Runtime/AudioCatalogSO.cs`
- `Assets/LeeJunMo/Script/Audio/Runtime/SoundManager.cs`
- `Assets/LeeJunMo/Script/Audio/Editor/AudioCatalogWindow.cs`
- `Assets/LeeJunMo/Datas/Resources/Audio/DefaultAudioCatalog.asset`

## Ownership And Lifecycle

- Catalog assets own authored tuning values and are saved by the editor window after property changes.
- `SoundManager.EnsureInstance()` owns runtime playback sources and loads `DefaultAudioCatalog.asset` from Resources when needed.
- Persistent `SoundManager` pooled sources stay parented under persistent manager roots. Scene-object following is projected from stored follow target/offset in `LateUpdate`; pooled source GameObjects must not be parented under scene-owned transforms.
- `SoundManager.RefreshCatalogRuntime(...)` only refreshes catalogs currently loaded by that manager.
- Active random variant clips are not replaced mid-play. The live refresh updates parameters such as volume, pitch, distance, spatial policy, category, and BGM target volume.
- Explicit `StopMusic()` fade-out protects its pending stop from being interrupted by live catalog refresh.

## Extension Entry Points

- Add new catalog-backed runtime parameters by storing enough original playback context in `RuntimeSoundState`, then applying them in `ApplyCatalogRuntimeProperties(...)`.
- Add BGM-only live parameters through `RefreshCurrentMusicFromCatalog(...)`.
- Add editor-side live controls through `AudioCatalogWindow.ApplyCatalogChanges(...)` so asset save and runtime refresh stay paired.

## Known Pitfalls

- Do not force-swap active random variant clips during live tuning unless the user explicitly wants playback restart behavior.
- Do not call `SoundManager.EnsureInstance()` from the editor window just to tune; only refresh `SoundManager.Instance` when Play Mode already has an active manager.
- Do not parent persistent audio pool objects under scene objects. Scene unload can destroy child sources while `SoundManager` still holds them in pool/runtime dictionaries; store follow targets instead.
- Direct legacy clips are not catalog-backed, so only global/settings volume refresh affects them.
- `dotnet build` may fail before script compile in this Unity-generated project due project reference target-framework lookup; Unity Editor compile or a working Visual Studio MSBuild path is still the reliable compile check.

## Promotion Candidate

This can be promoted to an Architecture or Contract document if audio authoring rules expand beyond catalog tuning into formal mix buses, snapshots, or designer workflow policy.
