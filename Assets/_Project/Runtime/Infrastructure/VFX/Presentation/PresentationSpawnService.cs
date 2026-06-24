using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneRuntime;
using UnityEngine;

namespace CapstonePresentation
{
    [DefaultExecutionOrder(-840)]
    [DisallowMultipleComponent]
    public sealed class PresentationSpawnService : MonoBehaviour
    {
        public readonly struct PoolDebugEntry
        {
            public PoolDebugEntry(string name, int pooledCount)
            {
                Name = name;
                PooledCount = pooledCount;
            }

            public string Name { get; }
            public int PooledCount { get; }
        }

        private const float LoopingAutoReleaseFallbackSeconds = 1f;

        [Header("Prewarm Budget")]
        [SerializeField, Min(1)] private int prewarmInstancesPerFrame = 2;
        [SerializeField, Min(0f)] private float prewarmFrameBudgetMilliseconds = 2f;

        private sealed class PooledPresentationInstance : MonoBehaviour
        {
            public int prefabId;
            public Vector3 initialScale = Vector3.one;
            public int activeVersion;
        }

        private sealed class PendingPrewarmRequest
        {
            public PendingPrewarmRequest(GameObject prefab, int count, AssetProviderOperation operation)
            {
                Prefab = prefab;
                Count = count;
                Operation = operation;
            }

            public GameObject Prefab { get; }
            public int Count { get; }
            public AssetProviderOperation Operation { get; }
            public int PrefabId { get; set; }
            public int CreatedCount { get; set; }
            public Queue<PooledPresentationInstance> Pool { get; set; }
            public bool IsInitialized { get; set; }
        }

        public static PresentationSpawnService Instance { get; private set; }

        private static bool s_isQuitting;

        private readonly Dictionary<int, Queue<PooledPresentationInstance>> poolByPrefabId = new();
        private readonly Dictionary<int, string> prefabNamesById = new();
        private readonly Queue<PendingPrewarmRequest> pendingPrewarmRequests = new();
        private Transform pooledRoot;
        private Coroutine prewarmQueueRoutine;

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
            PresentationSpawnService existing = RuntimeServiceOwnership.FindExistingService<PresentationSpawnService>();
#else
            PresentationSpawnService existing = RuntimeServiceOwnership.FindExistingService<PresentationSpawnService>();
#endif
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject root = RuntimeServiceOwnership.CreateServiceHost(nameof(PresentationSpawnService));
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

        public static IEnumerator SpawnOneShotAsync(
            SpawnedPresentationHook hook,
            WorldPresentationContext context,
            Action<GameObject> onSpawned = null)
        {
            PresentationSpawnService service = EnsureInstance();
            if (service == null)
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            yield return service.SpawnInternalAsync(hook, context, scheduleAutoRelease: true, onSpawned);
        }

