using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CapstonePresentation
{
    [DefaultExecutionOrder(-840)]
    [DisallowMultipleComponent]
    public sealed class PresentationSpawnService : MonoBehaviour
    {
        private sealed class PooledPresentationInstance : MonoBehaviour
        {
            public int prefabId;
            public Vector3 initialScale = Vector3.one;
            public int activeVersion;
        }

        public static PresentationSpawnService Instance { get; private set; }

        private static bool s_isQuitting;

        private readonly Dictionary<int, Queue<PooledPresentationInstance>> poolByPrefabId = new();
        private Transform pooledRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (s_isQuitting || Instance != null)
                return;

            EnsureInstance();
        }

        public static PresentationSpawnService EnsureInstance()
        {
            if (Instance != null)
                return Instance;

#if UNITY_2023_1_OR_NEWER
            PresentationSpawnService existing = FindAnyObjectByType<PresentationSpawnService>();
#else
            PresentationSpawnService existing = FindObjectOfType<PresentationSpawnService>();
#endif
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject root = new GameObject(nameof(PresentationSpawnService));
            return root.AddComponent<PresentationSpawnService>();
        }

        public static GameObject SpawnOneShot(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            PresentationSpawnService service = EnsureInstance();
            return service != null ? service.SpawnInternal(hook, context, scheduleAutoRelease: true) : null;
        }

        public static GameObject SpawnPersistent(in SpawnedPresentationHook hook, in WorldPresentationContext context)
        {
            PresentationSpawnService service = EnsureInstance();
            return service != null ? service.SpawnInternal(hook, context, scheduleAutoRelease: false) : null;
        }

        public static void Release(GameObject instance)
        {
            if (instance == null)
                return;

            PresentationSpawnService service = EnsureInstance();
            if (service == null)
                return;

            service.ReleaseInternal(instance);
        }

        public static void InitializeExternalInstance(GameObject instance, bool useUnscaledTime)
        {
            if (instance == null)
                return;

            InitializeSpawnedInstance(instance, useUnscaledTime);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsurePoolRoot();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit()
        {
            s_isQuitting = true;
        }

        private GameObject SpawnInternal(
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context,
            bool scheduleAutoRelease)
        {
            if (!hook.HasContent)
                return null;

            PooledPresentationInstance pooledInstance = Rent(hook.prefab);
            if (pooledInstance == null)
                return null;

            Transform instanceTransform = pooledInstance.transform;
            instanceTransform.SetParent(null, worldPositionStays: false);

            Vector3 position = context.Position + (context.Rotation * hook.localOffset);
            Quaternion rotation = context.Rotation * Quaternion.Euler(0f, 0f, hook.rotationOffsetZ);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = Vector3.Scale(pooledInstance.initialScale, hook.EffectiveScaleMultiplier);

            GameObject instance = pooledInstance.gameObject;
            instance.SetActive(true);
            InitializeSpawnedInstance(instance, hook.useUnscaledTime);

            if (scheduleAutoRelease)
            {
                float lifetime = ResolvePresentationLifetime(instance, hook.lifetimeOverrideSeconds);
                if (lifetime > 0f)
                {
                    int version = pooledInstance.activeVersion;
                    StartCoroutine(ReturnAfterDelay(pooledInstance, version, lifetime, hook.useUnscaledTime));
                }
            }

            return instance;
        }

        private IEnumerator ReturnAfterDelay(
            PooledPresentationInstance pooledInstance,
            int activeVersion,
            float delaySeconds,
            bool useUnscaledTime)
        {
            if (delaySeconds <= 0f)
                yield break;

            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delaySeconds);
            else
                yield return new WaitForSeconds(delaySeconds);

            if (pooledInstance == null || pooledInstance.activeVersion != activeVersion)
                yield break;

            ReleaseInternal(pooledInstance);
        }

        private PooledPresentationInstance Rent(GameObject prefab)
        {
            if (prefab == null)
                return null;

            int prefabId = prefab.GetInstanceID();
            if (poolByPrefabId.TryGetValue(prefabId, out Queue<PooledPresentationInstance> pool))
            {
                while (pool.Count > 0)
                {
                    PooledPresentationInstance pooled = pool.Dequeue();
                    if (pooled == null)
                        continue;

                    pooled.activeVersion++;
                    return pooled;
                }
            }

            GameObject instance = Instantiate(prefab);
            if (instance == null)
                return null;

            PooledPresentationInstance created = instance.GetComponent<PooledPresentationInstance>();
            if (created == null)
                created = instance.AddComponent<PooledPresentationInstance>();

            created.prefabId = prefabId;
            created.initialScale = instance.transform.localScale == Vector3.zero
                ? Vector3.one
                : instance.transform.localScale;
            created.activeVersion = 1;
            return created;
        }

        private void ReleaseInternal(GameObject instance)
        {
            if (instance == null)
                return;

            PooledPresentationInstance pooledInstance = instance.GetComponent<PooledPresentationInstance>();
            if (pooledInstance == null)
            {
                Destroy(instance);
                return;
            }

            ReleaseInternal(pooledInstance);
        }

        private void ReleaseInternal(PooledPresentationInstance pooledInstance)
        {
            if (pooledInstance == null)
                return;

            GameObject instance = pooledInstance.gameObject;
            pooledInstance.activeVersion++;
            StopAndResetInstance(instance);

            EnsurePoolRoot();
            pooledInstance.transform.SetParent(pooledRoot, worldPositionStays: false);
            pooledInstance.transform.localPosition = Vector3.zero;
            pooledInstance.transform.localRotation = Quaternion.identity;
            pooledInstance.transform.localScale = pooledInstance.initialScale;
            instance.SetActive(false);

            if (!poolByPrefabId.TryGetValue(pooledInstance.prefabId, out Queue<PooledPresentationInstance> pool))
            {
                pool = new Queue<PooledPresentationInstance>();
                poolByPrefabId[pooledInstance.prefabId] = pool;
            }

            pool.Enqueue(pooledInstance);
        }

        private void EnsurePoolRoot()
        {
            if (pooledRoot != null)
                return;

            GameObject root = new GameObject("PooledVisuals");
            root.transform.SetParent(transform, worldPositionStays: false);
            pooledRoot = root.transform;
        }

        private static void InitializeSpawnedInstance(GameObject instance, bool useUnscaledTime)
        {
            if (instance == null)
                return;

            Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                animator.Rebind();
                animator.Update(0f);
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = useUnscaledTime;

                particleSystem.Clear(withChildren: true);
                particleSystem.Play(withChildren: true);
            }

            Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
            for (int i = 0; i < animations.Length; i++)
            {
                Animation animationComponent = animations[i];
                if (animationComponent == null)
                    continue;

                animationComponent.Stop();
                animationComponent.Play();
            }
        }

        private static void StopAndResetInstance(GameObject instance)
        {
            if (instance == null)
                return;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(withChildren: true);
            }

            Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
            for (int i = 0; i < animations.Length; i++)
            {
                Animation animationComponent = animations[i];
                if (animationComponent == null)
                    continue;

                animationComponent.Stop();
            }

            Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                animator.Rebind();
                animator.Update(0f);
            }
        }

        private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
        {
            if (lifetimeOverrideSeconds > 0f)
                return lifetimeOverrideSeconds;

            float particleLifetime = ResolveParticleLifetime(instance);
            if (particleLifetime > 0f)
                return particleLifetime;

            float animationLifetime = ResolveAnimatorLifetime(instance);
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
                    return 1f;

                float startDelay = ResolveCurveMax(main.startDelay);
                float startLifetime = ResolveCurveMax(main.startLifetime);
                maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
            }

            return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
        }

        private static float ResolveAnimatorLifetime(GameObject instance)
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
    }
}
