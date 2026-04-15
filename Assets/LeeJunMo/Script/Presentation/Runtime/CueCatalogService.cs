using System.Collections.Generic;
using UnityEngine;

namespace CapstonePresentation
{
    [DefaultExecutionOrder(-839)]
    [DisallowMultipleComponent]
    public sealed class CueCatalogService : MonoBehaviour
    {
        public static CueCatalogService Instance { get; private set; }

        [SerializeField] private CueCatalogSO defaultCatalog;
        [SerializeField] private List<CueCatalogSO> additionalCatalogs = new();

        public static bool TryResolve(in CueRef cueRef, out PresentationCueSO cue)
        {
            cue = null;
            if (!cueRef.IsSet)
                return false;

            CueCatalogService service = ResolveInstance();
            return service != null && service.TryResolveInternal(cueRef.key, out cue);
        }

        public static bool TryResolve(in CueRef cueRef, out WorldPresentationHook presentation)
        {
            presentation = default;
            if (!TryResolve(cueRef, out PresentationCueSO cue) || cue == null || !cue.HasAnyContent)
                return false;

            presentation = cue.Presentation;
            return true;
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private static CueCatalogService ResolveInstance()
        {
            if (Instance != null)
                return Instance;

#if UNITY_2023_1_OR_NEWER
            Instance = FindAnyObjectByType<CueCatalogService>();
#else
            Instance = FindObjectOfType<CueCatalogService>();
#endif
            return Instance;
        }

        private bool TryResolveInternal(string key, out PresentationCueSO cue)
        {
            cue = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (defaultCatalog != null && defaultCatalog.TryGetCue(key, out cue))
                return true;

            for (int i = 0; i < additionalCatalogs.Count; i++)
            {
                CueCatalogSO catalog = additionalCatalogs[i];
                if (catalog != null && catalog.TryGetCue(key, out cue))
                    return true;
            }

            return false;
        }
    }
}
