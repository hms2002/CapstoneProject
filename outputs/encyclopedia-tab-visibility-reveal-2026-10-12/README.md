# Encyclopedia Item Visibility And Shared Reveal — 2026-10-12

User approved the proposed fix after the ProtoTypeHub screenshot showed old Item detail under Creature pages and an uncovered illustration during Reveal.

## Result And Changed Files

| Changed file | Reason |
| --- | --- |
| [EncyclopediaItemRightPage.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs) | Separate full ItemDetailPanel visibility from internal projection; block hidden/locked shortcuts, stop scroll/glossary work and apply pending locale refresh on return. |
| [EncyclopediaItemTab.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemTab.cs) | Pass screen interaction lock to the right presenter as well as the left. |
| [EncyclopediaScreen.cs](../../Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaScreen.cs) | Hide Item detail on immediate close before deactivating the popup. |
| [EncyclopediaCreatureUIAuthoring.cs](../../Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaCreatureUIAuthoring.cs) | Persist full-panel reference and Content pages→PageCover→full illustration order, including after existing polish/alignment. |
| [EncyclopediaUI.prefab](../../Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab) | Only ItemRightPage MonoBehaviour reference and Book child-order records changed; book rail stays active. |
| [Content Appear/Style1](<../../Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book/Book Content Appear/Style 1>) `1.png` through `18.png` | Fill original rounded upper transparent gaps to row197 using each frame's existing palette/alpha. Original opaque pixels and animated reveal holes remain. |
| [EncyclopediaCreaturePlayModeTests.cs](../../Assets/_Project/Tests/PlayMode/Procedural/EncyclopediaCreaturePlayModeTests.cs) | Add real Item-presenter round-trip, close/reopen, hidden input/locale and native Reveal regressions; retain existing limited isolated tests. |
| ActiveTask, TaskIndex, existing Creature StructureMemory, ErrorLog, SessionLog | Record approved scope, ownership, verified result and prior test blind spot. DecisionLog unchanged; no new architectural decision required. |

The active visibility boundary is ItemDetailPanel, not the shared RightPage bookmark owner. Item selection survives Creature round-trips. Hidden locale changes mark current projection dirty and refresh it on return without rebinding selection/preview. The existing PageCover clip/controller and0.4s timing remain intact. Its last sprite retains15 original opaque pixels; actual routine completion disables the Cover.

## Verification

- Unity6000.4.2f1 final [PlayMode26/26](playmode-final-results.xml), duration27.819s; CLI success and test exit0. Runtime/import and real animated transitions executed.
- Editor.csproj and ProceduralPlayModeTests.csproj MSBuild exit0: [Editor log](msbuild-final-editor.log), [test log](msbuild-final-tests.log). Existing unrelated compiler warnings remain; no compile-success claim is based solely on source inspection.
- [Preservation23/23](integration-preservation-report.json):2583 record IDs/classes/order identical, only2 authorized records changed,2581 unchanged raw records,2964 font/sprite/material/Fit fields preserved. Current594×389/Y−7 frame, rail, Item bindings,8 protected assets and56 protected mask/meta hashes match fresh snapshots. Original book atlas/character art, scenes, GlobalUIRoot, catalog and all metadata unchanged.
- Mask additions37,075 pixels across18 frames are restricted to the authorized upper gaps; existing RGBA and reveal boundary profile preserved, new palette colors0. First-frame illustration, titles, Monster preview and footer uncovered area0% by source-alpha projection. No whole-cover scaling or offset change.
- [Restoration report](test-file-restoration-report.json): ten protected save/runtime-output files and three historical renderer PNGs restored byte-for-byte; no newly created test saves found. Latest standard renderer captures copied here as `current-actual-*.png` before restoring historical outputs.
- Independent read-only runtime/lifecycle review found no remaining approved-scope defect. Scoped whitespace and Markdown links checked.

First run [24/25](playmode-first-results.xml) failed the keyboard positive control because synthetic input did not reach player state in headless Editor. The final harness clones Input System settings in memory, routes events to player state, asserts actual pressed/isPressed input and restores settings/devices. Production input settings were not modified. Final26 tests include the added hidden locale regression.

## Native Visual Evidence

These are the authored prefab rendered at1920×1080 with actual Item presenters and actual localized font bindings, after loading/application completed. Current locale zh-Hans is preserved; these are not Korean-language validation or a full ProtoTypeHub gameplay screenshot.

| Phase | Image | Verified state |
| --- | --- | --- |
| Start,0s | [Early](actual-boss-reveal-early-1920x1080.png) | Both content pages covered; Item detail inactive; rail active. |
| Mid,0.2s | [Mid](actual-boss-reveal-mid-1920x1080.png) | Original stepped reveal holes expose Creature art; remaining mask occludes it. |
| Clip sample,0.4s | [Final sample](actual-boss-reveal-final-1920x1080.png) | Last clip sprite; Cover still active during manual sample. |
| Routine completed | [Post Reveal](native-boss-post-reveal-1920x1080.png) | Cover inactive/disabled; no old Item detail behind Creature content. |

Native state metadata: [JSON](native-reveal-states.json). Reported user screenshot retained as [baseline](reported-hub-overlap.png).

## Limits And Doc Impact

Full ProtoTypeHub manual gameplay and Player build were not run. Existing isolated tests intentionally remove Item presenters/font localizers and can emit their missing-presenter/CJK-glyph warnings; they do not prove real Item or production localized-font integration. The new live fixture supplies those checks. No unrelated UI translation/content change was made.

Doc Impact Check: SessionLog, StructureMemory, ErrorLog and ActiveTask/TaskIndex updated. DecisionLog, Architecture/Contracts/Guides and Presentation HTML unchanged; no Presentation HTML stale candidate identified for this local visibility/render-order fix.
