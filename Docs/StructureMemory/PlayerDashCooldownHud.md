# Player Dash Cooldown HUD

Navigation aid, not an Architecture or Contracts source of truth.

- Purpose: show the remaining normal dash cooldown below the player as a shrinking orange bar; hide the entire gauge at zero.
- Authored owner: [PF Player.prefab](../../Assets/_Project/Prefabs/Player/PF%20Player.prefab), child `DashCooldownHUD`. World Canvas starts disabled, local Y -0.28, scale 0.01, size 100 x 12, same sorting layer/order as the overhead ammo HUD. Dark background and orange `OrangeFill` (96 x 8) do not receive raycasts.
- Presenter: [PlayerDashCooldownWorldHUD.cs](../../Assets/_Project/Runtime/UI/HUD/PlayerDashCooldownWorldHUD.cs), UI assembly. References point to the player's AbilitySystem, AD_Dash, Canvas and fill RectTransform. No runtime hierarchy creation or subscriptions.
- AbilitySystem owns cooldown state. LateUpdate reads GetCooldownRemaining; the first positive sample becomes the display denominator, and increases recapture it for a new cooldown between frames. Sampling may start a fraction of a frame after cooldown begins. Cooldown reductions and pause follow the existing clock. No gameplay timer or activation gate is added.
- The Flying Boots relic reduces the actual `AD_Dash` cooldown through the existing scoped cooldown-duration multiplier (0.9/0.8/0.7 at levels 1/2/3). Because the HUD samples the resulting AbilitySystem cooldown, its fill duration follows the relic automatically without separate UI branching.
- Awake caches authored fill width. Disable/enable clears display samples and hides the Canvas. The HUD follows its parent and keeps world rotation upright. Scene destruction follows the prefab lifetime.
- Extension: prefab position/size/colors; update the dash reference if the input's dash definition changes. Current AD_Dash is non-charge; this is not a charge-count HUD or general input-availability indicator.
- Verification: UI.csproj compiled with new source via temporary external MSBuild targets; generated project unchanged. Prefab IDs, references, hierarchy and original content preservation checked. Unity import/Play Mode visual acceptance pending.
- No Architecture/Contracts promotion needed for this local display feature.
