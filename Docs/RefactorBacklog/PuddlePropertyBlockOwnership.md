---
status: proposed
authority: refactor-backlog
category: rendering-performance
last_reviewed: 2026-10-07
---

# Puddle Material Property Block Ownership

- Current problem: `PuddleShaderVisual` and `PuddleBlobVisual` can write the same renderer's material property block. Stable Ground properties are consequently reapplied each update to preserve external-write recovery.
- Why it remains: Comparing every owned property through native `Has/Get` calls adds work. The first performance observations did not justify keeping that approach, so the original merged block application was restored. A cached copy alone can overwrite foreign properties or miss a cleared block.
- Target shape: Establish explicit invalidation between the existing writers before skipping stable property application. Preserve foreign properties, zero-valued owned properties, immediate setters, anchor movement and wall clipping. No new service or rendering framework is implied.
- Risks: Renderer replacement, mode transitions, Inspector validation, re-enable/reuse, external block clear, parent movement and existing shader-morph paths.
- Refactor trigger: A representative CPU profile demonstrates that these writes materially affect frame time, and matched before/after measurements show an improvement with correct invalidation.
- Related sources: `Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleShaderVisual.cs`, `PuddleBlobVisual.cs`; `PuddleProjectileVisualPlayModeTests`; [validation artifacts](../SessionLogs/2026-10-07-frame-performance/).
- Status: proposed. The current slice retains original property-block recovery while optimizing other proven hot paths.
