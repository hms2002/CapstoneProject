---
status: active
authority: project-log
category: decision-log
last_reviewed: 2026-06-01
---

# Decision Log

## 2026-06-06 - TaskIndex And ActiveTasks Replace CurrentTask Scope

Decision:
`Docs/CurrentTask.md` is deprecated as the active task-scope source. New Codex work should use the current prompt Task Brief and, when present, a matching `Docs/ActiveTasks/<task-id>.md` for scope. `Docs/TaskIndex.md` is the router/dashboard for active and proposed task documents.

Reason:
The project frequently uses multiple Codex threads in parallel. A single global current-task document can point at the wrong thread's work and create scope drift.

Implications:
- This decision supersedes the earlier `Update CurrentTask on Active Work Changes` policy for future work.
- Scope authority and technical authority are separate: ActiveTasks define thread scope, while `Docs/Contracts/` and `Docs/Architecture/` remain the strongest technical authorities.
- `Docs/ErrorLog.md` and `Docs/DecisionLog.md` should be searched when relevant instead of fully reread for every task.
- Historical `CurrentTask` mentions in session logs and old decisions remain as history unless a separate cleanup task is approved.

## 2026-06-02 - Audio Catalog Supports Play Mode Live Tuning

Decision:
`Tools/Audio/Audio Catalog` can be used during Play Mode as the live tuning surface for loaded `AudioCatalogSO` assets. When a catalog edit is applied, the editor window saves the selected catalog asset and asks the active `SoundManager` to refresh currently playing catalog-backed audio.

Reason:
Audio mix tuning needs to happen against the live game instead of editor-only preview. The catalog asset remains the source of truth, while `SoundManager` owns the runtime AudioSource state that must be refreshed without restarting gameplay audio.

Implications:
- Active BGM and currently playing catalog-backed SFX refresh volume, pitch/playback speed, distance, spatial policy, category ducking, and global multiplier effects from the edited catalog.
- Already chosen random variant clips are not swapped mid-play; clip/variant edits affect future playback or restarted sounds.
- The editor window saves the catalog asset after serialized changes so Play Mode tuning is not left as unsaved dirty state.
- `SoundManager.RefreshCatalogRuntime(...)` is a runtime-service refresh hook, not a new manager or persistent object.

## 2026-06-02 - Intro And Outro Use Start-Only Pencil Typing SFX

Decision:
Title intro and ending outro slide text use a dedicated `ui.inoutro.pencil` one-shot SFX when typing starts, not the shared repeated `boss.talking` typing tick.

Reason:
The intro/outro pencil sound is a longer presentation beat and should not be retriggered per typed character. General Dialogue UI and SpeechBubble typing retain their existing repeated talking sound because their rhythm is separate from the authored intro/outro slide presentation.

Implications:
- `TitleIntroPlayer` and `EndingOutroPlayer` play the pencil sound once per non-empty slide typing phase.
- Intro/outro players keep the pencil one-shot handle only so skip requests can fade out the currently playing pencil sound.
- `TypingAudioUtility.PlayBossTalking(...)` remains the shared call for normal Dialogue UI and SpeechBubble typing.
- `DefaultAudioCatalog.asset` owns the new `ui.inoutro.pencil` catalog key and requires Unity import/Inspector review after the new mp3 asset is imported.

## 2026-06-02 - Audio Catalog Variants Use Explicit Playback Mode

Decision:
`AudioCatalogEntry` variants use `AudioVariantPlaybackMode` to choose Random, First, or Simultaneous playback. `Random = 0` remains the default so existing catalog assets keep their current random variant behavior until designers change the field.

Reason:
Variant arrays were previously random-only. Designers need the same catalog key to support deterministic first-clip playback or layered one-shot SFX without adding extra sound keys or call-site-specific audio code.

Implications:
- Simultaneous playback is runtime-supported only for non-loop SFX. Loop SFX and BGM stay on the existing single-source/single-handle path.
- Same-source one-shot duplicate suppression and entry cooldown still apply once per playback request, even when that request emits multiple variant clips.
- `AudioCatalogSO` has a new serialized field, so existing catalog assets require Unity import/Inspector review before designers tune the mode.

## 2026-06-01 - RunRouteCatalog Can Pin Normal Route Order

Decision:
`RunRouteCatalogSO` can opt into fixed normal RouteSet ordering with `useFixedNormalRouteOrder`. When enabled, runtime run planning uses valid `normalRouteSets` in serialized order up to `normalStageCount`, then appends `finalRouteSet`.

Reason:
The pre-demo route needs to follow the authored three-boss path deterministically while preserving the existing random route behavior for catalogs that leave the toggle disabled.

Implications:
- The current demo `RunRouteCatalog.asset` uses fixed order: Shadow, Dragon, Slime Queen, then Demonking final.
- Fixed mode ignores duplicate random selection policy and requires enough valid authored normal RouteSets for `normalStageCount`.
- The existing random duplicate/non-duplicate branches remain available when the fixed-order toggle is disabled.

## 2026-06-01 - One-Shot SFX Duplicate Suppression Is Source-Scoped

Decision:
Rapid duplicate one-shot sound suppression is scoped to the same sound key from the same concrete source object/causer/instigator/target. BGM, loops, and the same key from different source objects are not suppressed.

Reason:
The pre-demo overlap problem is caused by the same actor/event stacking identical one-shots, but shared enemy groups and simultaneous attacks still need to sound like multiple sources.

Implications:
- Audio call sites should pass a concrete runtime source in `SoundPlaybackContext` when they want duplicate suppression to apply per actor or event owner.
- Shared ScriptableObject definitions should not be treated as the suppression source when a concrete causer, instigator, or target exists.

## 2026-05-31 - GameOver Inventory Uses Layer Lift And Inspection Mode

Decision:
GameOver may open only the authored Inventory through its existing owner exception. While that Inventory is open, GameOver temporarily lifts the existing `Popup` and `Hover` canvases above `GameOverCanvas` and puts the Inventory into inspection-only mode.

Reason:
`GameOverCanvas` is authored above `PopupCanvas`, so the Inventory can open but render behind GameOver. Reauthoring the prefab or globally changing canvas order would affect normal gameplay UI. The GameOver exception also must not let the player mutate items after defeat or victory.

Implications:
- The layer lift is runtime-only and restores the original `Popup`/`Hover` sorting state when the Inventory closes or GameOver cleans up.
- Inspection-only Inventory allows hover tooltips but blocks drag, drop, right-click quick move, and fixed-key world drop.
- FakeGameOver still does not expose the Inventory exception.

## 2026-05-31 - DemonKing Slash And Cinematic Sword Beats Are Pattern-Owned

Decision:
DemonKing HeavySlash, SwordThrow, HomingMagic aim warnings, FinalDesperation sword planting, and death sword planting stay owned by the relevant DemonKing pattern/actor code instead of animation events or scene YAML edits.

Reason:
These beats depend on live target position, current sword state, warning lock timing, and boss lifecycle cleanup. Keeping the timing in AL/actor code lets the Pattern Workbench and Inspector fields tune the behavior while the transition-free DarkLord controller remains a state library.

Implications:
- HeavySlash uses `stopBeforeTargetDistance` only for its initial approach spacing. The later warning/LockOn/commit sector is positioned by `playerAnchorInWarningRadius`, and commit movement dashes the boss so `SwordSlashOrigin` lands on the locked sector origin while the player sits at the configured point inside the warning.
- SwordThrow uses the `DarkLord_Sword_Throwing` clip last-frame timing as the release point; reflections remain physics-only and do not create warning lines.
- FinalDesperation and death use cinematic EgoSword planted states that do not start dropped sword subpatterns, damage, impact VFX, spin VFX, or afterimages.
- `DemonKingVfxCueRef.scale` is now a runtime size multiplier for cue-authored visuals; existing `Vector3.one` cues remain unchanged, but non-one serialized scales must be reviewed in Play Mode.

## 2026-05-31 - Encyclopedia Relics Do Not Use StoryText For Effects

Decision:
The encyclopedia Relic detail path does not use shared `StoryText` for effect display. Relic Story/Description UI stays inactive for now, and per-level relic effects render only through dedicated `relicEffectRoot` / `relicEffectText` authoring.

Reason:
Current relic entries do not have story copy. Reusing `StoryText` for effect output made the layout imply relic story support and hid whether the dedicated relic effect section was actually wired.

Implications:
- Add a separate relic story section later only if relic story content becomes a real requirement.
- Missing `relicEffectText` is an authoring gap, not a reason to fall back to `StoryText`.
- Weapon and Consumable entries may continue to use shared `StoryText` for their current description/story paths.

## 2026-05-31 - Encyclopedia Weapon Stats And Relic Preview Use Separate Authored References

Decision:
The encyclopedia Item RightPage treats weapon stats and relic level preview as separate authored UI references. Tab icons are also explicit authoring fields on the screen/left-page presenters, and relic preview guide icons are resolved through the existing Q/E KeyGlyph path.

Reason:
The weapon stat text box and relic level-preview box are visually different layout concerns. Sharing `LvPanel`/`LvTxt` made editor auto-wiring collapse those concerns and prevented authored tab/guide icons from being inspected directly.

Implications:
- Use `WeaponStatsRoot`/`StatTextPanel` and `RelicPreviewRoot`/`LvPanel` as separate prefab objects where the layout is split.
- The encyclopedia wiring tool may warn about shared weapon/relic references but should not create replacement visual hierarchy.
- Relic previous/next guide icons should come from `InputBindingService`/`InputGlyphDatabase` instead of hard-coded sprites.

## 2026-05-31 - Upgrade Rewards Use Node Metadata For Generic Effects

Decision:
Upgrade purchase reward popups use the purchased `UpgradeNodeSO` as the metadata source for generic effect rewards. Generic effect slots show the node icon and generic reward text uses the node description. Item unlock effects remain item-display rewards and show a generic item-unlock summary line.

Reason:
The upgrade tree node and the purchase reward popup should not drift because `UpgradeEffectSO.rewardIcon` or `rewardText` was tuned separately. Node icon and description are already what the player saw before purchase.

Implications:
- `RewardDisplayService.ShowUpgradeReward(...)` is the normal upgrade purchase reward entry point.
- `UpgradeEffectSO.rewardIcon` and `rewardText` remain serialized compatibility data for legacy or non-node reward calls.
- Missing generic reward icon/description should be fixed on the `UpgradeNodeSO`, not by restoring per-effect display overrides.

## 2026-05-31 - DemonKing Charge Uses Probe-Based Visible Trajectories

Decision:
HP50 `WallBounceRush` uses an optional authored wall-rush probe collider for endpoint casts, and each rush may retarget within a limited player-facing angle cone when the direct player path is too short to read as a visible charge. Charge presentation uses the same `DemonChargeEffectVfx` prefab instance, follows the boss in `Loop`, then switches that same instance to `Disappear` at the configured travel progress before the endpoint instead of authoring a second endpoint VFX prefab slot.

Reason:
The pattern is count-based and must read as the configured number of visible rushes. Near-wall player directions can be collision-correct while still looking like a wasted count. The Charge VFX asset is authored as one controller with multiple states, so a separate disappear prefab slot creates unnecessary and misleading tuning surface.

Implications:
- `wallRushCollisionProbe` should be authored on the DemonKing prefab/scene instance when body-accurate wall stopping matters.
- `chargeDisappearVfx` is legacy serialized data only; designers should tune `chargeLoopVfx` for both loop and disappear state placement, plus `chargeDisappearStartProgress` for when the loop detaches into `Disappear`.
- Missing or zero `chargeDisappearStartProgress` values fall back to `0.9` at runtime so older AL assets do not immediately switch the Charge VFX into `Disappear`.
- `chargeVfxFlipX` is the WallBounceRush-facing horizontal flip control for the Charge VFX; cue-level `Flip X` remains available for individual VFX cue overrides in the Workbench.
- WallBounceRush remains hand-only because the sword is forced dropped before the HP50 charge pattern.

## 2026-05-30 - DemonKing Terminal Ending Returns Through Victory GameOver

Decision:
DemonKing terminal ending defaults to a Victory GameOver completion after the Ending Outro instead of loading `TitleScene`. The previous target-scene load remains as an optional/fallback completion mode.

Reason:
The final boss should preserve the roguelike loop and return through the existing run-end UI path. Capturing pending run magic stones before `EndRun(Victory)` lets the Victory GameOver screen display the earned amount while the actual run commit remains owned by the return button flow.

Implications:
- `BossDefeatEndingSequence` shows Victory GameOver after outro and hides the outro view before the GameOver screen.
- Real defeat and victory GameOver screens may open Inventory through a GameOver-owned blocker exception; unrelated UI remains blocked.
- FakeGameOver explicitly disables GameOver inventory access and key-hint HUD.
- Scene/prefab UI remains authored; the existing inventory HUD button is temporarily presented on the GameOver canvas and restored.

## 2026-05-30 - Slime Queen Notion Variant Uses Project NPC Id

Decision:
The Notion Slime Queen source id `1004` is adapted to the project `SlimeQueenBossNpc.asset` id `3001` in the new Melta animated Ink variant. Only the new Slime Queen SpriteLibrary is wired into `SlimeQueenBossNpc.asset`; `primaryInk` and `bossEncounterInk` are left unchanged.

Reason:
Dialogue runtime speaker and face tags resolve against project `NPCData.id`, and no project NPCData uses `1004`. The new Notion dialogue should stay additive and inactive until the user explicitly chooses to swap the active Slime Queen Ink reference.

Implications:
- Notion boss dialogue imports should be normalized to project ids before compile/validation.
- SpriteLibrary labels can be wired independently from active Ink references to make current and future face tags resolve safely.
- `default`, `Normal`, and `Idle` should remain aliased for Slime Queen until the existing placeholder Ink is replaced.

## 2026-05-30 - DemonKing Body Pose Cues Are Pattern-Owned

Decision:
DemonKing body animation beats that designers need to tune should be exposed as `DemonKingBodyAnimationRef` fields on the owning `AL_DemonKing_*` asset. VFX/effect placement remains socket-owned through `DemonKingVfxCueRef` and `DemonKingVfxSocketMap`; body-only poses do not get fake sockets.

Reason:
Pattern phases such as WallBounceRush endpoint pause can share a visual state with another phase but still need independent authoring control. Treating those timings as sockets hides the real issue and makes body pose tuning dependent on unrelated VFX placement.

Implications:
- Pattern Workbench phase rows should show the editable body field that owns each previewed body pose.
- Reused body animations are acceptable only when the runtime timing intentionally shares the same field.
- Optional no-override phases can remain empty, but visible body poses should not be hardcoded in the preview without a corresponding runtime field.

## 2026-05-30 - Selected Top-Down Ellipse Warnings And DemonKing Wall-Safe Charge

Decision:
Selected circular ground warnings render as 70% Y-scale ellipses for the 3/4 top-down read, while unselected circular warnings and ring warnings remain visually circular. Overlap/point-distance damage paths paired with these selected warnings filter their final hit application through the same top-down ellipse; authored collider/timed-hit VFX paths still use their prefab collider shapes. HP50 `WallBounceRush` uses a body-radius wall cast for charge endpoints, and DemonKing keeps `State.Status.KnockbackImmune` active through its lifecycle.

Reason:
Ground-impact warnings should not visually overstate danger in a top-down perspective, but some attacks still read better as true circles or rings. DemonKing should also not cross walls during the charge set piece, and should not be displaced by player knockback effects.

Implications:
- `AttackTelegraphSpec.CreateCircle(...)` remains visually circular. `CreateTopDownCircle(...)` is the opt-in 70% Y-scale helper, `CreateEllipse(...)` remains available for explicit custom sizes, and `CreateRing(...)` remains circular.
- DemonKing warning helpers route their circular ground warnings through the same 70% Y-scale helper. Fallback non-collider circle damage uses the matching ellipse filter, while timed animated VFX collider hits stay collider-authored.
- WallBounceRush body radius and skin width are Inspector-facing pattern fields and need Play Mode tuning against the authored arena walls.

## 2026-05-30 - DemonKing VFX Uses Left-Baseline Socket Tuning

Decision:
DemonKing pattern VFX positions should be tuned through an optional `DemonKingVfxSocketMap`. Socket offsets are authored against the DarkLord sprite's natural left-facing baseline, and X is mirrored when the boss faces right. Child Transform anchors may override numeric offsets; if the socket map is absent, existing fallback positions are preserved.

Reason:
DarkLord patterns need precise hand, foot, eye, sword, and charge output points, but direct scene/prefab YAML editing would make Unity references fragile. A small boss-local socket map keeps this tuning visible in the Inspector and Scene view without introducing a generic manager or changing unrelated bosses.

Implications:
- Timed attack VFX positions and their generated hit colliders move together when a socket is used.
- Socket gizmos are authoring/debug visualization only; actual values still need Unity Inspector and Play Mode review.
- EgoSword keeps its existing held/throw/recall offset fields, with selected-object gizmos added for review rather than a serialization migration.

## 2026-05-30 - DarkLord Body Sorting Stays Entity Zero

Decision:
DarkLord/DemonKing root body rendering should remain `Entity / Order 0` during all patterns. Focus or dimming presentation must not temporarily move the body renderer to the Projectile layer.

Reason:
Moving the body to `Projectile / Order 2` during GroggyCounter made `DemonKingEyeLightVfx` and `DarkLordGroggyReleaseVfx` at `Projectile / Order 1` render behind the body. Keeping the body on Entity makes the layer relationship stable and keeps VFX naturally above the boss.

Implications:
- GroggyCounter world dim uses the Flowering-style policy: a world SpriteRenderer dim panel on `Entity / Order -1`.
- Entity-layer characters are not covered by this dim panel; this is an intentional visual tradeoff for stable body/VFX ordering.
- EyeFlash, GroggyRelease, attacks, projectiles, and other DemonKing spawned VFX continue to use Projectile sorting.

## 2026-05-30 - DemonKing Visual Tuning Uses An Editor Preview Tool

Decision:
DemonKing body clips, one-shot VFX hit windows, pattern AL tuning fields, EgoSword offsets, and VFX sockets should be adjusted through `Tools/DemonKing/Visual Tuning Preview` as an Editor-only authoring tool. The tool edits existing assets/components rather than introducing a new central runtime tuning profile. For runtime-sensitive pattern checks, the Workbench provides a Play Mode-only Actual Pattern Runner that executes the selected `AL_DemonKing_*` through the live DemonKing `AbilitySystem`, plus an optional live scene render mode that shows that Play Mode scene inside the Preview Window.

Reason:
DarkLord/DemonKing tuning values are intentionally split across `.anim` clips, generated Resources VFX prefabs, AbilityLogic ScriptableObject assets, and scene/prefab components. A central runtime profile would require migration and another source of truth, while an Editor-only surface can make the existing ownership visible and previewable.

Implications:
- Synthetic preview instances are hidden temporary objects rendered through a tool camera/RenderTexture and must not mutate scene or prefab contents during playback.
- Actual Pattern Runner is explicitly Play Mode-only and mutates only the live runtime scene state: it can move the boss, spawn real warnings/VFX, apply damage, play sound/shake, and cancel the current DemonKing ability when requested. For tuning, it can also isolate the selected pattern, run it once or in a loop, add a temporary `State.Invulnerable` tag to the player without disabling player input, refresh terminal test state after patterns such as FinalDesperation, and move the player to the DemonKing arena center.
- Live Runtime Preview is also Play Mode-only. It reuses the Workbench RenderTexture camera to frame the live DemonKing/target scene, and should be used for runtime socket/VFX placement checks before final Game view review.
- Composite preview should be used when checking combined body pose, selected VFX, hit-window marker, EgoSword markers, and socket positions in one authoring view.
- `.anim` frame curves and hit-window events are saved only through explicit Apply buttons.
- Pattern, EgoSword, and socket serialized fields are edited through `SerializedObject` with Undo/dirty handling, matching normal Inspector ownership.
- This tool is an authoring convenience, not a gameplay manager or runtime dependency.

## 2026-05-30 - DemonKing Explosions Emit High-Arc Debris

Decision:
`DemonKingExplosionVfx`, `DarkLordExplosion2Vfx`, and `DemonKingImpactVfx` should spawn `PF_ExplosionDebrisBounce_HighArc` at the same world position as an additional visual-only presentation layer.

Reason:
The boss's explosion and impact beats need a stronger top-down ground-hit read without changing damage windows, camera shake, fragment persistence, or pattern timing.

Implications:
- `DemonKingPatternVfx` owns the runtime pairing so every existing explosion/impact call site inherits the debris presentation.
- `PF_ExplosionDebrisBounce_HighArc` is the authored runtime prefab at `Assets/Resources/DemonKing/Vfx`, not a generated mirror. Do not maintain a second HighArc copy with separate tuning.
- The debris emitter must remain visual-only and must not add collision, damage, gameplay tags, or timing gates.

## 2026-05-30 - DemonKing HomingMagic Separates Stock And Fired Projectile VFX

Decision:
DemonKing HomingMagic should present its remaining shots as visual-only stock VFX over the boss, then spawn the real projectile from the consumed stock VFX position with a separate fired-projectile visual prefab.

Reason:
The pattern needs to communicate that five shots are loaded before firing, while keeping projectile movement, lifetime, collision, and damage owned by `DemonKingProjectile2D`.

Implications:
- `AbilityLogic_DemonKingHomingMagic` owns stock VFX layout, target-side-first consumption, recentering, and cleanup.
- `DemonKingProjectile2D.Spawn(...)` keeps the old primitive fallback and accepts an optional child visual prefab for fired projectiles.
- The stock and fired projectile prefabs are Inspector-assigned AbilityLogic slots; scene/prefab YAML should not be hand-edited for this wiring.
- If those slots are empty, `DemonKingPatternVfxAssetBuilder` generated Resources wrappers for `HomingMagicBaltVFX.anim` and `HomingMagicBaltProjectile.anim` are the default fallback.

## 2026-05-30 - DemonKingImpact Spawn Points Own Impact Shake

Decision:
Whenever DemonKing code directly commits a `DemonKingImpactVfx`, the same impact timing should own camera shake unless that impact already routes shake through a timed-hit callback.

Reason:
Impact shake should align with the visible impact frame, but duplicated shake on timed VFX callbacks makes repeated patterns feel noisy and harder to tune.

Implications:
- Bombardment release impact and EgoSword planting impact have their own camera shake hooks because they directly spawn `DemonKingImpactVfx`.
- ExplosionJump landing, WallBounceRush final landing, hand GroggyRecoverCounter, and EgoSword VerticalStrike keep their existing timed-hit/impact shake paths.
- `DarkLordExplosion2`, `DemonKingExplosion`, and `DarkLordGroggyReleaseVfx` keep their separate shake policies.

## 2026-05-30 - Top-Down Debris Bounce Uses Virtual Height

Decision:
Explosion debris bounce VFX should simulate debris on a 2D ground plane with separate virtual height, instead of relying on Unity ParticleSystem gravity/collision to decide where the piece hits the ground.

Reason:
The game is top-down, so screen-down gravity reads like movement across the map rather than falling. Keeping ground XY and virtual height separate lets the effect emit bounce/contact puffs at the intended map contact point while only using a small screen-space offset for the airborne read.

Implications:
- `TopDownDebrisBounceEmitter2D` owns visual-only debris simulation and contact puffs.
- Generated debris-bounce prefabs are presentation content and should be spawned through existing presentation paths.
- Final tuning must be reviewed in Scene/Game view because too much height offset can look like the debris moved north instead of upward.

## 2026-05-30 - Directional Telegraph Fill For Non-Radial Attacks

Decision:
Attack telegraph fill should grow from the attack start/origin toward the attack end for rectangular and sector warnings. Circle and ring warnings keep radial/center-based fill behavior.

Reason:
Directional attacks read more clearly when the warning communicates travel direction or attack reach over time. Center-growing rectangles and sectors make line slashes, rush lanes, and cones feel like they appear from the middle rather than from the attacker or attack start point.

Implications:
- `AttackTelegraphView` keeps sprite-based rectangle and sector fill anchored on the local start edge while scaling progress.
- `AttackTelegraphWallClippedMeshView` keeps rectangle mesh start vertices fixed and scales only the front edge toward the wall-clipped endpoint.
- Circle/ring warnings remain exceptions because their danger expands radially rather than from a directional start edge.

## 2026-05-30 - DemonKing One-Shot VFX Own Hit Windows

Decision:
DemonKing attacks that have concrete generated one-shot VFX should route damage, SFX, and camera shake through `TimedAnimatedHitEffect2D` hit-window callbacks where practical. Warnings use `AttackTelegraphService` specs, while continuous body-contact rushes and laser active windows remain code-driven.

Reason:
The boss's hit feedback needs to line up with visible VFX frames instead of firing at warning completion or coroutine commit time. Reusing the shared timed-hit component keeps frame timing in generated animation clips without adding pattern-specific animation-event relay scripts.

Implications:
- `DemonKingPatternVfxAssetBuilder` is responsible for adding `EnableHitCollision`/`DisableHitCollision` events and timed-hit components to generated one-shot VFX prefabs.
- Runtime pattern code still owns the hit shape/payload because size, damage, and shared-hit policy are pattern-specific.
- Sound and camera shake should fire from hit-window callbacks or direct impact commits, not from warning start.
- Primitive warning duplicates should not be reintroduced; use telegraph specs and reserve primitive visuals for active fallback flashes.
- AbilityLogic assets and `EgoSwordActor` now expose SFX/shake slots that require Unity Inspector review after import.

## 2026-05-30 - Dialogue Text Animation Preview Mirrors Runtime UGUI

Decision:
`Tools/Dialogue/Text Animation Tuner` previews dialogue text animation through a hidden world-space `Canvas + TextMeshProUGUI` rendered by a dedicated preview camera into a `RenderTexture`. It copies the default `GlobalUIRoot.prefab` `DialogueView.dialogueText` rendering settings before applying `DialogueTextAnimationUtility`.

Reason:
The previous world `TextMeshPro` preview used a small font size and arbitrary camera framing, so the same vertex offsets looked much larger than they did in the actual DialoguePanel. Tuning should be based on the runtime UGUI text scale, rect, wrapping, alignment, and font settings.

Implications:
- The shared parser/effect utility remains the source of text animation behavior; the change is preview rendering parity, not Ink syntax or runtime behavior.
- The default preview source is the project DialoguePanel, while a manual `TextMeshProUGUI` source can be assigned for scene/prefab override comparisons.
- Preview font changes are explicit through `Override Preview Font`; by default, the tool uses the DialoguePanel font.
- Source text rects with zero or invalid dimensions are interpreted through the parent Dialogue text container for preview visibility; the prefab/scene RectTransforms are not mutated.
- `PreviewRenderUtility` is avoided for this UGUI preview because the source/effective rects can be valid while CanvasRenderer output still renders black.

## 2026-05-30 - Dialogue Text Animation Uses A Central Profile SO

Decision:
Ink inline TextAnimating tags and `# CameraShake` text motion values are tuned through `DialogueTextAnimationProfileSO`. `DialogueView` may use an optional override, but the normal path loads `Assets/LeeJunMo/Datas/Resources/Dialogue/DefaultDialogueTextAnimationProfile.asset` through `Resources`; if that asset is unavailable, runtime falls back to the previous hardcoded defaults.

Reason:
Writers and designers need to tune `[shake]`, `[tremble]`, `[punch]`, `[wave]`, `[float]`, `[slowshake]`, `[rand_size]`, and CameraShake text motion without changing Ink syntax or editing scene/prefab references.

Implications:
- Existing Ink tags remain valid; this is a tuning-source migration, not an Ink syntax migration.
- `DialogueTextAnimationUtility` is the shared parser/vertex animation surface used by runtime and the editor preview.
- `Tools/Dialogue/Text Animation Tuner` is the intended editor surface for live TMP preview and default profile editing.
- Scene/prefab wiring is optional because the default profile is loaded from `Resources`.

## 2026-05-29 - DemonKing Pattern Body Animation Starts Once Per Pattern

Decision:
DemonKing body pattern animation start playback is limited to one start per Animator state per boss pattern execution. End-frame sampling and frame-control holds remain allowed for hit/recover beats such as `Balt`, `SwordRecover`, and `JumpAttack` frame transitions.

