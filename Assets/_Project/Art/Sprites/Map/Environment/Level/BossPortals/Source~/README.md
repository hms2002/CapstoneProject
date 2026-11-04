# Boss portal Aseprite sources

Approved 2026-11-03. Each `Portal_<theme>.aseprite` exports to the same-named PNG in the parent folder. Layers separate the stone frame, translucent membrane and inner energy rim. This `Source~` folder is ignored by Unity's importer.

| Theme / boss | Membrane | Rim | Particle min | Particle max |
| --- | --- | --- | --- | --- |
| Shadow / Chloe C | #79749B | #B7ACC9 | #998CB6 | #DAD0E8 |
| Dragon / Spiri | #D99A3F | #FFCA68 | #FFAF4D | #FFE6A8 |
| Slime / Melta | #38C9A3 | #6CF3D0 | #43DBB1 | #B9FFE8 |
| DemonKing | #9C55DC | #C47CFF | #B66AFF | #EBC2FF |

Preserve 50×49 RGBA, membrane alpha 129/255, rim alpha 255/255 and the original stone pixels. Unity import uses Sprite Single, PPU 16, custom pivot (0.5, 0.15), Point filtering, no mipmaps or compression. Particle colors are separate authored `PortalParticle` start-color values in TwoColors mode with alpha 1; exporting a PNG does not update them. Keep the existing sprite tint white.

Game wiring and verification: `outputs/boss-portal-integration-2026-11-03/README.md` from the project root. The retained native authoring source is outside `Assets`; it is not a runtime component.
