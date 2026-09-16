# Return portal encounter hide

- DungeonReturnPortal immediately clears reveal/interaction when combat resumes, but requests a one-shot ShrinkAndHide rather than disabling the view immediately. encounterHideSeconds defaults to 0.2 and is Inspector-configurable.
- DungeonReturnPortalView shrinks from the currently displayed scale using unscaled time, applied in LateUpdate after Animator evaluation. Duplicate requests do not restart the effect; Open cancels it and restores the authored scale. Scene/component disable retains immediate cleanup. Existing travel Close animation is unchanged.
- Added EncounterHide_ShrinksOnceAndCanReopen regression test. ProceduralPlayModeTests.csproj compiled successfully (0 errors, 205 warnings). Native Unity tests and visual gameplay validation were not executed.
- Doc impact: SessionLog. No prefab, scene, spawning, or reward settings changed.
