---
status: complete
authority: active-task
category: implementation
last_reviewed: 2026-10-07
---

# Frame Performance Optimization

- Mode: Implementation, approved in this chat after the investigation and behavior-preservation review.
- Goal: Reduce repeated perception/physics work, UI layout and formatting, query allocations, damage-popup churn, and puddle presentation/shader work.
- Preserve: AI response timing and custom detection side effects; damage target/order/RNG; numeric rounding and localization; tooltip positioning; pause/animation clocks; active popup scene ownership; existing user changes.
- Allowed: The investigated runtime sources, puddle shader, focused existing regression tests, and non-interactive HUD text raycast flags in `GlobalUIRoot.prefab`.
- Excluded: AI/physics frequency changes, shared frame/time caches, offscreen animation suspension, spawn limits, new Managers/Singletons, serialized schema/GUID changes, scenes, bootstrap/DDOL changes, packages/settings, and Architecture/Contracts/Presentation HTML edits.
- Verification: Generated-project MSBuild; targeted Unity EditMode/PlayMode tests; baseline/optimized allocation and puddle CPU comparisons; original/current shader pixel comparison; narrow asset diff and Markdown link checks.
- Risks: Custom detection mutations, overlap result ordering/capacity, dynamic Enemy replacement, popup reuse/localized materials/scene unload, external material-property writers, numeric culture and text-layout invalidation.
- Documentation: SessionLog plus narrow StructureMemory updates. Record unexecuted or failing checks explicitly.

## Outcome

- Runtime/source and three non-interactive HUD text raycast flags implemented; existing user changes preserved.
- Unity EditMode: 5/5 passed. Final changed-area PlayMode: 28/28 passed. The larger 291-case run retains four unchanged balance-data expectations and showed two unrelated Editor prewarm-trace IO failures; popup-specific diagnostic IO was isolated and its three cases subsequently passed.
- Shader output: 14 original/current Direct3D11 conditions, maximum channel difference 0.
- The puddle per-frame property/scale comparison experiment was removed after unfavorable CPU observations. Original `PuddleShaderVisual.cs` remains unchanged; the shader and rising-streak improvements remain.
- No gameplay FPS/GPU improvement percentage is claimed. The managed allocation counter was unavailable on this Editor backend.
- See [SessionLog](../SessionLogs/2026-10-07.md) and [artifacts](../SessionLogs/2026-10-07-frame-performance/).
- Subsequent requested [measurement](frame-performance-measurement.md) completed: native Player popup GC median decreased 81.6%, isolated AI/tooltip/popup CPU costs decreased, and ground shader GPU segments improved 16–19%. General FPS/tail improvement was not established; legacy preparing/projectile shader segments cost more in isolated repeats. The [measurement report](../SessionLogs/2026-10-07-frame-performance-measurement/README.md) preserves exact conditions and limitations.