Reason:
DarkLord clips are played directly through a transition-free Animator state library. Repeated `Animator.Play(state, 0, 0f)` calls inside pattern loops made one-shot poses look like they restarted several times instead of reading as a single committed action.

Implications:
- `DemonKingController` owns the per-pattern start-playback record and clears it on pattern end, death, and destroy cleanup.
- Pattern code should use once-per-pattern helpers for full clip starts and preparation poses, while keeping last-frame holds for impact/readability.
- Same-state pose holds must not blindly call `Animator.Play(...)`: if Groggy is already active, hold the current frame, and if a same-state one-shot has already reached the requested end frame, freeze it instead of resampling it.
- `WallBounceRush` now uses linear endpoint movement plus a short endpoint pause for the 50% set piece instead of relying on ease-out arrival feel.

## 2026-05-29 - Dialogue CameraShake Is Ink-Owned Impact Metadata

Decision:
Dialogue impact shake uses a controller-owned `# CameraShake: Low|Middle|High` line tag. `DialogueController` resolves the tag before line playback, and `DialogueView` applies TextBoxGroup shake, DOShake-like per-character TMP impact offsets, dialogue text inertia, and existing `CameraShakeService` camera shake from one preset.

Reason:
Writers need specific story beats to shake both the dialogue presentation and the visible gameplay background without adding scene/prefab wiring. Keeping the tag in the dialogue metadata path matches existing `anim` and `effect` ownership while reusing the established camera shake setting gate.

Implications:
- `Middle` is the only supported medium-strength spelling; `Medium` is intentionally unsupported.
- The camera component respects the player's screen-shake setting because `CameraShakeService` is called without `ignoreScreenShakeSetting`.
- Dialogue UI shake still plays even when gameplay screen shake is disabled.
- Low/Middle/High motion values, per-character impact offsets, and the global intensity multiplier now live in `DialogueTextAnimationProfileSO`; legacy `DialogueView` fields remain only as hidden fallback data.

## 2026-05-29 - Drunk Dialogue Uses Scoped TextAnimating Tags

Decision:
Drunk/slurred dialogue should use scoped TextAnimating tags instead of manually wrapping individual characters in many TMP `<size>` tags. `[rand_size=min,max]...[/rand_size]` assigns each visible character in the range a stable pseudo-random scale, clamped to 80%-120%, and `[slowshake]...[/slowshake]` adds a low-speed per-character shake.

Reason:
Writers need to mark the text area that should feel tipsy, then define a restrained size range, without turning the Ink source into unreadable per-character rich text. Keeping this in the existing inline text-effect parser preserves the typewriter path and allows the NPC Hub validator to reason about the tags.

Implications:
- Use these tags only on short drunk/slurred phrases, not whole paragraphs.
- `Spiri_Dragon` and `Spiri_Drink` lines can stay outside this drunk delivery rule; `Spiri_Drink` may still use restrained SFX size emphasis.
- The random size is deterministic per character for a line, so it reads as uneven vocal pitch without flickering every frame.
- `[slowshake]` is a scoped text effect, not a line-level `# CameraShake` replacement.

## 2026-05-29 - Dialogue Background Effect Switch Is Ink-Owned

Decision:
Dialogue background Effect changes during Ink playback use a controller-owned `# effect: <target>` line tag. Supported targets are an NPC id such as `1005`, `speaker`, and `default`.

Reason:
The Effect is already authored on `NPCData.DialogueTheme.effectOverride`, and writers need to change it at specific story beats without changing the active speaker or adding scene/prefab wiring. Keeping it as Ink metadata matches the existing `anim` and `face` tag style while preserving the current Dialogue UI ownership.

Implications:
- `DialogueController` applies the Effect tag after `speaker` processing and before line text playback.
- `DialogueView` exposes an Effect-only theme apply method so textbox and speaker-frame colors remain controlled by the speaker theme.
- Mid-dialogue Effect switching currently swaps the AnimatorOverrideController only; replaying the DialogueEffect `Intro` state is a planned follow-up.

## 2026-05-29 - DemonKing Held EgoSword Is Body-Only

Decision:
When DemonKing is in sword-held mode, the authored scene `EgoSwordActor` is kept hidden/inactive and the held sword is represented by the DemonKing body animation. The actor is reactivated when the `DarkLord_Sword_Throwing` release frame (`Throw_1`) begins, and recall holds the first `DarkLord_Hand_SwordRecover` frame while the sword lifts like VerticalStrike and then returns to `SwordThrowReturnOrigin` before showing the final recover frame. The 50% `WallBounceRush` requires dropped-sword mode, so HP50 selection forces `ThrowEgoSword` first when the sword is still held.

Reason:
The boss concept now treats the held weapon as part of the DemonKing sprite sheet, not as a separate follower object. Using clip length/frame-rate timing keeps sprite frame edits local to the clip and avoids adding animation-event dependencies for this pattern handoff.

Implications:
- `EgoSwordActor` remains a scene-authored reusable actor, not a runtime-instantiated prefab.
- The DemonKing Animator must use the DarkLord state-library controller for visual timing to match the intended throw/recover frames.
- `WallBounceRush` presentation is hand-only because it is selected only after `ThrowEgoSword`; do not reintroduce sword DashStab/DashStabReady branches for the HP50 charge set piece.
- Future `DarkLord_Sword_Throwing` or `DarkLord_Hand_SwordRecover` state renames must update the `DemonKingController` constants and mapping helpers.

## 2026-05-29 - ScenePortal Entrance Presentation Does Not Change Travel Semantics

Decision:
`ScenePortal` can play a short player pull-in presentation before calling `ScenePortalTravelService.TryTravel(...)`, but route resolution, run state mutation, transition context preparation, and player runtime capture remain owned by the existing portal travel service path.

Reason:
The requested effect is a local interaction presentation, not a new transition semantic. Reusing the compatibility travel entry point keeps hub, corridor, and boss reward portals on the same route/runtime-state path while allowing the player to visually move into the portal first.

Implications:
- `ScenePortal` releases `PlayerCinematicProtection` and `GameFlowInputBlocker` before `TryTravel(...)` so temporary cinematic/UI control tags are not captured into `PlayerRuntimeState`.
- `TutorialScenePortal` remains unchanged for tutorial-only direct scene travel.
- Portal center offsets are scene/prefab-facing tuning fields; they should be reviewed in Unity when a portal pivot is not the desired pull-in center.

## 2026-05-29 - Flow-Owned Presentations Block ESC Pause

Decision:
Flow-owned presentations acquire `GameFlowInputBlocker` while they own camera framing, letterbox, player control locks, fullscreen UI, dialogue handoff, scene transition, or reward reveal timing. Short combat VFX, pattern visuals, hover/button/tooltip animations, and stable stack UI close behavior do not get this blocker.

Reason:
ESC pause should not interrupt authored flow timing, but blocking every class named `Presentation` would make ordinary combat feedback and already-open UI feel unresponsive. The existing blocker keeps the rule centralized in `UIManager` without adding a new manager or prefab/schema dependency.

Implications:
- New flow-owned presentations should wrap only the protected window and release from normal completion plus disable/destroy cleanup.
- Stack UI close paths such as Pause, Settings, KeyBinding, Inventory, Chest, Reward, Upgrade, and Encyclopedia keep their normal ESC behavior when the game is in a stable UI state.
- Flow-owned UI that must open during a block should use `TryPushOwnedUI` or `TryPushFlowOwnedUI`.

## 2026-05-28 - NPC Customization Hub Edits NPCData Assets First

Decision:
NPC-related customization starts from one editor hub under `Tools/NPC/NPC Customization Hub`. V1 edits existing `NPCData` and selected `NPCDatabase` asset membership only, while scene/prefab usage and RunSpecial NPC assets are read-only validation surfaces.

Reason:
NPC dialogue, portrait, theme, affection, and Ink references are shared asset data and can be safely edited through `SerializedObject`, Undo, and `AssetDatabase.SaveAssets`. Scene components, prefab wiring, and RunSpecial interaction data have higher authoring/reference risk, so V1 should expose them for review before allowing mutation.

Implications:
- Do not use the hub as an automatic scene/prefab rewiring tool in V1.
- New NPCData creation remains out of scope until the existing asset review and template Ink flow are stable.
- RunSpecial NPCs stay visibly separate from Ink portrait dialogue even when they appear in the same NPC customization window.

## 2026-05-28 - Dialogue Rhythm Is Timing-First And SpeechBubble Opt-In

Decision:
Dialogue text rhythm uses per-character TMP visibility, punctuation pauses, line-level `anim` tags, inline `[pause=seconds]` markers, and scoped text-motion tags such as `[shake]`, `[tremble]`, `[punch]`, `[wave]`, and `[float]`. SpeechBubble reveal support exists only through explicit animated APIs; normal `Speak(...)` callers keep the existing DOTween behavior.

Reason:
Dialogue delivery needs pacing, pauses, and restrained emphasis more than global motion effects. Keeping SpeechBubble animation opt-in avoids changing normal boss barks, run-special NPC lines, and other authored bubble timings.

Implications:
- Ink dialogue can use `# anim: normal|slow|angry|whisper|cold`, `[pause=0.45]`, and scoped motion tags without changing the dialogue controller call site for ordinary lines.
- `DialogueTagHandler` treats `anim` tags as controller-owned presentation metadata, not unknown gameplay tags.
- Terminal boss death can explicitly route its pre-ending SpeechBubble through the new animated reveal path while ordinary boss SpeechBubble calls remain unchanged.

## 2026-05-28 - Animated Ink Variants Are Additive Review Copies

Decision:
Existing Ink files remain unchanged when adding dialogue rhythm passes. Animated versions live as clearly named additive copies under `Assets/LeeJunMo/Datas/Inks/AnimatedVariants/` with matching compiled JSON files.

Reason:
Several current Ink assets have provisional names such as `New Ink` and are already referenced by scenes/tools. Additive copies make the new timing and motion pass reviewable without breaking existing serialized TextAsset references.

Implications:
- Wire animated variants intentionally through existing Editor authoring tools or scene references after Unity import review.
- Keep original `.ink/json` assets available as rollback/reference content until the owning scene or tool is explicitly migrated.
- Use only supported `DialogueAnimType` values in variants unless the runtime enum is expanded first.

## 2026-05-28 - Normal Boss Routes Are The Default Reward Path

Decision:
The run route catalog uses three normal boss RouteSets (`ShadowCorridorBossRouteSet`, `Drunken_Dragon_Spili_CorridorBossRouteSet`, and `SlimeRouteSet`) followed by `DemonkingRouteSet` as the single final RouteSet. Direct boss-scene Play uses the full catalog route order deterministically and points `currentStageIndex` at the scene being tested, instead of creating a one-stage plan.

Reason:
Normal bosses are repeated route content and should keep the ordinary chest plus portal reward path. Final-route behavior is the exceptional terminal/final-boss policy. A one-stage direct Play plan made normal boss scenes look like the last stage and obscured reward diagnosis.

Implications:
- `RunRouteCatalog.asset` should keep `normalStageCount = 3`, `allowDuplicateNormalRoutes = false`, the three normal RouteSets, and `DemonkingRouteSet` as final.
- Runtime hub-start route generation still randomizes the normal route order according to the existing catalog policy.
- Editor direct Play uses catalog order only for deterministic local testing and route-context debugging.
- `BossRewardRevealSceneValidatorWindow` validates and can reapply the route catalog defaults alongside the target scene reveal setup.

## 2026-05-28 - Reward Gate Reveal Isolates Global Vision Masks

Decision:
When a boss reward portal reveal runs in a scene with `GlobalVisionMaskRoot`, `BossRewardObjectRevealPresentation` can temporarily narrow active global vision `SpriteMask` ranges to the dark overlay renderer's sorting range. This keeps the boss gimmick overlay masked while preventing the reward portal renderer from being clipped by the player vision mask during its local gate reveal.

Reason:
Unity `SpriteMask` interaction is sorting-range based. The reward gate reveal uses `VisibleInsideMask` on portal renderers for the local reveal mask, so the same renderers can unintentionally react to the scene's global player vision mask.

Implications:
- The target-scene apply tool enables `isolateGlobalVisionMasksDuringReveal` for portal reveal components.
- The reveal component defaults this option to enabled, but only applies it when the reveal actually uses a SpriteMask or masked renderers.
- Global vision mask ranges are restored when reveal completes, is stopped, or the reveal component is disabled.
- Player-following masks marked by `PlayerVisionMaskFollower` are excluded from this isolation so Shadow boss reward reveal does not make the player Light/vision mask appear disabled.
- `HeoMinSeok_Boss` needs Play Mode review because it combines the reward portal reveal with the boss gimmick `GlobalVisionMaskRoot`.

## 2026-05-28 - Global Ending Outro View Owns Runtime Closed Startup

Decision:
`EndingOutroView` snaps itself hidden during runtime startup when its authored root is active, while `Show(...)` marks intentional playback activation so inactive authored views can still be enabled and played normally.

Reason:
The ending outro view can be authored under persistent `GlobalUIRoot` without a colocated `EndingOutroPlayer`. If the shared `IntroOverlay` is left active for editing, relying only on `EndingOutroPlayer.hideViewOnAwake` lets the fullscreen overlay appear as soon as Play Mode starts.

Implications:
- Fullscreen ending outro UI can remain authored in the global UI prefab without requiring the root to be manually inactive after every edit.
- Real outro playback still activates inactive roots through `EndingOutroView.Show(...)`.
- This is a runtime state guard only; scene/prefab authoring should still keep presentation roots inactive when practical.

## 2026-05-28 - Boss Reward Reveal Ownership Is Split By Object

Decision:
Normal boss reward chests own their simple dust reveal directly through `TreasureChest.PlayRewardReveal()`. Reward exit portals own the mask, rise, dust loop/burst, collider lock, global vision mask isolation, camera shake, and local tremble through the optional `BossRewardObjectRevealPresentation` scene component. If no portal reveal component is present, the portal still activates immediately as before.

Reason:
Chest reveal is only a one-shot dust particle and does not need the portal reveal component's mask, movement, collider lock, camera shake, or global vision mask isolation fields. Keeping chest dust on `TreasureChest` and portal reveal on a portal scene component keeps each object's presentation API proportional to its behavior.

Implications:
- `BossBattleEndHandler` and `BossEncounterEndDirector` invoke `TreasureChest.PlayRewardReveal()` only after existing chest initialization succeeds.
- `BossRewardSpawnService` activates the referenced chest before boss loot generation so presentation visibility is not blocked by a later loot-content exception.
- `BossBattleEndHandler` and `BossEncounterEndDirector` invoke `BossRewardObjectRevealPresentation.PlayReveal()` only after portal visibility restoration succeeds.
- Portal reveal sub-effects share the same reveal timeline: mask/collider lock spans the reveal, loop dust and local/root loop shake run during the reveal, burst dust and completion camera shake run on reveal completion.
- Final-route chest suppression and terminal `BossDefeatEndingSequence` reward suppression stay unchanged.
- Unity scene authoring must wire TreasureChest reward dust fields for chest dust, and portal reveal root, SpriteRenderers, particles, and colliders for the gate reveal.

## 2026-05-28 - Target Boss Reward Scenes Use Editor Apply Tool

Decision:
`HeoMinSeok_Boss`, `HeoMinSeok_Boss_Dragon_Spili`, and `SangHyup_Boss_SlimeQueen` should use `BossEncounterEndDirector` as the canonical active reward owner and use the Unity Editor apply tool for first-pass reveal wiring. The tool uses Editor scene APIs, not handwritten YAML, and disables a duplicate `BossBattleEndHandler` only when it references the same chest and portal as the director.

Reason:
The target scenes already carry both director and legacy handler components referencing the same authored chest and portal. A repeatable Editor apply path keeps the scene-instance reveal setup consistent while avoiding duplicate reward ownership and avoiding shared prefab changes.

Implications:
- Run `Tools/Validation/Apply Boss Reward Reveal Target Scene Setup` after Unity recompiles the Editor script, then review and save the scene changes.
- If an enabled `BossBattleEndHandler` references different objects, the tool leaves it enabled and reports it for manual review.
- Generated mask/dust helpers are first-pass authoring defaults; visual timing, sorting, and positions still require Play Mode review.

## 2026-05-28 - Terminal Outro Uses Live GlobalUIRoot View Resolution

Decision:
The terminal boss ending outro does not trust a stale scene-instance `EndingOutroView` reference at playback time. `EndingOutroPlayer` first reuses its assigned view when it is alive and ready, then resolves a ready view under the active `GlobalUIRoot`, and only then falls back to a single unambiguous scene view.

Reason:
Playable scenes each author a `GlobalUIRoot` for direct scene testing, but at runtime the root is persistent and duplicates destroy themselves. A boss scene reached from Hub or Corridor can therefore lose serialized references to UI children on its duplicate root even though the persistent root still has the authored outro view.

Implications:
- Cross-scene terminal endings can play the same authored outro UI whether the boss scene is launched directly or reached from another scene.
- The outro player still uses authored UI only; it does not create Canvas, TMP, Image, or presentation hierarchy at runtime.
- Ending outro player control lock spans the outro and accepted TitleScene transition startup so gameplay input does not resume behind the fullscreen panel.

## 2026-05-28 - Boss Choice Failure Effect Requires Add-Affection Sibling

Decision:
Boss dialogue choice failure presentation is automatic only when the current choice set contains at least one `add_aff` choice and the selected choice does not carry `add_aff`. Explicit `choice_fail`, `aff_fail`, and `fail_aff` tags still force the failure effect.

Reason:
Some boss choice sets are weak story branches with no affection reward. Treating every non-`add_aff` boss choice result as failure made both sides of simple branches look like failed answers.

Implications:
- Ink authors can make a success/failure pair by putting `# add_aff: 1` in the success choice body and leaving the opposite choice without `add_aff`.
- Choice sets with no `add_aff` tags do not show the failure effect unless an explicit failure tag is authored.
- The runtime previews the first result tags of the current choice set from a cloned Ink story state so body-level `add_aff` tags are detected without advancing the real story.

## 2026-05-28 - Dialogue Hides Non-Dialogue UI Through Fade Suppression

Decision:
`DialogueService` owns the common non-dialogue UI suppression boundary for Ink dialogue and exposes owner-token suppression for outer flows that need the same hidden UI window before or after Dialogue playback. The suppressed layers are `GameplayHUD`, `Popup`, `Hover`, `Prompt`, `Reward`, `DamagePopup`, and `BossHUD`; `Dialogue`, `GameOver`, and `Loading` are never suppressed by this path.

Reason:
Dialogue should stay readable without gameplay HUD or boss HP noise, but instant `SetActive` toggles made the transition abrupt and let the terminal boss ending briefly restore HUD between death SpeechBubble, Dialogue, and outro. Owner-token suppression lets the terminal death flow hold the UI hidden across multiple presentation beats while normal Dialogue still restores the original UI state when it ends.

Implications:
- Active UI roots fade out through `CanvasGroup` before deactivation and fade back in to their captured alpha when the final suppression owner releases.
- Terminal boss death acquires suppression before the death SpeechBubble and releases it without restore only after a TitleScene transition has started, so HUD does not reappear behind the ending outro or transition fade.
- If a terminal handoff fails or is interrupted before scene transition starts, the same suppression owner is released with restore so gameplay UI returns.
- Flow-owned reward presentation may reactivate the authored `RewardCanvas` and non-raycasting `HoverCanvas` through `DialogueService`'s captured-layer temporary visibility boundary while Dialogue is still waiting for a reward callback; the Reward panel and its item detail hover are part of that dialogue reward flow, not ambient HUD noise.

## 2026-05-28 - DarkLord Hub Intro Uses Completion And Seen Gates

Decision:
The first Hub introduction after `DarkLord_Tutorial` is gated by `darklord_tutorial_forced_defeat_completed` and `hub_intro_after_darklord_seen`, with editor-only test bypass routed through the same `HubIntroProgressGate` helper used by both the Hub fall presentation and the Hub intro sequence.

Reason:
The sequence is both a first-Hub introduction and the continuation of the forced-loss tutorial boss flow. Requiring the DarkLord completion flag prevents normal first Hub entry from accidentally playing the post-defeat version, while the seen flag keeps it one-shot per profile.

Implications:
- `TutorialBossEncounterSequence` records the forced-defeat completion before fake GameOver returns to Hub.
- `PlayerHubSpawnPresentation2D` and `HubIntroAfterDarkLordSequence` must use matching completion/seen ids and matching editor-bypass settings for direct Hub testing.
- The Hub introduction remains scene-authored through serialized SpeechBubble, camera focus, and Dialogue references; scene/prefab wiring and Unity import validation are required before playtest sign-off.

## 2026-05-27 - DarkLord Tutorial Scene Authoring Uses Editor Menu

Decision:
`DarkLord_Tutorial` scene placement and serialized wiring for the tutorial boss presentation is performed through `Tools/Tutorial/DarkLord Tutorial/Apply Default Authoring To Active Scene`, with validation through the paired `Validate Active Scene` menu.

Reason:
The scene needs multiple authored objects, focus targets, laser origins, tutorial HP UI, Ink references, and disabled real-boss-start flags, but direct scene YAML edits are too risky for Unity object references. A scoped Editor menu keeps placement repeatable while leaving final scene save/review under Unity Editor control.

Implications:
- The menu only mutates the active `DarkLord_Tutorial` scene outside Play Mode and marks the scene dirty for the user to save.
- Compiled Ink `.json` assets are still produced by the Unity Ink import pipeline; the menu warns/validation reports when those generated TextAssets are not ready yet.
- Scene View review remains required for camera framing, HP safe-area placement, and laser angle fine tuning after the automated pass.

## 2026-05-27 - Tutorial Boss Dialogue Stays Outside Letterbox

Decision:
`TutorialBossEncounterSequence` keeps cinematic letterbox bars off while Ink dialogue is visible, and uses the bars only for non-dialogue presentation beats such as scripted lasers and the collapse/return-to-player moment.

Reason:
The authored Dialogue UI can occupy the same vertical screen space as the letterbox bars. Keeping dialogue outside the letterbox avoids hiding speaker names, choices, or dialogue body text while preserving cinematic framing during laser/failure presentation.

Implications:
- Tutorial boss Ink should use `# speaker: ...` tags for speaker names and should not include visible speaker prefixes such as `마왕:` in line text.
- A hidden or temporary speaker can be shown through a string speaker tag such as `# speaker: ???`.
- Player input and targetability protection still spans the full sequence, including both dialogue and laser sections.

## 2026-05-27 - Tutorial Chest Open Handoff Uses Chest UI Success Event

Decision:
Tutorial chest-open continuation is driven by `TreasureChest` UI-open success events and a scene-authored `TutorialChestOpenedTrigger` bridge.

Reason:
The combat tutorial flow needs to continue after the player actually opens the reward chest, but `TutorialSceneSequenceDirector.NotifyChestOpened()` previously had no reliable source event. Emitting from `TreasureChest` only after `ChestUIManager.OpenChest(...)` succeeds keeps the tutorial handoff aligned with the visible chest UI instead of the interaction request or world open prelude.

Implications:
- `TreasureChest.OpenedUi` can report repeated successful UI opens, while `FirstOpenedUi` reports only the first successful UI open.
- `TutorialChestOpenedTrigger` defaults to first-open-only and one-shot so tutorial steps do not replay when the same chest UI is reopened.
- Scene authoring should wire `TutorialSceneSequenceDirector.OnMonstersCleared` to the chest-open tutorial prompt, and `TutorialChestOpenedTrigger.OnChestOpened` to `TutorialSceneSequenceDirector.NotifyChestOpened()` or the next tutorial step.

## 2026-05-27 - Boss Terminal Ending Replaces Reward Portal Flow Per Boss

Decision:
Boss defeat ending is an explicit scene-authored terminal flow on the selected boss: death speech bubble, Ink dialogue, ending outro, `RunEndReason.Victory`, then `TitleScene`. It reuses the boss death presentation handoff but does not run normal reward-ready, chest, or portal activation for that selected flow.

Reason:
The ending is story/session completion, not a normal battle-end reward beat. Making it opt-in per boss avoids changing ordinary boss reward and portal behavior while still letting a final/story boss move directly from defeat presentation into ending content.

Implications:
- `BossDefeatEndingSequence` is authored only on bosses that should end the run.
- `EndingOutroSequenceSO`, `EndingOutroView`, and `EndingOutroPlayer` stay separate from TitleIntro so title launch timing remains isolated.
- Terminal death presentation removes the letterbox before post-speech Dialogue starts, but keeps the camera focused on the boss through terminal Dialogue and outro. The camera is restored only if the terminal handoff fails or is interrupted before completion.
- Terminal ending scene load uses terminal-flow fade-out and TitleScene fade-in durations owned by `BossDefeatEndingSequence`, leaving ordinary scene transition timings unchanged.
- Normal bosses without this sequence continue through `BossBattleEndHandler` / `BossEncounterEndDirector` reward and portal handling.

## 2026-05-27 - Combat Tutorial Intro Owns Temporary Presentation State

Decision:
The attack/skill combat tutorial intro is owned by a scene-authored `TutorialCombatIntroSequence` triggered after `TutorialDoorClosedTrigger`, while the tutorial-only HP safety is owned by `TutorialPlayerHealthAutoRecover`.

Reason:
The design needs a temporary presentation window after the door fully closes: focus the camera on the monster/chest composition, prevent player control and enemy target acquisition, show the attack/skill tutorial, return the camera to the player, then restore normal combat. Keeping HP auto-recovery scene-local avoids changing shared damage/death rules for a tutorial exception.

Implications:
- `DoorObject` remains generic and only reports close-presentation completion.
- `TutorialCombatIntroSequence` uses existing player protection, targetability blocking, cinematic letterbox, and gameplay camera ownership patterns instead of adding a manager.
- Combat tutorial letterbox bars are enabled by default, while global UI fading is opt-in so the attack/skill tutorial panel is not accidentally hidden by the prompt/dialogue layer fade.
- Scene authoring must provide the monster/chest camera focus marker, attack/skill `TutorialInfoTrigger`, and optional gameplay-released UnityEvents.
- `TutorialPlayerHealthAutoRecover` should be present only in the tutorial scene that requires instant HP restoration; its death-return suspension is a scene-local safety guard, not a global combat rule.

## 2026-05-27 - Room Enemy Navigation Uses Active Spawn Room Registration

Decision:
Room-level enemy navigation is driven by the currently active `MonsterSpawnRoomGroup`, reads its alive spawn/lock-registered monsters, and renders arrows through a scene-level world `SpriteRenderer` overlay using the existing kill-lock arrow prefab.

Reason:
The guidance should match room/chest lock semantics instead of scanning every `Enemy` in the scene. Keeping the arrows as a world overlay avoids adding UGUI hierarchy and lets the existing chest arrow visual stay consistent while changing only the placement rule to camera viewport edges.

Implications:
- `RoomEncounterEntryTrigger2D -> MonsterSpawnRoomGroup` remains the active-room signal.
- Spawn-registered roots and Slime split descendants count because they enter through the room group's registration path.
- General direct summons remain excluded unless they explicitly use the same registration path.
- Scene authoring must provide one `RoomEnemyNavigationOverlay` and assign `KillLockMonsterNavigationArrow.prefab`; the validation menu can create/wire the opened scene instance through UnityEditor APIs.

## 2026-05-27 - Tutorial Boss Portal Uses Direct Scene Portal

Decision:
Tutorial-only fixed scene travel, such as `TutorialCorridor` to `DarkLord_Tutorial`, uses `TutorialScenePortal` instead of adding a tutorial-specific `TransitionType` to the run route system. The direct portal still captures current player runtime state and prepares a minimal destination transition context so spawned tutorial scenes can restore inventory/loadout state.

Reason:
`TransitionType` currently expresses run-route semantics resolved by `PortalRouteManager` and `RunRouteCatalogSO`. The tutorial boss handoff is a fixed authored scene jump, not a run-progress route transition, so adding a global transition enum would mix tutorial-specific routing into the normal run plan.

Implications:
- `ScenePortal` remains owned by normal run route travel.
- `TutorialScenePortal` loads a serialized target scene through `SceneTransitionCoordinator` with direct `SceneManager.LoadScene(...)` fallback, without using `PortalRouteManager`.
- `TutorialScenePortal` preserves the current player runtime state by default; enable `resetPlayerRuntimeStateOnTravel` only for a tutorial jump that intentionally discards the current loadout.
- Fixed tutorial destination scenes must be present in BuildSettings.
- If tutorial scene travel later needs run progress, reward, or route-plan behavior, promote it into the route system deliberately instead of expanding the direct portal.