        public static IEnumerator SpawnPersistentAsync(
            SpawnedPresentationHook hook,
            WorldPresentationContext context,
            Action<GameObject> onSpawned = null)
        {
            PresentationSpawnService service = EnsureInstance();
            if (service == null)
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            yield return service.SpawnInternalAsync(hook, context, scheduleAutoRelease: false, onSpawned);
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

        public static void PrewarmPrefab(GameObject prefab, int count = 1)
        {
            if (prefab == null || count <= 0)
                return;

            PresentationSpawnService service = EnsureInstance();
            service?.PrewarmInternal(prefab, count);
        }

        public static AssetProviderOperation PrewarmPrefabAsync(GameObject prefab, int count = 1)
        {
            if (prefab == null || count <= 0)
                return AssetProviderOperation.Completed("PrewarmPrefab <none>");

            PresentationSpawnService service = EnsureInstance();
            return service != null
                ? service.PrewarmInternalAsync(prefab, count)
                : AssetProviderOperation.Completed(BuildPrewarmLabel(prefab, count));
        }

        public static void TrimPrewarmedPrefab(GameObject prefab, int count = 1)
        {
            if (prefab == null || count <= 0)
                return;

            PresentationSpawnService service = EnsureInstance();
            service?.TrimInternal(prefab, count);
        }

        public static void InitializeExternalInstance(GameObject instance, bool useUnscaledTime)
        {
            if (instance == null)
                return;

            InitializeSpawnedInstance(instance, useUnscaledTime);
        }

        public static int GetPooledPrefabTypeCount()
        {
            PresentationSpawnService service = EnsureInstance();
            return service != null ? service.poolByPrefabId.Count : 0;
        }

        public static int GetTotalPooledInstanceCount()
        {
            PresentationSpawnService service = EnsureInstance();
            return service != null ? service.GetTotalPooledInstanceCountInternal() : 0;
        }

        public static PoolDebugEntry[] GetPoolSnapshot(int maxCount = 24)
        {
            PresentationSpawnService service = EnsureInstance();
            return service != null ? service.BuildPoolSnapshot(maxCount) : System.Array.Empty<PoolDebugEntry>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RuntimeServiceOwnership.Adopt(this);
            EnsurePoolRoot();
        }

        private void OnDestroy()
        {
            CompletePendingPrewarmRequests("PresentationSpawnService was destroyed.");

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

            GameObject resolvedPrefab = ResolvePrefab(hook.prefab);
            if (resolvedPrefab == null)
                return null;

            return SpawnResolvedInternal(hook, context, scheduleAutoRelease, resolvedPrefab);
        }

        private IEnumerator SpawnInternalAsync(
            SpawnedPresentationHook hook,
            WorldPresentationContext context,
            bool scheduleAutoRelease,
            Action<GameObject> onSpawned)
        {
            if (!hook.HasContent)
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            AssetResolveOperation<GameObject> resolveOperation = PresentationAssetProvider.ResolvePrefabAsync(hook.prefab);
            if (resolveOperation != null && !resolveOperation.IsDone)
                yield return resolveOperation;

            GameObject resolvedPrefab = resolveOperation != null ? resolveOperation.Asset : hook.prefab;
            GameObject instance = resolvedPrefab != null
                ? SpawnResolvedInternal(hook, context, scheduleAutoRelease, resolvedPrefab)
                : null;

            onSpawned?.Invoke(instance);
        }

        private GameObject SpawnResolvedInternal(
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context,
            bool scheduleAutoRelease,
            GameObject resolvedPrefab)
        {
            PooledPresentationInstance pooledInstance = RentResolvedPrefab(resolvedPrefab, out bool coldSpawn);
            if (pooledInstance == null)
                return null;

            return ActivatePooledInstance(
                pooledInstance,
                resolvedPrefab,
                hook,
                context,
                scheduleAutoRelease,
                coldSpawn);
        }

        private GameObject ActivatePooledInstance(
            PooledPresentationInstance pooledInstance,
            GameObject resolvedPrefab,
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context,
            bool scheduleAutoRelease,
            bool coldSpawn)
        {
            Transform instanceTransform = pooledInstance.transform;
            instanceTransform.SetParent(null, worldPositionStays: false);

            Vector3 position = ResolveAnchorPosition(hook, context) + (context.Rotation * hook.localOffset);
            Quaternion rotation = context.Rotation * Quaternion.Euler(0f, 0f, hook.rotationOffsetZ);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = ResolveSpawnScale(pooledInstance.initialScale, hook, context);

            if (hook.attachToTarget && context.Target != null)
                instanceTransform.SetParent(context.Target.transform, worldPositionStays: true);

            GameObject instance = pooledInstance.gameObject;
            instance.SetActive(true);
            InitializeSpawnedInstance(instance, hook.useUnscaledTime);
#if UNITY_EDITOR
            PrewarmTraceRuntime.RecordSpawn(resolvedPrefab, coldSpawn);
#endif

            if (scheduleAutoRelease && hook.ShouldAutoRelease)
            {
                float lifetime = ResolvePresentationLifetime(instance, hook);
                if (lifetime > 0f)
                {
                    int version = pooledInstance.activeVersion;
                    StartCoroutine(ReturnAfterDelay(pooledInstance, version, lifetime, hook.useUnscaledTime));
                }
            }

            return instance;
        }

        private static Vector3 ResolveAnchorPosition(
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context)
        {
            if (hook.anchorMode == PresentationSpawnAnchorMode.TargetSpriteBoundsCenter
                && PresentationTargetBoundsUtility.TryResolveSpriteBounds(context.Target, out Bounds bounds, hook.boundsMode))
            {
                return bounds.center;
            }

            return context.Position;
        }

        private static Vector3 ResolveSpawnScale(
            Vector3 initialScale,
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context)
        {
            Vector3 scale = Vector3.Scale(initialScale, hook.EffectiveScaleMultiplier);
            if (hook.scaleMode != PresentationSpawnScaleMode.TargetSpriteBoundsUniform)
                return scale;

            if (!PresentationTargetBoundsUtility.TryResolveSpriteBounds(context.Target, out Bounds bounds, hook.boundsMode))
                return scale;

            float targetSize = Mathf.Max(bounds.size.x, bounds.size.y);
            if (targetSize <= 0f)
                return scale;

            float boundsScale = (targetSize / hook.EffectiveTargetBoundsReferenceSize)
                                * hook.EffectiveTargetBoundsScaleMultiplier;
            return scale * boundsScale;
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

        private static GameObject ResolvePrefab(GameObject prefab)
        {
            IAssetProvider assetProvider = PresentationAssetProvider.CurrentProvider;
            return assetProvider != null ? assetProvider.ResolvePrefab(prefab) : prefab;
        }

        private PooledPresentationInstance RentResolvedPrefab(GameObject resolvedPrefab, out bool coldSpawn)
        {
            coldSpawn = false;
            if (resolvedPrefab == null)
                return null;

            int prefabId = resolvedPrefab.GetInstanceID();
            prefabNamesById[prefabId] = resolvedPrefab.name;
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

            coldSpawn = true;
            return CreatePooledInstance(resolvedPrefab, prefabId, deactivateAfterCreate: false);
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

        private void PrewarmInternal(GameObject prefab, int count)
        {
            prefab = ResolvePrefab(prefab);
            if (prefab == null || count <= 0)
                return;

            int prefabId = prefab.GetInstanceID();
            prefabNamesById[prefabId] = prefab.name;
            if (!poolByPrefabId.TryGetValue(prefabId, out Queue<PooledPresentationInstance> pool))
            {
                pool = new Queue<PooledPresentationInstance>();
                poolByPrefabId[prefabId] = pool;
            }

            EnsurePoolRoot();

            for (int i = 0; i < count; i++)
            {
                PooledPresentationInstance created = CreatePooledInstance(prefab, prefabId);
                if (created == null)
                    break;

                EnqueuePrewarmedInstance(created, pool);
            }
        }

        private AssetProviderOperation PrewarmInternalAsync(GameObject prefab, int count)
        {
            prefab = ResolvePrefab(prefab);
            if (prefab == null || count <= 0)
                return AssetProviderOperation.Completed("PrewarmPrefab <none>");

            var operation = new AssetProviderOperation(BuildPrewarmLabel(prefab, count));
            operation.SetProgressUnits(Mathf.Max(1, count));
            pendingPrewarmRequests.Enqueue(new PendingPrewarmRequest(prefab, count, operation));
            if (prewarmQueueRoutine == null)
                prewarmQueueRoutine = StartCoroutine(CoRunPrewarmQueue());

            return operation;
        }

        private IEnumerator CoRunPrewarmQueue()
        {
            EnsurePoolRoot();

            while (pendingPrewarmRequests.Count > 0)
            {
                int maxPerFrame = Mathf.Max(1, prewarmInstancesPerFrame);
                long budgetTicks = prewarmFrameBudgetMilliseconds > 0f
                    ? (long)(System.Diagnostics.Stopwatch.Frequency * prewarmFrameBudgetMilliseconds / 1000f)
                    : 0L;
                int createdThisFrame = 0;
                long frameStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                while (pendingPrewarmRequests.Count > 0)
                {
                    PendingPrewarmRequest request = pendingPrewarmRequests.Peek();
                    if (!request.IsInitialized)
                        InitializePrewarmRequest(request);

                    if (request.Prefab == null || request.Pool == null)
                    {
                        request.Operation.Complete("Prewarm request target is missing.");
                        pendingPrewarmRequests.Dequeue();
                        continue;
                    }

                    PooledPresentationInstance created = CreatePooledInstance(request.Prefab, request.PrefabId);
                    if (created == null)
                    {
                        request.Operation.Complete($"Failed to prewarm {request.Prefab.name}.");
                        pendingPrewarmRequests.Dequeue();
                        continue;
                    }

                    EnqueuePrewarmedInstance(created, request.Pool);

                    request.CreatedCount++;
                    createdThisFrame++;
                    request.Operation.ReportProgress(request.CreatedCount / (float)request.Count);

                    if (request.CreatedCount >= request.Count)
                    {
                        request.Operation.Complete();
                        pendingPrewarmRequests.Dequeue();
                    }

                    bool reachedCountBudget = createdThisFrame >= maxPerFrame;
                    bool reachedTimeBudget =
                        budgetTicks > 0L &&
                        System.Diagnostics.Stopwatch.GetTimestamp() - frameStartTicks >= budgetTicks;
                    if (reachedCountBudget || reachedTimeBudget)
                        break;
                }

                if (pendingPrewarmRequests.Count > 0)
                    yield return null;
            }

            prewarmQueueRoutine = null;
        }

        private void InitializePrewarmRequest(PendingPrewarmRequest request)
        {
            request.IsInitialized = true;
            if (request.Prefab == null)
                return;

            int prefabId = request.Prefab.GetInstanceID();
            request.PrefabId = prefabId;
            prefabNamesById[prefabId] = request.Prefab.name;
            if (!poolByPrefabId.TryGetValue(prefabId, out Queue<PooledPresentationInstance> pool))
            {
                pool = new Queue<PooledPresentationInstance>();
                poolByPrefabId[prefabId] = pool;
            }

            request.Pool = pool;
        }

        private void EnqueuePrewarmedInstance(
            PooledPresentationInstance created,
            Queue<PooledPresentationInstance> pool)
        {
            if (created == null || pool == null)
                return;

            EnsurePoolRoot();
            created.transform.SetParent(pooledRoot, worldPositionStays: false);
            created.transform.localPosition = Vector3.zero;
            created.transform.localRotation = Quaternion.identity;
            created.transform.localScale = created.initialScale;
            created.gameObject.SetActive(false);
            pool.Enqueue(created);
        }

        private void CompletePendingPrewarmRequests(string errorMessage)
        {
            while (pendingPrewarmRequests.Count > 0)
            {
                PendingPrewarmRequest request = pendingPrewarmRequests.Dequeue();
                request.Operation?.Complete(errorMessage);
            }

            prewarmQueueRoutine = null;
        }

        private void TrimInternal(GameObject prefab, int count)
        {
            prefab = ResolvePrefab(prefab);
            if (prefab == null || count <= 0)
                return;

            int prefabId = prefab.GetInstanceID();
            if (!poolByPrefabId.TryGetValue(prefabId, out Queue<PooledPresentationInstance> pool) || pool.Count == 0)
                return;

            int removedCount = 0;
            int safety = pool.Count;

            while (pool.Count > 0 && removedCount < count && safety-- > 0)
            {
                PooledPresentationInstance pooled = pool.Dequeue();
                if (pooled == null)
                    continue;

                Destroy(pooled.gameObject);
                removedCount++;
            }

            if (pool.Count == 0)
            {
                poolByPrefabId.Remove(prefabId);
                prefabNamesById.Remove(prefabId);
            }
        }

        private PooledPresentationInstance CreatePooledInstance(
            GameObject prefab,
            int prefabId,
            bool deactivateAfterCreate = true)
        {
            EnsurePoolRoot();

            GameObject instance = Instantiate(prefab, pooledRoot, worldPositionStays: false);
            if (instance == null)
                return null;

            if (deactivateAfterCreate)
                instance.SetActive(false);

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

        private static string BuildPrewarmLabel(GameObject prefab, int count)
        {
            string prefabName = prefab != null ? prefab.name : "<none>";
            return $"PrewarmPrefab {prefabName} x{Mathf.Max(0, count)}";
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

        private static float ResolvePresentationLifetime(GameObject instance, in SpawnedPresentationHook hook)
        {
            switch (hook.lifetimeMode)
            {
                case PresentationLifetimeMode.ManualRelease:
                    return 0f;

                case PresentationLifetimeMode.FixedSeconds:
                    return Mathf.Max(0f, hook.lifetimeOverrideSeconds);
            }

            if (hook.lifetimeOverrideSeconds > 0f)
                return hook.lifetimeOverrideSeconds;

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
                    return LoopingAutoReleaseFallbackSeconds;

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

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0f)
                    maxLifetime = Mathf.Max(maxLifetime, stateInfo.length);

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

        private int GetTotalPooledInstanceCountInternal()
        {
            int total = 0;
            foreach (KeyValuePair<int, Queue<PooledPresentationInstance>> pair in poolByPrefabId)
                total += pair.Value != null ? pair.Value.Count : 0;

            return total;
        }

        private PoolDebugEntry[] BuildPoolSnapshot(int maxCount)
        {
            int safeMaxCount = Mathf.Max(1, maxCount);
            var results = new List<PoolDebugEntry>(poolByPrefabId.Count);
            foreach (KeyValuePair<int, Queue<PooledPresentationInstance>> pair in poolByPrefabId)
            {
                int pooledCount = pair.Value != null ? pair.Value.Count : 0;
                string name = prefabNamesById.TryGetValue(pair.Key, out string prefabName)
                    ? prefabName
                    : pair.Key.ToString();
                results.Add(new PoolDebugEntry(name, pooledCount));
            }

            results.Sort((left, right) => right.PooledCount.CompareTo(left.PooledCount));
            if (results.Count > safeMaxCount)
                results.RemoveRange(safeMaxCount, results.Count - safeMaxCount);

            return results.ToArray();
        }
    }
}
