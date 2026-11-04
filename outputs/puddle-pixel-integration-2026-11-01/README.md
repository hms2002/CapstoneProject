# Applied puddle pixel artwork

Selection: revision16 beer body/ripple colors, revision17 natural cream foam and three rising bubble variants, revision15/16 fire artwork. Ground opacity remains166/255. Native Aseprite sources are in [source](source/).

## Changed files and purpose

| Files | Purpose |
| --- | --- |
| `Assets/_Project/Art/Textures/Puddles/ApprovedPixel/` — seven PNGs and native Unity import metadata | Four-frame alcohol/fire ground,16-frame center ignition, sprite emission shape, bubble/near/airborne particle sheets; Point, no mipmaps, uncompressed |
| `Assets/_Project/Art/Materials/PuddlePixel/` — four materials | Dedicated approved ground and three particle materials |
| `Assets/_Project/Art/Shaders/PuddleShaderVisual.shader` | Optional pixel sheet sampling on the existing ground renderer; old material and non-ground fallback retained |
| `Assets/_Project/Art/Shaders/PuddlePixelParticle.shader` | Unlit transparent particle sheet rendering |
| `Assets/_Project/Prefabs/Map/Puddles/AlcoholPuddle.prefab`, `FirePuddle.prefab` | Native material/particle wiring, existing bubble system reuse, two authored flame children; existing gameplay objects and identifiers retained |
| `Assets/_Project/Runtime/Features/Map/Puddles/PuddlePresentationContracts.cs`, `AlcoholPuddleArea.cs` | Forward existing normalized ignition progress to particle presentation |
| `Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleParticleVisual.cs` | Center-first particle spread, burned bubble removal, separate near/air motion and disable cleanup |
| `Assets/_Project/Editor/Tools/Validation/PuddlePixelArtworkAuthoring.cs` | Explicit repeatable native import/prefab authoring command |
| `Assets/_Project/Editor/Tools/Validation/PuddlePixelVisualValidation.cs` | Actual prefab/GPU capture in an unsaved disposable preview scene |
| `Assets/_Project/Tests/PlayMode/Procedural/PuddleProjectileVisualPlayModeTests.cs` | Three additional center ignition, bubble removal and near/air lifecycle regressions |
| This output directory | Native sources, authoring scripts, immediate baselines, validation logs and rendered evidence |
| `Docs/ActiveTasks/puddle-pixel-integration.md`, `Docs/TaskIndex.md` | Approved scope and completed-task routing |
| `Docs/StructureMemory/PuddlePixelPresentation.md` | Current asset/presentation ownership and extension map |
| `Docs/SessionLogs/2026-11-01.md`, `Docs/DecisionLog.md`, `Docs/ErrorLog.md` | Actual changes/checks, accepted source selection and native authoring traps |

The game retains its existing0.8-second ignition timing, damage/buffs, gameplay radii/colliders, absorb/projectile animations, buff indicators and wall clipping. The selected art is attached to the existing spawn prefabs; no scene replacement or new manager is involved.

## Reproduce

1. `finalize-art.lua` in Aseprite rebuilds approved native sources/PNGs from revision17, restoring revision16 body/ripple RGB and asserting exact foam/alpha preservation.
2. In Unity, **Tools > Puddles > Apply Approved Pixel Artwork** imports and applies the exported textures through Unity APIs.
3. **Tools > Puddles > Capture Approved Pixel Artwork** renders both real prefabs and center ignition. `run-native.ps1` supports the equivalent batch command and waits for another Editor operation to finish.
4. `run-native.ps1 -Tests -LogName unity-playmode.log` runs the three scoped PlayMode suites. `verify-integration.py` checks source, protected hashes and prefab semantic scope.
5. `package-native-preview.lua` uses Aseprite to assemble native Unity PNG captures into review GIFs.

## Validation status

- Native Aseprite authoring verifies revision16 body/ripple pixels, exact revision17 foam/alpha preservation and exact center ignition endpoints.
- Unity imported all seven textures and applied both prefabs;101 native GPU frames were inspected. The final [native rerun](unity-native-reviewed.log) passed with [zero capture-related error/warning markers](native-log-verification.json).
- PlayMode: **57/57 passed**, zero skipped, including three new pixel lifecycle cases and existing absorb, buff, shader fallback and alcohol exposure/contact tests. See [results](playmode-results.xml) and [Unity log](unity-playmode.log).
- Final Editor and ProceduralPlayModeTests MSBuild both exit0 with project references, including Gameplay and Presentation. See `msbuild-verified-Editor.log` and `msbuild-verified-ProceduralPlayModeTests.log`. Existing obsolete API warnings remain.
- The final capture-only seed/initializer correction also compiled successfully in `msbuild-capture-final.log` and was executed in the native rerun above.
- [Semantic/source preservation](preservation-verification.json):36 protected files unchanged, seven engine textures byte-identical to native exports, existing prefab object IDs/gameplay/collider records retained. Each prefab has two added particle children/eight YAML records.
- [Preview/save checks](preview-verification.json): two Aseprite-exported GIFs reopened,80/21 frames, maximum RGB quantization difference1/255; all three saved-game files unchanged.
- Initial native attempts exposed Unity missing-object handling, the owner material template, mixed velocity curve modes, and Edit Mode prewarm/SendMessage warnings. Those were corrected; earlier logs remain as troubleshooting evidence. The passing PlayMode log has two unrelated Addressables `DelayedActionManager` validation warnings; it is not a clean-Console claim.

Preview captures use actual prefab materials, meshes and ParticleSystems in a disposable scene. They are not a recorded full gameplay session and do not invoke the unrelated buff indicator Update loop. Full manual combat acceptance and Player-build performance are outside the executed checks.

Automation references: Aseprite [single-file Image loading](https://www.aseprite.org/api/image#image) and [noninteractive palette quantization](https://www.aseprite.org/api/command/ColorQuantization).

Doc Impact Check: SessionLog + StructureMemory + DecisionLog + ErrorLog. Task scope/router closed. No Architecture/Contracts promotion or Presentation HTML stale candidate.
