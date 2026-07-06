using System;
using CapstoneAudio;
using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: Core 월드 연출 요청을 실제 presentation runtime 구현으로 넘기는 최소 backend 계약이다.
    /// </summary>
    public interface IWorldPresentationBackend
    {
        void Play(in WorldPresentationHook hook, in WorldPresentationContext context);
        void PlaySignalOnly(in WorldPresentationHook hook, in WorldPresentationContext context);
        void PlayMerged(
            in WorldPresentationHook hook,
            SoundRef legacySound,
            CameraShakeHook legacyShake,
            in WorldPresentationContext context);
        Coroutine PlayDeferredAsync(in WorldPresentationHook hook, in WorldPresentationContext context);
        GameObject SpawnOneShot(in SpawnedPresentationHook hook, in WorldPresentationContext context);
        Coroutine SpawnOneShotDeferredAsync(in SpawnedPresentationHook hook, in WorldPresentationContext context);
        GameObject SpawnPersistent(in SpawnedPresentationHook hook, in WorldPresentationContext context);
        void InitializeSpawnedPresentation(GameObject instance, bool useUnscaledTime);
        void Release(GameObject instance);
    }

    /// <summary>
    /// 책임: presentation spawn service의 prewarm trace 기록 요청을 구체 Editor 구현으로 전달하는 backend 계약이다.
    /// </summary>
    public interface IPresentationPrewarmTraceBackend
    {
        void RecordSpawn(GameObject prefab, bool coldSpawn);
    }

    /// <summary>
    /// 책임: Core/Gameplay 호출자가 구체 WorldPresentationRuntime 구현 없이 월드 연출 실행을 요청하게 한다.
    /// </summary>
    public static class WorldPresentationPlayback
    {
        private static IWorldPresentationBackend backend;

        public static void RegisterBackend(IWorldPresentationBackend presentationBackend)
        {
            backend = presentationBackend;
        }

        public static void Play(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            backend?.Play(hook, context);
        }

        public static void PlaySignalOnly(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            backend?.PlaySignalOnly(hook, context);
        }

        public static void PlayMerged(
            in WorldPresentationHook hook,
            SoundRef legacySound,
            CameraShakeHook legacyShake,
            in WorldPresentationContext context)
        {
            backend?.PlayMerged(hook, legacySound, legacyShake, context);
        }

        public static Coroutine PlayDeferredAsync(in WorldPresentationHook hook, in WorldPresentationContext context)
        {
            return backend != null
                ? backend.PlayDeferredAsync(hook, context)
                : null;
        }

        public static GameObject SpawnOneShot(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            return backend != null
                ? backend.SpawnOneShot(hook, context)
                : null;
        }

        public static Coroutine SpawnOneShotDeferredAsync(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            return backend != null
                ? backend.SpawnOneShotDeferredAsync(hook, context)
                : null;
        }

        public static GameObject SpawnPersistent(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            return backend != null
                ? backend.SpawnPersistent(hook, context)
                : null;
        }

        public static void InitializeSpawnedPresentation(GameObject instance, bool useUnscaledTime)
        {
            backend?.InitializeSpawnedPresentation(instance, useUnscaledTime);
        }

        public static void Release(GameObject instance)
        {
            backend?.Release(instance);
        }
    }

    /// <summary>
    /// 책임: 런타임 presentation spawn 코드가 Editor trace 구현 없이 prewarm 누락 기록을 요청하게 한다.
    /// </summary>
    public static class PresentationPrewarmTracePlayback
    {
        private static IPresentationPrewarmTraceBackend backend;

        public static void RegisterBackend(IPresentationPrewarmTraceBackend traceBackend)
        {
            backend = traceBackend;
        }

        public static void UnregisterBackend(IPresentationPrewarmTraceBackend traceBackend)
        {
            if (ReferenceEquals(backend, traceBackend))
                backend = null;
        }

        public static void RecordSpawn(GameObject prefab, bool coldSpawn)
        {
            if (prefab != null)
                backend?.RecordSpawn(prefab, coldSpawn);
        }
    }

    /// <summary>
    /// 책임: SpawnedPresentationHook으로 생성된 연출 오브젝트의 수명 관리 방식을 나타낸다.
    /// </summary>
    public enum PresentationLifetimeMode
    {
        AutoDetect,
        FixedSeconds,
        ManualRelease
    }

    /// <summary>
    /// 책임: 월드 연출 생성 위치를 요청 context 위치로 잡을지, 타겟 sprite bounds 중심으로 잡을지 나타낸다.
    /// </summary>
    public enum PresentationSpawnAnchorMode
    {
        ContextPosition,
        TargetSpriteBoundsCenter
    }

    /// <summary>
    /// 책임: 월드 연출 스케일을 고정값으로 둘지, 타겟 sprite bounds 기준으로 보정할지 나타낸다.
    /// </summary>
    public enum PresentationSpawnScaleMode
    {
        None,
        TargetSpriteBoundsUniform
    }

    /// <summary>
    /// 책임 : 타겟 기반 연출의 앵커/스케일 계산에 사용할 bounds 계산 방식을 표현한다.
    /// </summary>
    public enum PresentationTargetBoundsMode
    {
        RendererAabb,
        SpriteMeshTight
    }

    /// <summary>
    /// 책임: Core/Gameplay 데이터가 구체 spawn service 없이 생성할 월드 연출 prefab과 배치/스케일/수명 정책을 전달하는 직렬화 값 타입이다.
    /// </summary>
    [Serializable]
    public struct SpawnedPresentationHook
    {
        public GameObject prefab;
        public Vector3 localOffset;
        public float rotationOffsetZ;
        public Vector3 scaleMultiplier;
        public bool attachToTarget;
        public PresentationSpawnAnchorMode anchorMode;
        public PresentationSpawnScaleMode scaleMode;
        public PresentationTargetBoundsMode boundsMode;
        [Min(0.0001f)] public float targetBoundsReferenceSize;
        [Min(0f)] public float targetBoundsScaleMultiplier;
        public PresentationLifetimeMode lifetimeMode;
        [Min(0f)] public float lifetimeOverrideSeconds;
        public bool useUnscaledTime;

        public bool HasContent => prefab != null;
        public bool ShouldAutoRelease => lifetimeMode != PresentationLifetimeMode.ManualRelease;

        public Vector3 EffectiveScaleMultiplier =>
            scaleMultiplier == Vector3.zero ? Vector3.one : scaleMultiplier;

        public float EffectiveTargetBoundsReferenceSize =>
            targetBoundsReferenceSize > 0.0001f ? targetBoundsReferenceSize : 1f;

        public float EffectiveTargetBoundsScaleMultiplier =>
            targetBoundsScaleMultiplier > 0f ? targetBoundsScaleMultiplier : 1f;
    }

    /// <summary>
    /// 책임: Core/Gameplay 데이터가 구체 presentation runtime 없이 사운드, 카메라 흔들림, 월드 연출 요청을 한 번에 전달하는 직렬화 값 타입이다.
    /// </summary>
    [Serializable]
    public struct WorldPresentationHook
    {
        public SoundRef sound;
        [Tooltip("비어 있지 않으면 단일 sound 대신 유효한 후보 중 하나를 무작위로 재생합니다.")]
        public SoundRef[] randomSounds;
        [Tooltip("메인 사운드와 동시에 추가로 전부 재생할 사운드 목록입니다.")]
        public SoundRef[] additionalSounds;
        public CameraShakeHook cameraShake;
        public SpawnedPresentationHook effect;
        public SpawnedPresentationHook particle;

        public bool HasSound => sound.IsSet || HasRandomSound || HasAdditionalSound;
        public bool HasRandomSound => CountValidRandomSounds() > 0;
        public bool HasAdditionalSound => CountValidAdditionalSounds() > 0;
        public bool HasShake => cameraShake.amplitude > 0f;
        public bool HasVisuals => effect.HasContent || particle.HasContent;
        public bool HasAnyContent => HasSound || HasShake || HasVisuals;
        public SoundRef[] AdditionalSounds => additionalSounds;

        /// <summary>
        /// 책임:
        /// - 연출 hook에 여러 사운드 후보가 지정된 경우 그중 하나를 선택한다.
        /// - 랜덤 후보가 없으면 기존 단일 sound 경로를 그대로 유지한다.
        /// </summary>
        public SoundRef ResolveSound()
        {
            return TryGetRandomSound(out SoundRef randomSound) ? randomSound : sound;
        }

        private bool TryGetRandomSound(out SoundRef selected)
        {
            selected = default;
            int validCount = CountValidRandomSounds();
            if (validCount <= 0)
                return false;

            int targetIndex = UnityEngine.Random.Range(0, validCount);
            for (int i = 0; i < randomSounds.Length; i++)
            {
                if (!randomSounds[i].IsSet)
                    continue;

                if (targetIndex == 0)
                {
                    selected = randomSounds[i];
                    return true;
                }

                targetIndex--;
            }

            return false;
        }

        private int CountValidRandomSounds()
        {
            int validCount = 0;

            if (randomSounds == null || randomSounds.Length == 0)
                return 0;

            for (int i = 0; i < randomSounds.Length; i++)
            {
                if (randomSounds[i].IsSet)
                    validCount++;
            }

            return validCount;
        }

        private int CountValidAdditionalSounds()
        {
            int validCount = 0;

            if (additionalSounds == null || additionalSounds.Length == 0)
                return 0;

            for (int i = 0; i < additionalSounds.Length; i++)
            {
                if (additionalSounds[i].IsSet)
                    validCount++;
            }

            return validCount;
        }
    }

    /// <summary>
    /// 책임: 월드 연출 실행자가 요청을 어느 instigator/causer/target/source/위치/회전 기준으로 해석할지 전달한다.
    /// </summary>
    public readonly struct WorldPresentationContext
    {
        public readonly GameObject Instigator;
        public readonly GameObject Causer;
        public readonly GameObject Target;
        public readonly UnityEngine.Object SourceObject;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 FallbackDirection;

        public WorldPresentationContext(
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            Vector3 position,
            Quaternion rotation,
            Vector3 fallbackDirection)
        {
            Instigator = instigator;
            Causer = causer;
            Target = target;
            SourceObject = sourceObject;
            Position = position;
            Rotation = rotation;
            FallbackDirection = fallbackDirection;
        }

        public static WorldPresentationContext AtWorld(
            GameObject instigator,
            Vector3 position,
            Vector3 fallbackDirection,
            GameObject target = null,
            UnityEngine.Object sourceObject = null,
            Quaternion? rotation = null,
            GameObject causer = null)
        {
            return new WorldPresentationContext(
                instigator,
                causer != null ? causer : instigator,
                target,
                sourceObject,
                position,
                rotation ?? Quaternion.identity,
                fallbackDirection);
        }

        public static WorldPresentationContext AtAnchor(
            GameObject instigator,
            Transform anchor,
            Vector3 fallbackDirection,
            GameObject target = null,
            UnityEngine.Object sourceObject = null,
            GameObject causer = null)
        {
            Vector3 position = anchor != null
                ? anchor.position
                : instigator != null
                    ? instigator.transform.position
                    : Vector3.zero;
            Quaternion rotation = anchor != null ? anchor.rotation : Quaternion.identity;

            return new WorldPresentationContext(
                instigator,
                causer != null ? causer : instigator,
                target,
                sourceObject,
                position,
                rotation,
                fallbackDirection);
        }
    }

    /// <summary>
    /// 책임 : 월드 연출이 타겟의 시각적 크기/중심을 기준으로 배치될 수 있도록 SpriteRenderer bounds를 계산한다.
    /// </summary>
    public static class PresentationTargetBoundsUtility
    {
        public static bool TryResolveSpriteBounds(
            GameObject target,
            out Bounds bounds,
            PresentationTargetBoundsMode mode = PresentationTargetBoundsMode.RendererAabb)
        {
            return mode == PresentationTargetBoundsMode.SpriteMeshTight
                ? TryResolveTightSpriteBounds(target, out bounds)
                : TryResolveRendererBounds(target, out bounds);
        }

        private static bool TryResolveRendererBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            if (target.TryGetComponent(out SpriteRenderer rootSprite) && IsUsable(rootSprite))
            {
                bounds = rootSprite.bounds;
                return true;
            }

            bool hasBounds = false;
            SpriteRenderer[] sprites = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (!IsUsable(sprite))
                    continue;

                if (!hasBounds)
                {
                    bounds = sprite.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(sprite.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryResolveTightSpriteBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            SpriteRenderer[] sprites = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            if (TryResolveTightBoundsFromVisualRenderers(sprites, out bounds))
                return true;

            if (target.TryGetComponent(out SpriteRenderer rootSprite) && IsUsable(rootSprite))
                return TryResolveRendererTightBounds(rootSprite, out bounds);

            bool hasBounds = false;
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (!IsUsable(sprite) || IsShadowRenderer(sprite))
                    continue;

                if (!TryResolveRendererTightBounds(sprite, out Bounds spriteBounds))
                    continue;

                if (!hasBounds)
                {
                    bounds = spriteBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryResolveTightBoundsFromVisualRenderers(SpriteRenderer[] sprites, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (sprites == null)
                return false;

            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (!IsUsable(sprite) || !IsVisualRenderer(sprite) || IsShadowRenderer(sprite))
                    continue;

                if (!TryResolveRendererTightBounds(sprite, out Bounds spriteBounds))
                    continue;

                if (!hasBounds)
                {
                    bounds = spriteBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryResolveRendererTightBounds(SpriteRenderer sprite, out Bounds bounds)
        {
            bounds = default;
            if (!IsUsable(sprite))
                return false;

            Vector2[] vertices = sprite.sprite.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                bounds = sprite.bounds;
                return true;
            }

            Vector3 first = ToWorldSpriteVertex(sprite, vertices[0]);
            bounds = new Bounds(first, Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(ToWorldSpriteVertex(sprite, vertices[i]));

            return true;
        }

        private static Vector3 ToWorldSpriteVertex(SpriteRenderer sprite, Vector2 vertex)
        {
            if (sprite.flipX)
                vertex.x = -vertex.x;
            if (sprite.flipY)
                vertex.y = -vertex.y;

            return sprite.transform.TransformPoint(vertex);
        }

        private static bool IsVisualRenderer(SpriteRenderer sprite)
        {
            Transform current = sprite.transform;
            while (current != null)
            {
                string name = current.name;
                if (!string.IsNullOrEmpty(name)
                    && name.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsShadowRenderer(SpriteRenderer sprite)
        {
            Transform current = sprite.transform;
            while (current != null)
            {
                string name = current.name;
                if (!string.IsNullOrEmpty(name)
                    && name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsUsable(SpriteRenderer sprite)
        {
            return sprite != null
                   && sprite.enabled
                   && sprite.gameObject.activeInHierarchy
                   && sprite.sprite != null;
        }
    }
}
