using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 현재 씬의 장판 actor 등록 목록을 관리하고 장판 간 공간 질의를 제공한다.
    /// - 보스 패턴/변환 서비스가 개별 장판을 직접 탐색하지 않게 하는 얇은 조회 계층이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleManager : MonoBehaviour
    {
        private static PuddleManager cachedSceneInstance;

        private readonly List<PuddleAreaBase> puddles = new();

        public IReadOnlyList<PuddleAreaBase> Puddles => puddles;

        public static PuddleManager ResolveForScene()
        {
            if (cachedSceneInstance != null)
                return cachedSceneInstance;

            cachedSceneInstance = FindAnyObjectByType<PuddleManager>();
            if (cachedSceneInstance != null)
                return cachedSceneInstance;

            GameObject host = new("PuddleManager");
            cachedSceneInstance = host.AddComponent<PuddleManager>();
            return cachedSceneInstance;
        }

        private void Awake()
        {
            if (cachedSceneInstance == null)
                cachedSceneInstance = this;
        }

        private void OnDestroy()
        {
            if (cachedSceneInstance == this)
                cachedSceneInstance = null;
        }

        public void Register(PuddleAreaBase puddle)
        {
            if (puddle == null || puddles.Contains(puddle))
                return;

            puddles.Add(puddle);
        }

        public void Unregister(PuddleAreaBase puddle)
        {
            if (puddle == null)
                return;

            puddles.Remove(puddle);
        }

        /// <summary>
        /// 책임 :
        /// - 기준 술 장판과 접촉하거나 겹친 술 장판들을 반환한다.
        /// - 연쇄 점화 규칙에서 "같이 불이 붙을 장판 묶음"을 찾는 데 사용한다.
        /// </summary>
        public List<AlcoholPuddleArea> CollectOverlappingAlcoholPuddles(AlcoholPuddleArea origin)
        {
            List<AlcoholPuddleArea> result = new();
            if (origin == null)
                return result;

            Queue<AlcoholPuddleArea> queue = new();
            queue.Enqueue(origin);
            result.Add(origin);

            while (queue.Count > 0)
            {
                AlcoholPuddleArea current = queue.Dequeue();
                for (int i = 0; i < puddles.Count; i++)
                {
                    if (puddles[i] is not AlcoholPuddleArea candidate)
                        continue;

                    if (candidate == null || result.Contains(candidate))
                        continue;

                    if (!candidate.IsGroundActive)
                        continue;

                    if (!AreIgnitionContactAreasOverlapping(current, candidate))
                        continue;

                    result.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }

            return result;
        }

        /// <summary>
        /// 책임 :
        /// - 기준 장판과 직접 닿아 있는 점화 가능한 술 장판만 반환한다.
        /// - 불 장판 완성 후 주변 장판으로 한 단계씩 번지는 순차 전염 판정에 사용한다.
        /// </summary>
        public List<AlcoholPuddleArea> CollectDirectIgnitableAlcoholPuddles(PuddleAreaBase origin)
        {
            List<AlcoholPuddleArea> result = new();
            if (origin == null)
                return result;

            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i] is not AlcoholPuddleArea candidate)
                    continue;

                if (candidate == null || candidate.Mode != PuddleAreaMode.Ground)
                    continue;

                if (!AreIgnitionContactAreasOverlapping(origin, candidate))
                    continue;

                result.Add(candidate);
            }

            return result;
        }

        /// <summary>
        /// 책임 :
        /// - 장판의 실제 피해 반경이 아니라 점화 전이 전용 접촉 반경으로 두 장판의 전염 가능 여부를 판정한다.
        /// - collider trigger와 manager scan 경로가 같은 기준을 쓰도록 공개 유틸로 제공한다.
        /// </summary>
        public static bool AreIgnitionContactAreasOverlapping(PuddleAreaBase a, PuddleAreaBase b)
        {
            if (a == null || b == null)
                return false;

            float radiusSum = Mathf.Max(0f, a.IgnitionContactRadius + b.IgnitionContactRadius);
            return Vector2.Distance(a.transform.position, b.transform.position) <= radiusSum;
        }
    }
}
