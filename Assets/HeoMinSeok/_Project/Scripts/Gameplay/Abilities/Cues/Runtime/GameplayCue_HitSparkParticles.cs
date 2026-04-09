using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : 히트 스파크 파티클을 재생하고, 동시에 살아 있는 히트 스파크 개수를 제한하며,
    /// 비활성화된 인스턴스를 정적 풀에 되돌려 재사용한다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GameplayCue_HitSparkParticles : GameplayCueNotify
    {
        private const int MaxConcurrentHitSparks = 6;
        private static int s_playingHitSparkCount;
        private static readonly Dictionary<int, Stack<GameplayCue_HitSparkParticles>> s_poolByPrefabId = new();

        [SerializeField] private ParticleSystem particleSystemRef;
        [SerializeField] private bool clearBeforePlay = true;
        [SerializeField] private bool orientToImpactDirection = true;
        [SerializeField] private float rotationOffsetZ = 0f;
#if UNITY_EDITOR
        [Header("Editor Preview")]
        [SerializeField] private Vector2 previewDirection = new(1f, 0f);
#endif

        private bool countedAsPlaying;
        private bool isPooled;
        private int poolPrefabId;

        /// <summary>
        /// 책임 : HitSpark 프리팹의 비활성 인스턴스를 우선 재사용하고, 없을 때만 새 인스턴스를 만든다.
        /// </summary>
        public static GameObject AcquireInstance(GameObject prefab)
        {
            if (prefab == null)
                return null;

            int prefabId = prefab.GetInstanceID();
            if (s_poolByPrefabId.TryGetValue(prefabId, out Stack<GameplayCue_HitSparkParticles> pool))
            {
                while (pool.Count > 0)
                {
                    GameplayCue_HitSparkParticles pooled = pool.Pop();
                    if (pooled == null)
                        continue;

                    pooled.isPooled = false;
                    pooled.gameObject.SetActive(true);
                    return pooled.gameObject;
                }
            }

            GameObject created = Instantiate(prefab);
            if (created.TryGetComponent<GameplayCue_HitSparkParticles>(out var cue))
            {
                cue.poolPrefabId = prefabId;
                cue.isPooled = false;
            }

            return created;
        }

        private void Awake()
        {
            if (particleSystemRef == null)
                particleSystemRef = GetComponent<ParticleSystem>();
        }

        private void OnDisable()
        {
            ReleasePlayingCount();
            ReturnToPool();
        }

        private void OnDestroy()
        {
            ReleasePlayingCount();
        }

        public override void OnExecute(GameplayCueParams p)
        {
            if (particleSystemRef == null)
                return;

            if (s_playingHitSparkCount >= MaxConcurrentHitSparks)
            {
                gameObject.SetActive(false);
                return;
            }

            ResetSystem();

            if (orientToImpactDirection)
                ApplyRotation(ResolveImpactDirection(p));

            RegisterPlaying();
            particleSystemRef.Play(true);
        }

        private void ResetSystem()
        {
            if (!clearBeforePlay)
                return;

            particleSystemRef.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystemRef.Clear(true);
        }

        private void ApplyRotation(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            float zRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffsetZ;
            transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        }

        private static Vector2 ResolveImpactDirection(GameplayCueParams p)
        {
            Vector2 direction = Vector2.zero;
            Transform attacker = p.Causer != null ? p.Causer.transform : null;
            if (attacker == null && p.Instigator != null)
                attacker = p.Instigator.transform;

            Transform target = p.Target != null ? p.Target.transform : null;
            if (attacker != null && target != null)
                direction = (Vector2)(target.position - attacker.position);

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            return direction.normalized;
        }

        /// <summary>
        /// 책임 : 실제 재생을 시작한 히트 스파크만 동시 표시 개수에 포함되도록 기록한다.
        /// </summary>
        private void RegisterPlaying()
        {
            if (countedAsPlaying)
                return;

            countedAsPlaying = true;
            s_playingHitSparkCount++;
        }

        /// <summary>
        /// 책임 : 히트 스파크 오브젝트가 비활성/파괴될 때 정적 재생 개수를 정확히 반환한다.
        /// </summary>
        private void ReleasePlayingCount()
        {
            if (!countedAsPlaying)
                return;

            countedAsPlaying = false;
            s_playingHitSparkCount = Mathf.Max(0, s_playingHitSparkCount - 1);
        }

        /// <summary>
        /// 책임 : 재생이 끝나 비활성화된 히트 스파크를 프리팹별 정적 풀로 돌려 다음 적중 연출에 재사용한다.
        /// </summary>
        private void ReturnToPool()
        {
            if (poolPrefabId == 0 || isPooled)
                return;

            if (!s_poolByPrefabId.TryGetValue(poolPrefabId, out Stack<GameplayCue_HitSparkParticles> pool))
            {
                pool = new Stack<GameplayCue_HitSparkParticles>();
                s_poolByPrefabId[poolPrefabId] = pool;
            }

            isPooled = true;
            pool.Push(this);
        }

#if UNITY_EDITOR
        public void EditorPreviewBurst()
        {
            if (particleSystemRef == null)
                particleSystemRef = GetComponent<ParticleSystem>();

            if (particleSystemRef == null)
                return;

            ResetSystem();

            if (orientToImpactDirection)
            {
                Vector2 direction = previewDirection.sqrMagnitude > 0.0001f
                    ? previewDirection.normalized
                    : Vector2.right;
                ApplyRotation(direction);
            }

            particleSystemRef.Play(true);
        }
#endif
    }
}