## 2026-05-27 - New Profiles Enter Tutorial Corridor

Decision:
Empty-slot `StartNewRun` launches use `TitleProfileSlotService.newProfileTargetSceneName`, defaulting to `TutorialCorridor`, while existing-slot `ContinueRun` keeps using `targetSceneName`.

Reason:
The title intro is still a presentation gate and should not hard-code its own destination, but a first-time profile now needs to enter the tutorial scene after the intro instead of going straight to the hub. Keeping the split inside `TitleProfileSlotService` preserves the existing launch request path while allowing continue/default launch behavior to remain separate.

Implications:
- `TitleIntroPlayer` still does not own scene selection.
- `TutorialCorridor` must remain in BuildSettings for player builds.
- TitleScene Inspector can clear or change `newProfileTargetSceneName` if the first-time profile route changes later.
- Tutorial scene startup beats should be owned by scene-authored tutorial components, not title UI.

## 2026-05-26 - Demon King Threshold Patterns Own Temporary Damage Rules

Decision:
Demon King threshold set pieces apply their exceptional damage rules only while the set piece needs them: the 50% WallBounceRush owns a temporary `State.Status.StaggerImmune` guard, and the 10% FinalDesperation owns `State.Status.StaggerImmune` for the full final-pattern lifetime while its HP clamp only lasts until the center move finishes and the attack loop begins. FinalDesperation is the terminal threshold pattern and suppresses the 50% WallBounceRush when HP is already at or below the final threshold. If the final threshold is crossed during Groggy, FinalDesperation immediately ends the Groggy effect/tag and starts through a forced pattern reservation instead of waiting for Groggy recovery.

Reason:
The 50% rush and 10% final pattern should not be interrupted or weakened by stagger buildup during their scripted threshold set pieces, and the 10% phase should not be skipped by a large hit, delayed behind a lower-priority threshold set piece, or delayed by an already-active Groggy state. FinalDesperation also needs a center-position reset followed by a no-damage map-edge knockback before the attack loop starts. Once FinalDesperation attacks begin, normal damage and death rules should resume so the fight can end cleanly, but stagger buildup remains suppressed for the final loop.

Implications:
- Future Demon King threshold patterns should acquire and release their own temporary tags or HP guards instead of changing global combat damage behavior.
- FinalDesperation entry validation must include both the center-move HP clamp release point and the final-pattern `StaggerImmune` release path.
- Marking FinalDesperation started also consumes/suppresses the 50% pattern so a direct drop to 10% cannot play WallBounceRush first.
- FinalDesperation force-start must bypass normal selection gates and clear active Groggy state so the reactive Groggy FSM transition cannot reclaim control for the remaining Groggy duration.
- Scene/Inspector tuning can change threshold ratios, but the clamp lifetime remains code-owned unless the phase design changes.

## 2026-05-26 - Flowering Bloom Locks Active Weapon Changes

Decision:
While `Flowering` Skill1 Bloom activation is running, active weapon changes are rejected by `WeaponInventory2D`. The lock starts before the cut-in and is released only after Bloom active duration, reveal-out, cancellation, or scene cleanup finishes.

Reason:
Bloom owns player-root runtime state, cut-in presentation, weapon reveal state, dash slash augment, HUD duration projection, and a long-lived ability coroutine. Swapping or replacing the active weapon mid-flow can force weapon cleanup and leave the Bloom presentation/runtime lifecycle out of sequence.

Implications:
- Swap input, direct equip, active drop/unequip/destroy, active slot replacement, and active slot inventory swap all respect the Flowering lock.
- Offhand/non-active slot changes can still proceed because they do not tear down the active Flowering runtime.
- The lock is runtime-only and adds no serialized fields.

## 2026-05-26 - Tutorial Default Weapon Is Scene-Local

Decision:
Tutorial scenes that must force a basic starting weapon use a scene-authored `TutorialDefaultWeaponBootstrap` instead of changing global player spawn, save restore, or normal run loadout behavior.

Reason:
The tutorial needs a deterministic starting weapon, but that rule should not leak into normal corridor, hub, or boss entry. Running through `WeaponInventory2D.TrySetWeaponSlot(...)` and `Equip(...)` keeps weapon cleanup, ability ownership, stats, presentation, and inventory UI events on the existing weapon inventory path.

Implications:
- Tutorial scene authoring assigns the default `WeaponDefinition` directly on the bootstrap component.
- Other weapon slots can be cleared by the bootstrap when the tutorial must start with only the default weapon.
- The bootstrap changes the live runtime inventory for that session; if tutorial completion should transition into a different loadout policy, that later transition must reset or rebuild run state explicitly.

## 2026-05-26 - Weapon Detail SFX Stay In Logic Data

Decision:
Weapon-specific sub-timing sounds for Flowering and Lightning Spear are authored on the weapon logic data that owns the timing, while `AbilityDefinition` audio fields remain the broad cast/commit/end phase slots.

Reason:
BloomSlash, cut-in reveal, dash slash, MarkRush, recovered spear, and MarkRain timings are not shared ability lifecycle phases. Keeping those `SoundRef` slots beside the timing data lets designers tune them without adding a new manager or overloading generic `AbilityDefinition` audio.

Implications:
- Weapon logic should route these detail sounds through `AbilityAudioRouter.PlayOneShotAtPosition(...)` so world-positioned effects use the same playback context shape.
- Empty `SoundRef` values remain no-op defaults; adding the slots must not change existing sound behavior until keys are authored.
- Basic attack step sounds and common `AbilityDefinition` audio slots remain separate and can intentionally overlap if both are configured.

## 2026-05-26 - Common Demo Relics Stay Data-Centered

Decision:
Pre-demo common relic expansion is implemented as data-centered `RelicDefinition` / `RelicLogic` assets with small reusable runtime logic only for generic timed-event and health-ratio stat modifiers. Boss-target-specific and critical-kill relics stay out of this slice.

Reason:
The June 2 demo needs many stable, drop-ready relics without widening combat damage routing, boss identity checks, or kill-event payload contracts. Keeping the v1 relics at `maxLevel = 1`, `dropLevel = 1`, and default-unlocked reduces balance and verification load.

Implications:
- `결투자의 인장` remains deferred until boss target classification and damage calculation routing are reviewed.
- `처형자의 동전` remains deferred until `KillConfirmed` or an equivalent event carries critical-hit context.
- New pre-demo common relics should prefer existing stat attributes and shared `RelicLogic` assets over one-off code.

## 2026-05-26 - Tutorial Boss Encounter Uses Presentation-Only Failure

Decision:
Tutorial boss encounter support uses scene-authored presentation scripts for lasers, HP loss, collapse, and fake game-over. The scripted lasers reduce the tutorial presentation HP view through `ITutorialPresentationHpView` only, and fake game-over calls `GameOverPresentationPlayback.TryShow(...)` with `EndRunOnReturn = false`.

Reason:
The tutorial needs to teach a forced failure beat without mutating real player HP, triggering real death components, or ending the active run. Keeping the sequence presentation-only lets the same authored UI and game-over presentation be reused while avoiding save/runtime side effects.

Implications:
- Tutorial boss Ink uses a dedicated `NPCData` plus explicit first/second Ink TextAssets on `TutorialBossEncounterSequence`; it does not use `BossDialogueRunner` encounter progress.
- Hit, collapse, sound, and VFX timing are exposed as UnityEvents for scene authoring.
- Real damage/death/run-end systems should not be connected to the tutorial laser flow unless a later design explicitly changes the tutorial from scripted presentation to real combat.

## 2026-05-26 - Flowering Bloom Cut-in Reveal Uses Weapon Animation Event

Decision:
`Flowering` Skill1 Bloom cut-in now plays the weapon Animator `Skill1` trigger during the fade-in cut-in and starts the weapon reveal from the `Event.Anim.Flowering.WeaponReveal` animation event, following the Lightning Spear Skill2 pattern of data-owned trigger and event tag timing.

Reason:
The Bloom reveal should line up with the authored weapon swing animation instead of starting immediately from cut-in code. Lightning Spear Skill2 already uses an authored weapon animation event to time gameplay/presentation work, and matching that shape keeps the timing editable in the animation clip.

Implications:
- `FloweringBloomData` owns the cut-in animation trigger, reveal event tag, timeout, and fallback delay.
- The cut-in path temporarily runs the weapon Animator with unscaled time because the Bloom cut-in pauses combat time.
- The cut-in path uses `WeaponAimPresentationSettings` with `FacingSideOnly` so the Skill1 weapon motion ignores aim angle and only mirrors left/right. `FacingSideOnly` locks the cast-time side instead of following live aim changes during the animation.
- The cut-in path acquires a `GameFlowInputBlocker` before pausing combat time so unrelated UI freeze owners, such as pause menu entry, cannot stack on top of the Bloom cut-in and restore a stale `Time.timeScale = 0`.
- The final shake timing also spawns the cut-in completion particle at the player visual center, using the same cleanup/lifetime fallback path as other Flowering cut-in particles.
- The reveal waits on a Flowering-specific animation event tag so the previous SwordSkill2 event reference no longer drives Flowering reveal timing.

## 2026-05-25 - Flowering Bloom Attack Uses Single World-Lingering Slash Activations

Decision:
`Flowering` Bloom attack stays separate from normal combo state, but each `AD_FloweringAttack_Bloom` activation now creates exactly one hitbox-owned BloomSlash visual in world space. Rapid repeated activations provide the Bloom attack sequence; the ability no longer auto-spawns a multi-hit flurry inside one activation.

Reason:
Bloom attack should feel like a consistent rapid slash sequence where each slash remains visible long enough to read as a world-positioned slash mark. Bundling several hits inside one activation made timing harder to tune and made attached hitbox visuals visually follow the player instead of lingering at the strike location.

Implications:
- `AbilityLogic_FloweringAttack` stores the last Bloom hitbox variant index on the `AbilitySpec`, so immediate visual repeat avoidance works across repeated activations.
- BloomSlash prefabs are spawned as independent world objects, not player children; their colliders expire after `activeTime`, while their visuals remain until the authored animation clip finishes.
- `FloweringAttackData` no longer serializes Bloom flurry hit count or interval fields. Attack cadence is tuned through `nextAttackDelay` / `recoveryDuration`.

## 2026-05-25 - Flowering Normal Attack Uses Dedicated LightningSpear-Style Combo

Decision:
`Flowering` normal attack now uses dedicated `FloweringBaseAttackData` and a `Flowering` attack logic dispatcher modeled after the active `LightningSpearAttackData` combo schema. It no longer runs through the shared `SwordCombo2DData` / `AL_SwordCombo2D` path.

Reason:
The active Lightning Spear baseline attack is weapon-specific data and logic with nested `combo.steps[].attackPrefab` authoring, and its slash visual is authored inside the hitbox prefab's `VisualRoot/Render` hierarchy. Matching that shape for Flowering keeps the weapon's basic attack data local, makes step hitbox/visual authoring clearer, and avoids growing the legacy/sample `SwordCombo2D` path for Flowering-only needs.

Implications:
- `AD_FloweringAttack_Base` points to the existing import-stable `AL_FloweringAttack` asset, while `ALData_FloweringAttack_Base` serializes as `FloweringBaseAttackData`; runtime dispatch routes that data to the base attack runner.
- Flowering normal attack remains a three-hit combo with the existing step timing, damage, lunge, hit-event, and hit cue values. Its 1/2/1 slash visuals are owned by `Hitbox_Flowering_BasicAttack1/2/3` prefabs rather than spawned as separate step effect prefabs.
- Bloom attack remains a separate rapid single-slash branch in `AbilityLogic_FloweringAttack` and does not read/write normal combo state.
- `SwordCombo2DData.attackEffectPrefab` stays available for other weapons and compatibility, but Flowering normal attack no longer depends on it.

## 2026-05-25 - Flowering Kill Extension Requires Dedicated Relic

Decision:
`Flowering` Bloom kill duration extension is gated by a dedicated relic-granted gameplay tag. The weapon runtime still owns the actual `+1s` duration change, but it only applies when the relic tag is present.

Reason:
The Notion spec treats kill extension as the Flowering-specific relic effect, not as the weapon's baseline Bloom behavior. Keeping the duration mutation in `FloweringRuntimeState` avoids moving weapon state ownership into relic logic while still making the effect relic-conditional.

Implications:
- `RelicLogic_FloweringBloomExtension_Managed` grants/removes only the Flowering relic tag on equip/unequip/restore.
- `FloweringBloomData.killExtensionRequiredTag` is the data gate checked before `FloweringRuntimeData.ExtendBloom(...)`.
- Bloom status HUD and Skill1 active-duration HUD are runtime projections of `FloweringRuntimeData`; HUD views do not own Bloom gameplay state.

## 2026-05-25 - Flowering Splits Normal Combo From Bloom Attack

Decision:
This historical prototype decision established that `Flowering` normal and Bloom attacks stay separate. Its earlier `SwordCombo2D` and Bloom flurry implementation details are superseded by the dedicated normal attack and single Bloom slash activation decisions above.

Reason:
The design needs a stable three-hit baseline attack before Bloom, while Bloom should feel like a separate rapid state attack that can randomize hitbox-owned visual variants and avoid immediate visual repetition.

Implications:
- `AD_FloweringAttack_Base` and `AD_FloweringAttack_Bloom` continue to be selected separately by Flowering runtime state.
- `AD_FloweringAttack_Bloom` remains on `AbilityLogic_FloweringAttack`, but now owns one world-positioned slash activation at a time.
- Delayed Bloom dash slashes are owned by Bloom runtime state cleanup, not by the short-lived dash ability token.
- Bloom screen border presentation now uses the affection gradient UI graphic/material path for v1 instead of world `SpriteRenderer` border strips.

## 2026-05-25 - TitleScene Authors Shared Fade Service

Decision:
`TitleScene` should have an authored scene-root `SceneFadeTransitionService` for title-origin transitions, instead of relying on the runtime fallback overlay for the intro-to-gameplay load.

Reason:
The fade service is shared transition infrastructure, and the title intro needs the same Inspector-tunable fade-out/load/fade-in timing as gameplay scenes. A scene-root authored service can survive the scene load long enough to finish the fade-in, then yield to the loaded `GlobalUIRoot` authored service after the active transition session ends.

Implications:
- The title fade service should be authored as a root object, not as a child under the title canvas, so it is not destroyed before post-load fade-in completes.
- Loaded authored fade services must defer replacement while any existing fade service is actively transitioning.
- Runtime fallback remains an emergency path when no authored service exists, not the preferred title transition structure.
- Title fade overlay wiring should happen through Unity authoring tools or Inspector review, not direct scene YAML edits.

## 2026-05-24 - Prewarm Trace Uses Per-Tester Files

Decision:
Editor prewarm trace capture writes to tester/machine-specific JSON files under `Assets/LeeJunMo/Datas/Loading/`, while the recommendation tool aggregates all `PrewarmTrace_*.json` files plus the legacy `PrewarmTrace.json`.

Reason:
A single shared tracked trace file creates source-control conflicts when multiple testers run play sessions. Per-tester files let each tester commit their own results independently while preserving aggregate recommendation quality.

Implications:
- `PrewarmTraceRuntime` should not write new editor sessions back into the legacy shared `PrewarmTrace.json`.
- `PrewarmRecommendationWindow` is responsible for merging trace histories and deduplicating sessions by `sessionId`.
- If trace cleanup is needed, remove or archive individual tester files rather than collapsing all results into one generated JSON.

## 2026-05-23 - GlobalUIRoot Owns Persistent UI Instance Selection

Decision:
`GlobalUIRoot` remains the source of truth for persistent global UI ownership. Child global panels such as `SettingsPanelUI` and `KeyBindingPanelUI` may cache or find instances, but they must not destroy an existing persistent representative to replace it with a scene-local duplicate.

Reason:
Scene-local panels can be loaded under duplicate scene UI roots that are destroyed during scene transitions. If a child panel replaces the persistent singleton before that duplicate root is removed, the project can lose both the original persistent UI and the scene-local replacement.

Implications:
- `UIManager` continues to resolve settings/keybinding panels through `EnsureInstance()`.
- Scene-local child panels destroy themselves when another valid non-title instance already exists.
- Pad/keyboard selected-state and EventSystem selected-object behavior remain separate from persistent ownership.

## 2026-05-23 - Shared Hold Buttons Own Hold Input

Decision:
Reusable hold-confirm buttons should let `HoldActionButton` own pointer/keyboard hold timing and progress. Feature screens such as `ChestScreen` should consume hold events for domain behavior instead of recalculating the same hold input and progress.

Reason:
When a feature screen and a shared hold button both drive progress, authored fill visuals can be reset or bypassed by the wrong owner. Keeping hold input in `HoldActionButton` preserves reusable button behavior across chest reroll, tutorial panels, and title intro skip.

Implications:
- `HoldActionButton` supports authored pointer hold and optional keyboard hold.
- `HoldFillButtonView` remains visual projection only.
- Feature screens remain responsible for feature eligibility, side effects, and presentation sequences after hold events.

## 2026-05-24 - Tutorial Pages Reuse One Authored Layout

Decision:
Tutorial explanation UI uses one authored `TutorialInfoPanel` layout that swaps page image/body/title data at runtime. Page navigation belongs to the panel, while final confirmation remains owned by the authored `HoldActionButton` and is enabled only on the last page.

Reason:
The current tutorial plan has no variant layouts. Reusing one panel keeps scene/prefab authoring simple and avoids creating separate UI objects for each tutorial page while still letting designers author page content per trigger.

Implications:
- `TutorialInfoTrigger.pages` is the only tutorial content entry point.
- Prev/Next buttons are authored UI objects; `TutorialInfoPanel` only binds their click events and hides invalid directions with `SetActive(false)`.
- A/D page keys are panel-local navigation shortcuts while the panel is open.
- The Space hold-confirm button should not complete the tutorial until the final page is displayed.
- If future tutorials need different layout structure, add a separate presenter/prefab variant instead of overloading `TutorialInfoPage` or reintroducing top-level fallback content fields.

## 2026-05-23 - Title New-Slot Intro Reuses Profile Launch Target

Decision:
The title intro is a title-scene-local presentation step that runs only before empty-slot `StartNewRun` launches. It does not own or override the destination scene; after completion or skip, launch continues through the existing `TitleProfileSlotService.targetSceneName` / `TitleProfileLaunchService` path.

Reason:
The current project has no dedicated tutorial scene in BuildSettings, and the profile launch target already owns where a selected slot should enter gameplay. Keeping the intro as a presentation gate avoids introducing a second target configuration or a title-specific scene-loading branch.

Implications:
- Existing-slot `ContinueRun` launches bypass the intro and keep the current direct prepare/load path.
- Empty-slot intro completion and skip must call back into the existing profile launch preparation before scene load.
- Future changes to the post-intro destination should edit the profile slot target scene setting, not add an intro-only target override.
- Intro UI remains scene-authored in `TitleScene`; runtime code drives serialized references and does not create the intro UI hierarchy.

## 2026-05-25 - P2 Drain Completion Restores The Drain

Decision:
When a phase-two Slime Queen finishes the drain sink sequence, `DrainPipe` restores to its initial damageable cork state instead of becoming permanently blocked.

Reason:
The design intent is a temporary 4-second boss groggy/control-loss mechanic. The drain should be usable again after being re-opened by hits, not removed from future play.

Implications:
- `DrainPipe` resets `currentHitCount` to `0` after the P2 boss exits.
- The drain visual returns to the cork color/state.
- Existing captured Pawn slime targets are cleaned up during restore so disabled suction targets do not remain.
- Future P2 drain changes should avoid permanent disable/blocked flags unless a separate design request asks for one-time drains.

## 2026-05-25 - Boss HUD Uses One Slot Per Boss

Decision:
`BossHudController` manages registered bosses as separate HUD slots. A multi-body boss phase displays one slot per active boss body instead of projecting multiple bodies into one dual health/groggy bar.

Reason:
The slot model is easier to reason about than a second dual-channel rendering path inside `BossHealthBarUI` and `BossGroggyBarUI`. It keeps the common HUD controller boss-agnostic without requiring source adapters for every multi-body boss shape.

Implications:
- Bosses call `RegisterBoss`, `MarkBossDefeated`, and `UnregisterBoss` through the common HUD registration API.
- Slime Queen phase two Short/Long bodies register as independent slots.
- `BossHealthBarUI` and `BossGroggyBarUI` remain single-channel views.
- `IBossSplitHealthPresentation` is still available for split labels on a single boss slot, but dual boss rendering is not a supported HUD path.

## 2026-05-23 - Run-Special Unavailable Responses Belong To Choices

Decision:
Run-special NPC unavailable response lines belong to the selected `RunSpecialNpcChoiceDefinition`, not to a top-level dialogue branch, when the failure is discovered at choice execution time.

Reason:
Different choices and future feature actions can fail for different reasons. Keeping the failure line with the choice lets authors control "what the NPC says after this selected option cannot execute" without forcing every feature to add another initial branch.

Implications:
- `RunSpecialNpcInteractor` must check `CanExecute(...)` before success response lines and before `Execute(...)`.
- Unavailable choices may remain visible only when the choice has `unavailableResponseLines` and the feature explicitly allows showing that unavailable state.
- Construction insufficient-funds feedback is a payment-choice unavailable response, while construction pending/completed remain feature branch states.

## 2026-05-23 - Same-Scene Teleport Splits Appearance And Landing Points

Decision:
Run-special same-scene teleport uses two authored anchors: `appearancePoint` for where the player becomes visible after fade-out, and `landingPoint` for the final playable position. The old `destination` serialized reference migrates to `landingPoint`.

Reason:
The teleport arrival presentation needs a separate visual start position and final control-restoration position. A single destination can move the player, but it cannot express "appear here, then land here" without overloading one transform.

Implications:
- `landingPoint` is required for teleport availability.
- `appearancePoint` is optional; if it is empty, the feature uses `landingPoint` as the appearance point and preserves the old one-anchor behavior.
- Player protection and targetability blocking release after the appearance-to-landing movement finishes.

## 2026-05-23 - Same-Scene Teleport Uses Player Targetability Gate

Decision:
During run-special same-scene teleport execution, the player is protected by both `PlayerCinematicProtection` and `PlayerTargetabilityBlocker`. Enemies keep their normal perception flow, but `Enemy.CanPerceiveTarget(...)` treats a blocked player target as not perceivable.

Reason:
Teleport arrival presentation should not make nearby enemies chase or attack while the player is still in a cinematic/control-locked state. Invulnerability already prevents damage, but it should not also become the meaning for enemy recognition.

Implications:
- `PlayerTargetabilityBlocker` is a player-owned owner-token gate and can be reused by future player-centered presentation flows.
- Enemy recognition resumes when the teleport feature releases the gate, currently after its fade/warp/arrival movement execution finishes.
- Boss combat activation and general enemy objects are not disabled by this gate; only player targetability is filtered.

## 2026-05-23 - Same-Scene Teleport Executes After Run-Special Presentation Close

Decision:
Only same-scene teleport run-special features execute after speech/letterbox/HUD presentation closes and gameplay time is restored. Construction and other default run-special features continue to execute inside the paused dialogue flow.

Reason:
`RunSameSceneTeleportNpcFeature` relies on `MovementMotor2D.WarpTo(...)`, which applies the warp in `FixedUpdate`, then waits for `WaitForFixedUpdate`. Running that while `RunSpecialNpcInteractor` holds `Time.timeScale = 0` can fade the screen without ever applying the warp.

Implications:
- `RunSpecialNpcFeatureBase.ExecuteAfterRunSpecialPresentationClose` defaults to `false`.
- `RunSameSceneTeleportNpcFeature` overrides that policy to `true`.
- `RunSpecialNpcInteractor` restores run timer pause and `Time.timeScale` before executing post-presentation features, while keeping input/player locks until the feature finishes.

## 2026-05-23 - Run-Special NPC Dialogue Data Lives In DialogueSetSO

Decision:
Run-special NPC authored lines and choices live in `RunSpecialNpcDialogueSetSO`. `RunSpecialNpcInteractor` reads that SO and asks its `primaryFeature` for the current `RunSpecialNpcDialogueBranchKey`; feature-specific branch selection and text formatting stay on the feature implementation.

Reason:
Construction and teleport NPCs need different branch states, but the authored line/choice data should be reusable content rather than extra scene components. Keeping SO data separate from scene feature references also avoids putting scene component references inside reusable assets.

Implications:
- `RunSpecialNpcInteractor` owns presentation flow and generic choice-action execution only.
- `RunConstructionNpcFeature` returns construction branch keys, including `ConstructionInsufficientFunds`, and resolves the `N일` token.
- `RunSameSceneTeleportNpcFeature` returns teleport branch keys.
- `RunSpecialNpcDialogueSetSO` uses feature-kind-specific branch groups, shown through a custom Inspector.
- Line breaks authored inside one `RunSpecialNpcLine` text field are interpreted as separate speech-bubble lines at playback time. Visual wrapping inside one line should rely on the speech bubble layout instead of hard line breaks.
- Choice execution is represented by action enum values such as `ExecutePrimaryFeature`, not direct scene component references in the SO.
- The earlier provider-component decision is superseded for runtime. Provider components are migration-only compatibility until scene/prefab data is moved to SO assets.

## 2026-05-23 - Run-Special NPC Dialogue Branching Lives In Providers

Decision:
Run-special NPC line and choice branching belongs to feature-specific `RunSpecialNpcDialogueProviderBase` components. `RunSpecialNpcInteractor` remains the presentation flow owner that executes provider-built branches, but it should not contain construction-specific, teleport-specific, or future feature-specific branch rules.

Reason:
Construction and teleport NPCs split dialogue on different state axes: construction progress, completion, remaining run count, affection lock, destination availability, and feature execution eligibility. Keeping those decisions inside the interactor made generic presentation code depend on one feature's serialized field names and token replacement rules.

Implications:
- Construction dialogue state lives in `RunConstructionNpcDialogueProvider`.
- Same-scene teleport dialogue state lives in `RunSameSceneTeleportNpcDialogueProvider`.
- `RunSpecialNpcInteractor` asks for a `RunSpecialNpcBranch`, plays lines, shows choices, then executes the selected feature.
- Feature-specific line text formatting, such as the construction `N일` remaining-run token, belongs to the provider that understands the feature.
- Moving old interactor-authored fields to provider components requires Unity Editor migration; `[FormerlySerializedAs]` is not enough because the data moves across components.

## 2026-05-23 - Encyclopedia Relic Body Uses Logic Tooltip

Decision:
The encyclopedia Relic RightPage displays `RelicLogic.BuildTooltip(...).effectText` as the relic body text. It does not display `RelicDefinition.description` in the current Item slice.

Reason:
The inventory hover `ItemDetailPanel` and the encyclopedia should expose the same relic information. Relic logic assets are the source of truth for effect text, formatting tokens, bullet lines, glossary links, and per-level preview output; the serialized relic description can drift from the actual drop tooltip.

Implications:
- `EncyclopediaItemRightPage` writes formatted relic effect text into the authored body slot used by `StoryText`.
- `RelicDefinition.description` remains available for item data, but is not used by the encyclopedia Relic tab body in this slice.
- A level `1 / 1` relic still shows Prev/Next preview guides as disabled authored controls instead of hiding the guide objects.

## 2026-05-22 - Encyclopedia Content Disappear Reuses ContentAppear Clip

Decision:
The encyclopedia book content transition uses the authored `ContentAppear` animation clip as the single content-cover source. Appear samples the clip forward. Disappear samples the same clip backward through `AnimationClip.SampleAnimation`; it does not require a separate `DisAppear` clip and does not use negative Animator speed.

Reason:
The content cover is a presentation overlay, not gameplay state. Keeping one authored clip reduces prefab wiring and avoids Animator reverse-play edge cases while still allowing a visible hide-before-swap and reveal-after-swap sequence.

