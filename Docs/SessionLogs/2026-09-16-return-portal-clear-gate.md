# Return portal completion visibility

- DungeonReturnPortal now requires RoomWavesCompleted plus no live/pending units or encounter holds for both visibility and interaction when an encounter group exists. Delayed basic spawns no longer leave a visible unusable event-room portal.
- Bell activation hides an already revealed portal and disables its trigger; encounter completion allows it to reopen. Restore/enable paths obey the same gate. Rooms without a group retain entry reveal. Portal objects remain prebuilt, with only visuals/collision gated.
- Updated event portal regression coverage for entry-before-scheduling, delayed spawns, zero-live wave gaps, saved reveal, bell hold and re-reveal. Build of ProceduralPlayModeTests.csproj passed (0 errors, 205 warnings), scoped diff check passed. Unity PlayMode execution and actual-room visual verification were not performed.
- No spawn delays, room assets, bell activation conditions or rewards were changed.
- Doc impact: SessionLog and DungeonReturnPortals StructureMemory; no Architecture/Contracts changes.
