using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 술 장판이 불 요소와 접촉했을 때 지연 점화와 순차 전염 변환을 조율한다.
    /// - 개별 장판 actor가 주변 장판 탐색/중복 점화 방지를 직접 소유하지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleConversionService : MonoBehaviour
    {
        private static PuddleConversionService cachedSceneInstance;

        [SerializeField, Min(0f)] private float ignitionDelaySeconds = 0.8f;

        private readonly HashSet<AlcoholPuddleArea> ignitingPuddles = new();

        public static PuddleConversionService ResolveForScene()
        {
            if (cachedSceneInstance != null)
                return cachedSceneInstance;

            cachedSceneInstance = FindAnyObjectByType<PuddleConversionService>();
            if (cachedSceneInstance != null)
                return cachedSceneInstance;

            GameObject host = new("PuddleConversionService");
            cachedSceneInstance = host.AddComponent<PuddleConversionService>();
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

        public void RequestIgnite(AlcoholPuddleArea origin)
        {
            if (origin == null || origin.Mode != PuddleAreaMode.Ground)
                return;

            if (ignitingPuddles.Contains(origin))
                return;

            StartCoroutine(IgniteRoutine(origin));
        }

        private IEnumerator IgniteRoutine(AlcoholPuddleArea target)
        {
            ignitingPuddles.Add(target);
            target.BeginIgnitionPresentation();

            float delay = Mathf.Max(0f, ignitionDelaySeconds);
            if (delay > 0f)
            {
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.deltaTime;
                    target.SetIgnitionVisualProgress(Mathf.Clamp01(elapsed / delay));
                    yield return null;
                }
            }

            target.SetIgnitionVisualProgress(1f);

            ignitingPuddles.Remove(target);

            if (target != null && target.IsGroundActive)
                target.CompleteIgnitionToFire();
        }
    }
}