Implications:
- `EncyclopediaBookPresentation` depends on `pageCoverImage` and `contentAppearClip`; `pageCoverAnimator` is optional.
- If `pageCoverAnimator` exists on the same object, it must not drive the cover while `SampleAnimation` is sampling, because its default state can replay `ContentAppear` forward and override the reverse disappear frames.
- Item sub-tab transitions should sequence content disappear, page turn, content swap, and content appear through the book presentation.
- Slot selection remains an immediate RightPage bind in this slice.

## 2026-05-22 - Encyclopedia Item Detail Uses Dedicated Presenter

Decision:
The encyclopedia Item tab uses `EncyclopediaItemRightPage` as the sole active RightPage item presenter. It does not use `ItemDetailPanel.Instance`, hover-canvas adoption, drag/drop, inventory tooltip behavior, or the older `EncyclopediaDetailPanel` component on the active Item layout.

Reason:
Runtime inventory item detail UI owns hover positioning and canvas migration behavior that conflicts with a scene-authored book page. The encyclopedia needs stable serialized RightPage references that project selected item data without owning gameplay state.

Implications:
- Item details for `Weapon`, `Relic`, and `Consumable` read from `ItemDatabase` and bind through `EncyclopediaItemRightPage`.
- Weapon ability rows are pooled from `Panel_AbilityBlock_Encyclopedia.prefab` / `WeaponAbilityBlockView`, not from `WeaponDetailViewV2`.
- `ItemDetailPanel` under `RightPage` is a layout/ScrollRect/content host only. Presenter ownership should stay on `RightPage` through `EncyclopediaItemRightPage`.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` removes duplicate child `EncyclopediaItemRightPage` presenters and legacy `EncyclopediaDetailPanel` components under RightPage.
- Boss affection/reward detail remains out of this presenter and should use a future boss-specific detail component.
- Inspector wiring should target `EncyclopediaItemRightPage` authored header/detail roots and the encyclopedia ability-block prefab, not runtime hover detail components.

## 2026-05-22 - Encyclopedia Item Tab Owns Item Page State

Decision:
`EncyclopediaScreen` owns popup stack behavior, open/close presentation, and top-level tab entry. `EncyclopediaItemTab` owns Item sub-tab state, page index, selected entry, and item data binding. `EncyclopediaItemLeftPage` and `EncyclopediaItemRightPage` own the authored left/right page references for the Item tab.

Reason:
Keeping every Item control and selection field on `EncyclopediaScreen` made the Inspector hard to understand and would not scale when Monster and Boss get their own tab-specific layouts. Splitting the Item tab keeps screen-level presentation separate from item-specific content behavior.

Implications:
- `EncyclopediaScreen.OpenUI()` calls `EncyclopediaItemTab.ShowDefault()` after the book-open presentation completes.
- `OpenWeaponSubTab`, `OpenRelicSubTab`, `OpenConsumableSubTab`, and page-button methods remain on the screen only as button-friendly forwarding methods.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` are obsolete/migration fallback components, not the preferred new authoring target.
- Monster/Boss work should add their own tab/presenter components instead of expanding Item tab state back into `EncyclopediaScreen`.

## 2026-05-22 - Encyclopedia Tome Open Uses Authored Animator Sequence

Decision:
Tome interaction opens the encyclopedia through a `DimPanel` fade, a separate book motion-root drop, and the child book Animator `BookOpen` state. Close uses `BookClose`, LeftPage sub-tabs use `BookLeftPage`, and future RightPage main-tab transitions use `BookRightPage`.

Reason:
The book asset already exposes authored animation states, and the layout should remain scene-authored. Moving the parent motion root keeps screen-level drop timing separate from the child book Animator's page/book clips.

Implications:
- `EncyclopediaBookPresentation` owns sequencing and content visibility around the Animator states.
- `EncyclopediaScreen` delays list/detail binding until the open presentation completes.
- Unity authoring must wire `DimPanel`, `BookMotionRoot`, the book Animator, and optional content roots instead of relying on runtime hierarchy creation.

## 2026-05-05 - Use Docs as Project Memory Vault

Decision:
Use `Docs/` as both the Obsidian vault and the official project Markdown memory store.

Reason:
Codex and the developer should read the same source-of-truth documents. Keeping a separate vault would create drift between human notes and agent context.

Implications:
- `Docs/.obsidian/` remains local and ignored.
- Markdown documents in `Docs/` are project assets.
- Obsidian is the editing/navigation UI, not a separate authority layer.

## 2026-05-05 - Use AGENTS.md for Codex Project Instructions

Decision:
Place project-specific Codex instructions in the repository root `AGENTS.md`.

Reason:
Codex uses `AGENTS.md` as durable project guidance. This keeps execution rules close to the code and separate from runtime configuration.

Implications:
- `.codex/config.toml` is reserved for future MCP, sandbox, profile, and approval settings.
- Project behavior rules live in `AGENTS.md`.

## 2026-05-05 - Treat Reviews, Notes, and Handoffs as Reference-Only

Decision:
`Docs/Reviews/`, `Docs/Notes/`, and `Docs/Handoffs/` are reference-only unless promoted into `Contracts` or `Architecture`.

Reason:
Older reviews and handoff notes are valuable context, but they can conflict with current architecture and contracts.

Implications:
- Current implementation decisions should prefer `Contracts` and `Architecture`.
- Review documents can explain why a decision exists, but should not override active rules.

## 2026-05-06 - Update CurrentTask on Active Work Changes

Decision:
Update `Docs/CurrentTask.md` whenever the active implementation task changes.

Reason:
The file was being read but not updated, so it no longer represented the actual task in progress.

Implications:
- `CurrentTask.md` should hold the current goal, scope, and done criteria.
- Detailed implementation notes belong in `Docs/SessionLogs/`.
- Durable design choices still belong in `Docs/DecisionLog.md`.

## 2026-05-06 - Shop v2 Uses Definition-Driven Runtime Policy

Decision:
Use `ShopDefinitionSO` as the source of truth for merchant shop settings, and combine it with `RunModifierService.ShopModifiers` through a shop policy layer.

Reason:
Upgrade effects now modify shop availability, slot count, discounts, and refresh count. Keeping those policies inside `MerchantNPC` would make scene presentation, stock state, and upgrade logic too tightly coupled.

Implications:
- Merchant stock remains run/session scoped in `GamePlayData.merchantStates`.
- Existing stock is preserved when discounts change.
- Existing slots are preserved when slot count expands; only newly opened slots are rolled.
- Scene and prefab references must be manually wired to a `ShopDefinitionSO`.

## 2026-05-10 - Keep Lightning Spear Basic Attack Weapon-Specific

Decision:
Do not keep the temporary `WeaponComboAttack2D` shared layer. Lightning Spear basic attack owns its combo data and execution logic, while `SwordCombo2D` remains a legacy/sample ability.

Reason:
The shared layer only had one active consumer and made it unclear whether Sword combo behavior had become project-wide weapon policy.

Implications:
- Lightning Spear attack tuning stays in `LightningSpearAttackData`.
- New weapon combo logic should not depend on the removed shared runner by default.
- `SwordCombo2D` assets and logic remain untouched unless a separate task targets them.

## 2026-05-14 - Boss Rewards Use Additive Modifier Aggregates

Decision:
Keep `StageLootTable` and `LootManager` as the base boss reward source, and let Affection, upgrades, and future systems contribute only additive boss reward modifier aggregates.

Reason:
Affection should change extra boss rewards without bypassing stage progression reward rules. Keeping base rewards and modifiers separate prevents NPC Affection assets from becoming an alternate reward table system.

Implications:
- Boss chest base loot comes from `LootManager.GenerateBossChestLoot(...)`, which reads boss-specific count and rarity defaults from `StageLootTable`.
- Boss magic stone base count still comes from `LootManager.GetBossMagicStoneCount()`.
- Boss field heal base count comes from `StageLootTable`, while extra field heals remain additive modifiers.
- Boss-specific extra items, extra magic stones, extra field heals, and boss chest count deltas are additive modifiers.
- The former `BossDrop` adapter was temporary and has since been deleted; dedicated reward and portal components are the supported path.

## 2026-05-14 - Treat Markdown as Structure Memory

Decision:
Use project Markdown not only as task history, but as structure memory that helps future agents quickly understand previously changed systems before editing them again.

Reason:
Session logs that only say what was done are not enough when a later task starts in a different part of the codebase. Future work is faster and uses fewer tokens when the log also records ownership, lifecycle, key files, verification, and the next document or source file to read.

Implications:
- Material architecture, ownership, runtime state, lifecycle, shared service, interface, asmdef, MonoBehaviour, ScriptableObject, and prefab-facing changes should leave a concise task entry in `Docs/SessionLogs/YYYY-MM-DD.md`, and a feature-level `Docs/StructureMemory/` entry when future context reconstruction needs a stable system map.
- Durable decisions still belong in `Docs/DecisionLog.md`.
- `Docs/Architecture/` and `Docs/Contracts/` remain source-of-truth documents and should only be rewritten with explicit approval.
- Small implementation edits do not need new memory documents unless they change how future work should understand the system.

## 2026-05-14 - GameFlowInputBlocker Owns Flow Input Blocks

Decision:
Use `GameFlowInputBlocker` as the reusable lifecycle wrapper for temporary game-flow input blocks, while `UIManager` remains the central policy owner.

Reason:
Chest first-open sequences, dialogue playback, upgrade open fades, reward open presentations, and future authored flows all need the same temporary block behavior without each system directly manipulating `UIManager` internals.

Implications:
- New flow code should acquire/release `GameFlowInputBlocker` instead of calling `UIManager.SetExternalUiInputBlocked(...)` directly.
- `UIManager.TryPushUIForExternalBlockOwner(...)` is the owner exception path for a flow that must open its own stack UI while the block is active.
- The blocker is for stack-outside flow gaps and presentation windows; opened stack UI screens still express time freeze/control locks through `IStackableUI.GameplayLockProfile`.
- `GameFlowInputBlocker` must release from normal completion and from `OnDisable`/`OnDestroy` cleanup paths so interrupted flows do not leave controls locked.

## 2026-05-14 - Use Feature-Level StructureMemory and RefactorBacklog

Decision:
Add `Docs/StructureMemory/` for feature-level structure maps and `Docs/RefactorBacklog/` for feature-level structural debt tracking.

Reason:
Date-based session logs are useful for recent work, but they scatter system context over time. Future agents need a faster way to understand current structure and known refactor candidates before editing related systems.

Implications:
- `StructureMemory` is a fast context map, not a source-of-truth replacement for `Architecture` or `Contracts`.
- `RefactorBacklog` tracks intentional structural debt with target shape, risks, and refactor triggers; it is not a generic TODO list.
- Small edits usually update only `SessionLogs`; reusable structure, known debt, durable decisions, and recurring mistakes update the narrower matching memory document.
- Stable structure can be proposed for promotion from `StructureMemory` to `Architecture` or `Contracts`, but those official documents still require explicit approval before editing.

## 2026-05-15 - Prototype Runtime UI Creation Is Temporary

Decision:
Runtime creation of UI or presentation objects is acceptable for first-pass prototyping, debug fallback, or emergency fallback, but production-facing UI and presentation should be promoted to scene- or prefab-authored objects before build-facing use.

Reason:
Runtime-created UI is useful for quickly checking feel and avoiding blocked iteration. Keeping it as the final structure hides sorting, raycast, scaling, animation, and serialized-reference choices in code, making Unity authoring, inspection, and prefab/scene verification harder.

Implications:
- Gameplay-facing UI should normally live under scene/prefab-authored objects, often through `GlobalUIRoot` canvas layers or explicit serialized references.
- Runtime-created `Canvas`, `EventSystem`, button, TMP, image, overlay, and hierarchy fallbacks must be clearly treated as prototype/debug/fallback paths, not silent production defaults.
- Any runtime-created UI that remains beyond prototype should have explicit owner, cleanup, and a prefab/scene migration follow-up.
- Pooled gameplay presentation objects may still be instantiated from authored prefabs; the prefab is the authored unit, while the runtime service owns spawning and cleanup.
- Existing runtime fallback presentation debt is tracked through `Docs/RefactorBacklog/RuntimePresentationFallbackAuthoringSplit.md`.

## 2026-05-15 - Use ContentAuthoring Guides for Repeatable Content Pipelines

Decision:
Manage repeatable combat-content production through a `Docs/Guides/ContentAuthoring/` hub and focused type-level pipeline guides.

Reason:
Weapons, mobs, bosses, relics, consumables, and reward links will keep growing. A single structure map is useful for code review, but content creators need a production-order guide that starts from authored data and ends at loot, inventory, presentation, save, and verification.

Implications:
- `ContentAuthoring` guides are production entry points, not replacements for `Contracts` or `Architecture`.
- Stable rules still belong in `Docs/Contracts/` or `Docs/Architecture/` after explicit approval.
- Structure-specific context remains in `Docs/StructureMemory/`, and concrete structural debt remains in `Docs/RefactorBacklog/`.
- New content pipelines should link to source-of-truth documents instead of copying their full rules.

## 2026-05-15 - Treat TitleScene as App And Gameplay Session Boundary

Decision:
Use `Docs/Architecture/SceneDomainBootstrapArchitecture.md` as the source-of-truth document for title-to-game scene bootstrap ownership. `TitleScene` is both the app entry scene and the gameplay session boundary.

Reason:
Title, profile selection, return-to-title, gameplay runtime presentation, camera rig behavior, and run-session cleanup were implemented across several services without a single documented boundary.

Implications:
- App-scope services may exist on title, but gameplay-scope runtime presentation should not treat title as active gameplay.
- Title-local UI/camera presentation stays scene-authored unless an explicit fallback/prototype path is documented.
- Return-to-title changes must preserve run/session cleanup before or during the title scene transition.
- Code refactors in this area should start from `Docs/RefactorBacklog/SceneDomainBootstrapBoundarySplit.md`.

## 2026-05-15 - Use Source-Structure Verification For Unity Script File Splits

Decision:
For source-only Unity script file splits, proceed with helper `.cs` file extraction when behavior and APIs are unchanged, even if Unity-generated `.csproj` files have not refreshed yet.

Reason:
Unity Editor import/compile is the reliable final check for new `Assets/` scripts, while generated `.csproj` files can lag behind the filesystem and block otherwise safe source-only refactors.

Implications:
- Do not manually edit generated `.csproj` files.
- Verify moved helpers by source structure: duplicate definitions, original helper removal, call-site references, namespace/global-scope compatibility, and whitespace.
- Run MSBuild only when generated project files include the relevant source files.
- Do not claim Unity compile or MSBuild success for new files unless that verification actually ran.

## 2026-05-15 - Element Build-Up Applies From ElementOffenseSource

Decision:
Applied elemental build-up is resolved from the attacker's `ElementOffenseSource` through `ElementBuildUpResolver`, not from per-hit `DamagePayloadConfig.elementFormulas` or `elementBuildUps` payloads.

Reason:
The combat application path already used attacker-wide elemental build-up, while legacy per-hit fields made weapon tuning intent ambiguous.

Implications:
- `DamagePayloadConfig.elementFormulas`, `CombatDamageSnapshot.ElementBuildUps`, and `elementBuildUps` / `elementInputs` API parameters are compatibility debt until serialized data and call sites are migrated.
- Future per-hit elemental tuning must be introduced as a named merge/override policy instead of reviving the old implicit payload path.
- Elemental weapon tuning should verify attacker `ElementOffenseSource`, stat provider wiring, and `ElementBuildUpFormulaProfile` rather than per-hit payload formulas.

## 2026-05-16 - RouteSets Reference Boss Special Reward Presets

Decision:
Let `CorridorBossRouteSetSO` reference only an optional `BossSpecialRewardPresetSO` for boss-specific special loot candidates. Authored chest/portal objects live in boss scenes, and reward amount changes remain runtime modifier overlays.

Reason:
Route sets already compose the corridor/boss scene, BGM, and loading context for a run stage, so linking a boss-specific special loot preset there is acceptable. Putting common chest, magic stone, portal prefabs, portal offsets, placement policy, magic stone bonuses, field-heal bonuses, or chest-count deltas on route data mixes content composition with shared prefab catalogs and runtime reward effects.

Implications:
- `CorridorBossRouteSetSO` may reference `BossSpecialRewardPresetSO`, but should not own common prefabs, portal objects, spawn positions, offsets, magic stone bonuses, field-heal bonuses, or chest-count deltas.
- `BossSpecialRewardPresetSO` owns only boss-specific special loot candidates.
- Upgrade, Affection, and future runtime effects should keep contributing `BossRewardModifierAggregate` overlays instead of mutating preset SO assets.
- Scene/prefab-owned `TreasureChest` and `ScenePortal` objects provide chest and portal positions through their own transforms.
- `BossBattleEndDefinitionSO`, `BossRewardProfileSO`, `BossBattleEndPrefabCatalogSO`, and `BossBattleEndAnchors` are deleted; route special reward preset and scene `BossBattleEndHandler` are the supported authoring path.

## 2026-05-16 - Remove BossDrop Legacy Adapter

Decision:
Delete the `BossDrop` legacy adapter instead of keeping it as a prefab-safe fallback.

Reason:
Boss battle-end ownership is now split across route-linked special reward presets, scene `BossBattleEndHandler`, and run-progress coordination. Keeping `BossDrop` would preserve a second reward/portal authoring path and hide missing RouteSet or handler wiring.

Implications:
- Serialized scene/prefab references to the old `BossDrop` script GUID must be removed during the migration.
- `BossRewardFallbackService` must not read `BossDrop` fields.
- Unity Editor verification must run the boss battle-end validator and play-check boss death reward/portal behavior because deleted `BossDrop` field values are no longer available as fallback data.

## 2026-05-16 - Remove BossRewardModifierSO Authoring Layer

Decision:
Do not keep a separate `BossRewardModifierSO` asset layer. Boss-specific special loot candidates can live in a RouteSet-linked `BossSpecialRewardPresetSO`, while Affection, upgrades, and future runtime effects expose their own fields and project them into `BossRewardModifierAggregate` at runtime.

Reason:
The modifier SO duplicated boss reward authoring responsibility and made runtime-changing effects look like mutable asset state. Runtime effects need additive overlays without mutating ScriptableObject reward presets.

Implications:
- `BossRewardModifierAggregate` remains the runtime value type for combining boss reward deltas.
- Affection and upgrade effects should author their own relevant values directly, then emit aggregates during run modifier rebuild.
- Existing assets that referenced `BossRewardModifierSO` need migration to direct effect fields or RouteSet special reward preset data.
- New boss-specific base special loot candidates should be configured through route-linked special reward presets, not modifier assets.

## 2026-05-16 - StageLootTable Owns Boss Base Reward Defaults

Decision:
Keep boss base reward defaults in `StageLootTable`: boss weapon count profile, boss relic count profile, boss Common/Rare/Epic relic rarity weights, boss magic stone count, and boss field heal base count.

Reason:
These values are stage progression defaults, not boss prefab wiring and not RouteSet composition. RouteSets should only identify the optional boss-specific special loot preset for the selected boss route, while runtime systems such as Affection and Upgrade should add overlays through `BossRewardModifierAggregate` without mutating ScriptableObjects.

Implications:
- Normal chest rewards use normal chest profiles; boss chests use the boss-specific generation path.
- RouteSet `BossSpecialRewardPresetSO` entries are only additional boss-specific special loot candidates.
- `BossRewardModifierAggregate` remains the only runtime overlay path for boss magic stone bonuses, field heal bonuses, boss chest count deltas, and runtime bonus loot.
- Boss reward base values should be reviewed in the StageLootTable Inspector, not on boss prefabs or RouteSet assets.

## 2026-05-16 - Keep Scene-Facing Compatibility Facades For P2 Closure

Decision:
Keep `UpgradeManager`, `GamePlayDataManager`, and `ScenePortalTravelService.TryTravel(...)` as the public/scene-facing compatibility surfaces while moving more ownership into helpers.

Reason:
These names and entry points are already referenced by scenes, prefabs, runtime bootstrap, portal interaction, upgrade UI flows, and save/run lifecycle code. Renaming them would create serialized reference and static call-site risk without improving the current player-facing behavior.

Implications:
- `UpgradeManager` remains the public upgrade facade, but data-change/save/UI-close notification ownership goes through `UpgradeNotificationService`.
- `GamePlayDataManager` remains the scene-facing run/session holder, but volatile timer, pending transition, pending player state, pending reward, affection, and shortcut mutations go through `RunSessionStateService`.
- `ScenePortalTravelService.TryTravel(...)` remains the static portal entry point, but route resolution, transition lock, manager resolution, and execution handoff live behind `ScenePortalTravelCoordinator` and existing planner/executor helpers.
- Future refactors should not reopen MonoBehaviour or static entry naming unless a scene/prefab migration pass is explicitly planned.

## 2026-05-16 - Treat Representative GlobalUIRoot As Build-Facing Presentation Root

Decision:
Use `Assets/LeeJunMo/Prefab/UI/GlobalUIRoot.prefab` as the representative build-facing runtime presentation root for loading, cursor, status HUD, and Boss HUD authored-reference validation. Keep the display letterbox runtime-generated by `GamePresentationController`.

Reason:
Runtime presentation fallbacks are useful as emergency/debug paths, but build-facing UI structure needs a single prefab target that can be inspected, auto-fixed, validated, and reused by scene setup.

Implications:
- Other GlobalUIRoot variants are warning/follow-up targets unless they are promoted to build-facing roots.
- `MouseCursorService` must prefer authored references before creating fallback presentation hierarchy.
- `GamePresentationController` owns display letterbox runtime hierarchy because the overlay is resolution/window-mode policy rather than prefab layout.
- `Tools/Validation/Scene Setup Validator` should report missing representative prefab references and offers an auto-fix path for cursor authoring.
- Unity Editor validation/play checks remain required because code validation cannot prove canvas order, input behavior, or visual layout.

## 2026-05-16 - Keep Shared ScenePortal Prefab Semantic-Neutral

Decision:
Keep the shared `ScenePortal.prefab` at `TransitionType.None` with no `RunRouteCatalogSO` reference. Hub start portals must carry `HubToRunStart` and the run route catalog on their scene instance, while boss exit portals rely on the active `PortalRouteManager` plan to resolve `BossToCorridor` or `ReturnToHubAfterRun`.

Reason:
The same portal prefab can be reused by hub, corridor, and authored boss exit portal instances. Letting the prefab carry `HubToRunStart` made boss exit portals behave like run-start portals and bypassed the intended active route semantics.

Implications:
- Boss battle-end no longer reads `BossBattleEndPrefabCatalogSO.portalPrefab`; authored boss exit portals should use semantic-neutral `ScenePortal` instances.
- Hub start portals are the only supported owners of `RunRouteCatalogSO` references.
- Boss exit portal placement is authored by the portal object's own transform.
- Runtime boss portal creation is not supported; missing `portalObj` is an authoring error.

## 2026-05-17 - Boss BattleEnd Chest And Portal Are Authored Activation Objects

Decision:
Boss battle-end `TreasureChest` and `ScenePortal` objects are authored in the scene and toggled inactive/active by a scene-authored `BossBattleEndHandler`. Runtime chest/portal prefab creation, anchor-driven portal movement, boss-position fallback, and boss-prefab-owned battle-end placement are not supported.

Reason:
The required behavior is simple: after boss death, a chest and a portal appear at predetermined positions and the portal routes through the active `RunRouteCatalogSO` plan. Authoring the objects directly makes their transforms the source of truth and avoids code-side spawn offsets, captured positions, or boss-child transform ambiguity. The handler belongs with the scene's battle-end placement setup, not with the reusable boss actor prefab.

Implications:
- `BossBattleEndHandler` references the `BossControllerBase`, authored `TreasureChest`, and authored exit portal.
- `BossBattleEndHandler` initializes the authored chest with generated boss loot and activates it.
- Boss magic stones and field heals remain physical runtime drops. Their counts come from `StageLootTable` plus `BossRewardModifierAggregate`, and their drop origin is the boss death position rather than the authored chest/portal position or a separate anchor.
- `BossBattleEndHandler` activates the authored exit portal without moving it.
- `BossRewardSpawner`, `BossExitPortalActivator`, `BossBattleEndAnchors`, and `BossBattleEndPrefabCatalogSO` are deleted; new authoring should not add replacement spawn/anchor components for this flow.
- Boss exit portals should not carry `RunRouteCatalogSO`; hub start portals remain the route catalog owner.

## 2026-05-17 - Keep Markdown As Source And Use Presentation HTML As Human Dashboard

Decision:
Keep Markdown documents as the project documentation source of truth, and use `Docs/Presentation/` HTML/CSS/JS only as human-readable navigation dashboards.

Reason:
Markdown is cheaper for Codex context, easier to diff, and better for source edits. HTML is better for human scanning but adds presentation markup and should not duplicate full source document content.

Implications:
- Codex should continue reading Markdown documents first.
- Presentation HTML should stay thin and source-linked rather than copying full Markdown bodies.
- Routine dashboard updates should usually touch `_shared/docs-data.js`; HTML shells, shared CSS, and render JS should change rarely.
- If duplicated content drifts, update the Markdown source first and regenerate or adjust the presentation view from that source.
- Presentation HTML may be updated only when requested or approved by the authorized HTML maintainer `nadoman354`.
- When Markdown changes may make Presentation HTML stale, Codex should report the affected HTML page and required summary/diagram update instead of editing it automatically.
- `refactor-board.html` may exist as an approved thin overview page, but it must remain a risk/trigger board that links back to `RefactorBacklog`.
- `session-summary.html` is not kept as a Presentation entry by default because session logs remain Markdown-first history rather than a regular human decision surface.
- `authoring-guide.html` may be more detailed than the other Presentation pages when it works as a production handbook, but it should still link back to Markdown source documents instead of copying full pipeline bodies.
- `architecture-overview.html` should prefer focused UML-like ownership and runtime-flow diagrams over exhaustive component listings.
- Mermaid diagrams may be rendered from CDN for human-readable pages; if CDN rendering is unavailable, the Mermaid source text should remain readable rather than blocking the document.

## 2026-05-17 - Shop Slots Are Prefab-Instantiated From Typed Anchors

Decision:
Prefer authored slot anchor transforms plus a `ShopSlot` prefab reference for merchant shop layouts. Each anchor can specify the stock type filter (`Any`, `Weapon`, `Relic`, or `Consumable`) used by shop stock rolling.

Reason:
Scene-copied shop slot objects made layout maintenance repetitive and mixed visual placement with live slot behavior. Anchors keep scene authoring focused on position, while the prefab keeps slot presentation/interaction changes centralized.

Implications:
- `MerchantNPC` may instantiate or reuse `ShopSlot` instances under configured anchors during play.
- Existing child `ShopSlot` objects remain a compatibility fallback when prefab-slot authoring is not configured.
- `ShopDefinitionSO` still owns visible slot count, stock weights, and max weapon/consumable caps.
- Typed slot layouts must keep `ShopDefinitionSO.MaxWeaponSlots` and `MaxConsumableSlots` aligned with the intended display.

## 2026-05-17 - Enemy Player Targeting Uses Canonical Player Root

Decision:
For enemies targeting the `Player` tag, resolve the target transform through `PlayerRuntimeRegistry`/`PlayerInteractor2D` and map player-owned child colliders or directly assigned child transforms back to the player root.

Reason:
Player-attached relic/effect objects such as orbiting feathers can have colliders near the player. If boss encounter or combat movement targets those child objects, the boss appears to move even when the actual player is stationary.

Implications:
- Shared `Enemy` target acquisition should not use a player-owned child collider transform as the movement target.
- Explicit boss/enemy target assignment should normalize player-owned child transforms before the blackboard or movement code reads target position.
- Future player-attached objects should still avoid unintended `Player` tags, hurtboxes, or body-blocking colliders.
- Collision and damage target authoring remain separate from movement targeting and should be reviewed independently when new attached objects are added.

## 2026-05-17 - Final Boss Route Skips Chest Activation

Decision:
When `BossRewardContext.IsFinalRouteSet` is true, `BossBattleEndHandler` keeps the authored `TreasureChest` inactive, still spawns boss physical drops, marks rewards handled, and lets portal activation continue through the existing path.

Reason:
The final route ends the run sequence, so a post-boss reward chest is not needed. Physical drops are not chest presentation and should keep behaving like other boss death drops. Keeping portal handling unchanged preserves the existing final-route exit flow.

