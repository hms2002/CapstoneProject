#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CapstoneAudio;
using CapstoneAudio.EditorTools;
using UnityEditor;
using UnityEngine;

namespace CapstonePresentation.EditorTools
{
    internal static class CueCatalogPreviewUtility
    {
        private sealed class PreviewInstance
        {
            public GameObject gameObject;
            public Animator[] animators = Array.Empty<Animator>();
            public ParticleSystem[] particles = Array.Empty<ParticleSystem>();
            public Animation[] animations = Array.Empty<Animation>();
            public double endTime;
        }

        private sealed class ShakePreviewState
        {
            public double startTime;
            public float duration;
            public float maxOffset;
            public float seed;
            public Vector3 directionBias;
        }

        private readonly struct PreviewPose
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly Vector3 fallbackDirection;

            public PreviewPose(Vector3 position, Quaternion rotation, Vector3 fallbackDirection)
            {
                this.position = position;
                this.rotation = rotation;
                this.fallbackDirection = fallbackDirection;
            }
        }

        private static readonly List<PreviewInstance> activeInstances = new();
        private static PreviewRenderUtility previewUtility;
        private static PresentationCueSO currentCue;
        private static ShakePreviewState activeShake;
        private static double lastUpdateTime = -1d;

        static CueCatalogPreviewUtility()
        {
            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting += StopPreview;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static bool CanPreview => true;
        public static bool HasActiveVisualPreview => activeInstances.Count > 0;

        public static bool IsPreviewing(PresentationCueSO cue)
        {
            return cue != null && currentCue == cue;
        }

        public static bool PlayCue(PresentationCueSO cue)
        {
            if (cue == null || !cue.HasAnyContent)
                return false;

            StopPreview();
            currentCue = cue;

            WorldPresentationHook presentation = cue.Presentation;
            PreviewPose pose = ResolvePreviewPose();
            bool playedAny = false;

            if (presentation.HasSound)
                playedAny |= PreviewSound(presentation.sound);

            if (presentation.HasShake)
                playedAny |= PreviewShake(presentation.cameraShake, pose.fallbackDirection);

            if (presentation.effect.HasContent)
                playedAny |= PreviewVisual(presentation.effect, pose);

            if (presentation.particle.HasContent)
                playedAny |= PreviewVisual(presentation.particle, pose);

            if (!playedAny)
                currentCue = null;

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            return playedAny;
        }

        public static void DrawPreview(Rect rect, PresentationCueSO cue)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.16f, 1f));
            DrawPreviewBorder(rect);

            if (cue == null)
            {
                DrawCenteredLabel(rect, "Cue asset is missing.");
                return;
            }

            if (!cue.HasAnyContent)
            {
                DrawCenteredLabel(rect, "Cue has no presentation content.");
                return;
            }

            if (!IsPreviewing(cue))
            {
                DrawIdlePreview(rect, cue);
                return;
            }

            EnsurePreviewUtility();
            if (previewUtility == null)
            {
                DrawCenteredLabel(rect, "Failed to create preview renderer.");
                return;
            }

            DrawRenderedPreview(rect, cue);
        }

        public static void StopPreview()
        {
            AudioCatalogPreviewUtility.StopPreview();

            for (int i = activeInstances.Count - 1; i >= 0; i--)
                DestroyPreviewInstance(activeInstances[i]);

            activeInstances.Clear();
            activeShake = null;
            currentCue = null;
            lastUpdateTime = -1d;

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void HandleEditorUpdate()
        {
            bool hasVisualPreview = activeInstances.Count > 0;
            bool hasAudioPreview = AudioCatalogPreviewUtility.HasActivePreview;
            bool hasShakePreview = UpdateShakeState();

            if (!hasVisualPreview)
            {
                if (!hasAudioPreview && !hasShakePreview)
                {
                    currentCue = null;
                    lastUpdateTime = -1d;
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = lastUpdateTime < 0d ? 0.016f : Mathf.Max(0f, (float)(now - lastUpdateTime));
            lastUpdateTime = now;

            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                PreviewInstance instance = activeInstances[i];
                if (instance == null || instance.gameObject == null)
                {
                    activeInstances.RemoveAt(i);
                    continue;
                }

                UpdatePreviewInstance(instance, deltaTime);

                if (now >= instance.endTime)
                {
                    DestroyPreviewInstance(instance);
                    activeInstances.RemoveAt(i);
                }
            }

            if (activeInstances.Count == 0)
            {
                if (!AudioCatalogPreviewUtility.HasActivePreview && activeShake == null)
                    currentCue = null;

                lastUpdateTime = -1d;
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void DrawRenderedPreview(Rect rect, PresentationCueSO cue)
        {
            Bounds bounds = CalculatePreviewBounds();
            float aspect = Mathf.Max(1f, rect.width / Mathf.Max(1f, rect.height));
            Vector3 center = bounds.center;

            Vector3 shakeOffset = CalculateShakeOffset(bounds.extents.magnitude);

            previewUtility.BeginPreview(rect, GUIStyle.none);

            Camera previewCamera = previewUtility.camera;
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;

            float orthoSize = Mathf.Max(
                1.25f,
                Mathf.Max(bounds.extents.y * 1.25f, bounds.extents.x * 1.25f / aspect));

            previewCamera.orthographicSize = orthoSize;
            previewCamera.transform.position = center + shakeOffset + new Vector3(0f, 0f, -10f);
            previewCamera.transform.rotation = Quaternion.identity;

            previewUtility.lights[0].intensity = 1.1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
            previewUtility.lights[1].intensity = 1.1f;

            previewCamera.Render();

            Texture previewTexture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);
            DrawPreviewBorder(rect);
            DrawPreviewOverlay(rect, cue);
        }

        private static void DrawIdlePreview(Rect rect, PresentationCueSO cue)
        {
            DrawCenteredLabel(rect, "Play Cue로 프리뷰");

            WorldPresentationHook presentation = cue.Presentation;
            List<Texture> previews = new List<Texture>(2);
            if (presentation.effect.prefab != null)
                previews.Add(AssetPreview.GetAssetPreview(presentation.effect.prefab) ?? AssetPreview.GetMiniThumbnail(presentation.effect.prefab));

            if (presentation.particle.prefab != null)
                previews.Add(AssetPreview.GetAssetPreview(presentation.particle.prefab) ?? AssetPreview.GetMiniThumbnail(presentation.particle.prefab));

            if (previews.Count <= 0)
                return;

            float size = Mathf.Min(96f, rect.width * 0.32f);
            float spacing = 8f;
            float totalWidth = previews.Count * size + (previews.Count - 1) * spacing;
            float startX = rect.center.x - (totalWidth * 0.5f);
            float y = rect.yMax - size - 16f;

            for (int i = 0; i < previews.Count; i++)
            {
                if (previews[i] == null)
                    continue;

                Rect previewRect = new Rect(startX + (i * (size + spacing)), y, size, size);
                GUI.DrawTexture(previewRect, previews[i], ScaleMode.ScaleToFit, true);
            }
        }

        private static void DrawPreviewOverlay(Rect rect, PresentationCueSO cue)
        {
            WorldPresentationHook presentation = cue.Presentation;
            string overlay = presentation.HasSound
                ? $"Sound: {presentation.sound.key}"
                : "No Sound";

            if (presentation.HasShake)
                overlay += "  |  Shake";

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f),
                overlay,
                EditorStyles.miniBoldLabel);
        }

        private static void DrawPreviewBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.35f, 0.35f, 0.4f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.35f, 0.35f, 0.4f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0.35f, 0.35f, 0.4f, 1f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0.35f, 0.35f, 0.4f, 1f));
        }

        private static void DrawCenteredLabel(Rect rect, string text)
        {
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            GUI.Label(rect, text, style);
        }

        private static void EnsurePreviewUtility()
        {
            if (previewUtility != null)
                return;

            previewUtility = new PreviewRenderUtility();
            previewUtility.cameraFieldOfView = 30f;
        }

        private static Bounds CalculatePreviewBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 2f);

            for (int i = 0; i < activeInstances.Count; i++)
            {
                PreviewInstance instance = activeInstances[i];
                if (instance?.gameObject == null)
                    continue;

                Renderer[] renderers = instance.gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        private static Vector3 CalculateShakeOffset(float scaleReference)
        {
            if (activeShake == null)
                return Vector3.zero;

            float now = (float)EditorApplication.timeSinceStartup;
            float elapsed = now - (float)activeShake.startTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, activeShake.duration));
            float falloff = 1f - normalized;

            float noiseX = (Mathf.PerlinNoise(activeShake.seed, elapsed * 24f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(activeShake.seed + 17.31f, elapsed * 27f) - 0.5f) * 2f;

            Vector3 directionalBias = activeShake.directionBias * 0.35f;
            Vector3 noiseOffset = new Vector3(noiseX, noiseY, 0f);
            Vector3 blended = directionalBias + noiseOffset;
            if (blended.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            float scaledOffset = Mathf.Max(0.03f, activeShake.maxOffset * Mathf.Max(1f, scaleReference)) * falloff;
            return blended.normalized * scaledOffset;
        }

        private static bool UpdateShakeState()
        {
            if (activeShake == null)
                return false;

            float elapsed = (float)(EditorApplication.timeSinceStartup - activeShake.startTime);
            if (elapsed < activeShake.duration)
                return true;

            activeShake = null;
            return false;
        }

        private static void StartShakePreview(CameraShakeHook shake, Vector3 fallbackDirection)
        {
            float amplitude = Mathf.Max(0f, shake.amplitude) * shake.EffectiveAmplitudeMultiplier;
            if (shake.maxAmplitude > 0f)
                amplitude = Mathf.Min(amplitude, shake.maxAmplitude);

            if (amplitude <= 0f)
                return;

            Vector3 direction = fallbackDirection;
            if (shake.directionMode == CameraShakeDirectionMode.UseCustom &&
                shake.customDirection.sqrMagnitude > 0.0001f)
            {
                direction = shake.customDirection;
            }

            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.up;
            else
                direction.Normalize();

            activeShake = new ShakePreviewState
            {
                startTime = EditorApplication.timeSinceStartup,
                duration = Mathf.Max(0.12f, 0.12f + (amplitude * 0.04f)),
                maxOffset = amplitude * 0.05f,
                seed = UnityEngine.Random.value * 1000f,
                directionBias = direction
            };
        }

        private static bool PreviewSound(SoundRef sound)
        {
            if (!sound.IsSet)
                return false;

            IReadOnlyList<AudioCatalogSO> catalogs = AudioCatalogEditorUtility.FindCatalogs();
            for (int i = 0; i < catalogs.Count; i++)
            {
                AudioCatalogSO catalog = catalogs[i];
                if (catalog == null || !catalog.TryGetEntry(sound.key, out AudioCatalogEntry entry) || entry == null)
                    continue;

                if (!entry.TryPickClip(out AudioClip clip) || clip == null)
                    return false;

                AudioCatalogPreviewUtility.PlayVariant(
                    clip,
                    Mathf.Clamp01(entry.volume * sound.EffectiveVolumeMultiplier),
                    entry.playbackSpeed,
                    entry.pitchMin,
                    entry.pitchMax,
                    entry.loop);
                return true;
            }

            return false;
        }

        private static bool PreviewShake(CameraShakeHook shake, Vector3 fallbackDirection)
        {
            StartShakePreview(shake, fallbackDirection);
            return activeShake != null;
        }

        private static bool PreviewVisual(in SpawnedPresentationHook hook, PreviewPose pose)
        {
            if (!hook.HasContent || hook.prefab == null)
                return false;

            EnsurePreviewUtility();
            if (previewUtility == null)
                return false;

            GameObject instance = UnityEngine.Object.Instantiate(hook.prefab);
            if (instance == null)
                return false;

            ApplyHideFlagsRecursively(instance);

            Transform instanceTransform = instance.transform;
            Vector3 initialScale = instanceTransform.localScale == Vector3.zero ? Vector3.one : instanceTransform.localScale;
            instanceTransform.SetPositionAndRotation(
                pose.position + (pose.rotation * hook.localOffset),
                pose.rotation * Quaternion.Euler(0f, 0f, hook.rotationOffsetZ));
            instanceTransform.localScale = Vector3.Scale(initialScale, hook.EffectiveScaleMultiplier);

            WorldPresentationRuntime.InitializeSpawnedPresentation(instance, hook.useUnscaledTime);
            previewUtility.AddSingleGO(instance);

            activeInstances.Add(new PreviewInstance
            {
                gameObject = instance,
                animators = instance.GetComponentsInChildren<Animator>(includeInactive: true),
                particles = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true),
                animations = instance.GetComponentsInChildren<Animation>(includeInactive: true),
                endTime = EditorApplication.timeSinceStartup + ResolveLifetime(instance, hook.lifetimeOverrideSeconds)
            });

            return true;
        }

        private static PreviewPose ResolvePreviewPose()
        {
            Transform selected = Selection.activeTransform;
            if (selected != null)
            {
                Vector3 direction = selected.up;
                direction.z = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector3.up;
                else
                    direction.Normalize();

                return new PreviewPose(Vector3.zero, selected.rotation, direction);
            }

            return new PreviewPose(Vector3.zero, Quaternion.identity, Vector3.up);
        }

        private static void UpdatePreviewInstance(PreviewInstance instance, float deltaTime)
        {
            for (int i = 0; i < instance.animators.Length; i++)
            {
                Animator animator = instance.animators[i];
                if (animator == null)
                    continue;

                animator.Update(deltaTime);
            }

            for (int i = 0; i < instance.particles.Length; i++)
            {
                ParticleSystem particleSystem = instance.particles[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Simulate(deltaTime, withChildren: false, restart: false, fixedTimeStep: false);
            }

            for (int i = 0; i < instance.animations.Length; i++)
            {
                Animation animationComponent = instance.animations[i];
                if (animationComponent == null)
                    continue;

                bool sampled = false;
                foreach (AnimationState state in animationComponent)
                {
                    if (state == null || !state.enabled || state.clip == null)
                        continue;

                    sampled = true;
                    float nextTime = state.time + deltaTime * state.speed;
                    float clipLength = Mathf.Max(0.0001f, state.length);
                    if (state.wrapMode == WrapMode.Loop)
                        nextTime %= clipLength;
                    else
                        nextTime = Mathf.Min(nextTime, clipLength);

                    state.time = nextTime;
                }

                if (sampled)
                    animationComponent.Sample();
            }
        }

        private static void ApplyHideFlagsRecursively(GameObject root)
        {
            if (root == null)
                return;

            root.hideFlags = HideFlags.HideAndDontSave;
            Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < children.Length; i++)
                children[i].gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private static float ResolveLifetime(GameObject instance, float lifetimeOverrideSeconds)
        {
            if (lifetimeOverrideSeconds > 0f)
                return lifetimeOverrideSeconds;

            float particleLifetime = ResolveParticleLifetime(instance);
            if (particleLifetime > 0f)
                return particleLifetime;

            float animationLifetime = ResolveAnimationLifetime(instance);
            if (animationLifetime > 0f)
                return animationLifetime;

            return 1f;
        }

        private static float ResolveParticleLifetime(GameObject instance)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            if (particleSystems == null || particleSystems.Length == 0)
                return 0f;

            float maxLifetime = 0f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                if (main.loop)
                    return float.PositiveInfinity;

                float startDelay = ResolveCurveMax(main.startDelay);
                float startLifetime = ResolveCurveMax(main.startLifetime);
                maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
            }

            return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
        }

        private static float ResolveAnimationLifetime(GameObject instance)
        {
            float maxLifetime = 0f;

            Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || animator.runtimeAnimatorController == null)
                    continue;

                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    if (clip == null)
                        continue;

                    maxLifetime = Mathf.Max(maxLifetime, clip.length);
                }
            }

            Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
            for (int i = 0; i < animations.Length; i++)
            {
                Animation animationComponent = animations[i];
                if (animationComponent == null)
                    continue;

                foreach (AnimationState state in animationComponent)
                {
                    if (state?.clip == null)
                        continue;

                    maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
                }
            }

            return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
        }

        private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => curve.constant,
                ParticleSystemCurveMode.TwoConstants => curve.constantMax,
                ParticleSystemCurveMode.Curve => curve.curveMultiplier,
                ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
                _ => Mathf.Max(curve.constant, curve.constantMax)
            };
        }

        private static void DestroyPreviewInstance(PreviewInstance instance)
        {
            if (instance?.gameObject != null)
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
            {
                StopPreview();
            }
        }
    }
}
#endif
