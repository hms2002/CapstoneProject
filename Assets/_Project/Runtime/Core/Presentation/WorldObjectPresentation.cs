using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 월드 오브젝트가 성공/열림 같은 단발 상황에서 재생할 GameplayPresentation과 파티클 프리팹 데이터를 보관한다.
    /// - feature component가 concrete presentation runtime을 몰라도 같은 직렬화 구조로 단발 연출을 요청하게 한다.
    /// </summary>
    [Serializable]
    public sealed class WorldObjectPresentationDefinition
    {
        [Header("Gameplay Presentation (Optional)")]
        public GameplayPresentationDefinition gameplayPresentation;

        [Header("Particle Prefab (Optional)")]
        public GameObject particlePrefab;
        public Vector3 particleLocalOffset;
        [Min(-1f)] public float particleLifetimeOverrideSeconds = -1f;
        public bool useUnscaledParticleTime;

        public bool HasAnyContent => gameplayPresentation.HasAnyContent || particlePrefab != null;
    }

    /// <summary>
    /// 책임 :
    /// - WorldObjectPresentationDefinition을 현재 owner/target 문맥에 맞는 Core playback 요청과 파티클 인스턴스로 실행한다.
    /// - 문, 상자, 숏컷 같은 일반 월드 오브젝트가 공통 연출 실행 규칙을 재사용하게 한다.
    /// </summary>
    public sealed class WorldObjectPresentationRuntime
    {
        private const float DefaultParticleLifetimeSeconds = 5f;

        private readonly GameObject ownerObject;
        private readonly GameplayPresentationRuntime gameplayRuntime;

        public WorldObjectPresentationRuntime(GameObject ownerObject, GameplayCueManager cueManager = null)
        {
            this.ownerObject = ownerObject;
            gameplayRuntime = new GameplayPresentationRuntime(ownerObject, cueManager);
        }

        public void PlayExecuteOnly(
            WorldObjectPresentationDefinition definition,
            GameObject instigator = null,
            GameObject target = null,
            Transform anchor = null,
            UnityEngine.Object sourceObject = null,
            GameObject causer = null)
        {
            if (definition == null || !definition.HasAnyContent)
                return;

            Transform resolvedAnchor = anchor != null
                ? anchor
                : target != null
                    ? target.transform
                    : ownerObject != null
                        ? ownerObject.transform
                        : null;

            Vector3 position = resolvedAnchor != null
                ? resolvedAnchor.position
                : ownerObject != null
                    ? ownerObject.transform.position
                    : Vector3.zero;

            GameplayCueParams cueParams = new GameplayCueParams
            {
                Instigator = instigator != null ? instigator : ownerObject,
                Causer = causer != null ? causer : ownerObject,
                Target = target != null ? target : ownerObject,
                Position = position,
                HasExplicitPosition = true,
                Normal = Vector3.up,
                SourceObject = sourceObject != null ? sourceObject : ownerObject,
                Magnitude = definition.gameplayPresentation.EffectiveExecuteCueMagnitude
            };

            gameplayRuntime.ExecuteOnly(definition.gameplayPresentation, cueParams);
            SpawnParticlePrefab(definition, resolvedAnchor, position);
        }

        private static void SpawnParticlePrefab(
            WorldObjectPresentationDefinition definition,
            Transform anchor,
            Vector3 fallbackPosition)
        {
            if (definition.particlePrefab == null)
                return;

            Vector3 spawnPosition = anchor != null
                ? anchor.TransformPoint(definition.particleLocalOffset)
                : fallbackPosition + definition.particleLocalOffset;
            Quaternion spawnRotation = anchor != null ? anchor.rotation : Quaternion.identity;

            GameObject instance = UnityEngine.Object.Instantiate(definition.particlePrefab, spawnPosition, spawnRotation);
            if (instance == null)
                return;

            ConfigureSpawnedParticles(instance, definition.useUnscaledParticleTime);
            float lifetime = ResolveParticleLifetime(instance, definition.particleLifetimeOverrideSeconds);
            if (lifetime > 0f)
                UnityEngine.Object.Destroy(instance, lifetime);
        }

        private static void ConfigureSpawnedParticles(GameObject instance, bool useUnscaledTime)
        {
            instance.SetActive(true);

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (useUnscaledTime)
                {
                    var main = particleSystem.main;
                    main.useUnscaledTime = true;
                }

                particleSystem.Play(withChildren: true);
            }
        }

        private static float ResolveParticleLifetime(GameObject instance, float overrideLifetimeSeconds)
        {
            if (overrideLifetimeSeconds > 0f)
                return overrideLifetimeSeconds;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            if (particleSystems.Length == 0)
                return DefaultParticleLifetimeSeconds;

            float maxLifetime = 0f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                if (main.loop)
                    return DefaultParticleLifetimeSeconds;

                float startDelay = ResolveCurveMax(main.startDelay);
                float startLifetime = ResolveCurveMax(main.startLifetime);
                maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
            }

            return maxLifetime > 0f ? maxLifetime + 0.25f : DefaultParticleLifetimeSeconds;
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
    }
}