Implications:
- Final boss scenes can keep the common `BossBattleEndHandler` wiring without spawning/showing a chest.
- Final boss physical drops such as magic stones and field-heal pickups still spawn from the boss death position.
- Do not add a final-boss chest fallback unless final-route reward policy changes.
- Non-final boss reward behavior remains unchanged.

## 2026-05-17 - Room And Chest Locks Count Registered Combatants Plus Slime Splits

Decision:
Room and chest monster-kill locks count spawn-registered enemy roots and Slime split descendants that inherit a registered parent's lock context. General direct summons are excluded from lock conditions. Transform or phase changes on the same tracked GameObject remain the same enemy. Death presentation remains lock-active until the tracked root GameObject is destroyed.

Reason:
Lock state should follow explicit room/chest participation instead of every enemy instantiation. Slime splitting is the intended exception because the split children replace the parent's combat presence and must keep doors/chests locked while alive.

Implications:
- `RoomDoorMonsterKillLock` and `ChestMonsterKillLock` continue using tracked root GameObject lifetime.
- Slime split code must register valid split children into the same lock context as the parent.
- Boss/local summons and other direct `Instantiate(...)` enemies do not affect room/chest clear unless a future design explicitly registers them.
- No-loot or gimmick enemies count only when they enter through the same spawn registration or Slime split inheritance path.

## 2026-05-18 - Electric Trigger Splits Electrocute Status From Discharge Logic

Decision:
Electric gauge completion applies a dedicated instant trigger effect that refreshes the electrocute duration status, deals secondary electric damage, and then performs the discharge chain. The electrocute status effect itself remains status-only.

Reason:
Status refresh and chain execution have different responsibilities. Keeping discharge in the trigger effect lets each electric gauge completion and chain reapplication deal damage exactly once without making every generic status refresh recursively start another chain.

Implications:
- `GE_ElectricShockTrigger` is the Electric gauge trigger effect; `GE_ElectrocutedStatus` only grants `State.Status.Electrocuted`.
- Discharge is a single nearest-neighbor chain: every step scans around the current unit, chooses the nearest already-electrocuted unit, and visits each unit once per discharge event.
- Electric damage is applied through `GE_Damage_Spec` with `Data.Damage` SetByCaller so it stays inside GAS damage handling and does not re-enter element build-up.
- Electric chain visuals are driven from the final ordered chain point list; the current authored prefab renders SpriteRenderer segments with `ElectricParticleTrail` without changing the gameplay chain logic.

## 2026-05-19 - Closed Doors Block Mob Player Perception

Decision:
General mob player perception treats a closed `DoorObject` on the enemy-target sight line as an immediate perception blocker.

Reason:
Room doors should prevent monsters from detecting or continuing to act on a player beyond the closed door, even if the distance-based detection range still contains the player.

Implications:
- Shared `Enemy` perception checks line of sight for closed `DoorObject` colliders.
- Target acquisition, chase detection/movement, common mob attack continuation, and Dead's Skeleton self-destruct checks use the shared perception rule.
- Opening the door restores normal distance-based detection without clearing the cached player target.
- The rule depends on door colliders being authored so physics raycasts hit the closed `DoorObject`.

## 2026-05-19 - Run Special NPCs Use Speech Bubble Flow

Decision:
Run-internal special NPCs such as construction and same-scene teleport NPCs should use a separate speech-bubble interaction flow instead of extending the existing `DialogueController` / Ink / portrait dialogue stack.

Reason:
These NPCs need in-world `SpeechBubble` UI, local world choices, same-scene movement, construction progress, and shortcut unlock behavior. That interaction shape differs from the existing visual-novel dialogue UI and should not overload the Ink dialogue path.

Implications:
- `DialogueController`, `DialogueView`, Ink start paths, and portrait presentation remain the existing visual-novel/boss dialogue path.
- Run special NPCs should start from `Docs/StructureMemory/ScriptSystems/RunSpecialNpcStructure.md`.
- Teleport NPCs are planned as same-scene player movement, not `ScenePortal` scene transitions.
- Construction NPC path opening should prefer durable shortcut/map progress and scene-authored blocked/open objects before direct runtime tilemap mutation.

## 2026-05-20 - Run Special NPC Choice UI Stays Authored

Decision:
The run-special NPC source slice provides a `RunSpecialNpcChoicePresenter` that drives serialized `CanvasGroup`, `Button[]`, and `TMP_Text[]` references, but it does not create the choice UI hierarchy at runtime.

Reason:
The project UI contract prefers scene/prefab-authored UI. This keeps the new speech-bubble flow usable from code while leaving visual layout, world-space canvas placement, button styling, and references as explicit Unity authoring work.

Implications:
- `RunSpecialNpcInteractor` owns flow state; the presenter only projects labels and relays input.
- Missing choice presenter references are authoring errors, not a reason to instantiate buttons or canvases at runtime.
- Scene/prefab review is required before content can use the flow in play.

## 2026-05-20 - Run Special NPC Choices Use Screen-Space World Anchors

Decision:
Run-special NPC choices should use an authored screen-space presenter under `GlobalUIRoot > DialogueCanvas`, positioned from the player world coordinate through `RunSpecialNpcChoiceAnchorFollower`.

Reason:
The choices need to feel attached to the in-world conversation, but they still need shared UI raycast, scaling, input guard, and canvas ordering. Screen-space overlay with a world-anchor follower keeps the UI in the global canvas while avoiding per-NPC world-space Canvas sorting and camera setup.

Implications:
- `RunSpecialNpcInteractor` owns when the choice panel is shown and which player transform it follows.
- `RunSpecialNpcChoiceAnchorFollower` owns only coordinate projection and canvas clamping.
- The presenter remains authored UI; it does not create buttons at runtime.
- Scene references to prefab-owned presenters should be validated in Unity after prefab import.

## 2026-05-20 - Construction Progress Uses Run-Special Save Data

Decision:
Construction NPC payment/progress is stored in `GameData.runSpecialNpcData.constructionRecords`, with run-active starts staged in `GamePlayData.pendingRunSpecialNpcConstructionStarts` and committed through `RunSessionProgressCommitPolicy`.

Reason:
Construction progress is neither UI state nor a pure door unlock. It needs a durable start point so "required run completions after payment" can be computed before opening the permanent shortcut.

Implications:
- `CurrencyManager` still owns magic-stone spending.
- `DoorObject` / `ShortcutProgressService` still own the final permanent path unlock.
- Existing profiles rely on `GameDataManager` normalization to initialize the new save data safely.

## 2026-05-20 - Encyclopedia V1 Uses Public Catalog Before Discovery Save

Decision:
The first encyclopedia implementation uses `EncyclopediaCatalogSO` as authored display data and treats every registered weapon, monster, and boss entry as visible. Player-specific discovery IDs and save schema changes are deferred to a later release task.

Reason:
The feature does not yet have a built encyclopedia screen, content validation, or hub opening flow. Building and reviewing that visible v1 first reduces the risk of coupling UI/presentation work to unvalidated save events.

Implications:
- The catalog owns display content, not player discovery state.
- `GameData` and encounter/acquisition event hooks are intentionally unchanged in v1.
- Release discovery should add a save-owned discovered ID set and record from weapon acquisition, monster encounter, and boss encounter/defeat events.
- The encyclopedia screen and interactable should stay prefab/scene-authored; editor-only builders may generate temporary authoring assets, but runtime must not create the UI hierarchy.

## 2026-05-20 - Encyclopedia Book Presentation Is Optional Authored Sprite Playback

Decision:
The encyclopedia can experiment with UI book open/close and page-cover reveal presentation, but the runtime still only drives authored `Image`, `CanvasGroup`, `SpriteRenderer`, and sprite-array references. Missing book presentation references fall back to the existing immediate/pixel-reveal behavior instead of creating UI at runtime.

Reason:
The updated planning note prefers a simpler already-open UI, but the current requester explicitly asked to try the book unfolding and page-appearance feel. Keeping the animation optional and authored preserves the UI stack/input policy while allowing visual iteration.

Implications:
- UI book close is handled through `ICloseRequestHandler` so the screen remains on the popup stack until the close presentation finishes.
- Category/item data still updates immediately behind a visual cover; item selection remains unanimated.
- EarthTome field-book sheets are treated as 48x40 frame strips by editor import support.
- If the experiment is rejected, the screen can clear `EncyclopediaBookPresentation` references and continue using the prior reveal fallback.

## 2026-05-20 - Encyclopedia Book Presentation Uses Animator Clips

Decision:
The encyclopedia UI book, page content-appear cover, and EarthTome world book presentation should be driven by authored `AnimatorController` states and `AnimationClip` assets, not by runtime sprite-frame stepping coroutines.

Reason:
The book/page presentation is authored visual rhythm. Letting an `Animator` own sprite frame binding makes the state visible and replaceable in Unity, matches the project's presentation authoring direction, and avoids hiding clip structure inside runtime code.

Implications:
- `EncyclopediaBookPresentation` only requests states such as `Open`, `Close`, `Opened`, `Closed`, and `ContentAppear`; it uses clip lengths only to sequence callbacks.
- `BookWorldSpriteSequencePresentation` keeps its legacy class name for serialized reference compatibility, but it now requests EarthTome Animator states such as `ClosedIdle`, `OpenedIdle`, `Open`, and `Close`.
- `EncyclopediaV1AssetBuilder` is responsible for creating/wiring the temporary `.anim` and `.controller` assets through UnityEditor APIs.
- Manual prefab YAML patching is not an acceptable substitute for UnityEditor API generation when adding Animator components or serialized clip/controller references.

## 2026-05-20 - Encyclopedia Entry List Uses Prefab-Instantiated Slots

Decision:
The encyclopedia entry list uses an authored `EncyclopediaEntrySlot.prefab` instantiated and pooled under an authored `entryGridRoot`. Serialized `entrySlots` remain only as a migration fallback for old generated prefabs.

Reason:
The fixed 48-slot vertical list was hard to style, capped visible entries, and produced solid rectangular rows that competed with the book art. A prefab slot gives the UI a single editable holder/highlight/icon structure while keeping runtime creation limited to approved authored instances.

Implications:
- Runtime encyclopedia code may instantiate `EncyclopediaEntrySlot.prefab` but must not create raw `Button`, `Image`, `TMP_Text`, `Canvas`, or arbitrary UI hierarchy.
- `EncyclopediaScreen.prefab` must wire both `entryGridRoot` and `entrySlotPrefab` to use the new grid path.
- `EncyclopediaEntryButton` should resolve named `Icon`, `IndexText`, and `TitleText` children before fallback searches, so the holder background is not mistaken for entry art.
- Final visual layout can be tuned by editing the slot prefab and grid root in Unity without changing the runtime list population policy.

## 2026-05-22 - Encyclopedia Detail Uses Fixed Right-Page Presenter

Decision:
The encyclopedia right-page detail layout uses dedicated fixed presenters instead of reusing inventory `ItemDetailPanel`. This entry originally introduced `EncyclopediaDetailPanel`; the current Item tab presenter is now `EncyclopediaItemRightPage`, and `EncyclopediaDetailPanel` is retained only as a migration fallback.

Reason:
`ItemDetailPanel` is a hover UI singleton that adopts itself to the hover canvas and hides itself during `Awake()`. Reusing it inside the book page would fight the authored RightPage layout and inventory hover lifecycle.

Implications:
- `EncyclopediaItemRightPage` is the preferred Item RightPage wiring target.
- `EncyclopediaDetailPanel` may still exist on older layouts while serialized references migrate.
- RightPage presenters must not create arbitrary runtime UI hierarchy and must not invoke inventory hover controllers, drag/drop, or item detail popup behavior.

## 2026-05-22 - Encyclopedia Current Slice Is Item First

Decision:
The current encyclopedia implementation slice is limited to the Item tab, with `Weapon`, `Relic`, and `Consumable` sub-tabs active. Monster pages, boss pages, monster theme tabs, and boss-specific tabs/details are deferred.

Reason:
The full encyclopedia UI would take too long to finish in one pass, and the user is actively authoring the item-side layout first. Keeping the slice narrow prevents unfinished Monster/Boss UI from blocking item iteration.

Implications:
- `EncyclopediaScreen` keeps Monster/Boss code paths but exposes them as disabled serialized category toggles by default.
- Current scene/prefab authoring should wire and validate the Item tab, including weapon/relic/consumable layouts.
- Deferred category work is tracked in `Docs/RefactorBacklog/EncyclopediaDeferredCategories.md`.

## 2026-05-22 - Encyclopedia UI Uses Layout-Scoped View Components

Decision:
Split encyclopedia UI driving into layout-scoped tab/page components while keeping `EncyclopediaScreen` as the popup-stack and presentation owner.

Reason:
The item encyclopedia layout is now authored directly in Unity. Keeping every title, tab, page button, slot grid, and detail reference on `EncyclopediaScreen` makes the Inspector hard to reason about and couples layout editing to screen-level state.

Implications:
- `EncyclopediaScreen` owns open/close policy, main-tab entry, and presentation sequencing.
- `EncyclopediaItemTab` owns Item sub-tab, page, selection, and item data binding.
- `EncyclopediaItemLeftPage` owns authored Item LeftPage controls such as title, item sub-tab buttons, pagination labels, and list notices.
- `EncyclopediaItemRightPage` owns authored Item RightPage header/detail roots and weapon ability block pooling.
- `EncyclopediaEntryGridView` owns the slot pool and page binding under the authored grid root.
- `EncyclopediaLeftPageView` and `EncyclopediaDetailPanel` remain migration fallbacks only.
- View components relay user input upward and project state downward; they should not own catalog selection or gameplay state.

## 2026-05-22 - Encyclopedia Item Data Reads ItemDatabase Directly

Decision:
The item/weapon side of the encyclopedia reads `ItemDatabase.allWeapons` directly instead of requiring `EncyclopediaCatalogSO` as a copied weapon list.

Reason:
Weapon, relic, and consumable source data already exists in item definition/database assets. A separate catalog for those entries creates duplicate maintenance and makes the encyclopedia look like another source of truth.

Implications:
- `EncyclopediaItemTab` uses `ItemDatabase` for weapon/relic/consumable counts, names, icons, and detail binding.
- `EncyclopediaCatalogSO` remains only as a legacy fallback and temporary Monster/Boss display-data path until those pages get their own source/provider shape.
- `EncyclopediaInteractable` should no longer require a catalog for the item page to open.
- Relic/Consumable subtabs also read existing item data sources directly rather than adding copied catalog entries.

## 2026-05-20 - DemonKing EgoSword Subpatterns Stay In GAS

Decision:
EgoSword dropped VerticalStrike and CrossLaser are DemonKing-owned GAS abilities, but they run through `ParallelIndependent` AbilityDefinitions triggered by EgoSword's independent dropped-pattern runner instead of the main `BossPhaseConfig` pattern timer.

Reason:
The sword's dropped behavior is still part of DemonKing's pattern kit, so it should remain inspectable and cancellable through GAS. Its cadence is intentionally separate from the boss FSM's main pattern selection, so putting it in phase selection would couple two timers that should stay independent.

Implications:
- `DemonKingController` registers the two subpattern AbilityDefinitions separately from phase pattern entries.
- `EgoSwordActor` owns only the interval/toggle runner and visual state cleanup, then asks DemonKing's AbilitySystem to activate the subpattern ability.
- The subpattern AbilityDefinitions must remain `ParallelIndependent` and instant, otherwise they can be blocked by the boss's active main pattern execution.
- EgoSword should not grow its own runtime-created AbilitySystem for these patterns unless a future design explicitly separates sword ownership from DemonKing.

## 2026-05-20 - Hit Impact Cue Classification Lives On ALData

Decision:
Hit impact cue classification is authored on ALData at the actual hit unit, not on `AbilityDefinition`.

Reason:
Combo attacks, multi-hit patterns, and projectile/helper hit configs can mix slash, blow, or suppress-hit styles inside one AbilityDefinition. ALData already owns execution presentation intent and builds the `CombatHitPayload`, so it is the narrowest authoring layer that can describe each hit correctly.

Implications:
- Runtime transport is `ALData -> CombatHitPayload -> CombatDamageAction -> AbilityEventData -> AbilityHitCueRouter`.
- `AbilityDefinition` hit-confirmed cue lists remain for explicit reusable cues such as hit spark and camera shake.
- `HitImpactCueKind.Default`, `Slash`, and `Blow` currently resolve to `SlashHit` until a separate BlowHit cue exists; `None` suppresses the automatic impact cue.
- New combo or hit-config ALData should expose the classification at the same granularity as the hit payload it creates.

## 2026-05-20 - Runtime Split And Summoned Mobs Do Not Drop Monster Loot

Decision:
General mobs created by runtime split or boss/local summon paths suppress the normal `Mob.OnDeathStarted` monster loot request. Spawn-registered mobs from `MonsterSpawner` / `MonsterSpawnContainer` keep the existing monster drop behavior.

Reason:
Split and summon mobs are not authored baseline room population. Letting every temporary child roll the full monster loot table can multiply rewards, including field-heal pickups, beyond the intended room placement.

Implications:
- Slime split descendants can still inherit room/chest lock tracking, but they do not spawn monster loot on death.
- SlimeQueen call/drop summons and Witch retreat skeleton summons are marked no-loot immediately after instantiation.
- Boss rewards, boss physical drops, graves, chests, and direct world pickups are outside this policy.

## 2026-05-20 - Construction Sites Use Additive Tilemap State Modules

Decision:
Run construction shortcuts use scene-authored additive `ConstructionSiteTilemapModule` blocks with `BlockedState` and `OpenState` roots instead of editing the main Ground/Wall tilemaps at runtime.

Reason:
Construction completion needs a visible wall-before/path-after map change, but raw `Tilemap.SetTile` mutation would be harder to author, review, save, and validate. A block module keeps temporary wall tilemaps, open ground tilemaps, optional Door/Shortcut anchors, and optional Chest objects together as one scene-authored construction site.

Implications:
- `RunConstructionNpcFeature` should call the construction site module when assigned; its direct blocked/open root and target door fields are fallback authoring only.
- Save data stores semantic construction completion by `constructionId`, not individual tile cells.
- Tilemap consumers that scan the scene must ignore inactive tilemaps, and pathfinding must accept additive open ground tilemaps after completion.

## 2026-05-20 - Run-Special NPC Interaction Freezes Time With Unscaled Presentation

Decision:
Run-special NPC interaction pauses the run timer and `Time.timeScale`, while its speech-bubble typing, line skip, choice UI, and letterbox presentation run on unscaled time.

Reason:
These NPCs are an interaction flow rather than live combat dialogue. The player should not lose run time or be attacked through scaled gameplay while reading construction/teleport choices, but the presentation still needs to animate and accept Dialogue-style skip input.

Implications:
- `RunSpecialNpcInteractor` owns the time-scale pause and restores the previous value during cleanup.
- `SpeechBubble` uses unscaled DOTween updates so bubble typing, auto-hide, fade, and scale presentation continue while `Time.timeScale` is `0`.
- Run-special line skip uses left click or `InputActionId.DialogueAdvance`; run-special choices remain click/number-key only so Space does not confirm choices.
- The existing `CinematicLetterboxOverlay` can be reused for letterbox plus HUD fade, but run-special NPCs pass an explicit faded layer list that excludes `GlobalCanvasLayer.Dialogue` so the authored `DialogueCanvas` choice panel stays visible and interactive.

## 2026-05-20 - Run-Special Speech Bubbles Pre-Size Only In Special NPC Flow

Decision:
Run-special NPC dialogue uses a dedicated pre-sized speech-bubble call that measures the full line, clamps maximum text width, enables wrapping, and then types inside the prepared bubble. General speech-bubble users keep the existing empty-text-then-grow `Speak(...)` path.

Reason:
Construction/teleport NPC lines are longer and should feel closer to dialogue UI, but globally changing `SpeechBubble` sizing would alter unrelated short world barks and cinematic bubbles.

Implications:
- `RunSpecialNpcInteractor` owns the opt-in sizing limits for this flow.
- `SpeechBubbleComponent.Speak(...)` remains the compatibility path.
- `SpeechBubbleComponent.SpeakWithPreSizedLayout(...)` is the explicit path for dialogue-style wrapping.

## 2026-05-22 - Authored GlobalUIRoot Encyclopedia Uses Scoped Wiring Tool

Decision:
The current authored `GlobalUIRoot.prefab` encyclopedia layout is repaired through `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia`, not through `EncyclopediaV1AssetBuilder` and not through direct prefab YAML string patches.

Reason:
The active encyclopedia layout is user-authored under `GlobalUIRoot/EncyclopediaUI/Book`. The V1 builder can rebuild generated shell assets and has previously been a risky path for preserving the current layout. A scoped UnityEditor API tool can wire existing components and report missing authored roots while keeping visual placement under Unity authoring control.

Implications:
- `EncyclopediaV1AssetBuilder` remains a generated-asset support tool, but it should not be used as the repair path for the current `GlobalUIRoot` layout.
- Prefab reference repairs should use `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` or direct Unity Inspector work.
- The wiring tool should not create replacement visual layout, item sub-tab buttons, or detail section roots for the current authored layout. Missing roots are authoring gaps to fix in Unity.
- After running the wiring menu, Inspector review is still required because duplicate generic names in authored UI can bind to the wrong candidate.

## 2026-05-22 - Encyclopedia Ability Switch Is Variant-Only

Decision:
`Panel_AbilityBlock_Encyclopedia` represents one displayed weapon skill. Skill1 and Skill2 are separate block instances. The switch guide and preview behavior are shown only when that specific skill has multiple tooltip variants through `IAbilityTooltipVariantProvider`.

Reason:
The switch UI represents replacement-mode skills such as LightningSpear, not the fact that a weapon has two different skill slots. Treating normal Skill1/Skill2 as one switchable panel collapses distinct abilities into a confusing single UI block.

Implications:
- Normal weapons should show two separate ability blocks when both Skill1 and Skill2 exist.
- Variant-capable skills may reserve an extra preview block for the switch animation, but that preview belongs to that one skill's variant cycle.
- `AbilityBlockContainer` must be authored or normalized as a vertical list that controls child height so pooled block instances do not overlap.

## 2026-05-22 - Encyclopedia Item RightPage Uses Type Sections

Decision:
The encyclopedia Item RightPage uses common `Icon` / `Name` / `Description` fields plus item-type section roots instead of one shared metadata/story presenter for Weapon, Relic, and Consumable.

Reason:
Weapon, Relic, and Consumable detail layouts have different information contracts. Automatically injecting ID, rarity, max level, restore amount, or target attribute into one shared text path made the UI harder to author and did not match the planned encyclopedia layout.

Implications:
- Weapon entries show story, stat text, and separate Skill1/Skill2 ability blocks. They do not show a relic-style level preview.
- Relic entries show description, preview level, and `RelicLogic.BuildTooltip(...)` effect text. They do not show weapon stats or weapon skill blocks.
- Consumable entries show only name and description in the current encyclopedia scope.
- Any additional metadata must be added as an explicit authored section decision, not as a shared fallback string.

## 2026-05-22 - EncyclopediaScreen Is Orchestration Only

Decision:
`EncyclopediaScreen` owns only stack participation, open/close requests, main-tab routing, and book/dim presentation. Item sub-tab state, page index, selection, grid binding, and detail binding belong to `EncyclopediaItemTab`, `EncyclopediaItemLeftPage`, and `EncyclopediaItemRightPage`.

Reason:
Putting Item data and page state directly on the screen made the Inspector hard to understand and created multiple competing places for button/page/detail references. The screen should be readable as the popup shell; Item behavior should be readable in the Item tab components.

Implications:
- Screen public button methods remain for Unity Button bindings, but Item-related calls forward to `EncyclopediaItemTab`.
- `EncyclopediaScreen` should not regain direct item grid/detail/page serialized fields.
- If the screen is authored active, `closeOnRuntimeAwake` snaps the presentation closed and deactivates the screen at Play startup so opening still happens only through Tome/UI stack flow.

## 2026-05-22 - Encyclopedia Item RightPage Shares Story And Preview Roots

Decision:
The Item RightPage uses one shared `StoryText` body for Weapon, Relic, and Consumable descriptions, and Weapon stat output may share the same authored root as Relic level preview.

Reason:
The current authored layout is one detail panel whose internal elements are switched on/off per item type. Splitting description fields or forcing separate stat/preview roots added confusing fallback paths and did not match the layout the user is wiring.

Implications:
- Weapon mode writes story to `StoryText`, shows stat text, and hides Relic preview guide icons even when the stat root is the same object as the Relic preview root.
- Relic mode writes description to `StoryText`, shows preview level/effect, and enables preview guides only when the relic has multiple levels.
- Consumable mode writes description to `StoryText` and keeps weapon/relic-only elements inactive.
- Legacy metadata or type-specific description text fields should remain migration-only and should not be touched by the primary binding path.

## 2026-05-22 - Encyclopedia Book Presentation Is A Required Screen Dependency

Decision:
The authored encyclopedia screen must have an `EncyclopediaBookPresentation` on the Book/screen root when Tome open/close and tab page-turn animations are expected.

Reason:
Without that component, `EncyclopediaScreen` and `EncyclopediaItemTab` can only perform immediate content binding. That makes Tome interaction and tab changes look like no animation is wired even when Animator clips exist in the child Book asset.

