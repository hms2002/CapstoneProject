# Frame performance measurements — 2026-10-07

Requested verification of the behavior-preserving frame optimization slice. This report separates isolated operation costs, shader GPU command segments, and complete rendered player frames.

## Conditions and provenance

- Unity 6000.4.2f1; Intel Core i7-10750H; NVIDIA RTX 2060 Max-Q; Direct3D 11.
- Windows Development player, existing authored TestScene, 1280×720 windowed, VSync disabled and targetFrameRate -1 during capture. The measurement component is injected into a build copy of the existing test controller; saved scenes and ProjectSettings are not edited.
- Fixed seed, no movement/attack input, normal HP/resource values and a counted test-owned `State.Invulnerable` tag. AI/attack/animation continue, while damage and its player feedback are blocked. Each scenario restores the initial player position. This is a controlled workload rather than a deterministic gameplay replay.
- Physics can still push the player. Failed invulnerable hits can repeat the charge runner's hit query, so the crowd results describe this invulnerable-target stress fixture rather than natural combat with successful damage.
- GoblinWarrior crowds: 0, 30 and 60. Damage-popup stream: 120 requests/second. Five seconds warmup and eight seconds capture per scenario. Actual actor/request counts and player positions accompany raw samples.
- [Source manifest](source-manifest.json) records the exact twenty baseline/current hashes. Baseline preserves prior user changes and reverses only the approved optimization. The HUD prefab differs only in three text raycastTarget flags; its large snapshot is retained outside the repository. [Original operation sources](original-operation-sources/) retain the nineteen source files as `.txt` to avoid compilation.
- Baseline source replacement is temporary, performed with the Editor closed, and restored only when hashes still match. The measurement-only UI helper is temporarily stubbed for the baseline build because its checks reference new APIs; that unused helper is restored byte-for-byte afterward.

## Measurement validity

- CPU operation times use warmed alternating before/after rounds without allocation recording enabled. The reported median/p95 are distributions of batch-average operation cost, not frame-tail measurements.
- Native GC.Alloc operation probes require Bytes metadata, a zero-allocation control and known payload/event-count checks. Byte figures are withheld when calibration fails; heap growth or an unsupported managed counter is not substituted.
- Allocation event counts are separately calibrated against 0/32/32 known allocations. `Sample.Count` can describe allocation events when `GC.Alloc` values have a time unit; those values cannot describe allocated bytes.
- Player GC frame counters require controlled 0/4096/8192-byte payload steps. Main Thread includes waits; available wait-marker measurements are reported separately and cannot be added/subtracted blindly.
- Missing draw counters mean unsupported counters. Completed SRP callbacks and a nonuniform warmup screenshot provide independent rendering evidence. Screenshot/readback happens outside the capture window.
- GPU shader probes use native GPU timestamps, positive-block and rendering checks, and single-draw versus repeated-draw scaling. CPU submission duration is never labelled GPU time.
- A hidden or occluded window can throttle presentation. Such results must not be represented as normal foreground gameplay FPS or as an optimization gain.

## Results

The four Player runs passed rendering, focus, actor count, HUD count, Main Thread availability and calibrated GC-byte checks. Every scenario had 732–1385 frames, seven heart tokens, and the requested enemy count. Popup windows contained 959–960 requests. The table is the arithmetic mean of the two run medians/FPS for each variant; it does not pool frames or imply a confidence interval.

| Player workload | Median frame ms, before → after | Mean FPS, before → after | Median GC B/frame, before → after |
| --- | ---: | ---: | ---: |
| No enemies | 3.257 → 3.238 | 161.6 → 168.6 | 3,494 → 2,878 |
| 30 enemies | 7.709 → 7.166 | 119.9 → 119.9 | 16,626 → 16,644 |
| 60 enemies | 8.914 → 8.360 | 101.5 → 108.0 | 46,942 → 46,232 |
| Popup stream, 120/s | 6.339 → 4.833 | 123.7 → 126.8 | 17,198 → 3,168 |

Popup GC median decreased **81.6%**, and both alternating comparisons reduced its frame median (30.0% and 14.4%). The aggregate frame-median reduction was 23.8%, while average FPS increased only about 2.5%. Its p95 was **13.988 → 14.624 ms**, so tail-frame improvement was not demonstrated. Baseline no-enemy FPS ranged 150.0–173.2 and 60-enemy FPS 91.4–111.6. The 30-enemy mean FPS/GC was effectively unchanged. These overlapping ranges and two runs per variant do not establish a general FPS increase or statistical significance. See [Player summary](player-summary.json).

All **16 CPU operation cases and 12 UI cases** returned equivalent results. Representative recorder-free CPU medians:

