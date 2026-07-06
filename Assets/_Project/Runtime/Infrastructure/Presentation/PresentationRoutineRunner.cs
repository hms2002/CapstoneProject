using System.Collections;
using CapstoneRuntime;
using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: 정적 presentation API가 scene object 없이 coroutine 기반 비동기 presentation 실행을 시작하게 한다.
    /// </summary>
    [DefaultExecutionOrder(-841)]
    [DisallowMultipleComponent]
    public sealed class PresentationRoutineRunner : MonoBehaviour
    {
        public static PresentationRoutineRunner Instance { get; private set; }

        private static bool s_isQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (s_isQuitting || Instance != null)
                return;

            EnsureInstance();
        }

        public static PresentationRoutineRunner EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            PresentationRoutineRunner existing = RuntimeServiceOwnership.FindExistingService<PresentationRoutineRunner>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(PresentationRoutineRunner));
            return host.AddComponent<PresentationRoutineRunner>();
        }

        public static Coroutine Run(IEnumerator routine)
        {
            if (routine == null)
                return null;

            PresentationRoutineRunner runner = EnsureInstance();
            return runner != null ? runner.StartCoroutine(routine) : null;
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
    }
}