Implications:
- `EncyclopediaScreen` does not add the missing component at runtime. Missing presentation references produce warnings and immediate fallback behavior until the prefab is wired.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` also adds/wires the component on `GlobalUIRoot/EncyclopediaUI/Book`.
- Direct prefab YAML string patching remains avoided; persistent prefab repair should happen through Unity serialization or the scoped wire tool.

## 2026-05-22 - Encyclopedia Runtime Does Not Self-Wire Missing UI References

Decision:
The active encyclopedia runtime path does not create presenter components, canvas/button/TMP/image objects, or missing UI references during `Awake`, `OpenUI`, or item binding. Runtime code validates required serialized references, logs explicit warnings, and disables or falls back only at the feature level.

Reason:
Silent runtime self-repair made the Inspector unclear and hid the difference between the intended authored `GlobalUIRoot` layout and accidental fallback behavior. The current goal is a stable authoring contract where `EncyclopediaScreen -> EncyclopediaItemTab -> EncyclopediaItemLeftPage / EncyclopediaItemRightPage` is visible in the prefab.

Implications:
- `Reset()` and explicit `Auto Wire References` context menus remain authoring conveniences; normal runtime binding should use serialized references.
- `Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia` is the persistent repair path for `GlobalUIRoot.prefab`.
- `EncyclopediaEntryGridView` may instantiate the authored slot prefab pool, and `EncyclopediaItemRightPage` may instantiate the authored ability-block prefab pool. These are approved content pools, not fallback hierarchy construction.
- Missing `screenActiveRoot`, `itemTab`, `EntryGridView`, `StoryText`, `ScrollRect`, or ability-block references should be treated as prefab/scene wiring issues instead of being hidden by broad child searches.

## 2026-05-22 - EncyclopediaUI Is The Screen Active Boundary

Decision:
The encyclopedia popup active boundary is `EncyclopediaUI`, not the child `Book` object.

Reason:
The authored layout can place `DimPanel`, `Book`, and page/presentation objects as siblings under `EncyclopediaUI`. Closing only `Book` can leave `DimPanel` active and black out the screen even though the encyclopedia content appears closed.

Implications:
- `EncyclopediaScreen.screenActiveRoot` should resolve to or be wired to `EncyclopediaUI`.
- `OpenUI()` activates `screenActiveRoot` before resolving/presenting content.
- Runtime startup close and `CloseUI()` deactivate `screenActiveRoot`.
- `Book` remains the animation target for open/close/page-turns, not the popup lifetime root.

## 2026-05-23 - Run Special NPC Camera Focus Reuses Gameplay Camera State

Decision:
Run-special NPC dialogue focuses the existing `CameraBootstrap` gameplay `CinemachineCamera` on the NPC target and restores the cached gameplay camera state after the interaction. It does not create a new manager or runtime UI/camera hierarchy.

Reason:
The requested dialogue-start camera move matches the Merchant activation cinematic pattern. Reusing the gameplay camera target swap keeps the feature source-only and avoids adding another camera ownership layer for a local NPC conversation.

Implications:
- `RunSpecialNpcInteractor` caches Follow/LookAt/Priority, legacy `CameraFollow` enabled state, and `CinemachineBrain.IgnoreTimeScale` before focusing.
- Camera blending runs with unscaled time because run-special dialogue pauses `Time.timeScale`.
- Scene authoring may assign a specific `cameraFocusTarget`; otherwise the speech-bubble transform is used, then the NPC transform.
- User choices are framed on the player: the flow returns the camera to the player before showing the authored choice panel, then refocuses the NPC only when the selected choice has NPC response lines.
- Manual play validation is required for scene-specific framing and damping.

## 2026-05-25 - Flowering Dash Augment Keeps Dash Global

Decision:
`개화` Bloom dash behavior uses a narrow `IWeaponDashAugment` opt-in hook from `AbilityLogic_Dash2D`; it does not add a dash `WeaponAbilitySlot` or route dash through weapon input selection.

Reason:
Dash is authored and executed as the global `AD_Dash`, while weapon attacks and skills use `WeaponAbilitySelector -> WeaponAbilityBridge -> AbilitySystem`. Extending weapon slots for one weapon's dash modifier would blur that boundary and risk changing all dash input behavior.

Implications:
- Bloom state lives in `FloweringRuntimeData` and a transient `FloweringRuntimeState` attached to the player while active.
- `AbilityLogic_Dash2D` only asks for an optional augment; if none is active, global dash behavior is unchanged.
- Bloom can keep dash cooldown at zero and add three delayed slash hitboxes without making dash a weapon-owned ability.
- The current Flowering presentation is runtime-created prototype presentation; if reused, move it to authored prefab/material references under the presentation contract.

## 2026-05-25 - Flowering Bloom Splits World Dim From UI Border

Decision:
Flowering Bloom uses a world `SpriteRenderer` DimPanel on `Sorting Layer = Entity`, `Order in Layer = -1` for black dimming, while the red Bloom border remains a raycast-free UI overlay using `AffectionGradientBorderGraphic`.

Reason:
The black DimPanel must darken tiles/background without covering player, monsters, effects, or HUD. The affection gradient border already gives the desired soft screen-edge quality and should stay in UI space.

Implications:
- `FloweringBloomData` owns the DimPanel sorting values, cut-in zoom values, eye-flash frames/settings, and Flowering OFF/ON weapon sprites.
- `FloweringBloomPresentationController` may create runtime-only DimPanel and eye-flash objects for the current v1 implementation and must destroy them during Bloom cleanup/weapon release.
- Camera zoom is applied to the existing `CameraBootstrap` player `CinemachineCamera` and restored from cached lens values after the cut-in.
- If Flowering cut-in presentation becomes reusable, migrate the runtime-created presentation objects into authored prefab/scene references under the presentation authoring contract.

## 2026-05-25 - Flowering Cut-in Uses SpriteMask Reveal And Player_Idle Silhouette

Decision:
Flowering weapon OFF/ON reveal uses a target sprite overlay with a runtime `SpriteMask`, and the player blackout cut-in uses the first frame of `Player_Idle.anim` as a fixed silhouette. `Eagle-eyed_YesPlayer` remains a reference image only.

Reason:
The reveal needs handle-to-blade spatial control that is easier to tune with mask position/size than material UV parameters. The blackout cut-in must match the player's actual idle silhouette while letting `Eagle-eyed_NoPlayer` remain a separate one-shot eye flash layered over the player.

Implications:
- Keep the old reveal material fields serialized on `FloweringBloomData` for compatibility, but do not use them as the runtime reveal gate.
- `FloweringBloomPresentationController` must restore the hidden `PlayerRender`, destroy the silhouette, and clear any reveal masks during Bloom cleanup/weapon release.
- If this cut-in becomes shared presentation, migrate the runtime-created silhouette/reveal objects to authored prefab references under the presentation authoring contract.

## 2026-05-30 - DemonKing Animation Replay Guards Are Pattern-Specific

Decision:
DarkLord/DemonKing body animation replay guards apply to single-commit pattern clips, not to every named state. Multi-hit or multi-rush motions such as DashStab can restart per strike, while Slash and GroggyCounter keep stricter replay guards and frame-hold sampling.

Reason:
The one-start-per-pattern guard fixed accidental replay loops for Groggy, GroggyCounter, and Slash, but applying the same rule to DashStab hid the intended three-attack rhythm. The guard boundary must follow pattern semantics rather than clip name alone.

Implications:
- Use `PlayPatternAnimationOncePerPattern(...)` only when the pattern has one visual commit for that state.
- Use normal `PlayPatternAnimation(...)` for per-hit DashStab/rush starts.
- GroggyRecoverCounter should flow Groggy eye flash -> GroggyCounter impact frame -> combat idle, using `DarkLordGroggyReleaseVfx` for sword-held counter impacts and `DemonKingImpactVfx` for hand-state counter impacts.
- Pattern motion windows should hold DemonKing face-target locks so auto-facing does not flip the body during authored attack poses.

## 2026-05-30 - DemonKing DarkLord Effects Use Generated Resource VFX

Decision:
New DarkLord effect sheets for DemonKing combat are integrated as generated `Resources/DemonKing/Vfx` Animator-prefab assets, with runtime code spawning named VFX helpers instead of reading sprite sheets directly.

Reason:
The existing DemonKing non-laser VFX path already validates Resources prefabs, Animator states, and sorting. Extending that path keeps effect authoring repairable through `DemonKingPatternVfxAssetBuilder` and avoids adding animation-event or runtime sprite-loading dependencies.

Implications:
- `DarkLordExplosion2` is used only where the pattern asks for that stronger explosion style: HeavySlash follow-up line explosions, Bombardment lane explosions, and 10% FinalDesperation bombs.
- Circular explosion/impact cue overrides ignore direction-vector rotation and use only their authored rotation offset, so delayed circle explosions do not inherit placeholder directions such as `Vector2.down`.
- `DarkLordFragment` is a separate crack visual: timed after general `DemonKingImpactVfx`, persistent while EgoSword is fixed in the ground, and faded/cleared by the owning runtime path.
- HP50 charge and EgoSword spin are loop-follow visuals. The sword spin follows position but not sword rotation, matching the authored spinning effect sheet.
- Unity import or `Tools/DemonKing/Rebuild Pattern VFX Assets` must create/repair the generated Resources assets before runtime loads the new prefabs.

## 2026-05-30 - DemonKing Visual Tuning Uses Pattern-Unit Workbench

Decision:
`Tools/DemonKing/Visual Tuning Preview` treats `AL_DemonKing_*` assets as the pattern-level visual tuning source of truth. The Pattern Workbench shows a synthesized phase timeline for each pattern and groups quick controls by timing, animation, VFX, warning/hit, SFX/shake, and movement, while the full serialized asset inspector remains available below it.

Reason:
DemonKing visual timing is authored at the pattern level, not as isolated clips or VFX assets. Frame holds, warning fill, body pose changes, impact VFX, sockets, SFX, shake, and movement beats need to be reviewed together before Play Mode validation.

Implications:
- The Workbench is Editor-only and does not execute live Ability coroutines or own runtime state.
- New tunable pattern policy values should live on the relevant `AbilityLogic_DemonKing*` asset when designers need to tune them, instead of staying hidden as code constants.
- Serialized fields should be added conservatively and with defaults matching current behavior because existing ScriptableObject assets require Unity import/Inspector review.
- Scene/prefab YAML is not modified by this tool; final pattern feel still requires Play Mode checks.

## 2026-05-30 - DemonKing GroggyCounter Visuals Are Branch-Owned

Decision:
`AbilityLogic_DemonKingGroggyRecoverCounter` owns separate `swordVisual` and `handVisual` branch structs for groggy pose, counter animation, impact VFX, impact socket, and eye-flash socket/offset/size.

Reason:
Sword-held and hand-state counters are now intentionally different reads: sword keeps the slash/release style and secondary eye point, while hand uses DemonKingImpact/DarkLordImpact-style release and the primary eye point. Keeping both under one generic field caused the Workbench to hide the real branch differences and made runtime edits easy to misapply.

Implications:
- Sword branch defaults to `DarkLord_Sword_Groggy`, `DarkLord_Sword_GroggyCounter`, `DarkLordGroggyReleaseVfx`, `SwordCounterOrigin`, and `EyeFlashSecondary`.
- Hand branch defaults to `DarkLord_Hand_Groggy`, `DarkLord_Hand_GroggyCounter`, `DemonKingImpactVfx`, `HandCounterImpact`, and `EyeFlash`.
- Common damage, knockback, dim timing, warning ping SFX, and shake remain shared on the AL. Counter impact SFX is split through Sword/Hand `SoundRef` slots so it can match the selected GroggyCounter branch.
- Existing AL assets need Unity import/Inspector review for the new nested serialized fields.

## 2026-05-30 - DemonKing Pattern Presentation Mapping Is Asset-Owned

Decision:
DemonKing pattern body animation and VFX selection should be editable from the Pattern Workbench through serialized fields on the owning `AL_DemonKing_*` asset, with `EgoSwordActor` owning sword-specific subpattern cue refs.

Reason:
Designers need to tune a whole pattern as one unit: body clip choice, hold/sample policy, VFX prefab override, built-in fallback, socket, scale, rotation, and resource fallback must be visible beside the synthesized pattern timeline. Keeping this data on the existing runtime owner avoids a second profile asset and keeps Play Mode behavior tied to the same fields shown in the tool.

Implications:
- `DemonKingBodyAnimationRef` and `DemonKingVfxCueRef` are the preferred pattern-facing presentation reference structs for new DemonKing visual mappings.
- The Workbench `Animation / VFX Mapping` panel should expose the runtime-owned fields rather than only showing hardcoded preview descriptors.
- Existing AL assets and `EgoSwordActor` instances require Unity import/Inspector review after new serialized fields are added.
- The Workbench remains Editor-only preview; it does not execute live pattern coroutines or replace Play Mode validation.

## 2026-06-02 - Split Monsters Share One KillLock Tracking Unit

Decision:
Room and chest KillLocks count a Slime split chain as one original monster unit. Split children join the parent's tracking unit, and the unit is removed only when no registered member in that chain is alive.

Reason:
Counting every split child as a new lock target makes doors and chests evaluate split monsters inconsistently. The intended policy is that the original monster contributes one lock count, but that count remains alive until all descendants produced by the split are cleared.

Implications:
- `MonsterSpawnRoomGroup`, `RoomDoorMonsterKillLock`, and `ChestMonsterKillLock` should register and count `MonsterLockTrackingUnit` instances, not raw GameObjects.
- Navigation arrows should point at the live representative of a tracking unit, so arrows move from a dead original to a living split child.
- General direct summons remain outside KillLock counts unless they enter through spawn registration or explicit split inheritance.

## 2026-06-02 - SlimeQueen Speech Uses BossSpeechData Without Controller Merge

Decision:
SlimeQueen P1/P2 speech text and bubble theme are authored in `SlimeBossSpeechData.asset`, but SlimeQueen keeps its dedicated split, phase-two, callback, and timing flow instead of fully adopting `BossSpeechController`.

Reason:
SlimeQueen speech is coupled to split spawning, castling pair timing, pitfall-return slam timing, and the two-body finale. A data-only bridge centralizes text/theme authoring without forcing those special lifecycle rules into the generic boss speech controller.

Implications:
- Add new SlimeQueen speech situations only at the end of `BossSpeechSituationEnum` to preserve existing serialized enum values.
- Use `SlimeQueenBossBase` speech helpers for SlimeQueen data-backed lines; keep pattern durations, offsets, and callbacks at their current SlimeQueen call sites.
- `SlimeQueen`, `SlimeQueenP2Short`, and `SlimeQueenP2Long` prefabs must keep `slimeQueenSpeechData` assigned to the same `SlimeBossSpeechData.asset`.

## 2026-06-02 - DemonKing And EgoSword Speech Use Separate Situation Keys

Decision:
DemonKing sword-related patterns use separate `BossSpeechSituationEnum` keys for DarkLord/body speech and EgoSword-position speech instead of adding a new serialized speech schema.

Reason:
`BossSpeechData` is already a simple situation-to-lines authoring asset, and changing its schema would create ScriptableObject migration risk. Appending speaker-specific enum values preserves existing serialized enum values while letting designers fill DarkLord and EgoSword lines independently for the same pattern.

Implications:
- `DemonKingThrowEgoSword`, `DemonKingRecallEgoSword`, `DemonKingEgoSwordVerticalStrike`, and `DemonKingEgoSwordCrossLaser` are DarkLord/body speech keys.
- `EgoSwordThrowEgoSword`, `EgoSwordRecallEgoSword`, `EgoSwordVerticalStrike`, and `EgoSwordCrossLaser` are EgoSword-position speech keys.
- Throw/Recall can append step-specific keys such as `EgoSwordThrowEgoSwordRelease`, `DemonKingRecallEgoSwordRetort`, and `EgoSwordRecallEgoSwordRetort` when a pattern needs fixed turn order but each turn should still use the existing random `lines[]` lookup.
- Parallel speech placement is runtime-only and anchor-aware: `SpeechBubbleComponent` scores tail side, actual world bounds, tail pivot distance from the bubble root, overlap, screen bounds, and small layout nudges before applying a placement. Single/NPC `Speak(...)` keeps the default left-tail placement.
- Parallel DemonKing/EgoSword two-person dialogue uses a pair solver. The active DarkLord bubble may flip tail side but does not move; the EgoSword parallel bubble can use only a small bounded nudge, so the solver prefers flipping the active bubble over pulling the sword bubble far from its socket. The existing background sprite can be X-flipped, text is counter-flipped to stay readable, and the background layout padding is swapped on flip so typing starts from a stable visual side.
- Throw/Recall speech rhythm tuning belongs on the owning DemonKing AL assets, not `BossSpeechData`; text data stays line-only while pattern timing remains Inspector-facing pattern data.
- Throw landing speech uses the appended `EgoSwordThrowEgoSwordPlant` key and is emitted by `EgoSwordActor` when the throw planting transition completes, because the Throw AL has already handed off to sword flight by then.
- Inactive EgoSword speech resolves through separate `EgoSwordActor` socket fields: `inactiveThrowSpeechSocket` defaults to `SwordThrowOrigin`, and `inactiveRecallSpeechSocket` defaults to `SwordThrowReturnOrigin`. This keeps throw pre-release and recall-response anchors independently tunable through the existing DemonKing socket map without scene YAML edits from code.
- EgoSword speech fine-positioning is `EgoSwordActor`-owned: the actor exposes `egoSwordSpeechOffsetDelta` as the single Inspector source of truth, DemonKing ALs consume it through `EgoSwordActor.SpeechOffsetDelta`, and actor-emitted throw landing speech uses the same field.
- The post-recall gap is AL-owned timing through `postRecallPatternDelaySeconds`; delaying the Recall ability completion is what prevents the next main DemonKing pattern from starting immediately after sword recovery.
- Pattern code may try both keys; missing lines remain no-op through the existing `BossSpeechData.GetLine(...)` path.

## 2026-06-02 - Same-Scene Teleport Arrival Treats Hole Crossing As Airborne Presentation

Decision:
`RunSameSceneTeleportNpcFeature` treats the movement from `appearancePoint` to `landingPoint` as an airborne arrival presentation. The final landing endpoint must still be safe, but the `appearancePoint` and intermediate path may cross `HoleTrap` space, and the player body collider is suppressed until the arrival movement finishes.

Reason:
The previous ground-path `HoleTrap` sampling treated a jump-like arrival as if it were normal grounded traversal. If the authored appearance-to-landing line crossed a hole, the feature skipped the landing movement and collapsed to a direct warp, hiding the intended arrival presentation.

Implications:
- Keep landing endpoint validation before teleport execution.
- Do not reintroduce `appearancePoint` or intermediate `HoleTrap` sampling as an arrival-movement skip condition. The initial warp to `appearancePoint` may bypass `HoleTrap` target validation because this point is the airborne presentation start, not the final safe ground position.
- Keep `PlayerCinematicProtection` and `PlayerTargetabilityBlocker` active until the true end of arrival presentation.
- Play Mode validation must confirm the landing movement is visible, no pitfall starts during arrival, the player body collider restores, and the final landing position remains safe.

## 2026-06-02 - Same-Scene Teleport Landing Start Particle Is Feature-Owned

Decision:
The same-scene teleport landing-start particle is authored directly on `RunSameSceneTeleportNpcFeature` as a local `SpawnedPresentationHook`. It spawns at the resolved `appearancePoint` after fade-in and before the appearance hold / movement to `landingPoint`.

Reason:
The particle timing belongs to this feature's arrival presentation rather than shared dialogue flow or scene transition fade. Keeping it feature-owned lets each teleport NPC tune its own start-point VFX without adding another manager, cue requirement, or scene runtime fallback.

Implications:
- Existing teleport NPC components gain a new serialized `landingStartParticle` field and need Unity Inspector/import review.
- Empty `landingStartParticle` preserves existing behavior.
- The spawned prefab should be visual-only; gameplay safety still comes from endpoint validation, cinematic protection, targetability blocking, and body collider suppression.
- Play Mode validation must confirm particle sorting, scale, lifetime, position, and timing against fade-in / appearance hold / movement.

## 2026-06-02 - Same-Scene Teleport Arrival Uses A Parabolic World-Y Arc

Decision:
The same-scene teleport movement from `appearancePoint` to `landingPoint` uses `moveToLandingArcHeight` with a quadratic `4h*t*(1-t)` world-Y offset. `moveToLandingCurve` remains the timing control for progress along that arc, and the endpoints stay exact.

Reason:
The arrival should read as a jump-like landing presentation. A direct position lerp can look like sliding even when fade, particle, and landing timing are correct.

Implications:
- Existing teleport NPC components gain a new serialized `moveToLandingArcHeight` field and need Unity import/Inspector review.
- Play Mode validation must tune arc height, movement duration, and timing curve against camera framing, tile clearance, and landing readability.
- Final landing safety validation remains endpoint-based; do not reintroduce intermediate ground-path `HoleTrap` checks for this airborne presentation.

## 2026-06-09 - Use Policy-First Migration-Style Refactoring

Decision:
Treat project-level refactoring as policy-first, migration-style work. Refactors should preserve player-visible behavior and Unity-facing contracts while lowering the cost and risk of the next change.

Reason:
The project now has many interconnected player, loot, save, UI, dialogue, upgrade, scene transition, and runtime service paths. Large visual architecture rewrites would risk scene/prefab references, serialized data, save semantics, and bootstrap lifecycle. The safer direction is to define ownership and validation rules first, then move one responsibility at a time behind existing compatibility facades.

Implications:
- SOLID is applied for practical benefits, not ceremony: clearer responsibilities, safer extension points, smaller contracts, replaceable implementations, and less tangled dependency direction.
- Runtime services must be classified by App, Gameplay Session, Run, Scene, UI Root, or Fallback scope before lifecycle changes; use `RuntimeServiceOwnershipArchitecture.md`.
- Durable profile save fields need a source of truth, commit timing, and overwrite guard before save collection changes; use `ProfileSaveOwnershipArchitecture.md`.
- Scene evidence must be classified; current structure decisions default to `ProtoType*` scenes, while legacy scenes are reference-only; use `SceneClassificationArchitecture.md`.
- `GameDataManager.SaveData()` item unlock preservation with `ItemManager.IsReady` is the next P0 code follow-up, not part of the policy-only documentation slice.

## 2026-06-18 - Prewarm Trace Is Editor-Only

Decision:
`PrewarmTraceRuntime` is an editor-only measurement tool for prewarm recommendation authoring. Player builds must not create the trace runtime service or write `PrewarmTrace.json` under `Application.persistentDataPath`.

Reason:
Prewarm trace data is used before release to identify presentation prefabs that may need manifest prewarm entries. Keeping it active in player builds adds unnecessary runtime file I/O and leaves development measurement data in user save locations without improving release loading behavior.

Implications:
- `PresentationSpawnService` may record spawn trace data in the editor only.
- `PrewarmRecommendationWindow` remains the consumer of editor trace files.
- Release loading readiness still depends on manifest, Addressables registry, Addressables content build, and clean build validation rather than runtime trace capture.

## 2026-06-18 - FirstRun Intro Uses A Separate Loading Scope

Decision:
The title-to-tutorial-to-first-Hub-intro path uses a separate `FirstRunIntro` loading scope instead of being folded into always-on Boot or active-run RouteSet manifests.

Reason:
The intro/tutorial path is shown once per save file and now sits before the Hub start portal creates a RouteSet load window. Keeping those assets in Boot would retain one-time tutorial assets for the whole app session, while keeping them in RouteSet would miss the pre-run tutorial scenes.

Implications:
- `LoadingBootstrapConfigSO.firstRunIntroManifest` is retained while the loaded profile has not completed `hub_intro_after_darklord_seen`.
- The FirstRun manifest is released when the Hub intro marks the configured completion tutorial id as seen.
- Existing saves that initialized a profile but did not finish the DarkLord tutorial route back to `TutorialCorridor` instead of jumping straight to Hub.
- After the DarkLord forced-defeat completion id is saved, unfinished first-Hub-intro saves route to Hub so the Hub intro can complete.
- Release validation must regenerate manifest assets, rebuild the Addressables registry/content, and verify the first-run path separately from run RouteSets.

## 2026-06-18 - Loading Manifests Store Root-Loadable Assets

Decision:
Generated loading manifests should list root-loadable assets only. Dependency-only Unity assets such as sprites, textures, materials, animation clips, animator controllers, tile assets, shaders, and compute shaders should be reached through their owning prefab or ScriptableObject rather than listed directly.

Reason:
Scene dependency collection is intentionally broad and can pull route, boss, monster, weapon, and art dependencies into Boot just because a scene references a manager, database, or presentation object. Listing every dependency directly makes Boot too large and hides the intended split between Boot, FirstRunIntro, and RouteSet scopes.

Implications:
- `RouteSetLoadManifestBuilderWindow` filters dependency-only assets and known non-Boot route content before writing generated manifests.
- If a dependency asset must be directly preloaded, add an explicit root asset or a narrow allowlist instead of broadening Boot.
- Existing generated manifest assets must be regenerated after this policy change, then the Addressables registry/content should be rebuilt before release validation.

## 2026-07-04 - Core Audio Requests Use Backend Contract

Decision:
Core-owned gameplay, cue, and damage code requests sound through Core-level `SoundRef`, `SoundPlaybackContext`, `AudioHandle`, `SoundPlaybackUtility`, and `ISoundPlaybackBackend`. `SoundManager` remains Infrastructure-owned and registers itself as the runtime backend.

Reason:
The asmdef split requires `Core` to stop depending upward on Infrastructure. Audio request data is safe Core contract data, but catalog lookup, pooled sources, looping sources, and volume control remain concrete Infrastructure responsibilities.

Implications:
- Keep `SoundPlaybackUtility` free of direct `SoundManager` calls.
- `SoundManager` may depend on the Core audio contract and provide the backend.
- Core callers should not call `SoundManager.EnsureInstance()` directly.
- Gameplay feature callers should use `SoundPlaybackUtility` for one-shot/loop playback, music start/stop, and combat SFX ducking instead of calling `SoundManager`.
- If a new audio operation is needed by Core, add it to the Core backend contract only when there is a real Core caller, then implement it in Infrastructure.

## 2026-07-04 - Core Presentation Requests Use Backend Contracts

Decision:
Core-owned combat, ability, cue, and effect code may own presentation request data and lightweight routing contracts, but concrete UI/VFX/camera services remain outside Core and register backend implementations. Current Core-facing contracts include `CameraShakePlayback`, `WorldPresentationPlayback`, `DamagePopupPlayback`, `IChainPointPresentation`, `IStaggerGaugePresentationBinding`, and `ITimedHitEffect2D`.

Reason:
The asmdef split requires Core to stop depending on Infrastructure/UI/Feature implementations while preserving existing gameplay data and serialized authoring fields. Moving request data into Core and routing execution through backend contracts preserves call sites and lets concrete presentation services stay in their owning layers.

Implications:
- Do not add direct Core calls to `SoundManager`, `CameraShakeService`, `WorldPresentationRuntime`, `PresentationSpawnService`, `DamagePopupService`, boss UI components, or concrete VFX components.
- If Core needs a new presentation action, add the smallest Core request/backend contract first and implement it in the owning Infrastructure/UI/Feature layer.
- Gameplay feature callers should use `WorldPresentationPlayback` and `ITimedHitEffect2D` instead of concrete `WorldPresentationRuntime`, `PresentationSpawnService`, or `TimedAnimatedHitEffect2D`.
- Generic Unity `GameObject` presentation fields still need later classification; they do not create project-assembly dependencies, but they are not proof that the final Presentation split is complete.

## 2026-07-04 - Core Cue Manager Uses Provider Contracts For Concrete Cue Pools

Decision:
`GameplayCueManager` may own generic cue execution, placement, and auto-destroy timing, but concrete cue prefab pooling/lifetime exceptions are registered through Core `IGameplayCuePrefabInstanceProvider` providers. `GameplayCue_HitSparkParticles` now registers its provider from Presentation and keeps the existing HitSpark pool behavior outside Core.

Reason:
The Core asmdef split exposed `GameplayCueManager`'s direct `GameplayCue_HitSparkParticles` checks as a Core-to-Presentation implementation dependency. Removing the checks outright would change HitSpark pooling and auto-destroy behavior. A tiny provider contract preserves behavior while keeping dependency direction `Presentation -> Core`.

Implications:
- Do not add concrete cue notify type checks inside `GameplayCueManager`.
- Cue-specific pooling or manager auto-destroy suppression should be implemented as a Presentation provider registered against `GameplayCuePrefabInstanceProviders`.
- If several cue providers need ordering, keep provider selection explicit and small rather than turning Core cue execution into a full presentation service locator.

## 2026-07-04 - Screen Shake Settings Use Core Query Contract

Decision:
Presentation-owned camera shake code reads the screen-shake setting through Core `GameSettingsQuery` / `IGameSettingsBackend`. The UI-owned `GameSettingsService` registers the backend and remains the concrete owner of saved settings, display settings, and UI scale application.

Reason:
`CameraShakeService` is Presentation code and must not depend on UI's concrete `GameSettingsService` when asmdefs are introduced. A Core query contract preserves the existing user setting behavior while keeping the dependency direction `UI -> Core` and `Presentation -> Core`.

Implications:
- Do not call `GameSettingsService` directly from Core or Presentation code.
- Add new Core settings queries only when a non-UI assembly has a real caller.
- Keep display/UI-scale application in the UI settings layer until a separate settings architecture pass is approved.

## 2026-07-04 - Telegraph Data Lives In Core And Renderers Live In Presentation

Decision:
Attack telegraph request/style data lives under Core (`AttackTelegraphSpec`, `AttackTelegraphShape`, `AttackTelegraphStyle`, `AttackTelegraphStyleUtility`), while concrete telegraph rendering and lifecycle components live under Presentation (`AttackTelegraphService`, `AttackTelegraphView`, `AttackTelegraphWallClippedMeshView`).

Reason:
Gameplay patterns need stable request data for warning shapes, timing, and style references, but the renderer/view lifecycle is a concrete presentation implementation. Splitting data from renderers removes the old Infrastructure ownership ambiguity and prepares the remaining Gameplay-to-Presentation calls for a smaller contract pass.

Implications:
- Core may own telegraph request data and style assets, but must not own renderer/view objects.
- Presentation telegraph renderers may depend on Core telegraph data.
- Gameplay should reference `IAttackTelegraphPresenter` and `IAttackTelegraphHandle` instead of concrete `AttackTelegraphService`/`AttackTelegraphView`.
- SlimeQueen, DemonKing, DragonBoss, Knight jump slam, general monsters, Shadow monsters, and ShadowBoss telegraph usage have been moved to the Core telegraph contracts. Static search under `Assets/_Project/Runtime/Features` should remain free of concrete telegraph implementation references before `Gameplay.asmdef` is introduced.

## 2026-07-04 - Core Runtime State DTOs And Narrow Markers Stay In Core

Decision:
Runtime DTOs produced or consumed by Core services live in Core even when UI or player save systems display/store them. Current examples are `DamagePopupDuplicateSuppressor`, `ElementGaugeUiModel`, `ActiveGameplayEffectSnapshot`, and `ExplicitTagSnapshot`. Concrete gameplay components should expose only narrow Core contracts when Core needs classification, such as `ICombatTimingProfile` and `IAttackCollisionSource2D`.

Reason:
`Core.asmdef` cannot depend on UI/Features implementations, but Core systems still need stable state snapshots and small classification surfaces. Moving DTOs and marker contracts into Core keeps dependency direction `Features/UI -> Core` while avoiding broad moves of concrete gameplay or UI classes.

Implications:
- Do not place Core-produced state DTOs in UI/Features just because they are displayed or persisted there.
- If Core only needs a boolean/classification capability from a gameplay component, add a narrow Core interface instead of referencing the concrete class.
- Infrastructure/Presentation/UI adapters may register or implement Core contracts, but Core must not call those concrete adapters directly.

## 2026-07-04 - Gameplay Warning Popups Use Core Playback Contract

Decision:
Reusable warning popup reasons and requests live in Core through `WarningPopupCode`, `WarningPopupRequest`, `IWarningPopupBackend`, and `WarningPopupPlayback`. `UIManager` remains the concrete UI owner that resolves localized warning messages and forwards them to `WarningPopupService`.

Reason:
Gameplay systems need to request warning feedback for inventory, shop, upgrade, shortcut, pickup, and debug-cheat outcomes, but they should not depend on the concrete UIManager implementation. Moving the request code and playback contract to Core keeps dependency direction `Gameplay -> Core` and `UI -> Core`.

Implications:
- Features should call `WarningPopupPlayback` instead of `UIManager.Instance.ShowWarning(...)`.
- Keep warning text resolution in UI unless a localization/domain text architecture is explicitly introduced.
- Add new reusable warning reasons to `WarningPopupCode` only when more than one caller or a durable gameplay result needs the code.

## 2026-07-04 - Gameplay Reads UI Block State Through Core Query

Decision:
Gameplay code reads UI input-blocking and popup-open state through Core `UiInteractionStateQuery` / `IUiInteractionStateBackend`. `UIManager` remains the concrete owner of popup stack and external UI input blockers.

Reason:
Player input, upgrade handoff, merchant cinematic, and ability input cancellation need to know whether UI currently blocks gameplay input, but they do not need to know the concrete UIManager implementation. A narrow Core query contract removes Gameplay-to-UI dependency without moving UI command ownership.

Implications:
- Features should not call `UIManager.Instance.HasBlockingUI`, `UIManager.Instance.HasActivePopup`, or `UIManager.Instance.IsExternalUiInputBlocked` directly.
- Keep command-style UI operations separate; do not expand the query contract with screen opening, prompt updates, or popup closing without a separate command contract decision.
- Backend-missing behavior is intentionally non-blocking to match the previous `UIManager.Instance == null` checks.

## 2026-07-04 - Gameplay Sends Common UI Commands Through Core Playback

Decision:
Gameplay code sends common UI cleanup and world-prompt commands through Core `UiCommandPlayback` / `IUiCommandBackend`. `UIManager` remains the concrete UI owner that closes popups, hides hover/prompt UI, and refreshes the world prompt.

Reason:
Dialogue flow, player death, run timeout, encyclopedia interaction, and player prompt code need to request simple UI cleanup without depending on the concrete UIManager assembly. A narrow command backend keeps the dependency direction `Gameplay -> Core` and `UI -> Core` while preserving the existing no-op behavior when no UIManager exists.

Implications:
- Features should not call `UIManager.Instance.CloseAllPopups`, `HideHoverImmediate`, `HideWorldPrompt`, or `RefreshWorldPrompt` directly.
- Do not add screen-opening commands such as `TryPushUI` or `CanOpenUI` to this generic command contract until UI screen ownership and serialized screen references are handled separately.
- Keep command methods no-op when no backend is registered, matching legacy null-check behavior.

## 2026-07-04 - UI Stack Ownership Contracts Live In Core

Decision:
Shared UI stack contracts and external UI input-block ownership live in Core through `IUIView`, `IStackableUI`, `ICloseRequestHandler`, `GameFlowInputBlocker`, `IUiStackBackend`, and `UiStackPlayback`. `UIManager` remains the concrete backend that owns popup stack rules, gameplay locking, and external blocker storage.

Reason:
Gameplay and presentation flow code need to acquire input locks and sometimes open a UI that belongs to that lock, but they should not reference the concrete UI manager assembly. Keeping only the contract and blocker ownership object in Core preserves the dependency direction `Gameplay -> Core` and `UI -> Core`.

Implications:
- Features should use `GameFlowInputBlocker` / `UiStackPlayback` rather than `UIManager.Instance` for owned UI input-block flows.
- Keep concrete stack policy, popup conflict checks, and gameplay lock application in UI.
- Add new stack operations only when a non-UI assembly has a concrete caller; do not turn this into a broad UI service locator.

## 2026-07-04 - Gameplay UI Open Requests Use Narrow Playback Contracts

Decision:
Feature-specific UI open/detail requests should use the narrowest viable playback contract instead of concrete UI singletons. Chest opening uses Gameplay-owned `ChestUiOpenPlayback` because the UI backend consumes concrete `TreasureChest`/`ChestInventory` data. World item hover uses Core `WorldItemHoverPlayback` because callers only need to pass a `Transform`, `ScriptableObject`, and optional relic level.

Reason:
Not every UI request belongs in Core. Contracts that expose concrete gameplay data can live in Gameplay and be implemented by UI, while pure presentation requests with Unity base types can live in Core. This removes Gameplay-to-UI references without forcing gameplay-specific inventory models into Core prematurely.

Implications:
- `TreasureChest` should not call `ChestUIManager` directly; use `ChestUiOpenPlayback`.
- World drops, weapon drops, and shop slots should not call `WorldItemDetailPresenter` directly; use `WorldItemHoverPlayback`.
- If `ChestInventory` later becomes Core-safe, the chest UI open contract can be reconsidered and possibly moved down to Core.

## 2026-07-04 - Cinematic Letterbox And Global Canvas Access Use Core Contracts

Decision:
Gameplay/cutscene code creates and drives cinematic letterbox overlays through Core `CinematicLetterboxPlayback` / `ICinematicLetterboxOverlayHandle`, while UI `CinematicLetterboxOverlay` remains the concrete implementation. Global canvas identity and lookup flow through Core `GlobalCanvasLayer`, `IGlobalCanvasBackend`, and `GlobalCanvasPlayback`, while UI `GlobalUIRoot` remains the concrete owner of authored canvas references and service-root parenting.

Reason:
Cutscenes, dialogue flows, game-over presentation, and tutorial sequences need to fade/hide authored UI layers or parent service objects, but they should not depend on concrete UI root or overlay classes. Moving only the identifiers and request handles to Core preserves the dependency direction `Gameplay -> Core` and `UI -> Core` without moving actual UI hierarchy creation or canvas ownership into Gameplay/Core.

Implications:
- Features should not instantiate `CinematicLetterboxOverlay` or call `GlobalUIRoot` directly.
- Add new cross-layer canvas operations to `GlobalCanvasPlayback` only when a non-UI assembly has a concrete caller.
- Keep authored canvas references, fallback hierarchy lookup, and actual overlay object creation in UI.

## 2026-07-04 - Dialogue Playback Uses Gameplay Contract And UI Implementation

Decision:
Gameplay dialogue callers use `DialoguePlayback` / `IDialoguePlaybackBackend` for dialogue start requests, dialogue-active checks, and dialogue-owned non-dialogue UI suppression. Concrete `DialogueService`, `DialogueController`, and `DialogueRuntimeReferenceResolver` live under UI/Dialogue because they own or resolve `DialogueView`, `CinematicDirector`, `PortraitController`, canvas suppression, and input blocking behavior.

Reason:
Dialogue requests are gameplay-driven and use gameplay data such as `NPCData`, `DialogueStorySegment`, `NPCFeatureController`, and `DialoguePresentationOptions`, so the request contract currently belongs in Gameplay rather than Core. The implementation is UI-facing and must not remain in Gameplay once asmdefs separate `Gameplay` from `UI`.

Implications:
- Features should not call `DialogueService.Instance` or `DialogueService.EnsureInstance()` directly.
- UI `DialogueService` registers the active backend and remains responsible for controller discovery, dialogue input blocking, run timer pause, and UI layer suppression.
- If dialogue request data is later promoted to Core, `DialoguePlayback` can be reconsidered for Core ownership; do not move it there while it exposes gameplay-specific dialogue data.

## 2026-07-04 - Camera Presentation Uses Core Playback Contract

Decision:
Gameplay/cutscene code requests boss focus, death/phase lens focus, target focus, and return-to-player camera sequences through Core `CameraPresentationPlayback` / `ICameraPresentationDirector`. Concrete `CameraPresentationDirector` remains under Presentation/Camera because it owns Cinemachine camera references, priorities, lens animation, and legacy camera follow coordination.

Reason:
Boss dialogue, tutorial, death, ShadowBoss phase, and SlimeQueen finale flows need camera presentation, but they should not reference a Presentation implementation once `Gameplay.asmdef` and `Presentation.asmdef` are separated. The Core contract exposes only sequence operations and Unity base types. Legacy setup still uses `ICameraPresentationSettingsReceiver` with `Component` arguments so Core does not depend on Cinemachine.

Implications:
- Features should not declare or search for concrete `CameraPresentationDirector`; use `MonoBehaviour` serialized references plus `CameraPresentationPlayback` resolution when authored references are needed.
- Presentation `CameraPresentationDirector` registers the factory backend for old `BossTalkManager` auto-add behavior.
- Scene/prefab validation must confirm widened `MonoBehaviour` fields preserve existing component references after Unity import.

## 2026-07-04 - GameOver Presentation Uses Gameplay Contract And UI Implementation

Decision:
Gameplay game-over callers use `GameOverPresentationPlayback` / `IGameOverPresentationBackend` for defeat, time-over, victory, and tutorial fake-game-over presentation requests. Concrete `GameOverPresentationController` lives under UI/GameOver because it owns Canvas/TMP/Button presentation, return animation, UI layer sorting, and `InventoryScreen` inspection-mode integration.

Reason:
The request data is gameplay/run outcome data: player transform, cause kind, run-end reason, remaining time, location, reward amount, hub scene, and tutorial override flags. The implementation is UI-heavy and should not remain in Gameplay once `Gameplay.asmdef` and `UI.asmdef` are separated. A Gameplay-owned playback contract lets UI depend downward on Gameplay data while removing Gameplay-to-UI implementation references.

Implications:
- Features should call `GameOverPresentationPlayback.TryShow(...)`, not `GameOverPresentationController.TryShow(...)`.
- UI may keep direct coordination between `InventoryUIManager`, `InventoryScreen`, and `GameOverPresentationController` because that behavior is inside the UI assembly boundary.
- Scene/prefab validation must confirm the moved controller keeps its authored references through the preserved `.meta` GUID.

## 2026-07-04 - Ending Outro View Uses Gameplay Contract And UI Implementation

Decision:
`EndingOutroPlayer` uses Gameplay `IEndingOutroView` to drive ending outro slides, text, root alpha, slide alpha, skip prompt, and skip fill. Concrete `EndingOutroView` lives under UI/Progression/Ending because it owns TMP, Image, CanvasGroup, glyph rendering, and authored UI widget references.

Reason:
The player owns gameplay-flow concerns: sequence selection, skip/advance input, typing audio cadence, completion callbacks, and runtime view discovery. The view owns UI rendering details. Splitting these through `IEndingOutroView` removes a Gameplay-to-UI implementation dependency while preserving scene-authored references by keeping the serialized field name and moved script GUID.

Implications:
- Features should not reference concrete `EndingOutroView`; use `IEndingOutroView` through the `EndingOutroPlayer.view` MonoBehaviour field or runtime discovery.
- UI `EndingOutroView` may keep UI/input glyph dependencies because it is inside the UI implementation boundary.
- Scene/prefab validation must confirm the widened `EndingOutroPlayer.view` field keeps its assigned view after Unity import.

## 2026-07-04 - Tutorial Presentation HP Uses Gameplay Contract And UI Implementation

Decision:
Tutorial boss sequence and laser playback use Gameplay `ITutorialPresentationHpView` for presentation HP reset, reduction, visibility, refresh, current HP, and depletion event subscription. Concrete `TutorialPresentationHpView` lives under UI/Tutorial because it owns TMP text, CanvasGroup visibility, authored slot roots, and heart UI widgets.

Reason:
The tutorial sequence owns the scripted failure timing and fake-game-over flow, while the HP view owns authored UI rendering. Splitting these through `ITutorialPresentationHpView` removes a Gameplay-to-UI implementation dependency without moving tutorial gameplay timing into UI.

Implications:
- Features should not reference concrete `TutorialPresentationHpView`; use `ITutorialPresentationHpView` through the serialized `presentationHpView` MonoBehaviour field.
- UI `TutorialPresentationHpView` may keep `HeartTokenUI`, TMP, and Unity UI dependencies because it is inside the UI implementation boundary.
- Scene/prefab validation must confirm widened `presentationHpView` fields keep their assigned view after Unity import.

## 2026-07-04 - Tutorial Info Panel Uses Gameplay Contract And UI Implementation

Decision:
Tutorial trigger and combat intro flows use Gameplay `ITutorialInfoPanel` for panel show requests and open-state checks. Concrete `TutorialInfoPanel` lives under UI/Tutorial because it owns TMP text, Images, Buttons, CanvasGroups, hold-button progress UI, input glyph rendering, and open/close presentation.

Reason:
Tutorial gameplay owns request timing, completion gating, collider/direct-call activation, camera/letterbox timing, and prompt wait flow. The panel owns authored UI rendering and interaction widgets. Splitting these through `ITutorialInfoPanel` removes a Gameplay-to-UI implementation dependency without moving tutorial request data or completion timing into UI.

Implications:
- Features should not reference concrete `TutorialInfoPanel`; use `ITutorialInfoPanel` through serialized `infoPanel` MonoBehaviour fields or runtime discovery.
- UI `TutorialInfoPanel` may keep `HoldActionButton`, `HoldFillButtonView`, `InputGlyphDatabase`, TMP, and Unity UI dependencies because it is inside the UI implementation boundary.
- Scene/prefab validation must confirm widened `infoPanel` fields keep their assigned panel after Unity import.

## 2026-07-04 - Gameplay Features Avoid DOTween Source Dependency

Decision:
`Assets/_Project/Runtime/Features` should not reference DOTween directly while preparing for `Gameplay.asmdef`. Small gameplay-local presentation motions are implemented as Unity coroutines, and `LightningSpearRecoveredSpearActor` keeps its serialized `moveEase` / `floatEase` field names with local numeric ease values instead of referencing `DG.Tweening.Ease`.

Reason:
DOTween is a concrete presentation/tweening implementation dependency. Leaving it in Gameplay would force the future Gameplay assembly to reference that package for local animation helpers. The lightning spear prefab already stores `moveEase: 6` and `floatEase: 4`; matching those numeric values preserves the current serialized authoring while removing the source dependency.

Implications:
- Do not reintroduce `using DG.Tweening` under `Assets/_Project/Runtime/Features` without an explicit assembly-boundary reason.
- New gameplay-local motion should use a local coroutine or a lower-layer contract; reusable presentation-heavy tweening should live under Presentation/UI/Infrastructure as appropriate.
- Unity import/playtest must validate the lightning spear recovered-spear movement because the enum type changed while field names and numeric values were preserved.

## 2026-07-04 - Scene Flow Infrastructure Uses Core Playback Contracts

Decision:
Gameplay/Features code should not reference concrete Infrastructure flow services such as `SceneTransitionCoordinator`, `SceneFadeTransitionService`, `LoadingOverlayController`, or `TimeScalePauseService`. Scene transition requests use Core `SceneTransitionPlayback`, fade/unlock-blocking uses Core `SceneFadeTransitionPlayback`, loading input-block checks use Core `LoadingPresentationQuery`, and global time-scale pause tokens use Core `TimeScalePausePlayback`.

Reason:
These services are runtime Infrastructure implementations. Leaving direct references in Gameplay would force a future `Gameplay.asmdef` to depend on Infrastructure or the default assembly. The Core contracts expose only the narrow operations gameplay needs: transition state/load requests, fade state/session control, loading presentation activity, and pause acquire/release.

Implications:
- Features should not call those concrete Infrastructure service types directly.
- Infrastructure remains responsible for bootstrap, fallback creation, overlay ownership, loading UI behavior, and actual `Time.timeScale` restoration.
- Add new flow operations to these Core playback contracts only when a non-Infrastructure caller has a concrete need; do not turn them into broad service locators.

## 2026-07-04 - Gameplay Audio Helpers Use Core Audio Contracts

Decision:
Gameplay/Features code should use Core audio helper entry points for ability sounds, typing sounds, and run-route BGM notifications. `AbilityAudioRouter`, `TypingAudioUtility`, and `RunRouteBgmPlayback` live in Core, while `SoundManager` and `RunRouteBgmService` remain Infrastructure implementations registered through Core contracts.

Reason:
Ability and typing callers only need Core gameplay/audio context, not concrete audio service ownership. Leaving these helpers under Infrastructure would force a future `Gameplay.asmdef` to depend on Infrastructure just to play authored sounds. Tracked one-shot playback is now part of `ISoundPlaybackBackend` because `EndingOutroPlayer` needs a valid `AudioHandle` for skip/stop behavior.

Implications:
- Features should not call `SoundManager`, `RunRouteBgmService`, `CombatHitAudioRouter`, or audio catalog implementation types directly.
- Add Core audio contract methods only for operations with concrete non-Infrastructure callers.
- Infrastructure remains responsible for catalog resolution, pooling, BGM service bootstrap, and actual audio source lifetime.

## 2026-07-04 - Status HUD Data And Source Registry Live In Core

Decision:
Status HUD display contracts and source registration live in Core through `StatusHudDefinition`, `StatusHudEntry`, `StatusHudGroup`, `IStatusHudSource`, and `StatusHudSourceRegistry`. Gameplay-owned source adapters such as `PlayerStatusHudSource` and `SunMoonStatusHudSource` live beside their gameplay owner systems, while UI `StatusHudService` remains a facade that collects from the Core registry for HUD rendering.

Reason:
Gameplay systems author and publish status HUD data, but they should not depend on UI implementation services. The source list must also be available even if UI service initialization order changes. Keeping the contracts and registry in Core preserves `Gameplay -> Core` and `UI -> Core` dependency direction while leaving concrete rendering in UI.

Implications:
- Features should register status HUD sources through `StatusHudSourceRegistry`, not `StatusHudService`.
- UI should render collected `StatusHudEntry` data and avoid owning gameplay source adapters that read concrete gameplay runtime components.
- New status HUD source contracts should stay narrow and data-oriented; renderer widgets such as presenters, views, and tooltips stay in UI.

## 2026-07-04 - Item Detail Contracts Belong To Gameplay, Gauge Visibility Filter To Core

Decision:
Item inventory/detail projection contracts live with Gameplay/Features when they expose gameplay item concepts or container operations. Current examples are `IItemContainer`, `IRelicLevelProvider`, `IRelicSlotReceiver`, `ItemDetailContext`, `ItemDetailActionHint`, `AbilityTooltipVariant`, `IAbilityTooltipVariantProvider`, `IDetailProvider`, `ItemDetailBlock`, and `InventoryWeaponRetentionPolicy`. Monster element gauge visibility uses the neutral Core `IMonsterGaugeVisibilityFilter` contract because UI queries it and monster feature components implement it.

Reason:
Gameplay item data builds tooltip variants and detail blocks, while UI only renders them. Keeping these contracts in UI forced gameplay item definitions to depend upward on UI. The gauge visibility filter is not item gameplay data or UI implementation; it is a small capability query between a renderer and a target component, so Core is the appropriate dependency-inversion layer.

Implications:
- Features should not add item tooltip/detail provider interfaces under UI folders.
- UI inventory/detail views may depend on Gameplay item contracts to render authored item data.
- Monster-specific gauge display suppression should implement `IMonsterGaugeVisibilityFilter` rather than introducing UI-specific monster references.

## 2026-07-04 - Ability Runtime Visual Components Use Core Playback Contracts

Decision:
Gameplay ability and boss pattern code should request spec-owned or owner-owned runtime visuals through Core playback contracts instead of directly adding Presentation components. Current contracts are `AfterimageEmitterPlayback` / `IAfterimageEmitter2D` and `MotionAlignedParticlePlayback` / `IMotionAlignedParticleVisual2D`; Presentation `SpriteAfterimageEmitter2D` and `MotionAlignedParticleVisual2D` register the concrete backends.

Reason:
The timing and cleanup of Rush/boss afterimages and wind particles belong to Gameplay patterns, but SpriteRenderer cloning and ParticleSystem placement are Presentation implementation details. Direct `AddComponent<SpriteAfterimageEmitter2D>()` or `GetOrAddOwnedComponent<MotionAlignedParticleVisual2D>()` calls would force a future Gameplay assembly to depend on Presentation.

Implications:
- Features should not directly reference concrete runtime visual components for afterimages or motion-aligned particles.
- Add new ability runtime visual surfaces as narrow Core playback contracts only when Gameplay has a concrete timing/lifetime caller.
- Presentation remains responsible for component creation, particle instance ownership, renderer cloning, and backend registration.

## 2026-07-04 - Speech Data And Playback Contracts Live In Core

Decision:
Speech situation enums, speech data assets, speech theme settings, dialogue animation keys, and narrow speech playback contracts live in Core. UI `BossSpeechController` and `SpeechBubbleComponent` remain concrete implementations and expose themselves through `IBossSpeechPlayback` / `ISpeechBubblePlayback`. Gameplay code stores legacy scene references as generic `MonoBehaviour` where needed and casts only to the Core contracts.

Reason:
Boss, player, NPC, tutorial, and SlimeQueen gameplay code need to request speech by situation or line text, but should not depend on UI implementation classes. Moving the shared data and request surface into Core preserves `Gameplay -> Core` and `UI -> Core` direction while leaving TMP/pooling/layout behavior in UI.

Implications:
- Features should not add new direct references to `BossSpeechController` or `SpeechBubbleComponent`.
- Serialized gameplay fields that point at speech UI components should use `MonoBehaviour` plus `IBossSpeechPlayback` / `ISpeechBubblePlayback` validation until a prefab-side contract component strategy is approved.
- UI remains responsible for speech bubble prefab pooling, typing layout, and concrete rendering.

## 2026-07-04 - Damage Payload Config Is Core Combat Data

Decision:
`DamagePayloadConfig` and `ElementFormulaEntry` live in Core combat code because Core `DamageSnapshotBuilder` consumes them directly. Weapon and ability gameplay data can still serialize the config, but the defining type is no longer owned by Features.

Reason:
Leaving `DamagePayloadConfig` under weapon Features forced `Core.asmdef` to depend upward on Gameplay/Features. The payload config is a generic combat hit contract, not a weapon-specific implementation, so Core is the correct owner.

Implications:
- New fields consumed by Core damage snapshot/application code should be added to the Core config, not to a Features-only wrapper.
- Weapon data may reference `UnityGAS.DamagePayloadConfig`, but should not introduce another global or feature-owned type with the same name.

## 2026-07-04 - HUD Requests Use Core Playback Contracts

Decision:
Gameplay code requests monster element-gauge view installation and boss HUD registration through Core contracts. `IMonsterElementGaugeViewInstaller` abstracts the concrete UI gauge installer. `IBossHudSource`, `IBossHudBackend`, and `BossHudPlayback` abstract boss HUD registration, defeat marking, and unbinding. `BossHudHealthBarTheme` lives in Core as shared HUD authoring data.

Reason:
Monster spawning, boss death cleanup, and boss combat activation are gameplay responsibilities, but concrete HUD components and Canvas slot management are UI responsibilities. Direct references from `Features` to `MonsterElementGaugeViewInstaller` or `BossHudController` would force a future Gameplay assembly to depend upward on UI.

Implications:
- Features should not call `BossHudController.Instance` or concrete monster gauge UI installers directly.
- UI HUD implementations may depend on Core contracts and register themselves as backends.
- Serialized cross-boundary component references should use `MonoBehaviour` plus contract validation until prefab-side contract authoring is fully migrated.
- HUD authoring data that must live in Core should not expose Unity UI implementation enums directly; use Core-owned value enums with preserved numeric values when serialized compatibility matters.

## 2026-07-04 - Gameplay Does Not Own Concrete World Prompt Or Upgrade UI Types

Decision:
Player interaction prompt display routes through Core `UiCommandPlayback` / `IWorldInteractionPromptView`, and upgrade screen opening routes through Gameplay `UpgradeUiPlayback` / `IUpgradeUiBackend`. `WorldInteractionPromptController`, `UpgradeTreeUI`, and `UpgradeUiOpenFlow` remain UI implementation details.

Reason:
Interaction scanning and upgrade purchase/progress are gameplay responsibilities, but prompt rendering and upgrade screen open/close flow are UI responsibilities. Direct references from Features to UI implementation types would force a future Gameplay assembly to depend upward on UI.

Implications:
- Features should not serialize or search for concrete `WorldInteractionPromptController`, `UpgradeTreeUI`, or `UpgradeUiOpenFlow`.
- Cross-boundary scene fields should stay as `MonoBehaviour` plus contract casts until prefab-side authoring is migrated.
- UI may depend downward on gameplay upgrade data and manager APIs, but open/close orchestration should be exposed to gameplay only through `IUpgradeUiBackend`.

## 2026-07-04 - Player Backpack Inventory Is Gameplay State

Decision:
`PlayerBackpackInventory` lives under `Assets/_Project/Runtime/Features/Player/Inventory`, not UI.

Reason:
The component stores player-owned weapon/relic slots and is queried by boss dialogue/gameplay conditions. It does not render UI. Keeping it under UI made gameplay condition evaluation depend upward on the UI source folder.

Implications:
- UI screens may render or edit the backpack, but the owning runtime component belongs with player gameplay.
- Dialogue, loot, and inventory gameplay code may reference `PlayerBackpackInventory` without importing UI.

## 2026-07-04 - Shared Presentation Authoring And Lock Bridges Do Not Belong To UI

Decision:
`DialogueThemeSO`, `PlayerUIControlLockBridge`, and `IDefaultHudVisibilityTarget` live in Core/Presentation. Gameplay, Infrastructure, and UI may depend on these shared contracts/data, while concrete widgets and views remain in UI.

Reason:
`DialogueThemeSO` is serialized by gameplay dialogue data and consumed by UI rendering. `PlayerUIControlLockBridge` applies player tag locks for UI, cutscenes, scene-domain cleanup, and upgrade flows. `IDefaultHudVisibilityTarget` lets gameplay cutscenes hide HUD roots without knowing concrete HUD classes. Keeping any of these under UI forced lower layers to import UI implementation folders.

Implications:
- Do not add gameplay references to concrete HUD widget classes for visibility control; add or reuse a Core marker/contract.
- Shared ScriptableObject authoring data may live in Core when gameplay serializes it and UI only consumes it.
- Player control-lock tag application should route through the Core bridge, not a UI-owned component.

## 2026-07-04 - Reward Display Requests Use Gameplay Playback Contract

Decision:
Affection and upgrade gameplay request reward presentation through `RewardDisplayPlayback` / `IRewardDisplayBackend`. UI `RewardDisplayService` remains the concrete queue/view owner and registers as the backend.

Reason:
Reward display data carries gameplay upgrade and affection effect types, so the request contract belongs with Gameplay. The queue, UI open gating, view registration, and `RewardDisplayUI` rendering are UI responsibilities. This preserves `Gameplay -> Gameplay contract` and `UI -> Gameplay contract/data`, without `Features` depending upward on UI services.

Implications:
- Features should not call `RewardDisplayService.Instance` directly.
- New reward presentation requests should be added to `IRewardDisplayBackend` only when gameplay has a concrete caller.
- UI remains responsible for deciding whether a reward view can open, retrying presentation, and invoking completion callbacks.

## 2026-07-04 - Save And Transition DTOs Belong To Core

Decision:
Serializable save/session/transition DTOs shared by Gameplay and Infrastructure live in Core. Current examples are `GameData`, `GamePlayData`, `MerchantRuntimeState`, `MerchantStockEntryState`, `RunEndReason`, `TransitionType`, `SceneTransitionContext`, and `PlayerRuntimeState`. Concrete services such as `GameDataManager`, `GamePlayDataManager`, and scene route managers remain Infrastructure until they are hidden behind narrower contracts.

Reason:
Gameplay features need to read and write run/session/save state, while Infrastructure owns persistence, scene transition execution, and service lifetime. Keeping shared DTO definitions in Infrastructure forced Gameplay to depend on Infrastructure implementation folders even when it only needed serialized data contracts.

Implications:
- Shared save DTOs should stay data-oriented and avoid calling Feature services or Infrastructure managers.
- Feature-specific lookup helpers, such as merchant stock item-definition resolution through `ItemManager`, should live with the feature code rather than on Core DTOs.
- Remaining Gameplay-to-Infrastructure references to `GameDataManager`, `GamePlayDataManager`, `PortalRouteManager`, and related services should be handled through query/command contracts or facade extraction in later slices.

## 2026-07-04 - Save And Run Session Access Uses Core Gateways

Decision:
Gameplay code should access persistent save state through `GameDataStore` and active run/session state through `RunSessionStore`. `GameDataManager` and `GamePlayDataManager` remain Infrastructure-owned concrete lifecycle/persistence components and register as `IGameDataStoreBackend` / `IRunSessionStoreBackend`.

Reason:
Many Features only need data reads, save requests, run-active checks, pending reward deltas, or run timer events. Directly importing the concrete Infrastructure managers for these operations would keep the future Gameplay assembly dependent on Infrastructure. A Core gateway keeps the dependency direction `Gameplay -> Core <- Infrastructure`.

Implications:
- New gameplay code should not call `GameDataManager.Instance`, `GamePlayDataManager.Instance`, or `GameDataSaveCoordinator` for ordinary data access/save requests.
- If gameplay needs a new run/session operation, add it to the narrow Core backend only after confirming a concrete caller.
- Player restore, tutorial portal compatibility paths, and boss defeat ending completion now use `RunSessionStore`; keep future pending player/transition state operations on that Core gateway rather than reintroducing manager-typed parameters.

## 2026-07-04 - Shortcut Progress Uses Core Gateway

Decision:
Gameplay code should query and unlock permanent shortcuts through Core `ShortcutProgressStore` / `IShortcutProgressStoreBackend`. `ShortcutProgressService` remains the Infrastructure-owned concrete implementation that combines durable map save data with active run-session pending shortcut unlocks.

Reason:
Doors, permanent shortcuts, and construction completion flows need shortcut progress state, but importing `ShortcutProgressService` directly keeps the future Gameplay assembly dependent on Infrastructure. A Core gateway preserves the dependency direction `Gameplay -> Core <- Infrastructure` while leaving save/run-session coordination in the existing service.

Implications:
- Features should not call `ShortcutProgressService.Instance` or serialize the concrete service type.
- Shortcut progress operations that are broadly needed by gameplay should be added to `IShortcutProgressStoreBackend` only after a concrete caller exists.
- `ShortcutProgressService` can keep using `GameDataStore` and `RunSessionStore` internally, but gameplay callers should stay on `ShortcutProgressStore`.

## 2026-07-04 - Gameplay Cursor State Requests Use Core Playback

Decision:
Gameplay code should request cursor interactable and hidden states through Core `MouseCursorPlayback` / `IMouseCursorBackend`. `MouseCursorService` remains the Infrastructure-owned concrete cursor domain/theme/rendering service.

Reason:
Weapon selection feedback and ending outro playback need to influence cursor state, but they do not need the concrete cursor renderer, cursor theme, or UI domain implementation. Routing the narrow state requests through Core removes another direct `Features -> Infrastructure` dependency without moving cursor presentation details out of Infrastructure.

Implications:
- Features should not call `MouseCursorService.Instance` or `MouseCursorService.EnsureInstance()` for cursor state requests.
- Add new cursor operations to `IMouseCursorBackend` only when gameplay has a real caller.
- UI may continue using the concrete cursor service until the UI assembly boundary is reviewed separately.

## 2026-07-04 - Hub Intro Preload Refresh Uses Core Playback

Decision:
Gameplay code should request first-run intro preload window refresh through Core `PresentationPreloadPlayback` / `IPresentationPreloadBackend`. `PresentationPreloadService` remains the Infrastructure-owned concrete preload manifest/provider owner.

Reason:
The hub intro sequence only needs to notify that the first-run intro preload window should refresh after the intro is marked seen. Directly calling the Infrastructure preload service from Gameplay keeps the future Gameplay assembly dependent on Infrastructure for a presentation preload side effect.

Implications:
- Features should not call `PresentationPreloadService.RefreshFirstRunIntroWindow(...)` directly.
- Add preload playback operations only for concrete cross-layer callers; do not expose the full preload debug/provider surface through Core.
- Loading/debug UI and Infrastructure transition services may continue using concrete preload APIs until their assembly boundaries are reviewed separately.

## 2026-07-04 - Scene Portal Travel Uses Gameplay-Owned Playback Contract

Decision:
`ScenePortal` requests travel through Gameplay `ScenePortalTravelPlayback` / `IScenePortalTravelBackend`. Infrastructure `ScenePortalTravelService` registers the backend and keeps the concrete route planning, run/session mutation, player runtime capture, and scene transition coordinator integration.

Reason:
The travel request API uses the Gameplay `ScenePortal` type, so putting this contract in Core would make Core depend on Gameplay. Keeping the contract beside `ScenePortal` preserves `Gameplay <- Infrastructure` for this service while removing the direct `Features -> Infrastructure` call.

Implications:
- Scene portal gameplay should call `ScenePortalTravelPlayback`, not `ScenePortalTravelService`.
- Infrastructure travel code may depend on the Gameplay portal contract and data, but Gameplay should not import the concrete scene-flow service.
- If portal travel request data is later extracted away from `ScenePortal`, this contract can be reconsidered for Core promotion.

## 2026-07-04 - Hit Flash Presentation Uses Core Contract

Decision:
Gameplay hit feedback code uses Core `IHitFlashController2D` for sprite hit flash playback. Infrastructure `SpriteHitFlashController` implements the contract and remains the concrete shader/property-block presentation component.

Reason:
Player, monster, and candlestick gameplay need to trigger hit flash timing, but they should not depend on a concrete Infrastructure rendering component. The contract is narrow enough for Gameplay to call and for the concrete presentation component to implement without moving shader/rendering details into Gameplay.

Implications:
- Features should not add new direct references to `SpriteHitFlashController`.
- Serialized gameplay fields that point to hit flash components should use `MonoBehaviour` plus `IHitFlashController2D` resolution until prefab-side contract authoring is fully migrated.
- Unity import/play-mode validation is required for widened serialized hit flash fields.

## 2026-07-04 - Route And Run Progress Managers Stay Behind Gameplay Contracts

Decision:
Gameplay route users should call `RunRoutePlayback` / `IRunRouteBackend`, and boss progress users should call `RunProgressPlayback` / `IRunProgressBackend`. `PortalRouteManager` and `RunProgressCoordinator` remain Infrastructure-owned concrete runtime services and register as the corresponding backends.

Reason:
`ScenePortal`, boss reward policy, game-over location naming, and monster stage scaling need run route state, but they do not need the concrete manager lifecycle, DDOL ownership, route-history logging, or scene-flow implementation. Boss gameplay also needs to announce combat/defeat/reward readiness without importing the concrete coordinator. Keeping both manager types out of Features removes a blocker for a future `Gameplay.asmdef`.

Implications:
- Features should not call `PortalRouteManager.Instance`, `PortalRouteManager.EnsureInstance()`, or `RunProgressCoordinator` directly.
- New route operations needed by gameplay should be added to `IRunRouteBackend` only when a concrete caller exists.
- `PortalRouteManager` can keep owning route plan mutation and load-presentation context inside Infrastructure.

## 2026-07-04 - Route Authoring Data Moves To Gameplay, Shared Load Data Moves To Core

Decision:
`CorridorBossRouteSetSO` and `RunRouteCatalogSO` live under Gameplay/Features because they are route authoring data used by gameplay portals, rewards, and stage scaling. `PortalRouteDecision`, `LoadManifestSO`, `RouteSetLoadManifestSO`, and `LoadScopeKind` live in Core because they are shared route/loading data contracts used by both Gameplay and Infrastructure.

Reason:
Keeping route authoring ScriptableObjects under Infrastructure forced Gameplay to depend on Infrastructure for serialized gameplay route data. Keeping manifest and route decision DTOs in Infrastructure caused the same dependency even though they are shared contracts, not concrete services.

Implications:
- Preserve moved `.meta` GUIDs for all route and loading ScriptableObjects/DTOs.
- Infrastructure services may depend on the Gameplay route authoring data, but Gameplay should access concrete route services only through `RunRoutePlayback`.
- Core loading data must remain data-oriented and should not gain provider, Addressables, scene transition, or route manager behavior.

## 2026-07-04 - Presentation Asset Resolve Uses Core Playback

Decision:
Gameplay and Presentation code should resolve presentation prefabs through Core `PresentationAssetPlayback` / `IPresentationAssetBackend`. Infrastructure `PresentationAssetProvider` remains the concrete provider and registers the backend.

Reason:
Gameplay and Presentation callers only need prefab resolve semantics, not preload reference counting, Addressables resolution, debug snapshots, or provider lifetime. A Core playback gateway removes direct references to the concrete provider from future non-Infrastructure assemblies.

Implications:
- Do not add new `PresentationAssetProvider.ResolvePrefab(...)` calls outside Infrastructure.
- Add async/preload operations to Core only if a non-Infrastructure caller has a concrete need.
- Provider ownership, manifests, and Addressables behavior stay in Infrastructure.

## 2026-07-04 - Project Runtime Folders Receive Explicit Asmdefs

Decision:
Project-owned runtime folders are split into `Core`, `Gameplay`, `Infrastructure`, `Presentation`, and `UI` asmdefs, with `Editor` as an Editor-only asmdef over `Assets/_Project/Editor`.

Reason:
Dependency inversion work has removed the main direct `Gameplay/Core/Infrastructure -> UI/Presentation concrete implementation` blockers. Explicit asmdefs now make the desired boundary enforceable by Unity instead of relying only on folder conventions and static searches.

Implications:
- `Core` must keep an empty project reference list.
- `Gameplay` may depend on `Core` and package data it directly uses, such as Ink runtime.
- `Infrastructure` may depend on `Core` and `Gameplay` to implement concrete runtime services for gameplay-owned contracts.
- `Presentation` may depend on `Infrastructure` while it still uses concrete camera/bootstrap/presentation provider services.
- `UI` is the highest project-owned runtime presentation layer and may depend on `Presentation`, `Infrastructure`, `Gameplay`, and `Core`.
- Unity import/compile is now the next authoritative check; stale generated `.csproj` files are not enough to prove this split.

## 2026-07-04 - DOTweenPro Remains A Vendor Boundary Decision

Decision:
Do not fold `Assets/Plugins/Demigiant/DOTweenPro` source into the six project-owned assemblies as part of the project asmdef split.

Reason:
DOTweenPro is third-party/vendor source, not project-owned gameplay/UI architecture. Forcing it into `Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`, or `Editor` would blur package ownership and make the six project assemblies responsible for vendor maintenance.

Implications:
- If default `Assembly-CSharp` residual cleanup must include vendor source, add a separate vendor asmdef or package-specific decision instead of moving DOTweenPro into project-owned assemblies.
- Project-owned asmdefs should continue depending on the narrow `DOTween.Modules` support asmdef only where DOTween extension methods are used.

## 2026-07-04 - Vendor And Demo Residual Source Gets Package-Owned Asmdefs

Decision:
Remaining non-project `.cs` source is covered by package/demo asmdefs instead of being moved into the six project-owned assemblies. DOTweenPro companion source uses `DOTweenPro.Scripts` and `DOTweenPro.Scripts.Editor`; Ink basic demo source uses `Ink.Demos.Basic` and `Ink.Demos.Basic.Editor`.

Reason:
The default assembly residual scan showed only DOTweenPro vendor source and Ink demo source. These files should compile outside default `Assembly-CSharp`, but they are not project-owned gameplay architecture. Naming DOTweenPro source asmdefs after the existing DLL assembly names would also risk collision with `DOTweenPro.dll` and `DOTweenProEditor.dll`, so the source companions use distinct names.

Implications:
- Do not move third-party vendor or demo source into `Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`, or `Editor`.
- If Unity import reports hidden package reference errors, fix the specific vendor/demo asmdef reference list instead of changing project-owned assembly direction.
- Default `Assembly-CSharp` residual cleanup must distinguish project-owned source from package/demo support source.

## 2026-07-04 - Missing Script Recovery Prefers Historical GUID Compatibility Only When Type Is Gone

Decision:
When a missing `m_Script` GUID maps to a deleted legacy type and there is no current replacement type with the same class name, restore a narrow compatibility script with the historical `.meta` GUID. When the same class already exists under a current MonoScript GUID, do not add a duplicate compatibility class; migrate or reserialize the affected asset/prefab to the current script GUID instead.

Reason:
Restoring `DamagePopupSceneAnchor`, `MonsterDefinition`, `UIHoverKeepAliveArea`, and `BossDrop` with their historical GUIDs reduced missing script references without touching scene/prefab/ScriptableObject YAML. In contrast, old `Boss` and `AttackTelegraphStyle` GUIDs point to classes that already exist under current GUIDs, so adding duplicate class definitions would either fail compilation or silently change serialized asset type semantics.

Implications:
- Compatibility scripts must keep serialized field names needed by old assets but should avoid reintroducing obsolete service dependencies.
- Remaining old `Boss` / `AttackTelegraphStyle` references require serialized GUID migration or Unity reserialization, not another code-only stub.
- Visual Scripting missing GUIDs should be solved by package restoration or graph/component removal, not by project-owned fake replacements.

## 2026-07-04 - Addressables Linker Preserves Project Types By Target Assembly

Decision:
`Assets/AddressableAssetsData/link.xml` should preserve project-owned runtime types under their actual asmdef assemblies (`Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`) instead of the legacy `Assembly-CSharp` assembly.

Reason:
After the asmdef split, project-owned runtime types no longer compile into `Assembly-CSharp`. Leaving the generated preserve block under `Assembly-CSharp` keeps a stale linker dependency and can fail to preserve the intended types in player builds. Current source ownership can map every former `Assembly-CSharp` type entry to one of the five runtime assemblies.

Implications:
- When adding project runtime types that need linker preservation, place them under the owning asmdef assembly in `link.xml`.
- Prefer Unity/Addressables regeneration when possible, but generated output must still be checked for stale `Assembly-CSharp` after asmdef changes.
- Editor-only types should not be added to the runtime linker preserve list.

## 2026-07-04 - Serialized Assembly String Migration Requires Classification

Decision:
Serialized `Assembly-CSharp` strings and missing `m_Script` GUIDs should be classified before migration. Known-safe UnityEvent `m_TargetAssemblyTypeName` entries may be migrated by exact type-to-assembly replacements when the target class still exists. Missing script GUIDs may be migrated only when the current replacement MonoScript is known. Stale `m_EditorClassIdentifier` entries should be treated as reserialization cache data unless Unity reports an import problem. Deleted or renamed UnityEvent target types must stay manual.

Reason:
After the asmdef split, the remaining serialized references are not equivalent. `UpgradeTreeUI` and tutorial scene target types still exist and only need an assembly-name change, while `DialogueManager` no longer exists as a class and cannot be safely repaired by a blind assembly rename. Legacy `UnlockResultUI` strings are safe only when the UnityEvent target component's MonoScript GUID proves the actual target is current `RewardDisplayUI`. Old `Boss` and `AttackTelegraphStyle` GUIDs map to current replacement MonoScripts, while Visual Scripting GUIDs depend on package/graph migration.

Implications:
- Use `Tools/Validation/Assembly Split Serialized References` to report the remaining serialized references.
- Do not broad-replace `Assembly-CSharp` across scenes/prefabs.
- Add explicit safe replacements only after proving the target class or target MonoScript exists in the intended asmdef assembly.

## 2026-07-04 - Visual Scripting Assembly Options Follow Runtime Asmdefs

Decision:
Visual Scripting `assemblyOptions` should list the current project runtime assemblies (`Core`, `Gameplay`, `Infrastructure`, `Presentation`, `UI`) instead of the removed default assemblies (`Assembly-CSharp-firstpass`, `Assembly-CSharp`).

Reason:
The assembly split goal removes project-owned code from Unity's default assemblies. Keeping Visual Scripting configured against `Assembly-CSharp` preserves a stale default-assembly dependency even if the graph assets themselves do not currently serialize project type names.

Implications:
- Do not re-add `Assembly-CSharp` or `Assembly-CSharp-firstpass` to Visual Scripting project settings during future package restoration.
- If Visual Scripting is restored, regenerate or validate its options against actual asmdef names rather than default assembly names.
- Editor-only project assembly types should not be exposed to runtime Visual Scripting graphs unless a separate Editor-only graph use case is explicitly authored.

## 2026-07-04 - Visual Scripting Residuals Require Explicit Restore Or Cleanup

Decision:
Do not restore `com.unity.visualscripting` blindly just to satisfy missing-script scans. Treat current Visual Scripting references as stale residuals unless a live scene/prefab reference proves they are still authored gameplay/tooling content.

Reason:
`com.unity.visualscripting` is absent from both `Packages/manifest.json` and `Library/PackageCache`, while the remaining graph asset GUIDs are not referenced outside their own `.meta` files in the current static scan. PixelLightTest's ScaleWave behavior already has a code replacement path, so re-adding the full package would reintroduce a package dependency for content that appears obsolete.

Implications:
- PixelLightTest Visual Scripting scene variables/components should be removed through the dedicated Editor cleanup tool or deliberate scene authoring, then revalidated.
- Unreferenced graph assets under `Assets/_Project/Data/VisualScripting/Graphs` can be deleted only after an explicit asset cleanup decision.
- `ProjectSettings/VisualScriptingSettings.asset` should either be removed with a ProjectSettings cleanup decision or kept only if the Visual Scripting package is intentionally restored.
- The preferred cleanup entry point is `Tools/Validation/Assembly Split/Apply Visual Scripting Residual Cleanup`, which refuses to run if `com.unity.visualscripting` is installed and checks graph GUID references before deleting graph assets.

## 2026-07-04 - Null-Target UnityEvents Are Removed, Not Repointed

Decision:
When a serialized UnityEvent points at `Assembly-CSharp` but its `m_Target` is `{fileID: 0}` and the named target type no longer exists, remove the stale persistent call instead of inventing a new target assembly/type.

Reason:
The `DialogueManager, Assembly-CSharp` entries in `Choice0.prefab` and `TextCanvas.prefab` had no target object and `DialogueManager` no longer exists in source. Repointing these entries would preserve dead serialized state and could imply a live dependency that is not present.

Implications:
- Treat null-target UnityEvents as stale authoring residue after proving the target type is gone.
- Do not replace deleted type names by guesswork.
- Prefer changing the event list back to Unity's empty `m_Calls: []` form when the call list contains only the dead entry.

## 2026-07-05 - Pre-Existing Missing Asset GUIDs Do Not Block Asmdef Split Regression Gate

Decision:
Generic serialized asset references whose target GUID is missing from current metadata should block the asmdef split only when the missing GUID reference is newly introduced by the current worktree. If the same GUID reference already existed in the same file at `HEAD`, report it as `PreExistingMissingAssetReference` info and keep it as content debt outside the asmdef split completion gate.

Reason:
The stronger generic asset reference scan found 110 missing non-script GUID references, but every one already existed in the same file at `HEAD` and the referenced `.meta` files were absent from both current metadata and `HEAD`. Treating those as current split regressions would hide real split status behind unrelated legacy content debt. Some refs are also unsafe to auto-fix: old `UpgradeTreePanel` prefab refs carry obsolete prefab fileIDs, `legacyInkJSON` still acts as fallback data when `NPCData.PrimaryInk` is empty, and several dialogue JSON assets are absent.

Implications:
- `MissingAssetReference` remains an error for newly introduced missing non-script asset GUIDs.
- `PreExistingMissingAssetReference` is information for content repair planning, not a green light to ignore broken authored content.
- Do not broad-replace missing GUIDs in scenes/prefabs/assets. Restore missing assets, rewire via Unity authoring, or clear obsolete refs only after a per-GUID content decision.

## 2026-07-05 - Addressables ConfigFolder link.xml Is Temporary

Decision:
Do not treat `Assets/AddressableAssetsData/link.xml` as a persistent completion artifact for the asmdef split. For Addressables 2021.2+ behavior, validate the generated `Library/com.unity.addressables/aa/<BuildTarget>/AddressablesLink/link.xml` output instead.

Reason:
`AddressablesPlayerBuildProcessor.CleanTemporaryPlayerBuildData()` runs on editor load and removes the ConfigFolder `link.xml`. During a player build, Addressables copies the generated `Addressables.BuildPath/AddressablesLink/link.xml` into the ConfigFolder only temporarily for Unity linker processing. Forcing the ConfigFolder file to remain restored fights the package lifecycle and is deleted again by Unity import/editor load.

Implications:
- The completion gate must run an Addressables player content build validation and check the generated `AddressablesLink/link.xml` for stale `Assembly-CSharp` references.
- `Assets/AddressableAssetsData/link.xml` and `.meta` can stay deleted after the split when the generated Addressables build link output is clean.
- Future linker-preserve validation should distinguish the temporary ConfigFolder copy from the generated Addressables build output.

## 2026-08-11 - Level-Up Effects Are Run-Owned Definitions With Rebuildable Live Handles

Decision:
Level-up rewards are separate from permanent `UpgradeEffectSO` purchases. Stable reward/effect IDs and serializable effect payloads live in `GamePlayData`, while player-bound subscriptions and modifiers are rebuilt from registered definitions and disposed through `ILevelRewardEffectHandle`.

Reason:
Level-up effects reset with the run but must survive gameplay scene transitions. Storing scene object references would violate runtime save ownership, while reusing permanent upgrade effects would mix profile and run lifetimes. Instant rewards also need a durable applied marker so player restoration does not grant them twice.

Implications:
- UI does not own selected rewards or effect state.
- Persistent effects must provide deterministic cleanup and reapply behavior.
- Instant effects must use the `InstantOnce` lifetime and persist `instantApplied`.
- Reward/effect IDs become run-state compatibility keys and should not be renamed after authoring without migration.
- `한 자루의 맹세` seals weapon slot index 1 during the run; it must not delete or overwrite the stored slot weapon.

## 2026-08-11 - Level-Up Offers Persist Before UI And Use A Run Seed

Decision:
The active three-card offer, reroll usage, offer sequence, and reward random seed belong to `LevelProgressionState`. Closing or rebuilding authored UI does not roll again. The normal player selection flow consumes only a reward ID present in the stored active offer.

Reason:
Candidate identity must survive UI close/reopen and scene/run-state serialization without letting presentation code own randomness or mutate gameplay progression.

Implications:
- Authored UI reads `LevelRewardSessionController.Candidates` and calls its commands instead of rolling locally.
- A successful selection clears the current offer, advances the sequence, and rolls the next offer only when another pending reward remains.
- Reward/effect IDs are persistence keys; renaming authored IDs requires migration.

## 2026-08-17 - Level HUD EXP Progress And Reward Readiness Are Independent

Decision:
`ExperienceFill` projects only current EXP progress, while `RewardReadyBorder` and `LevelUpPrompt` project whether the reward session can actually be opened. Neither display state derives from the other.

Reason:
Pending reward choices can coexist with progress toward a later level, and a full or partially filled EXP diamond says nothing about combat, recent damage, dialogue, blocking UI, player registration, or candidate availability. Coupling the visuals would show false R-key prompts or erase valid EXP progress.

Implications:
- `LevelHudPresenter` updates fill/level from `RunLevelProgression` and readiness from `LevelRewardSessionController.CanOpenSession` through separate paths.
- Lv.10 forces EXP fill to one but does not automatically show the green reward border.
- A pending reward does not show the green border while any session-open condition is blocked.
- Do not attach an active `LevelRewardSessionController` before an authored selection-window consumer can present and close the paused session.

## 2026-08-17 - Level Reward Selection Uses Fixed Escape Close Input

Decision:
The level-reward selection window closes with `Escape`. This shortcut is fixed and is not part of the player key-mapping surface.

Reason:
Closing the modal selection window is a UI-stack escape action rather than a configurable gameplay action. Keeping it fixed avoids an unnecessary binding dependency and keeps the authored close control consistent with other modal UI behavior.

Implications:
- `UIManager` handles the fixed `Escape` request through the top UI-stack entry. A stack UI pushed by its matching external blocker owner may close while that owner is the only blocker; unrelated blockers still suppress the request.
- `LevelRewardSelectionPresenter` must not also query `Escape` directly, because duplicate handling can close the reward window and open Pause in the same frame depending on script order.
- Do not add a key-binding action, rebinding row, or binding persistence entry for this close command.
- Any close hint should display a fixed ESC label/icon rather than a dynamically resolved gameplay binding.
- Closing must preserve the current offer and reroll state through `LevelRewardSessionController.CloseSession()`.

## 2026-08-18 - Boss EXP Uses Run Stage Position And Encounter Completion

Decision:
Normal boss EXP is determined by the boss encounter's current run stage position: stage 1 grants 120, stage 2 grants 150, and stage 3 grants 180. The final route set, currently the Demon King, grants no EXP. Boss EXP is emitted once from `BossEncounterEndDirector` after encounter completion rather than from each boss actor's death event.

Reason:
Normal boss routes are randomized, so boss identity cannot represent progression stage. Split or multi-actor encounters such as Slime Queen also make an individual death subscription capable of duplicate payout. The encounter-end path already owns the one-time clear result and final-route classification.

Implications:
- Reordering which boss appears in stages 1-3 does not change the stage EXP curve.
- New multi-actor bosses still pay only once when their clear condition completes.
- Final-route exclusion uses `BossRewardContext.IsFinalRouteSet`; do not add a Demon King name/type comparison.
- Boss EXP Square count is capped, but the exact total EXP must remain preserved by the per-pickup amount.

## 2026-08-18 - Level Reward Close Presentation Completes Before Gameplay Resumes

Decision:
The level-reward modal keeps its session-owned pause and input block until the card, presentation, and blocker OFF sequence is fully transparent and the authored panel is deactivated. A previously revealed active offer skips only its flip when reopened; rerolls and consecutive pending offers use a new reveal identity and run the card replacement reveal.

Reason:
Releasing the session when close is requested would restart gameplay under a still-visible blocker. Treating every reopen as a new reveal would also repeat information the player already inspected, while treating rerolls as already revealed would hide the intended new-card presentation.

Implications:
- `LevelRewardSelectionPresenter` holds the UI stack entry during the close coroutine and permits the final pop only after the fade completes.
- The session controller does not auto-close after the last successful selection; the Presenter completes the normal visual close and then releases the session.
- Transition input is blocked for card selection, reroll, number keys, close button, and fixed Escape.
- Reveal memory is presentation-only and keyed by run seed, offer sequence, and reroll usage; gameplay candidate ownership remains unchanged.

## 2026-08-19 - Direct PrototypeHub Play Skips Hub Arrival Presentation

Decision:
When `ProtoTypeHub` is the first gameplay scene played directly in the Unity Editor, development bootstrap skips both `PlayerHubSpawnPresentation2D` and `HubIntroAfterDarkLordSequence`. It still resets volatile run state, prepares the Hub start portal plan, and normalizes player interaction. Normal Title/tutorial entry and later Hub returns keep their authored arrival presentation.

Reason:
Direct Hub Play is an iteration path for immediately testing Hub and run-start behavior. Letting either arrival presentation acquire input or cinematic protection makes the correctly authored `HubToRunStart` portal appear broken. The exception must be scoped to the initial direct-Play scene rather than stored in profile, run, portal, or prefab data.

Implications:
- Core `EditorDirectSceneStartContext` stores only the initial direct Hub scene handle and is a no-op outside `UNITY_EDITOR`.
- `SceneDomainCoordinator` is the only writer and clears the marker when any later scene loads.
- Hub spawn and Hub intro presentation must use the same marker; do not restore a one-sided skip.
- `ScenePortal`, its shared prefab, the Hub scene instance's `HubToRunStart` semantic, and `RunRouteCatalogSO` remain unchanged.
