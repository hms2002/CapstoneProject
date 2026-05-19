using System;
using CapstoneAudio;
using UnityEngine;

namespace CapstonePresentation
{
    public enum PresentationLifetimeMode
    {
        AutoDetect,
        FixedSeconds,
        ManualRelease
    }

    public enum PresentationSpawnAnchorMode
    {
        ContextPosition,
        TargetSpriteBoundsCenter
    }

    public enum PresentationSpawnScaleMode
    {
        None,
        TargetSpriteBoundsUniform
    }

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

    [Serializable]
    public struct WorldPresentationHook
    {
        public SoundRef sound;
        public CameraShakeHook cameraShake;
        public SpawnedPresentationHook effect;
        public SpawnedPresentationHook particle;

        public bool HasSound => sound.IsSet;
        public bool HasShake => cameraShake.amplitude > 0f;
        public bool HasVisuals => effect.HasContent || particle.HasContent;
        public bool HasAnyContent => HasSound || HasShake || HasVisuals;
    }

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

    public static class PresentationTargetBoundsUtility
    {
        public static bool TryResolveSpriteBounds(GameObject target, out Bounds bounds)
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

        private static bool IsUsable(SpriteRenderer sprite)
        {
            return sprite != null
                   && sprite.enabled
                   && sprite.gameObject.activeInHierarchy
                   && sprite.sprite != null;
        }
    }
}