| Isolated operation | Before ms | After ms | Scope |
| --- | ---: | ---: | --- |
| Outside-range perception, 60 mobs | 0.791 | 0.116 | Sight calls 60 → 0; no whole-frame claim |
| Busy chase tick, 60 mobs | 1.628 | 0.785 | Sight calls 120 → 60 |
| Alive-member lock poll, 60 mobs | 0.0447 | 0.0061 | Actual root-Mob cache path |
| Raw overlap query, 60-mob collider fixture | 0.0545 | 0.0540 | CPU cost essentially unchanged; full damage resolution excluded |
| Effect repository, 128 entries, last match | 0.0179 | 0.00385 | Four lookup overloads per operation |
| Authored run-timer refresh | 0.00131 | 0.000169 | Text/color projection only |
| Stable status tooltip | 0.969 | 0.00720 | Forced layout and anchor-follow; about 99.3% reduction |
| Ticking status tooltip | 0.874 | 0.142 | About 83.7% reduction |
| Damage popup spawn/setup/release | 0.244 | 0.0715 | About 70.7% reduction; deferred native destruction/rendering excluded |

Status entry binding without hover changed little (roughly −2% to +5% improvement across the four cases), and the popup-canvas query changed about 4%. No major CPU gain is claimed for those small differences. The operation GC.Alloc marker had TimeNanoseconds metadata and returned zero events for the known-allocation controls; **both operation bytes and event counts are unavailable**, not zero allocation. The calibrated Player frame-byte results above remain valid because they use a different native counter. See [CPU operations](cpu-operations.json) and [UI operations](ui-operations.json), including their batch sizes and limits.

Two isolated shader GPU ABBA runs passed timestamp, block-count, nonblank-pixel and draw-count calibration: 18 segments each, 30 positive samples per segment, 20 warmup frames. At 512×512 and 32 repeated draws, the ground-alcohol segment decreased **16.3–18.6%** and ground-fire **16.0–16.3%**. These are GPU command-segment costs rather than entire game GPU/frame/FPS changes. The legacy preparing control was **4.1–10.0% slower**, and projectile control **2.2–3.5% slower**, in both runs. This remains a performance tradeoff/Suggested Later; no additional shader fix was made in Verification mode. Previous 14-condition pixel-equivalence checks concern appearance, not performance. See [GPU run one](gpu.json), [GPU repeat](gpu-repeat.json), and [GPU summary](gpu-summary.json).

## Raw captures and verification

- Player raw JSON is losslessly gzip-compressed: [baseline 1](valid-baseline-1.json.gz), [optimized 2](valid-optimized-2.json.gz), [optimized 3](valid-optimized-3.json.gz), [baseline 4](valid-baseline-4.json.gz). Decompress a file to read every frame sample, counter calibration, focus and workload count.
- Warmup screenshots retained for [60-enemy optimized workload](valid-optimized-3-crowd-60.png) and [popup stream](valid-optimized-3-popup-stream-120-per-second.png). Readback and file writing occurred outside the capture windows.
- Both complete Development players built successfully. After the fixture correction, existing corresponding players were updated with Unity's [BuildScriptsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildOptions.BuildScriptsOnly.html) option using their matching baseline/current sources; no authored asset or serialized field/schema change was introduced. Unity can update script metadata during that build, so identical player-data file hashes are not asserted.
- Generated-project MSBuild, Unity import/compilation, operation equivalence, deferred destruction fences and GPU calibration completed. The production optimization sources were restored to all twenty frozen optimized hashes. Existing persistent save-slot hashes were checked before and after the measurements.
- The Editor operation run logged one [teardown assertion](editor-teardown-assertion.txt) after the comparisons, during `ExitPlayMode/DestroyWorldObjects`: `OutOfCombatMoveSpeedRuntime.OnDisable` ended an effect, whose presentation tried to create a service host via `RuntimeServiceOwnership.EnsureRoot` while the service root was closing. Unity also reported a newly spawned `[RuntimeServices]` root. Those source paths were not changed by this measurement. This is a Suggested Later lifecycle investigation; Console-clean Editor shutdown is not claimed. No corresponding assertion/exception was found in the four valid Player logs or GPU-only repeat. Completed measurements are retained separately from shutdown diagnostics.
- [Editor operation-run record](scene.json) deliberately contains no Editor FPS comparison. Main Thread is elapsed time including waits; individual Stopwatch operation times and native GPU timestamp times have separate labels.
- Player/backend limitations: one Windows/D3D11 machine, Development build, one TestScene and an invulnerable stress target. Whole-game manual playtesting, other scenes/hardware/platforms, natural successful-hit costs and final release-build performance are outside this measurement.

## Excluded fixture

The [first visible probes](excluded-high-hp-probe/) are invalid performance evidence. The measurement helper raised MaxHP to 100000 for survival. `PlayerHealthHeartHUD.GetHeartCount` divides HP by the authored health-per-heart value, and `EnsureHeartTokenCount` creates that many tokens. This generated about 100000 HUD hearts and caused the observed long frames. It was a measurement-fixture error, not representative game performance. The helper now preserves normal attributes, holds one existing invulnerability tag and rejects abnormal heart counts. The production HUD was not changed.
